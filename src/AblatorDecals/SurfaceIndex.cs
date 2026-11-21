using System;
using System.Collections.Generic;
using UnityEngine;

namespace AblatorDecals
{
    public struct SurfaceHit
    {
        public Vector3 P;     // point on the mesh
        public Vector3 N;     // smoothed normal (small details like ribs averaged out)
        public Vector3 FaceN; // actual normal of the triangle that was hit
        public int Owner;     // which source mesh/part the triangle came from
        public int Triangle;  // index of the hit triangle
    }

    // Triangle soup with a spatial hash for closest-point and short raycast queries.
    // Normals are smoothed over SmoothRadius so tiles ride over ribs and panel lines instead of stopping at them.
    // Only managed Unity math is used so tests/SelfCheck can run it outside the game.
    public class SurfaceIndex
    {
        private readonly Vector3[] pos, faceN;
        private readonly float[] faceArea;
        private readonly int[] tri, owners;
        private readonly float cell;
        private readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        private readonly int[] seen;
        private readonly bool[] indexed;
        private int query;

        public float SmoothRadius { get; }
        public int TriangleCount { get; private set; }
        public int CellCount => grid.Count; // index size (DESIGN-v8 B4)
        public int TriangleSlots => faceN.Length;
        public bool HasOwners { get; private set; } // more than one owner id among the indexed triangles

        // Indexed, non-degenerate triangle t: corners, face normal, owner.
        public bool Triangle(int t, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int owner)
        {
            a = pos[tri[3 * t]]; b = pos[tri[3 * t + 1]]; c = pos[tri[3 * t + 2]];
            n = faceN[t];
            owner = owners != null ? owners[t] : 0;
            return indexed[t];
        }

        // Indexes only triangles whose bounds touch the sphere (center, radius).
        // triangleOwners (optional): owner id per triangle, reported back in SurfaceHit.Owner.
        public SurfaceIndex(Vector3[] positions, int[] triangles, Vector3 center, float radius, float cellSize, float smoothRadius,
            int[] triangleOwners = null)
        {
            pos = positions;
            tri = triangles;
            owners = triangleOwners;
            cell = cellSize;
            SmoothRadius = smoothRadius;
            int count = triangles.Length / 3;
            faceN = new Vector3[count];
            faceArea = new float[count];
            seen = new int[count];
            indexed = new bool[count];

            Vector3 rMin = center - Vector3.one * radius, rMax = center + Vector3.one * radius;

            for (int t = 0; t < count; t++)
            {
                Vector3 a = pos[tri[3 * t]], b = pos[tri[3 * t + 1]], c = pos[tri[3 * t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a); // Unity front face = clockwise = this direction
                float len = n.magnitude;
                if (len < 1e-9f) continue;
                faceN[t] = n / len;
                faceArea[t] = len * 0.5f;

                Vector3 lo = Vector3.Max(Vector3.Min(Vector3.Min(a, b), c), rMin);
                Vector3 hi = Vector3.Min(Vector3.Max(Vector3.Max(a, b), c), rMax);
                if (lo.x > hi.x || lo.y > hi.y || lo.z > hi.z) continue;

                Cell(lo, out int x0, out int y0, out int z0);
                Cell(hi, out int x1, out int y1, out int z1);
                for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                {
                    long key = Key(x, y, z);
                    if (!grid.TryGetValue(key, out List<int> list)) grid[key] = list = new List<int>(4);
                    list.Add(t);
                }

                TriangleCount++;
                indexed[t] = true;
                if (owners != null && owners[t] != owners[0]) HasOwners = true;
            }

            maxWeight = Mathf.PI * smoothRadius * smoothRadius;
        }

        private readonly float maxWeight;
        private int[] buf = new int[256];

        // Normal at p averaged over the triangles within SmoothRadius, weighted by (capped) area and closeness.
        // Point based, so it behaves the same on coarse and fine meshes. Weighted by how much a triangle faces the same
        // way as the hit one: both flanks of a rib count, faces at right angles hardly (the hidden end caps of stacked
        // tanks would bend the normal at every joint and stop the flood there), the far side of a thin wing not at all.
        private Vector3 SmoothAt(Vector3 p, int hitTriangle)
        {
            Vector3 refN = faceN[hitTriangle], sum = Vector3.zero;
            float r = SmoothRadius, r2 = r * r;
            int n = Gather(p - Vector3.one * r, p + Vector3.one * r);
            for (int i = 0; i < n; i++)
            {
                int t = buf[i];
                float facing = Vector3.Dot(faceN[t], refN);
                if (facing < 0.05f) continue;
                Vector3 a = pos[tri[3 * t]];
                float dp = Vector3.Dot(p - a, faceN[t]); // plane distance: cheap reject
                if (dp * dp >= r2) continue;
                Vector3 q = ClosestPointOnTriangle(p, a, pos[tri[3 * t + 1]], pos[tri[3 * t + 2]], out _, out _, out _);
                float d2 = (q - p).sqrMagnitude;
                if (d2 < r2) sum += faceN[t] * (Mathf.Min(faceArea[t], maxWeight) * (1 - Mathf.Sqrt(d2) / r) * facing);
            }
            return sum.sqrMagnitude > 1e-12f ? sum.normalized : refN;
        }

        public struct ShellTri
        {
            public int Index, Owner;
            public Vector3 A, B, C, NA, NB, NC;   // corners and welded vertex normals
            public bool OpenAB, OpenBC, OpenCA;    // edge not shared with another shell triangle
            public Vector2 UA, UB, UC; public bool HasUv; // own UVs (1.6 filler pieces), else the cap's plain ones
        }

        // Hull too curved both ways for rigid tiles (tip of a pointed nose, DESIGN-v8 B1): triangles at a vertex around which
        // the angle deficit (2 pi - sum of corner angles, i.e. the Gaussian curvature) within radius adds up to minDeficit.
        // A sphere of radius r gives pi radius^2 / r^2. Also rims bent tighter than that (vertex normals within turnRadius
        // turning beyond acos(maxTurnCos)). Vertices welded by position (1 mm); rim vertices of an open mesh
        // (small angle sum, normals all one way) don't count. accept(triangle corners) filters the result.
        public List<ShellTri> CurvedShell(float radius, float minDeficit, float turnRadius, float maxTurnCos, Func<Vector3, Vector3, Vector3, bool> accept)
        {
            var at = new Dictionary<long, int>();
            var p = new List<Vector3>();
            var angle = new List<float>();
            var nsum = new List<Vector3>();
            var corner = new int[tri.Length];
            for (int t = 0; t < faceN.Length; t++)
            {
                if (faceArea[t] <= 0) continue;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 a = pos[tri[3 * t + k]], b = pos[tri[3 * t + (k + 1) % 3]], c = pos[tri[3 * t + (k + 2) % 3]];
                    long key = Key(Mathf.RoundToInt(a.x * 1000), Mathf.RoundToInt(a.y * 1000), Mathf.RoundToInt(a.z * 1000));
                    if (!at.TryGetValue(key, out int i)) { at[key] = i = p.Count; p.Add(a); angle.Add(0); nsum.Add(Vector3.zero); }
                    float w = Vector3.Angle(b - a, c - a) * Mathf.Deg2Rad;
                    angle[i] += w;
                    nsum[i] += faceN[t] * w;
                    corner[3 * t + k] = i;
                }
            }
            var deficit = new float[p.Count];
            var norm = new Vector3[p.Count];
            float cs = Math.Max(Math.Max(radius, turnRadius), 1e-3f);
            var vgrid = new Dictionary<long, List<int>>(p.Count);
            for (int i = 0; i < p.Count; i++)
            {
                if (!(angle[i] < 4.5f && nsum[i].magnitude > 0.85f * angle[i])) deficit[i] = 2 * Mathf.PI - angle[i];
                norm[i] = nsum[i].normalized;
                long key = Key(Mathf.FloorToInt(p[i].x / cs), Mathf.FloorToInt(p[i].y / cs), Mathf.FloorToInt(p[i].z / cs));
                if (!vgrid.TryGetValue(key, out List<int> bucket)) vgrid[key] = bucket = new List<int>(4);
                bucket.Add(i);
            }
            var curved = new bool[p.Count];
            var turned = new bool[p.Count];
            var gaussPts = new List<Vector3>();
            float r2 = radius * radius, t2 = turnRadius * turnRadius;
            for (int i = 0; i < p.Count; i++)
            {
                float sum = 0;
                Vector3 pi = p[i];
                Vector3 ni = norm[i];
                int cx = Mathf.FloorToInt(pi.x / cs), cy = Mathf.FloorToInt(pi.y / cs), cz = Mathf.FloorToInt(pi.z / cs);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!vgrid.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out List<int> bucket)) continue;
                    for (int b = 0; b < bucket.Count; b++)
                    {
                        int j = bucket[b];
                        float d2 = (p[j] - pi).sqrMagnitude;
                        if (d2 <= r2) sum += deficit[j];
                        if (d2 <= t2 && Vector3.Dot(ni, norm[j]) < maxTurnCos) turned[i] = true;
                    }
                }
                if (sum >= minDeficit) { curved[i] = true; gaussPts.Add(pi); }
            }
            // Only let 1D normal-turn extend a true doubly-curved (Gaussian) tip, never grab long 1D creases/chines/belly edges.
            if (gaussPts.Count > 0)
            {
                float near2 = 4 * r2;
                for (int i = 0; i < p.Count; i++)
                {
                    if (curved[i] || !turned[i]) continue;
                    Vector3 pi = p[i];
                    for (int g = 0; g < gaussPts.Count; g++)
                        if ((gaussPts[g] - pi).sqrMagnitude <= near2) { curved[i] = true; break; }
                }
            }
            var list = new List<ShellTri>();
            var edges = new Dictionary<long, int>();
            for (int t = 0; t < faceN.Length; t++)
            {
                if (faceArea[t] <= 0) continue;
                int i0 = corner[3 * t], i1 = corner[3 * t + 1], i2 = corner[3 * t + 2];
                if (!curved[i0] && !curved[i1] && !curved[i2]) continue;
                if (!accept(p[i0], p[i1], p[i2])) continue;
                list.Add(new ShellTri { Index = t, Owner = owners != null ? owners[t] : 0, A = p[i0], B = p[i1], C = p[i2],
                    NA = norm[i0], NB = norm[i1], NC = norm[i2] });
                foreach (long e in new[] { EdgeKey(i0, i1), EdgeKey(i1, i2), EdgeKey(i2, i0) })
                    edges[e] = edges.TryGetValue(e, out int n) ? n + 1 : 1;
            }
            for (int k = 0; k < list.Count; k++)
            {
                ShellTri st = list[k];
                int t = st.Index, i0 = corner[3 * t], i1 = corner[3 * t + 1], i2 = corner[3 * t + 2];
                st.OpenAB = edges[EdgeKey(i0, i1)] == 1;
                st.OpenBC = edges[EdgeKey(i1, i2)] == 1;
                st.OpenCA = edges[EdgeKey(i2, i0)] == 1;
                list[k] = st;
            }
            return list;
        }

        // DESIGN-v10 G1/G2: per triangle slot, whether it lies within the wrap limit around n (by its face normal, so the limit is a
        // line on the hull, the same for every pattern and tile size). Faces are grouped into patches joined across edges bent
        // less than creaseDeg (vertices welded at 1 mm). A patch reaching past the limit by at most snapDeg is taken whole, one
        // starting past the limit minus snapDeg (and past half of it) is left whole (the nearer of the two wins): the cover ends exactly on the crease
        // (Mk3 chine) instead of a facet or two beside it. Any other patch is cut face by face where its normal crosses the limit.
        public bool[] WrapMask(Vector3 n, float maxWrapDeg, float creaseDeg, float snapDeg)
        {
            int count = faceN.Length;
            var angle = new float[count];
            for (int t = 0; t < count; t++)
                angle[t] = faceArea[t] > 0 ? Mathf.Acos(Mathf.Clamp(Vector3.Dot(faceN[t], n), -1f, 1f)) * Mathf.Rad2Deg : 0;
            int[] root = Patches(creaseDeg, false);
            int Find(int x) => root[x];
            var lo = new Dictionary<int, float>();
            var hi = new Dictionary<int, float>();
            for (int t = 0; t < count; t++)
            {
                if (!indexed[t]) continue;
                int r = Find(t);
                lo[r] = lo.TryGetValue(r, out float l) ? Math.Min(l, angle[t]) : angle[t];
                hi[r] = hi.TryGetValue(r, out float h) ? Math.Max(h, angle[t]) : angle[t];
            }
            var mask = new bool[count];
            for (int t = 0; t < count; t++)
            {
                if (!indexed[t]) continue;
                int r = Find(t);
                float l = lo[r], h = hi[r];
                bool snapOut = l > Math.Max(maxWrapDeg - snapDeg, 0.5f * maxWrapDeg), snapIn = h <= maxWrapDeg + snapDeg;
                if (h <= maxWrapDeg) mask[t] = true;
                else if (l > maxWrapDeg) mask[t] = false;
                else if (snapOut && (!snapIn || maxWrapDeg - l <= h - maxWrapDeg)) mask[t] = false;
                else mask[t] = snapIn || angle[t] <= maxWrapDeg;
            }
            return mask;
        }

        // Face pieces: per triangle slot the id of its patch (the smallest slot in it), triangles joined across edges bent less
        // than creaseDeg (vertices welded at 1 mm), with byOwner only of the same owner. Unindexed slots are their own patch.
        public int[] Patches(float creaseDeg, bool byOwner)
        {
            int count = faceN.Length;
            var parent = new int[count];
            for (int t = 0; t < count; t++) parent[t] = t;
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            void Join(int a, int b) { a = Find(a); b = Find(b); if (a < b) parent[b] = a; else if (b < a) parent[a] = b; }
            var weld = new Dictionary<long, int>();
            int Vert(Vector3 p)
            {
                long key = Key(Mathf.RoundToInt(p.x * 1000), Mathf.RoundToInt(p.y * 1000), Mathf.RoundToInt(p.z * 1000));
                if (!weld.TryGetValue(key, out int i)) weld[key] = i = weld.Count;
                return i;
            }
            float cosCrease = Mathf.Cos(creaseDeg * Mathf.Deg2Rad);
            var edges = new Dictionary<long, int>();
            for (int t = 0; t < count; t++)
            {
                if (!indexed[t]) continue;
                int a = Vert(pos[tri[3 * t]]), b = Vert(pos[tri[3 * t + 1]]), c = Vert(pos[tri[3 * t + 2]]);
                foreach (long e in new[] { EdgeKey(a, b), EdgeKey(b, c), EdgeKey(c, a) })
                {
                    if (!edges.TryGetValue(e, out int o)) { edges[e] = t; continue; }
                    if (Vector3.Dot(faceN[o], faceN[t]) >= cosCrease && (!byOwner || owners == null || owners[o] == owners[t])) Join(o, t);
                }
            }
            for (int t = 0; t < count; t++) parent[t] = Find(t);
            return parent;
        }

        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // Smoothed normal for a hit made with smooth = false.
        public Vector3 Smooth(SurfaceHit h) => SmoothAt(h.P, h.Triangle);

        // Closest surface point within radius of q, ignoring triangles facing away from refNormal by more than acos(minDot).
        public bool Closest(Vector3 q, float radius, Vector3 refNormal, float minDot, out SurfaceHit hit)
        {
            SurfaceHit best = default;
            float bestD = radius * radius;
            int bestT = -1;
            int n = Gather(q - Vector3.one * radius, q + Vector3.one * radius);
            for (int i = 0; i < n; i++)
            {
                int t = buf[i];
                if (Vector3.Dot(faceN[t], refNormal) < minDot) continue;
                Vector3 p = ClosestPointOnTriangle(q, pos[tri[3 * t]], pos[tri[3 * t + 1]], pos[tri[3 * t + 2]], out _, out _, out _);
                float d = (p - q).sqrMagnitude;
                if (d > bestD) continue;
                bestD = d;
                bestT = t;
                best.P = p;
            }
            if (bestT >= 0) Finish(ref best, bestT, true);
            hit = best;
            return bestT >= 0;
        }

        private void Finish(ref SurfaceHit h, int t, bool smooth)
        {
            h.FaceN = faceN[t];
            h.N = smooth ? SmoothAt(h.P, t) : faceN[t];
            h.Owner = owners != null ? owners[t] : 0;
            h.Triangle = t;
        }

        // First hit along a short ray, only triangles whose front faces the ray origin (dot(faceN, -dir) >= minDot).
        // smooth = false leaves N = FaceN (call Smooth later if needed) and saves the smoothing query.
        public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, float minDot, out SurfaceHit hit, bool smooth = true)
        {
            SurfaceHit best = default;
            float bestDist = maxDist;
            int bestT = -1;
            // long rays (overlay: across a cargo bay) in pieces of 4 cells, nearest first: the box around a long diagonal ray
            // holds far too many cells. The first piece with a hit inside it has the nearest hit.
            float piece = Math.Max(4 * cell, 1e-3f);
            for (float t0 = 0; t0 < maxDist && bestT < 0; t0 += piece)
            {
                float t1 = Math.Min(maxDist, t0 + piece);
                Vector3 a = origin + dir * t0, end = origin + dir * t1;
                int n = Gather(Vector3.Min(a, end), Vector3.Max(a, end));
                for (int i = 0; i < n; i++)
                {
                    int t = buf[i];
                    if (Vector3.Dot(faceN[t], -dir) < minDot) continue;
                    if (!RayTriangle(origin, dir, pos[tri[3 * t]], pos[tri[3 * t + 1]], pos[tri[3 * t + 2]], out float rt, out _, out _)) continue;
                    if (rt > bestDist || rt > t1 + 1e-5f) continue; // beyond this piece: found again (or nearer) in a later one
                    bestDist = rt;
                    bestT = t;
                    best.P = origin + dir * rt;
                }
            }
            if (bestT >= 0) Finish(ref best, bestT, smooth);
            hit = best;
            return bestT >= 0;
        }

        // Distinct triangles of all grid cells touching the box, into buf; returns the count. No allocations.
        private int Gather(Vector3 lo, Vector3 hi)
        {
            query++;
            int n = 0;
            Cell(lo, out int x0, out int y0, out int z0);
            Cell(hi, out int x1, out int y1, out int z1);
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            {
                if (!grid.TryGetValue(Key(x, y, z), out List<int> list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    int t = list[i];
                    if (seen[t] == query) continue;
                    seen[t] = query;
                    if (n == buf.Length) Array.Resize(ref buf, n * 2);
                    buf[n++] = t;
                }
            }
            return n;
        }

        private void Cell(Vector3 p, out int x, out int y, out int z)
        {
            x = Mathf.FloorToInt(p.x / cell);
            y = Mathf.FloorToInt(p.y / cell);
            z = Mathf.FloorToInt(p.z / cell);
        }

        private static long Key(int x, int y, int z) =>
            ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (uint)(z & 0x1FFFFF);

        // Möller-Trumbore; u, v are the barycentric weights of b and c.
        private static bool RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t, out float u, out float v)
        {
            t = u = v = 0;
            Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-12f) return false;
            float inv = 1 / det;
            Vector3 s = o - a;
            u = Vector3.Dot(s, p) * inv;
            if (u < 0 || u > 1) return false;
            Vector3 q = Vector3.Cross(s, e1);
            v = Vector3.Dot(d, q) * inv;
            if (v < 0 || u + v > 1) return false;
            t = Vector3.Dot(e2, q) * inv;
            return t >= 0;
        }

        // Ericson, Real-Time Collision Detection 5.1.5; u, v, w are the barycentric weights of a, b, c.
        public static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out float u, out float v, out float w)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) { u = 1; v = 0; w = 0; return a; }

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) { u = 0; v = 1; w = 0; return b; }

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0)
            {
                float t = d1 / (d1 - d3);
                u = 1 - t; v = t; w = 0;
                return a + ab * t;
            }

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) { u = 0; v = 0; w = 1; return c; }

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0)
            {
                float t = d2 / (d2 - d6);
                u = 1 - t; v = 0; w = t;
                return a + ac * t;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
            {
                float t = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                u = 0; v = 1 - t; w = t;
                return b + (c - b) * t;
            }

            float denom = 1 / (va + vb + vc);
            v = vb * denom;
            w = vc * denom;
            u = 1 - v - w;
            return a + ab * v + ac * w;
        }
    }
}
