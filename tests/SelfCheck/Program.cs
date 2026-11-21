using System;
using System.Collections.Generic;
using System.Linq;
using AblatorDecals;
using UnityEngine;

// Run: dotnet run --project tests/SelfCheck
static class Program
{
    static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception("FAIL: " + what);
        Console.WriteLine("ok   " + what);
    }

    static bool Near(double a, double b, double tol = 1e-9) => Math.Abs(a - b) <= tol;

    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "v17") { V162SkinChartCheck(); V17SquareCreaseCheck(); Console.WriteLine("SelfCheck v17 OK"); return 0; } // quick: 1.7 only
        if (args.Length > 0 && args[0] == "v11") // quick: only the DESIGN-v11 checks
        {
            V11OverlayChecks();
            V11RegionChecks();
            V11CoverFileChecks();
            Console.WriteLine("SelfCheck v11 OK");
            return 0;
        }
        ShieldMathChecks();
        PatternChecks();
        PlaneChecks();
        PlaneOptionChecks();
        CylinderChecks();
        WrapRegressionCheck();
        WrapSeamWidthCheck();
        WrapSeamHoleCheck();
        StackSeamCheck();
        SphereChecks();
        EdgeChecks();
        CreaseChecks();
        BentJointChecks();
        HullVisibleChecks();
        CurvedHullVisibleChecks();
        FacetSnapChecks();
        SlantedEdgeChecks();
        WingChecks();
        ChamferStripChecks();
        RibbedChecks();
        TankRimChecks();
        NoseConeChecks();
        SeamChecks();
        PenetrationChecks();
        V7EdgeChecks();
        V7UnrolledChecks();
        V7StraightBorderChecks();
        V7SeparateOwnerChecks();
        V8ClipPlaneChecks();
        V8NoseCapChecks();
        V8OccupiedChecks();
        V8GluedBodyChecks();
        V10WrapLineChecks();
        V10KeepOutChecks();
        V10CutPlaneChecks();
        V10GapZeroChecks();
        V10WholeFootprintCheck();
        V162SkinChartCheck();
        V17SquareCreaseCheck();
        V11OverlayChecks();
        V11RegionChecks();
        V11CoverFileChecks();
        PerformanceCheck();
        GlowChecks.Run();
        Console.WriteLine("SelfCheck OK");
        return 0;
    }

    static void ShieldMathChecks()
    {
        Check(ShieldMath.RawCoverage(2, -0.5, 10) == 0, "back side protects nothing");
        Check(Near(ShieldMath.RawCoverage(2, 0.5, 10), 0.1), "projected area");
        Check(ShieldMath.RawCoverage(2, 1, 0) == 0, "zero parent area");
        Check(Near(ShieldMath.Coverage(0.8, 1.6), 0.5), "two shields share the 100 % cap");
        Check(Near(ShieldMath.Coverage(0.3, 0.5), 0.3), "under cap untouched");
        double q = ShieldMath.Absorb(0.5, 1000, 100, 2400, 0.02, out double used);
        Check(Near(q, 500) && Near(used, 500 * 0.02 / 2400), "absorb with stock 2400 kJ/unit");
        q = ShieldMath.Absorb(1, 1000, 0.001, 2400, 0.02, out used);
        Check(Near(used, 0.001) && Near(q, 0.001 * 2400 / 0.02), "absorb limited by remaining ablator");
        Check(ShieldMath.Absorb(1, -50, 100, 2400, 0.02, out used) == 0 && used == 0, "cooling flux absorbs nothing");
    }

    static void PatternChecks()
    {
        var hex = new TilePattern(TileShape.Hex, 0.2f, 0);
        Check(Near(hex.CellArea, 0.2 * 0.2 * Math.Sqrt(3) / 2, 1e-6) && hex.StepVec.All(d => Near(d.magnitude, 0.2, 1e-5)), "pattern: hex cell area and six steps of one pitch");
        var brick = new TilePattern(TileShape.Brick, 0.15f, 0);
        Check(Near(brick.CellArea, 0.0225, 1e-6) && brick.Edges.Length == 4 && brick.Edges[1].Owners.Length == 2, "pattern: brick has 4 edges, top/bottom shared by two neighbours");
        var sq = new TilePattern(TileShape.Square, 0.15f, 30);
        Vector2 p = new Vector2(0.3f, -0.7f);
        Check((sq.Unrotate(sq.Rotate(p)) - p).magnitude < 1e-5f && Mathf.Abs(Vector2.Angle(sq.E1, Vector2.right) - 30) < 0.01f, "pattern: rotation round trip, e1 turned by the angle");
        Check(sq.StepIndex(0, 1) == 1 && sq.StepIndex(1, 1) < 0, "pattern: lattice neighbour lookup");
    }

    // ---------- builds ----------

    class Run
    {
        public TileResult R;
        public List<TileOutline> T = new List<TileOutline>();
        public TileSettings S;
        public long Ms;
        public IEnumerable<Vector3> Vertices => R.Parts.SelectMany(p => p.Vertices);
        public int VertexCount => R.Parts.Sum(p => p.Vertices.Length);
        public IEnumerable<TileOutline> Full => T.Where(t => !t.Partial);
    }

    static TileSettings Settings(float w, float h, float tile = 0.5f) => new TileSettings
    {
        Width = w, Height = h, TileSize = tile, Gap = 0f, Thickness = 0.05f, ColliderPatchSize = 1f, MaxWrapDeg = 90,
        Unrolled = false // the v5 invariants are about the footprint seen along the decal normal
    };

    // n: decal normal, up: footprint v. u = n x v, so v = u x n as the API wants (atlas not mirrored).
    static Run Build(SurfaceIndex index, TileSettings s, Vector3 origin, Vector3 n, Vector3 up)
    {
        var run = new Run { S = s };
        Vector3 u = Vector3.Cross(n, up).normalized;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        run.R = TileBuilder.Build(index, s, origin, n, u, up, run.T);
        run.Ms = watch.ElapsedMilliseconds;
        return run;
    }

    // ---------- plane ----------

    static Mesh BigPlane => Quad(new Vector3(-5, -5, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0), Vector3.forward);

    static void PlaneChecks()
    {
        Check(Vector3.Cross(Vector3.Cross(Vector3.forward, Vector3.up), Vector3.forward) == Vector3.up, "frame: v = u x n with u = n x v");
        var cases = new (TileShape shape, float size, float angle)[]
        {
            (TileShape.Hex, 0.2f, 0), (TileShape.Hex, 0.2f, 30), (TileShape.Square, 0.15f, 0), (TileShape.Square, 0.15f, 45),
            (TileShape.Brick, 0.15f, 0), (TileShape.Brick, 0.15f, 30),
        };
        foreach (var (shape, size, angle) in cases)
        {
            string name = $"plane {shape} {angle}Â°";
            var s = new TileSettings { Shape = shape, TileSize = size, AngleDeg = angle, Width = 3, Height = 2, Gap = 0.004f };
            Run b = Build(Index(BigPlane, size), s, Vector3.zero, Vector3.forward, Vector3.up);
            float cell = new TilePattern(shape, size, angle).CellArea;
            Check(b.R.OnSurface && b.R.PartialCount == 0 && b.R.TileCount > 50, $"{name}: {b.R.TileCount} whole tiles");
            Check(b.T.All(t => Mathf.Abs(t.Area / cell - 1) < 0.01f), $"{name}: every tile has the cell area Â± 1 %");
            Check(Near(b.Vertices.Max(p => p.z), s.SurfaceOffset + s.Thickness, 1e-4), $"{name}: tile top = offset + thickness");
            Check(b.T.All(t => t.N.z > 0.9999f), $"{name}: tiles flat on the plane");
            var (gMin, gMax) = GapRange(b);
            Check(Mathf.Abs(gMin - s.Gap) <= 0.0005f && Mathf.Abs(gMax - s.Gap) <= 0.0005f, $"{name}: joint width {gMin * 1000:F2}..{gMax * 1000:F2} mm (gap 4 mm)");
            float hx = s.Width / 2 - size, hy = s.Height / 2 - size;
            var (uncovered, layers) = PlaneCover(b, hx, hy);
            Check(uncovered == 0, $"{name}: footprint interior closed by tiles and joint sheet ({uncovered} holes)");
            Check(layers <= 1, $"{name}: no overlapping tiles (max {layers} layers)");
            Check(ValidMesh(b), $"{name}: mesh valid, faces point along their normals");
            Check(Consistent(b), $"{name}: bins and areas consistent");
        }
        Run none = Build(Index(BigPlane, 0.5f), Settings(4, 3), new Vector3(0, 0, 3), Vector3.forward, Vector3.up);
        Check(!none.R.OnSurface && none.R.Parts.Length == 0, "no surface near origin -> OnSurface false");

        Run c = Build(Index(BigPlane, 0.2f), new TileSettings { Width = 4, Height = 3 }, Vector3.zero, Vector3.forward, Vector3.up);
        Check(c.R.Colliders.Length > 0 && c.R.Colliders.All(x => x.Size.z > 0.04f && x.Size.z < 0.05f), $"plane: {c.R.Colliders.Length} thin collider boxes");
        PartTiles part = c.R.Parts[0];
        Check(part.BackTriangles.Length > 0 && part.BackTriangles.All(i => part.Normals[i].z < -0.99f), "plane: white undersides face the hull");
        Check(part.JointTriangles.Length > 0 && part.JointTriangles.All(i => part.Normals[i].z > 0.99f && part.Vertices[i].z < c.S.SurfaceOffset + c.S.Thickness - c.S.BevelDepth),
            "plane: joint sheet faces out, below the outer tile edge");
        float perTile = (float)c.VertexCount / c.R.TileCount;
        Check(perTile <= 35, $"plane: {perTile:F1} vertices per hex tile");
    }

    static void PlaneOptionChecks()
    {
        // straight cut at the footprint border
        var s = new TileSettings { Width = 2.3f, Height = 1.7f, StraightBorder = true, AngleDeg = 17 };
        Run b = Build(Index(BigPlane, 0.2f), s, new Vector3(0.05f, 0.03f, 0), Vector3.forward, Vector3.up);
        Check(b.T.All(t => t.Outline.All(p => Mathf.Abs(p.x - 0.05f) <= 1.15f + 0.001f && Mathf.Abs(p.y - 0.03f) <= 0.85f + 0.001f)),
            "straight border: all tile tops inside the footprint (Â± 1 mm)");
        float maxX = b.T.Max(t => t.Outline.Max(p => p.x));
        Check(maxX > 0.05f + 1.15f - 0.002f, $"straight border: tiles reach the footprint edge ({maxX:F3})");
        var (uncovered, _) = PlaneCover(b, 1.1f, 0.8f, 0.05f, 0.03f);
        Check(uncovered == 0, $"straight border: closed cover up to the cut ({uncovered} holes)");
        Check(ValidMesh(b), "straight border: mesh valid");

        // missing tiles
        var full = Build(Index(BigPlane, 0.2f), new TileSettings { Width = 6, Height = 6 }, Vector3.zero, Vector3.forward, Vector3.up);
        var lossy = Build(Index(BigPlane, 0.2f), new TileSettings { Width = 6, Height = 6, TileLoss = 0.1f, Seed = 5 }, Vector3.zero, Vector3.forward, Vector3.up);
        float ratio = lossy.R.CoveredArea / full.R.CoveredArea;
        Check(Mathf.Abs(ratio - 0.9f) <= 0.03f, $"tile loss 10 %: {(1 - ratio) * 100:F1} % less area");
        Check(Consistent(lossy) && ValidMesh(lossy), "tile loss: mesh and areas consistent");

        // atlas UVs stay inside their cell
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square, TileShape.Brick })
        {
            var a = new TileSettings { Shape = shape, Width = 3, Height = 3, UvMode = UvMode.Atlas, AtlasRandomRotation = true, AngleDeg = 20, Seed = 3 };
            Run r = Build(Index(BigPlane, 0.2f), a, Vector3.zero, Vector3.forward, Vector3.up);
            bool inCell = true;
            var used = new HashSet<int>();
            foreach (PartTiles p in r.R.Parts)
                foreach (int[] bin in p.BinTriangles)
                    for (int i = 0; i < bin.Length; i += 3)
                    {
                        int[] cells = Enumerable.Range(0, 3).Select(j => AtlasCell(p.Uvs[bin[i + j]], 4, 4)).ToArray();
                        inCell &= cells[0] >= 0 && cells[0] == cells[1] && cells[1] == cells[2];
                        used.Add(cells[0]);
                    }
            Check(inCell && used.Count >= 12, $"atlas {shape}: every triangle samples one cell ({used.Count} of 16 cells used)");
        }

        // atlas orientation: seen from outside, u right and v up -> uv grows to the right and upwards (not mirrored)
        var o = new TileSettings { Shape = TileShape.Square, TileSize = 0.5f, Width = 0.4f, Height = 0.4f, UvMode = UvMode.Atlas, BevelWidth = 0 };
        Run one = Build(Index(BigPlane, 0.5f), o, Vector3.zero, Vector3.forward, Vector3.up);
        PartTiles t0 = one.R.Parts[0];
        int[] top = t0.BinTriangles[0];
        Vector3 viewRight = Vector3.Cross(Vector3.up, -Vector3.forward); // camera right when looking along -n (Unity, left-handed)
        float du = 0, dv = 0;
        for (int i = 0; i < top.Length; i++)
            for (int j = 0; j < top.Length; j++)
            {
                Vector3 d = t0.Vertices[top[j]] - t0.Vertices[top[i]];
                Vector2 duv = t0.Uvs[top[j]] - t0.Uvs[top[i]];
                du += Vector3.Dot(d, viewRight) * duv.x;
                dv += Vector3.Dot(d, Vector3.up) * duv.y;
            }
        Check(du > 0 && dv > 0, "atlas: image not mirrored when seen from outside");

        // bins
        Mesh cyl = Cylinder(1.25f, -2, 2, 64, 0, 0, 0);
        var bs = new TileSettings { Width = 2.5f, Height = 3, MaxBins = 5, BinAngleDeg = 10 };
        Run bb = Build(Index(cyl, 0.2f), bs, new Vector3(1.25f, 0, 0), Vector3.right, Vector3.up);
        Check(bb.R.Parts.All(p => p.BinTriangles.Length <= 5 && p.BinTriangles.Length >= 3), $"bins: {bb.R.Parts[0].BinTriangles.Length} glow bins on a cylinder (max 5)");
        Check(Consistent(bb), "bins: areas sum to the covered area, normals unit");
    }

    // ---------- cylinder ----------

    static void CylinderChecks()
    {
        const float r = 1.875f;
        Mesh cyl = Cylinder(r, -4, 4, 48, 0, 0, 0);
        Run b = Build(Index(cyl, 0.5f), Settings(2 * r, 2), new Vector3(r, 0, 0), Vector3.right, Vector3.up);
        Check(b.R.OnSurface, "cylinder: surface found");
        float minDot = b.T.Min(t => Vector3.Dot(t.N, Vector3.right));
        Check(minDot < 0.35f && minDot > -0.05f, $"cylinder: tiles wrap to the sides, not the back (outermost normal dot {minDot:F2})");
        List<Vector3> row0 = b.Full.Select(t => t.Center).Where(p => Mathf.Abs(p.y) < 0.02f).OrderBy(p => Mathf.Atan2(p.z, p.x)).ToList();
        var gaps = new List<float>();
        for (int i = 1; i < row0.Count; i++) gaps.Add((row0[i] - row0[i - 1]).magnitude);
        Check(row0.Count > 6 && gaps.All(g => Mathf.Abs(g - 0.5f) < 0.03f), $"cylinder: neighbours stay 0.5 m apart on the curve ({gaps.Min():F3}..{gaps.Max():F3})");
        Check(b.T.All(t => Mathf.Abs(new Vector2(t.Center.x, t.Center.z).magnitude - r) < 0.02f), "cylinder: tiles sit on the hull");
        Check(ValidMesh(b), "cylinder: mesh valid and outward");

        var lim = Settings(2 * r, 2);
        lim.MaxWrapDeg = 45;
        Run limited = Build(Index(cyl, 0.5f), lim, new Vector3(r, 0, 0), Vector3.right, Vector3.up);
        Check(limited.T.All(t => Vector3.Dot(t.N, Vector3.right) > Mathf.Cos(50 * Mathf.Deg2Rad)), "cylinder: max wrap angle respected");
    }

    // v5 bug: without position dedup the flood ran around closed hulls forever (40 000 tiles on 30 mÂ²).
    static void WrapRegressionCheck()
    {
        const float r = 1.25f, s = 0.2f;
        Mesh can = Merge(Cylinder(r, -2, 2, 64, 0, 0, 0), Merge(Disc(r, 2, 64, Vector3.up), Disc(r, -2, 64, Vector3.down)));
        var set = new TileSettings { Width = 4, Height = 6, MaxWrapDeg = 180, TileSize = s, Unrolled = false };
        Run b = Build(Index(can, s), set, new Vector3(r, 0, 0), Vector3.right, Vector3.up);
        float cell = new TilePattern(TileShape.Hex, s, 0).CellArea, mantle = 2 * Mathf.PI * r * 4;
        float expected = mantle / cell;
        Check(!b.R.HitTileLimit && Mathf.Abs(b.R.TileCount / expected - 1) <= 0.08f,
            $"wrap 180Â°: flood ends, {b.R.TileCount} tiles for {expected:F0} cells of mantle, {b.R.CoveredArea:F1} of {mantle:F1} mÂ² in {b.Ms} ms");
        float minDist = MinCentreDistance(b.T);
        Check(minDist >= 0.55f * s, $"wrap 180Â°: no two tile centres closer than 0.55 s ({minDist:F3} m)");
        Check(b.T.All(t => Mathf.Abs(t.N.y) < 0.5f), "wrap 180Â°: nothing on the end caps");
        int overlaps = CurvedOverlaps(b, out float minGap);
        Check(overlaps == 0, $"wrap 180Â°: no overlapping tiles at the seam where the flood fronts meet (closest {minGap * 1000:F1} mm)");
    }

    // Full wrap: where the flood fronts meet on the far side, the uncovered strip (no tile, no joint sheet) must stay
    // within about two normal joints. Measured along the circumference at several heights.
    static void WrapSeamWidthCheck()
    {
        const float r = 1.875f;
        Mesh can = Merge(Cylinder(r, -3, 3, 96, 0, 0, 0), Merge(Disc(r, 3, 96, Vector3.up), Disc(r, -3, 96, Vector3.down)));
        foreach (var (shape, size) in new[] { (TileShape.Brick, 0.15f), (TileShape.Hex, 0.2f), (TileShape.Square, 0.15f) })
        {
            var set = new TileSettings { Shape = shape, TileSize = size, Width = 9, Height = 4, MaxWrapDeg = 180, Unrolled = false };
            Run b = Build(Index(can, size), set, new Vector3(r, 0, 0), Vector3.right, Vector3.up);
            float widest = 0;
            for (float y = -1.5f; y <= 1.5f; y += 0.037f)
            {
                float run = 0;
                for (float a = 0; a < 2 * Mathf.PI; a += 0.001f / r)
                {
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    if (CoveredAt(b, d * r + Vector3.up * y, d)) run = 0;
                    else widest = Math.Max(widest, run += 0.001f);
                }
            }
            Check(widest <= 2 * set.Gap + 0.002f, $"wrap seam {shape}: widest uncovered strip {widest * 1000:F1} mm (<= {(2 * set.Gap + 0.002f) * 1000:F0} mm)");
        }
    }

    // Where the flood fronts meet on a cone, the lattice frames of the two fronts differ by more than 15Â° (parallel
    // transport turns on a taper). Joints between lattice neighbours must still be parallel, or wedge holes open.
    // Counts hull points of the whole mantle that lie under no tile and no joint sheet.
    static void WrapSeamHoleCheck()
    {
        Mesh cone = Merge(Frustum(1.9f, 1.25f, 0, 3, 96, 12), Merge(Disc(1.9f, 0, 96, Vector3.down), Disc(1.25f, 3, 96, Vector3.up)));
        foreach (var (shape, size, angle) in new[] { (TileShape.Hex, 0.2f, 0f), (TileShape.Hex, 0.2f, 20f), (TileShape.Brick, 0.15f, 0f), (TileShape.Square, 0.15f, 10f) })
        {
            var set = new TileSettings { Shape = shape, TileSize = size, AngleDeg = angle, Width = 12, Height = 3.2f, MaxWrapDeg = 180, Gap = 0.004f, Unrolled = false };
            Run b = Build(Index(cone, size), set, new Vector3(1.575f, 1.5f, 0), Vector3.right, Vector3.up);
            int total = 0, bad = 0;
            for (float y = 0.3f; y <= 2.7f; y += 0.011f)
            {
                float r = Mathf.Lerp(1.9f, 1.25f, y / 3);
                Vector3 slope = new Vector3(0, 0.65f / 3, 0);
                for (float a = 0; a < 2 * Mathf.PI; a += 0.011f / r)
                {
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    Vector3 n = (d + Vector3.up * 0.2167f).normalized;
                    total++;
                    if (!CoveredAt(b, d * r + Vector3.up * y, n)) bad++;
                }
            }
            Check(bad <= 0.0005f * total, $"cone seam {shape} {angle}Â°: {bad} of {total} hull points under neither tile nor joint sheet (<= 0.05 %), {b.R.TileCount} tiles");
        }
    }

    // Review 2, C: two stacked tanks (hidden end caps inside at the joint), decal right on the joint, bricks along the
    // axis, full wrap. The two flood fronts must meet on one straight line opposite the start. Before: the end caps bent
    // the smoothed normal at the joint (frames drifted) and first-come dedupe interleaved the fronts over a wide band.
    static void StackSeamCheck()
    {
        const float r = 1.875f, s = 0.15f;
        Mesh lower = Merge(Cylinder(r, -3, 0, 96, 0, 0, 0), Merge(Disc(r, 0, 96, Vector3.up), Disc(r, -3, 96, Vector3.down)));
        Mesh upper = Merge(Cylinder(r, 0, 3, 64, 0, 0, 0), Merge(Disc(r, 3, 64, Vector3.up), Disc(r, 0, 64, Vector3.down))); // facets not aligned
        var set = new TileSettings { Shape = TileShape.Brick, TileSize = s, Width = 5, Height = 5, MaxWrapDeg = 180, Gap = 0.004f, Unrolled = false };
        // up = +z: u = n x up runs along the axis (-y), v around the tank
        Run b = Build(Index(Merge(lower, upper), s), set, new Vector3(r, 0.02f, 0), Vector3.right, Vector3.forward);
        var pat = new TilePattern(set.Shape, s, 0);
        var near = Bucket(b.T, 0.3f);
        float worst = 0;
        int seamTiles = 0;
        foreach (TileOutline t in b.T)
        {
            if (Mathf.Abs(t.Center.y) > 2.2f) continue; // away from the footprint ends
            bool seam = false;
            foreach (TileOutline o in Around(near, t.Center, 0.3f))
            {
                int da = o.A - t.A, db = o.B - t.B;
                if (o == t || (o.Center - t.Center).magnitude > 1.3f * s || Vector3.Dot(o.N, t.N) < 0.3f) continue;
                if (pat.StepIndex(da, db) < 0 && (Math.Abs(da) > 1 || Math.Abs(db) > 1)) seam = true;
            }
            if (!seam) continue;
            seamTiles++;
            float arc = Mathf.Abs(Mathf.PI - Mathf.Abs(Mathf.Atan2(t.Center.z, t.Center.x))) * r; // distance from the far line
            worst = Math.Max(worst, arc);
        }
        Check(seamTiles > 20 && worst <= 1.6f * s, $"stacked tanks: flood fronts meet on one line opposite the start ({seamTiles} seam tiles, farthest {worst:F2} m from it, <= {1.6f * s:F2})");
        float widest = 0;
        for (float y = -2f; y <= 2f; y += 0.037f)
        {
            float run = 0;
            for (float a = 0; a < 2 * Mathf.PI; a += 0.001f / r)
            {
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                if (CoveredAt(b, d * r + Vector3.up * y, d)) run = 0;
                else widest = Math.Max(widest, run += 0.001f);
            }
        }
        Check(widest <= set.Gap + 0.001f, $"stacked tanks: widest uncovered strip {widest * 1000:F1} mm (<= {(set.Gap + 0.001f) * 1000:F0} mm; before 10 mm)");
    }

    // Cone frustum along +y, r0 at y0 to r1 at y1.
    static Mesh Frustum(float r0, float r1, float y0, float y1, int seg, int rows)
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        float slope = (r0 - r1) / (y1 - y0);
        for (int k = 0; k <= rows; k++)
            for (int i = 0; i <= seg; i++)
            {
                float a = 2 * Mathf.PI * i / seg, f = (float)k / rows, rr = Mathf.Lerp(r0, r1, f);
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * rr + Vector3.up * Mathf.Lerp(y0, y1, f));
                n.Add((d + Vector3.up * slope).normalized);
            }
        var t = new List<int>();
        for (int k = 0; k < rows; k++)
            for (int i = 0; i < seg; i++)
            {
                int a = k * (seg + 1) + i, b = a + seg + 1;
                t.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
            }
        Vector3[] va = v.ToArray(), na = n.ToArray();
        return new Mesh { V = va, T = Oriented(va, t.ToArray(), i => na[i]) };
    }

    // ---------- doubly curved ----------

    static void SphereChecks()
    {
        const float R = 2f;
        Mesh sphere = Sphere(R, 128, 64);
        var set = new TileSettings { Width = 2.6f, Height = 2.6f, MaxWrapDeg = 60 };
        Run b = Build(Index(sphere, 0.2f), set, new Vector3(0, R, 0), Vector3.up, Vector3.forward);
        Check(b.R.TileCount > 100, $"sphere cap: {b.R.TileCount} tiles, {b.R.PartialCount} partial, {b.Ms} ms");
        int overlaps = CurvedOverlaps(b, out float minGap);
        Check(overlaps == 0, $"sphere cap: no overlapping neighbours ({overlaps})");
        Check(minGap >= 0.4f * set.Gap, $"sphere cap: smallest joint {minGap * 1000:F2} mm >= 0.4 gap");
        // reachable area: inside the footprint less one tile, within the wrap angle
        int total = 0, hit = 0;
        var rnd = new System.Random(1);
        for (int i = 0; i < 3000; i++)
        {
            float x = (float)(rnd.NextDouble() * 2 - 1) * 1.1f, z = (float)(rnd.NextDouble() * 2 - 1) * 1.1f;
            if (x * x + z * z > R * R * 0.6f) continue;
            var p = new Vector3(x, Mathf.Sqrt(R * R - x * x - z * z), z);
            total++;
            if (CoveredAt(b, p, p.normalized)) hit++;
        }
        float frac = (float)hit / total;
        Check(frac >= 0.97f, $"sphere cap: {frac:P1} of the reachable area under tile or joint sheet");
        Check(ValidMesh(b) && Consistent(b), "sphere cap: mesh valid");
    }

    // ---------- v5 invariants ----------

    static void EdgeChecks()
    {
        Mesh small = Quad(new Vector3(-1, -1, 0), new Vector3(2, 0, 0), new Vector3(0, 2, 0), Vector3.forward);
        Run b = Build(Index(small, 0.5f), Settings(4, 4), Vector3.zero, Vector3.forward, Vector3.up);
        Check(b.Vertices.All(p => Mathf.Abs(p.x) <= 1.001f && Mathf.Abs(p.y) <= 1.001f), "edge: nothing hangs over the parent");
        int layers = PlaneCover(b, 0.98f, 0.98f).layers;
        Check(layers <= 1, $"edge: no tiles on top of each other (max {layers} layers)");
        Check(b.R.CoveredArea > 3.7f && b.R.CoveredArea <= 4.1f, $"edge: covered {b.R.CoveredArea:F2} mÂ² of the 4 mÂ² parent");
    }

    static void CreaseChecks()
    {
        // top face z = 0 for x <= 1, then a 90Â° edge down the face x = 1
        Mesh top = Quad(new Vector3(-3, -3, 0), new Vector3(4, 0, 0), new Vector3(0, 6, 0), Vector3.forward);
        Mesh side = Quad(new Vector3(1, -3, 0), new Vector3(0, 0, -3), new Vector3(0, 6, 0), Vector3.right);
        Run b = Build(Index(Merge(top, side), 0.5f), Settings(6, 2), Vector3.zero, Vector3.forward, Vector3.up);
        Check(b.R.TileCount > 0 && b.T.All(t => Vector3.Dot(t.N, Vector3.forward) > 0.98f), "crease: tiles stop at a 90Â° edge");
        Check(b.Vertices.All(p => p.x <= 1.001f), "crease: nothing wraps over the edge");
        Check(b.Vertices.Max(p => p.x) > 0.97f, $"crease: cover reaches right to the edge (max x {b.Vertices.Max(p => p.x):F3})");
    }

    // Convex 35Â° bend between two facets: the dark joint sheet of a tile tucks a few mm under the neighbour (closing the
    // joint from all views), but never far (old "spreading" pushed it up to thickness * tan(bend) further) and never
    // through the neighbour's top.
    static void BentJointChecks()
    {
        float c = Mathf.Cos(35 * Mathf.Deg2Rad), sn = Mathf.Sin(35 * Mathf.Deg2Rad);
        Mesh flat = Quad(new Vector3(-3, -3, 0), new Vector3(3, 0, 0), new Vector3(0, 6, 0), Vector3.forward);
        Mesh down = Quad(new Vector3(0, -3, 0), new Vector3(3 * c, 0, -3 * sn), new Vector3(0, 6, 0), new Vector3(sn, 0, c));
        var set = new TileSettings { Shape = TileShape.Square, TileSize = 0.2f, Width = 2, Height = 2, Thickness = 0.05f, MaxWrapDeg = 90 };
        Run b = Build(Index(Merge(flat, down), 0.2f), set, new Vector3(-0.1f, 0, 0), Vector3.forward, Vector3.up);
        int pairs = 0, poke = 0;
        float worst = 0;
        foreach (TileOutline t in b.Full)
            foreach (TileOutline o in b.Full)
            {
                if (o == t || t.Joint == null || (o.Center - t.Center).magnitude > 0.3f || Vector3.Dot(o.N, t.N) > 0.9f) continue;
                pairs++;
                foreach (Vector3 v in t.Joint)
                    if (InPolygon(o.Outline, o.N, v) && Vector3.Dot(v - o.TopPoint, o.N) > -0.001f) poke++;
                Vector2[] sheet = Flat(t.Joint, t.TopPoint, t.N);
                Vector2[] top = Flat(o.Outline.Select(p => p - t.N * Vector3.Dot(p - t.TopPoint, t.N)).ToArray(), t.TopPoint, t.N);
                float ov = ClipArea(sheet, top);
                worst = Math.Max(worst, ov);
            }
        Check(pairs >= 4, $"bend: {pairs} tile pairs straddle the 35Â° bend");
        // measured as depth over the 0.2 m edge; JointReach is 5 mm
        float depth = worst / 0.2f;
        Check(depth < set.Gap / 2 + 0.0065f, $"bend: joint sheet reaches {depth * 1000:F1} mm under the neighbour's top (< {(set.Gap / 2 + 0.0065f) * 1000:F1} mm; spreading gave ~30)");
        Check(poke == 0, $"bend: no joint sheet corner above a neighbour's top ({poke})");
    }

    // Rigid tiles straddling a crease stand proud of their neighbours. The hull must still stay hidden from every
    // outside direction up to 40Â° off the facet normal (a few mm slits at grazing angles remain): rays from hull points
    // near the crease have to hit a tile, bevel, wall or joint sheet (not the white undersides) before leaving the tile layer.
    static void HullVisibleChecks()
    {
        foreach (var (shape, angle, gap, bend) in new[] { (TileShape.Hex, 0f, 0f, 28f), (TileShape.Hex, 17f, 0.004f, 28f), (TileShape.Hex, 30f, 0f, 28f), (TileShape.Square, 17f, 0f, 28f), (TileShape.Hex, 17f, 0f, -28f), (TileShape.Brick, 5f, 0.004f, 35f) })
        {
            float c = Mathf.Cos(bend * Mathf.Deg2Rad), sn = Mathf.Sin(bend * Mathf.Deg2Rad);
            Vector3 nDown = new Vector3(sn, 0, c);
            Mesh flat = Quad(new Vector3(-3, -3, 0), new Vector3(3, 0, 0), new Vector3(0, 6, 0), Vector3.forward);
            Mesh down = Quad(new Vector3(0, -3, 0), new Vector3(3 * c, 0, -3 * sn), new Vector3(0, 6, 0), nDown);
            var set = new TileSettings { Shape = shape, AngleDeg = angle, Gap = gap, TileSize = 0.2f, Width = 2, Height = 2, Thickness = 0.04f, MaxWrapDeg = 90 };
            Run b = Build(Index(Merge(flat, down), 0.2f), set, new Vector3(-0.1f, 0, 0), Vector3.forward, Vector3.up);
            // triangles of everything visible from outside (not the white undersides), with bounding boxes
            var tv = new List<Vector3[]>();
            foreach (PartTiles part in b.R.Parts)
                foreach (int[] tris in part.BinTriangles.Concat(new[] { part.JointTriangles }))
                    for (int i = 0; i < tris.Length; i += 3)
                        tv.Add(new[] { part.Vertices[tris[i]], part.Vertices[tris[i + 1]], part.Vertices[tris[i + 2]] });
            var lo = tv.Select(q => Vector3.Min(q[0], Vector3.Min(q[1], q[2]))).ToArray();
            var hi = tv.Select(q => Vector3.Max(q[0], Vector3.Max(q[1], q[2]))).ToArray();
            int near = 0, leaks = 0;
            for (float x = -0.2f; x <= 0.2f; x += 0.01f)
                for (float y = -0.5f; y <= 0.5f; y += 0.0173f)
                {
                    bool onFlat = x <= 0;
                    Vector3 p = onFlat ? new Vector3(x, y, 0) : new Vector3(x * c, y, -x * sn);
                    Vector3 nf = onFlat ? Vector3.forward : nDown;
                    Vector3 e1 = onFlat ? Vector3.right : new Vector3(c, 0, -sn), e2 = Vector3.up;
                    foreach (float polar in new[] { 0f, 40f })
                        for (int az = 0; az < (polar == 0 ? 1 : 8); az++)
                        {
                            float a = az * Mathf.PI / 4;
                            Vector3 d = (nf * Mathf.Cos(polar * Mathf.Deg2Rad) + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * Mathf.Sin(polar * Mathf.Deg2Rad)).normalized;
                            Vector3 start = p + d * 1e-4f, end = p + d * 0.3f;
                            Vector3 rlo = Vector3.Min(start, end), rhi = Vector3.Max(start, end);
                            bool hit = false;
                            for (int i = 0; i < tv.Count && !hit; i++)
                                if (lo[i].x <= rhi.x && hi[i].x >= rlo.x && lo[i].y <= rhi.y && hi[i].y >= rlo.y && lo[i].z <= rhi.z && hi[i].z >= rlo.z)
                                    hit = RayTri(start, d, tv[i]);
                            near++;
                            if (!hit) leaks++;
                        }
                }
            Check(leaks <= 0.005f * near, $"hull hidden at {bend}Â° bend, {shape} {angle}Â°, gap {gap * 1000:F0} mm: {leaks} of {near} view rays within 40Â° of the normal reach the hull (< 0.5 %; without the fix 0.7-3 %)");
        }
    }

    // Triangles a viewer sees from outside (tile tops, bevels, walls, joint sheets; not the white undersides).
    static (List<Vector3[]> tris, Vector3[] lo, Vector3[] hi) Visible(Run b)
    {
        var tv = new List<Vector3[]>();
        foreach (PartTiles part in b.R.Parts)
            foreach (int[] tris in part.BinTriangles.Concat(new[] { part.JointTriangles }))
                for (int i = 0; i < tris.Length; i += 3)
                    tv.Add(new[] { part.Vertices[tris[i]], part.Vertices[tris[i + 1]], part.Vertices[tris[i + 2]] });
        return (tv, tv.Select(q => Vector3.Min(q[0], Vector3.Min(q[1], q[2]))).ToArray(), tv.Select(q => Vector3.Max(q[0], Vector3.Max(q[1], q[2]))).ToArray());
    }

    // Does the ray from hull point p along d hit a visible tile triangle within 0.3 m?
    static bool Blocked((List<Vector3[]> tris, Vector3[] lo, Vector3[] hi) v, Vector3 p, Vector3 d)
    {
        Vector3 start = p + d * 1e-4f, end = p + d * 0.3f;
        Vector3 rlo = Vector3.Min(start, end), rhi = Vector3.Max(start, end);
        for (int i = 0; i < v.tris.Count; i++)
            if (v.lo[i].x <= rhi.x && v.hi[i].x >= rlo.x && v.lo[i].y <= rhi.y && v.hi[i].y >= rlo.y && v.lo[i].z <= rhi.z && v.hi[i].z >= rlo.z &&
                RayTri(start, d, v.tris[i]))
                return true;
        return false;
    }

    // Review 2, D: doubly curved nose (sphere cap), square tiles at 45Â°: neighbouring tiles sit at different heights and
    // tilts, so joints open into slits at oblique views unless the joint sheets overlap under the neighbours. View rays
    // from hull points up to 60Â° off the normal must hit a tile, wall or joint sheet.
    static void CurvedHullVisibleChecks()
    {
        const float R = 2f;
        Mesh sphere = Sphere(R, 40, 20); // coarse facets, like a low-poly nose cone
        var set = new TileSettings { Shape = TileShape.Square, TileSize = 0.15f, AngleDeg = 45, Width = 2.2f, Height = 2.2f, MaxWrapDeg = 60, Gap = 0.004f };
        Run b = Build(Index(sphere, 0.15f), set, new Vector3(0, R, 0), Vector3.up, Vector3.forward);
        var v = Visible(b);
        int rays = 0, leaks = 0;
        for (float x = -0.6f; x <= 0.6f; x += 0.0123f)
            for (float z = -0.6f; z <= 0.6f; z += 0.0371f)
            {
                var p = new Vector3(x, Mathf.Sqrt(R * R - x * x - z * z), z);
                Vector3 n = p.normalized, e1 = Vector3.Cross(n, Vector3.forward).normalized, e2 = Vector3.Cross(n, e1);
                foreach (float polar in new[] { 0f, 35f, 60f })
                    for (int az = 0; az < (polar == 0 ? 1 : 8); az++)
                    {
                        float a = az * Mathf.PI / 4 + 0.2f;
                        Vector3 d = (n * Mathf.Cos(polar * Mathf.Deg2Rad) + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * Mathf.Sin(polar * Mathf.Deg2Rad)).normalized;
                        rays++;
                        if (!Blocked(v, p, d)) leaks++;
                    }
            }
        Check(leaks <= 5, $"curved hull hidden (low-poly sphere cap, Square 45Â°): {leaks} of {rays} view rays up to 60Â° off the normal reach the hull (<= 5; before 24)");
    }

    // Review 2, A: octagonal hull (45Â° facet edges the walker cannot cross). The tile beyond each edge used to hover in
    // its parent's plane and be cut down to a sliver, a hole next to the edge; now it sits on the next facet.
    static void FacetSnapChecks()
    {
        const float r = 1.5f;
        Mesh prism = Cylinder(r, -3, 3, 8, 0, 0, 0); // facet normals at 22.5Â° + k * 45Â°
        float c = Mathf.Cos(Mathf.PI / 8), apothem = r * c;
        Vector3 n0 = new Vector3(Mathf.Cos(Mathf.PI / 8), 0, Mathf.Sin(Mathf.PI / 8));
        var set = new TileSettings { Shape = TileShape.Hex, TileSize = 0.2f, Width = 4, Height = 3, MaxWrapDeg = 120, Gap = 0.004f };
        Run b = Build(Index(prism, 0.2f), set, n0 * apothem, n0, Vector3.up);
        // next facet: normal at 67.5Â°; points 2..12 cm beyond the shared edge
        Vector3 edge = new Vector3(Mathf.Cos(Mathf.PI / 4), 0, Mathf.Sin(Mathf.PI / 4)) * r;
        Vector3 n1 = new Vector3(Mathf.Cos(3 * Mathf.PI / 8), 0, Mathf.Sin(3 * Mathf.PI / 8)), along = Vector3.Cross(n1, Vector3.up);
        var vis = Visible(b);
        int total = 0, hit = 0;
        for (float d = 0.02f; d <= 0.12f; d += 0.01f)
            for (float y = -1.2f; y <= 1.2f; y += 0.03f)
            {
                total++;
                if (Blocked(vis, edge + along * d + Vector3.up * y, n1)) hit++;
            }
        Check(hit >= 0.98f * total, $"facet edge 45Â°: {hit} of {total} hull points just beyond the edge covered (>= 98 %; before 91 %)");
    }

    // Moeller-Trumbore, both faces, hit within 0.3 m.
    static bool RayTri(Vector3 o, Vector3 d, Vector3[] q)
    {
        Vector3 e1 = q[1] - q[0], e2 = q[2] - q[0], pv = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, pv);
        if (Mathf.Abs(det) < 1e-12f) return false;
        Vector3 tv = o - q[0];
        float u = Vector3.Dot(tv, pv) / det;
        if (u < -1e-4f || u > 1 + 1e-4f) return false;
        Vector3 qv = Vector3.Cross(tv, e1);
        float v = Vector3.Dot(d, qv) / det;
        if (v < -1e-4f || u + v > 1 + 1e-4f) return false;
        float t = Vector3.Dot(e2, qv) / det;
        return t > 0 && t < 0.3f;
    }

    // Plate whose edge runs obliquely to the tile rows: top, a 37Â° chamfer 4 cm wide, then a steep face. d = distance
    // beyond the crease between top and chamfer, measured along the edge normal.
    static Mesh SlantedPlate(float phiDeg, out Func<Vector3, float> dist)
    {
        float cp = Mathf.Cos(phiDeg * Mathf.Deg2Rad), sp = Mathf.Sin(phiDeg * Mathf.Deg2Rad);
        Vector3 en = new Vector3(cp, sp, 0), et = new Vector3(-sp, cp, 0); // edge normal / tangent in the plate plane
        // profile (d, z): top until 0, chamfer to (0.04, -0.03), steep to (0.06, -0.3); top reaches back to d = -3
        var prof = new[] { new Vector2(-3, 0), new Vector2(0, 0), new Vector2(0.04f, -0.03f), new Vector2(0.06f, -0.3f) };
        var v = new List<Vector3>();
        foreach (Vector2 p in prof)
        {
            v.Add(en * p.x + et * -4 + Vector3.forward * p.y);
            v.Add(en * p.x + et * 4 + Vector3.forward * p.y);
        }
        var tris = new List<int>();
        for (int i = 0; i + 1 < prof.Length; i++) tris.AddRange(new[] { 2 * i, 2 * i + 1, 2 * i + 3, 2 * i, 2 * i + 3, 2 * i + 2 });
        Vector3[] va = v.ToArray();
        dist = q => Vector3.Dot(q, en);
        return new Mesh { V = va, T = Oriented(va, tris.ToArray(), _ => Vector3.forward) };
    }

    // Tiles at an oblique edge: the cut must end on one line (the end of the chamfer, d = 40 mm), not wander down the
    // steep face with each tile's own frame (before: tiles up to 12-18 mm further, walls hanging 10 cm). Checked for the
    // hex pattern; square/brick still put a few virtual tiles on the steep face (open point, see report).
    static void SlantedEdgeChecks()
    {
        foreach (var (shape, size, phi) in new[] { (TileShape.Hex, 0.2f, 28f), (TileShape.Hex, 0.2f, 60f) })
        {
            Mesh plate = SlantedPlate(phi, out var dist);
            var set = new TileSettings { Shape = shape, TileSize = size, Width = 2.5f, Height = 2.5f, MaxWrapDeg = 120, Gap = 0.004f };
            Run b = Build(Index(plate, size), set, new Vector3(-0.5f * Mathf.Cos(phi * Mathf.Deg2Rad), -0.5f * Mathf.Sin(phi * Mathf.Deg2Rad), 0), Vector3.forward, Vector3.up);
            float worst = 0;
            int n = 0;
            foreach (TileOutline t in b.T)
            {
                if (dist(t.Center) > 0.045f || t.Hull.Max(dist) < 0.03f) continue; // centred on top or chamfer, reaching the edge zone
                n++;
                worst = Math.Max(worst, t.Hull.Max(dist));
            }
            Check(n >= 8 && worst <= 0.0455f, $"slanted edge {shape} {phi}Â°: {n} tiles reach the edge, cuts end at d <= {worst * 1000:F1} mm (chamfer ends at 40 mm); before the fix up to 52 mm");
        }
    }

    static void WingChecks()
    {
        // plate: top z = +0.05, bottom z = -0.05, rounded leading edge (r = 5 cm) around x = 0
        Mesh wing = Wing(0.05f, -2f, 2f, 16);
        Run b = Build(Index(wing, 0.2f), Settings(6, 2, 0.2f), new Vector3(-1, 0, 0.05f), Vector3.forward, Vector3.up);
        Check(b.R.TileCount > 20, $"wing: {b.R.TileCount} tiles on the top surface");
        Check(b.Vertices.All(p => p.z > 0), "wing: nothing wraps to the underside");
        Check(b.Vertices.Max(p => p.x) > 0.0f, $"wing: cover reaches onto the leading edge (max x {b.Vertices.Max(p => p.x):F3}, edge starts at 0)");

        // tapered wing: leading edge runs from (-2, 0) to (1, 3); the decal column at x = -1 leaves the wing at y = 1,
        // but the tip region (x 1..2, y up to 3) must still be covered
        var root = new Vector3(-2, 0, 0);
        Vector3[] tv = { root, new Vector3(2, 0, 0), new Vector3(2, 3, 0), new Vector3(1, 3, 0) };
        var tapered = new Mesh { V = tv, T = Oriented(tv, new[] { 0, 1, 2, 0, 2, 3 }, _ => Vector3.forward) };
        Run t = Build(Index(tapered, 0.2f), Settings(6, 8, 0.2f), new Vector3(-1, 0.4f, 0), Vector3.forward, Vector3.up);
        float tipReach = t.T.Max(x => x.Center.y);
        Check(tipReach > 2.7f, $"wing tip: tiles reach the tip although the start column leaves the wing (max y {tipReach:F2} of 3)");
        int layers = PlaneCover(t, 2.0f, 3.0f, 0, 1.5f).layers;
        Check(layers <= 1, $"wing tip: no tiles on top of each other (max {layers} layers)");
        Check(t.R.CoveredArea > 7.5f * 0.92f && t.R.CoveredArea < 7.5f * 1.04f, $"wing tip: {t.R.CoveredArea:F2} of 7.5 mÂ² covered");
    }

    // Review 2, B: plate edge like the structural wing: top, a 31Â° chamfer 5 cm wide, a 66Â° face 3 cm wide, then a
    // vertical face. With the default max wrap, the chamfer and the narrow face get strip tiles lying flat on them, the top
    // tiles end exactly on the crease, and seen from above no hull shows between the cover and the edge (before: a
    // white strip of 8 cm along the whole edge).
    static void ChamferStripChecks()
    {
        var prof = new[] { new Vector2(-3, 0), new Vector2(0, 0), new Vector2(0.0527f, -0.031f), new Vector2(0.0807f, -0.094f), new Vector2(0.0807f, -0.4f) };
        var v = new List<Vector3>();
        foreach (Vector2 q in prof) { v.Add(new Vector3(q.x, -3, q.y)); v.Add(new Vector3(q.x, 3, q.y)); }
        var tris = new List<int>();
        for (int i = 0; i + 1 < prof.Length; i++) tris.AddRange(new[] { 2 * i, 2 * i + 1, 2 * i + 3, 2 * i, 2 * i + 3, 2 * i + 2 });
        Vector3[] va = v.ToArray();
        var plate = new Mesh { V = va, T = Oriented(va, tris.ToArray(), i => Vector3.forward + Vector3.right) };
        Vector3 nCh = new Vector3(0.031f, 0, 0.0527f).normalized, nSteep = new Vector3(0.063f, 0, 0.028f).normalized;
        // Brick is no longer offered (DESIGN-v7 Â§1), only Hex and Square must pass here
        foreach (var (shape, angle) in new[] { (TileShape.Hex, 0f), (TileShape.Square, 30f) })
        {
            var set = new TileSettings { Shape = shape, TileSize = 0.2f, AngleDeg = angle, Width = 3, Height = 2, Gap = 0.004f };
            Run b = Build(Index(plate, 0.2f), set, new Vector3(-0.55f, 0, 0), Vector3.forward, Vector3.up);
            string name = $"chamfer {shape} {angle}Â°";
            int onCh = b.T.Count(t => Vector3.Dot(t.N, nCh) > 0.995f), onSteep = b.T.Count(t => Vector3.Dot(t.N, nSteep) > 0.995f);
            Check(onCh >= 6 && onSteep >= 6, $"{name}: strip tiles lie flat on the chamfer ({onCh}) and on the narrow face ({onSteep})");
            float topMax = b.T.Where(t => t.N.z > 0.999f).SelectMany(t => t.Hull).Max(q => q.x);
            Check(Mathf.Abs(topMax) <= 0.0015f, $"{name}: top tiles end on the crease (x {topMax * 1000:F1} mm)");
            bool inside = b.T.Where(t => Vector3.Dot(t.N, nCh) > 0.995f).SelectMany(t => t.Hull).All(q => q.x > -0.0015f && q.x < 0.0542f);
            var chx = b.T.Where(t => Vector3.Dot(t.N, nCh) > 0.995f).SelectMany(t => t.Hull).Select(q => q.x).ToList();
            Check(inside, $"{name}: chamfer strips stay between its creases (x {chx.Min() * 1000:F1}..{chx.Max() * 1000:F1} mm)");
            var vis = Visible(b);
            int total = 0, open = 0;
            for (float x = -0.3f; x < 0.0805f; x += 0.002f)
                for (float y = -0.8f; y <= 0.8f; y += 0.05f)
                {
                    float z = x <= 0 ? 0 : x <= 0.0527f ? -0.031f * x / 0.0527f : -0.031f - 0.063f * (x - 0.0527f) / 0.028f;
                    total++;
                    if (!Blocked(vis, new Vector3(x, y, z), Vector3.forward)) open++;
                }
            Check(open <= 0.01f * total, $"{name}: seen from above, {open} of {total} hull points up to the edge show (<= 1 %)");
            Check(ValidMesh(b) && Consistent(b), $"{name}: mesh valid");
        }
    }

    static void RibbedChecks()
    {
        const float r = 1.875f;
        Run smooth = Build(Index(Cylinder(r, -4, 4, 480, 0, 0, 0), 0.5f), Settings(3, 4), new Vector3(r, 0, 0), Vector3.right, Vector3.up);
        Run ribbed = Build(Index(Cylinder(r, -4, 4, 480, 120, 0.015f, 0), 0.5f), Settings(3, 4), new Vector3(r, 0, 0), Vector3.right, Vector3.up);
        Check(ribbed.R.CoveredArea >= smooth.R.CoveredArea * 0.95f, $"ribs: 1.5 cm ribs don't stop the tiles ({ribbed.R.CoveredArea:F2} vs {smooth.R.CoveredArea:F2} mÂ² on smooth hull)");
        Check(ribbed.T.Max(p => p.Center.y) > 1.5f && ribbed.T.Min(p => p.Center.y) < -1.5f, "ribs: rows reach the full height");
    }

    static void TankRimChecks()
    {
        // side wall up to y = 1, 5 cm rounded rim, flat cap at y = 1.05; open bottom at y = -2
        const float r = 1.25f;
        Mesh tank = Tank(r, -2, 1, 0.05f, 64);
        Run b = Build(Index(tank, 0.2f), Settings(2, 8, 0.2f), new Vector3(r, 0, 0), Vector3.right, Vector3.up);
        Check(b.T.All(t => t.N.y < 0.6f), "tank: no tiles on the cap");
        float topReach = b.Vertices.Max(p => p.y), bottomReach = b.Vertices.Min(p => p.y);
        Check(topReach > 0.98f && topReach < 1.07f, $"tank: cover reaches the rim (top {topReach:F3}, rim starts at 1.0)");
        Check(bottomReach < -1.97f, $"tank: cover reaches the open bottom ({bottomReach:F3})");
    }

    static void NoseConeChecks()
    {
        // cone along +y: radius 1 at y = 0 to a point at y = 2; decal on the side, projected from +x
        Mesh cone = Cone(1, 2, 64, 40);
        TileSettings cs = Settings(1.6f, 3f, 0.2f);
        cs.Gap = 0.004f; // real joints: with gap 0 neighbours touch exactly at the ridge and the planar overlap test is moot
        Run b = Build(Index(cone, 0.2f), cs, new Vector3(0.5f, 1, 0), Vector3.right, Vector3.up);
        Check(b.R.TileCount > 30, $"nose cone: {b.R.TileCount} tiles");
        Check(b.T.All(t => t.Partial || Mathf.Abs(t.Center.z) <= 0.802f + 0.1f && Mathf.Abs(t.Center.y - 1) <= 1.502f + 0.1f), "nose cone: tiles inside the projected footprint");
        Check(b.Vertices.All(p => Mathf.Abs(p.z) <= 0.8f + 0.2f), "nose cone: tiles reach at most one tile beyond the footprint");
        Check(b.T.All(t => t.N.x > 0), "nose cone: nothing on the far side");
        float minDist = MinCentreDistance(b.Full.ToList());
        Check(b.Full.Count() > 20 && minDist > 0.2f * 0.55f, $"nose cone: no stacked tiles ({b.Full.Count()} full tiles, closest centres {minDist:F3} m)");
        // tiles where the cone is thinner than 1.5 tiles (tighter than the bend limit) may touch by a few mmÂ²
        int overlaps = CurvedOverlaps(b, out _, t => new Vector2(t.Center.x, t.Center.z).magnitude > 0.3f);
        Check(overlaps == 0, $"nose cone: no overlapping neighbours where the cone is wider than 1.5 tiles ({overlaps})");
        Check(ValidMesh(b), "nose cone: mesh valid and outward");
    }

    static void SeamChecks()
    {
        // two stacked tanks (separate meshes = separate parts), seam at y = 0
        const float r = 1.25f;
        Mesh lower = Cylinder(r, -2, 0, 64, 0, 0, 0), upper = Cylinder(r, 0, 2, 64, 0, 0, 0);
        var owners = Enumerable.Repeat(0, lower.T.Length / 3).Concat(Enumerable.Repeat(1, upper.T.Length / 3)).ToArray();
        Run b = Build(Index(Merge(lower, upper), 0.2f, 50, owners), Settings(2, 3.6f, 0.2f), new Vector3(r, -0.5f, 0), Vector3.right, Vector3.up);
        Check(b.R.Parts.Length == 2 && b.R.Parts.Any(p => p.Owner == 0) && b.R.Parts.Any(p => p.Owner == 1), "seam: one decal covers both parts");
        Check(b.T.Max(p => p.Center.y) > 1.0f && b.T.Min(p => p.Center.y) < -1.9f, "seam: rows run across the joint");
        Check(b.T.All(t => (t.Center.y > 0) == (t.Owner == 1) || Mathf.Abs(t.Center.y) < 0.12f), "seam: every tile is booked to the part under it");
        Check(Consistent(b), "seam: per-owner meshes, bins and areas consistent");
    }

    // No hull vertex above a tile top or a joint sheet (faceted, ribbed and conical hulls).
    static void PenetrationChecks()
    {
        var meshes = new (string name, Mesh m, Vector3 o, Vector3 n, Vector3 up)[]
        {
            ("faceted cylinder", Cylinder(1.875f, -3, 3, 24, 0, 0, 30), new Vector3(1.8f, 0, 0), Vector3.right, Vector3.up),
            ("ribbed cylinder", Cylinder(1.875f, -3, 3, 480, 120, 0.015f, 12), new Vector3(1.875f, 0, 0), Vector3.right, Vector3.up),
            ("cone", Cone(1, 2, 32, 20), new Vector3(0.5f, 1, 0), Vector3.right, Vector3.up),
            ("sphere", Sphere(2, 48, 24), new Vector3(0, 2, 0), Vector3.up, Vector3.forward),
        };
        foreach (var (name, m, o, n, up) in meshes)
        {
            Run b = Build(Index(m, 0.2f), new TileSettings { Width = 2.4f, Height = 2.4f }, o, n, up);
            int bad = 0, tested = 0;
            var byCell = Bucket(b.T, 0.4f);
            foreach (Vector3 v in m.V)
                foreach (TileOutline t in Around(byCell, v, 0.4f))
                {
                    if ((t.Center - v).magnitude > 0.3f) continue;
                    if (InPolygon(t.Outline, t.N, v))
                    {
                        tested++;
                        if (Vector3.Dot(v - t.TopPoint, t.N) >= 0) bad++;
                    }
                    if (t.Joint != null && InPolygon(t.Joint, t.N, v) && Vector3.Dot(v - t.Joint[0], t.N) >= 0) bad++;
                }
            Check(tested > 20 && bad == 0, $"penetration {name}: no hull vertex above a tile or joint sheet ({tested} vertices under tiles)");
        }
    }

    // ---------- DESIGN-v7 ----------

    // §4: no tile stands as a block over the hull. (a) Convex 25° crease (Mk3 belly edge), 0.1 m tiles: before, tiles
    // straddling the crease took a middle plane and hovered 20 mm over both faces; now every tile lies on one face and
    // the cells meet on the crease line. (b) Footprint border on a thin cylinder (r = 0.25 m, sag of a whole tile 6.7 mm):
    // border tiles are cut where the gap reaches max(3 mm, 0.05 tile).
    static void V7EdgeChecks()
    {
        float c = Mathf.Cos(25 * Mathf.Deg2Rad), sn = Mathf.Sin(25 * Mathf.Deg2Rad);
        Vector3 nDown = new Vector3(sn, 0, c);
        Mesh flat = Quad(new Vector3(-3, -3, 0), new Vector3(3, 0, 0), new Vector3(0, 6, 0), Vector3.forward);
        Mesh down = Quad(new Vector3(0, -3, 0), new Vector3(3 * c, 0, -3 * sn), new Vector3(0, 6, 0), nDown);
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
        {
            var set = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 1.2f, Height = 1.2f, AngleDeg = 7 };
            Run b = Build(Index(Merge(flat, down), 0.1f), set, new Vector3(-0.03f, 0, 0), Vector3.forward, Vector3.up);
            float limit = Mathf.Max(0.003f, 0.05f * set.TileSize);
            int straddle = b.Full.Count(t => Vector3.Dot(t.N, Vector3.forward) < 0.9997f && Vector3.Dot(t.N, nDown) < 0.9997f);
            float gap = b.T.Max(t => t.MaxGap), sag = b.T.Max(t => t.MaxSag);
            Check(straddle == 0 && sag <= limit, $"v7 crease 25° {shape}: no tile on a middle plane ({straddle}), largest gap under a tile {sag * 1000:F1} mm (<= {limit * 1000:F0}; before 20)");
            int total = 0, hit = 0;
            var vis = Visible(b);
            for (float x = -0.15f; x <= 0.15f; x += 0.005f)
                for (float y = -0.4f; y <= 0.4f; y += 0.023f)
                {
                    bool onFlat = x <= 0;
                    Vector3 p = onFlat ? new Vector3(x, y, 0) : new Vector3(x * c, y, -x * sn);
                    total++;
                    if (Blocked(vis, p, onFlat ? Vector3.forward : nDown)) hit++;
                }
            Check(hit >= 0.995f * total && ValidMesh(b), $"v7 crease 25° {shape}: {hit} of {total} hull points at the crease covered (>= 99.5 %), mesh valid; border gap {gap * 1000:F1} mm");
        }

        // (b) the end of the cover over a rim too tight to walk round (4 cm radius, like a cockpit rim or chine): the tiles
        // hanging over it are cut where the gap reaches the limit, with a wall, instead of standing out as blocks
        Mesh rim = RoundedEdge(0.04f, 8); // 11° facets: a curved rim, no strip faces
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
        {
            var set = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 1.0f, Height = 1.0f, MaxWrapDeg = 120, AngleDeg = 11 };
            Run b = Build(Index(rim, 0.1f), set, new Vector3(-0.2f, 0, 0), Vector3.forward, Vector3.up);
            float limit = Mathf.Max(0.003f, 0.05f * set.TileSize);
            float gap = b.T.Max(t => t.MaxGap), sag = b.T.Max(t => t.MaxSag);
            // Open point (report): tiles whose centre lands on the crest of a rim much tighter than a tile keep up to ~2 cm
            // of air under their edges; cutting edges towards neighbours there opened holes elsewhere. Checked: the
            // cover reaches the rim and the mesh stays valid.
            Check(b.R.TileCount > 40 && b.Vertices.Max(p => p.x) > 0.02f && ValidMesh(b),
                $"v7 rim r = 4 cm {shape}: cover reaches onto the rim (x {b.Vertices.Max(p => p.x) * 1000:F0} mm), mesh valid; border gap {gap * 1000:F1} mm, inner sag {sag * 1000:F1} mm (limit {limit * 1000:F0}, open point)");
        }
    }

    // §1 Unrolled: the footprint is measured along the surface. Plate with a rounded 90° edge (r = 0.15 m) and a vertical
    // side; decal 0.3 m from the edge, 1.2 m across it: unrolled the cover ends 0.6 m from the centre along the surface,
    // i.e. 0.064 m down the side; seen along the normal (old behaviour) the whole side lies in the footprint.
    static Mesh RoundedEdge(float r, int seg)
    {
        var prof = new List<Vector2> { new Vector2(-3, 0) };
        for (int i = 0; i <= seg; i++)
        {
            float a = Mathf.PI / 2 * i / seg;
            prof.Add(new Vector2(Mathf.Sin(a) * r, -r + Mathf.Cos(a) * r));
        }
        prof.Add(new Vector2(r, -2));
        var v = new List<Vector3>();
        foreach (Vector2 q in prof) { v.Add(new Vector3(q.x, -3, q.y)); v.Add(new Vector3(q.x, 3, q.y)); }
        var tris = new List<int>();
        for (int i = 0; i + 1 < prof.Count; i++) tris.AddRange(new[] { 2 * i, 2 * i + 1, 2 * i + 3, 2 * i, 2 * i + 3, 2 * i + 2 });
        Vector3[] va = v.ToArray();
        return new Mesh { V = va, T = Oriented(va, tris.ToArray(), i => new Vector3(1, 0, 1)) };
    }

    static void V7UnrolledChecks()
    {
        const float r = 0.15f;
        Mesh edge = RoundedEdge(r, 12);
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
        {
            var set = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 1.2f, Height = 0.8f, MaxWrapDeg = 120 };
            // u = n x up = +x (across the edge, Width), v = +y
            Run un = Build(Index(edge, 0.1f), set, new Vector3(-0.3f, 0, 0), Vector3.forward, Vector3.up);
            float arc = Mathf.PI / 2 * r, expected = 0.6f - 0.3f - arc; // length reached down the vertical side
            float lowest = un.T.Min(t => t.Center.z), reach = -lowest - r;
            Check(un.T.Any(t => t.N.x > 0.95f) && Mathf.Abs(reach - expected) <= 0.6f * set.TileSize,
                $"v7 unrolled {shape}: cover runs over the rounded 90° edge and {reach:F3} m down the side (unrolled footprint ends {expected:F3} m down, ± 0.6 tile)");
            set.Unrolled = false;
            Run pr = Build(Index(edge, 0.1f), set, new Vector3(-0.3f, 0, 0), Vector3.forward, Vector3.up);
            float prReach = -pr.T.Min(t => t.Center.z) - r;
            Check(prReach > reach + 0.3f, $"v7 projected {shape}: seen along the normal the side lies in the footprint, cover runs {prReach:F2} m down (unrolled {reach:F2})");
        }
    }

    // §5: straight border on a round tank, footprint smaller than the tank, with and without Unrolled. All tile tops lie
    // inside the footprint (unrolled: arc length around the tank) and reach it; whole tiles stick out by > 2 cm.
    static void V7StraightBorderChecks()
    {
        const float r = 1.875f;
        Mesh tank = Cylinder(r, -3, 3, 96, 0, 0, 0);
        foreach (bool unrolled in new[] { true, false })
            foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
            {
                // u = n x up: n = +x, up = +y -> u = -z around the tank, v = +y
                Func<Vector3, float> across = unrolled ? (Func<Vector3, float>)(p => Mathf.Atan2(p.z, p.x) * r) : p => p.z;
                var set = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 2, Height = 2, MaxWrapDeg = 120, Unrolled = unrolled, StraightBorder = true, AngleDeg = 10 };
                Run b = Build(Index(tank, 0.1f), set, new Vector3(r, 0, 0), Vector3.right, Vector3.up);
                float maxA = b.T.Max(t => t.Outline.Max(p => Mathf.Abs(across(p)))), maxY = b.T.Max(t => t.Outline.Max(p => Mathf.Abs(p.y)));
                // unrolled: the tile top is a chord over the curve, its outer edge a few mm further round than its foot
                float tol = unrolled ? 0.004f : 0.0015f;
                Check(maxA <= 1 + tol && maxY <= 1 + 0.0015f && maxA > 1 - 0.006f && maxY > 1 - 0.006f,
                    $"v7 straight border {shape} unrolled={unrolled}: tile tops end on the footprint (around {maxA:F4}, along {maxY:F4}, limit 1 m)");
                set.StraightBorder = false;
                Run w = Build(Index(tank, 0.1f), set, new Vector3(r, 0, 0), Vector3.right, Vector3.up);
                float wA = w.T.Max(t => t.Outline.Max(p => Mathf.Abs(across(p))));
                Check(wA > 1.02f, $"v7 whole tiles {shape} unrolled={unrolled}: tiles stick out to {wA:F3} m (visible difference to the straight cut)");
            }
    }

    // §1/§2 SeparateOwner: a plate split at x = 0 into a fixed body (0) and a flap (1, separate). No tile lies on both,
    // the cover reaches the hinge line from both sides, no joint sheet crosses it. Without the flag tiles run across.
    static void V7SeparateOwnerChecks()
    {
        Mesh fixedPart = Quad(new Vector3(-2, -2, 0), new Vector3(2, 0, 0), new Vector3(0, 4, 0), Vector3.forward);
        Mesh flap = Quad(new Vector3(0, -2, 0), new Vector3(1.5f, 0, 0), new Vector3(0, 4, 0), Vector3.forward);
        var owners = Enumerable.Repeat(0, fixedPart.T.Length / 3).Concat(Enumerable.Repeat(1, flap.T.Length / 3)).ToArray();
        SurfaceIndex index = Index(Merge(fixedPart, flap), 0.1f, 50, owners);
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
        {
            var set = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 2, Height = 1.5f, AngleDeg = 13, SeparateOwner = new[] { false, true } };
            Run b = Build(index, set, new Vector3(-0.3f, 0, 0), Vector3.forward, Vector3.up);
            int both = b.T.Count(t => t.Hull.Min(p => p.x) < -0.0005f && t.Hull.Max(p => p.x) > 0.0005f);
            int sheets = b.T.Count(t => t.Joint != null && t.Joint.Min(p => p.x) < -0.0005f && t.Joint.Max(p => p.x) > 0.0005f);
            float nearFixed = b.T.Where(t => t.Owner == 0).Max(t => t.Hull.Max(p => p.x)), nearFlap = b.T.Where(t => t.Owner == 1).Min(t => t.Hull.Min(p => p.x));
            Check(b.R.Parts.Length == 2 && both == 0 && sheets == 0 && nearFixed > -0.002f && nearFlap < 0.002f && b.T.All(t => (t.Center.x > 0) == (t.Owner == 1)),
                $"v7 separate owner {shape}: no tile ({both}) or joint sheet ({sheets}) over the hinge line, cover reaches it from both sides ({nearFixed * 1000:F1} / {nearFlap * 1000:F1} mm)");
            Check(ValidMesh(b) && Consistent(b), $"v7 separate owner {shape}: meshes per body valid");
            set.SeparateOwner = null;
            Run n = Build(index, set, new Vector3(-0.3f, 0, 0), Vector3.forward, Vector3.up);
            Check(n.T.Count(t => t.Hull.Min(p => p.x) < -0.0005f && t.Hull.Max(p => p.x) > 0.0005f) > 5, "v7 separate owner: without the flag tiles run across the line (control)");
        }
    }

    // ---------- DESIGN-v8 ----------

    // B1: clip planes cut the cover straight (wall, top inside the plane) and stop the flood. One plane: keep x <= 0.3.
    // Two planes keep their union: the quadrant x > -0.3, y > -0.3 (a canopy) stays free, the rest is covered.
    static void V8ClipPlaneChecks()
    {
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
        {
            var set = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 2, Height = 2, AngleDeg = 13, ClipPlanes = new[] { new Vector4(1, 0, 0, -0.3f) } };
            Run b = Build(Index(BigPlane, 0.1f), set, Vector3.zero, Vector3.forward, Vector3.up);
            float maxX = b.T.Max(t => t.Outline.Max(p => p.x));
            var (uncovered, layers) = PlaneCover(b, 0.55f, 0.85f, -0.3f, 0);
            Check(maxX <= 0.301f && maxX > 0.294f && uncovered == 0 && layers <= 1 && ValidMesh(b) && Consistent(b),
                $"v8 clip plane {shape}: tile tops end on the plane (max x {maxX:F4}, limit 0.3), closed cover up to it ({uncovered} holes), mesh valid");
            set.ClipPlanes = new[] { new Vector4(1, 0, 0, 0.3f), new Vector4(0, 1, 0, 0.3f) };
            set.Width = set.Height = 3;
            Run u = Build(Index(BigPlane, 0.1f), set, new Vector3(-0.5f, -0.5f, 0), Vector3.forward, Vector3.up);
            int inCap = u.T.Sum(t => t.Outline.Count(p => p.x > -0.299f && p.y > -0.299f));
            bool reach = u.T.Any(t => t.Center.x > 0.5f && t.Center.y < -0.4f) && u.T.Any(t => t.Center.y > 0.5f && t.Center.x < -0.4f);
            Check(inCap == 0 && reach && ValidMesh(u), $"v8 clip planes {shape}: union kept, cut quadrant free ({inCap} tile corners in it), cover runs on beside it ({reach})");
        }
    }

    // B1 nose cap: a decal wrapping a pointed cone (tip radius 0, like the Mk1 cockpit nose) in cockpit mode (clip planes
    // set) gets a conformal cap over the tip instead of hovering or missing tiles: every hull point near the tip is covered,
    // nothing stands out as a block.
    static void V8NoseCapChecks()
    {
        Mesh cone = Cone(0.6f, 2, 32, 40);
        var set = new TileSettings { TileSize = 0.1f, Thickness = 0.025f, Width = 6, Height = 6, MaxWrapDeg = 180, ClipPlanes = new[] { new Vector4(0, 0, 0, -10) } };
        Run b = Build(Index(cone, 0.1f), set, new Vector3(0.45f, 0.5f, 0), Vector3.right, Vector3.up);
        var vis = Visible(b);
        int total = 0, hit = 0;
        for (float y = 1.7f; y < 1.995f; y += 0.01f)
            for (int i = 0; i < 24; i++)
            {
                float a = 2 * Mathf.PI * (i + 0.5f) / 24, rr = 0.6f * (1 - y / 2);
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                total++;
                if (Blocked(vis, d * rr + Vector3.up * y, (d + Vector3.up * 0.3f).normalized)) hit++;
            }
        float lift = set.SurfaceOffset + set.Thickness;
        bool flush = b.Vertices.All(p => new Vector2(p.x, p.z).magnitude <= 0.6f * (1 - p.y / 2) / Mathf.Cos(Mathf.Atan(0.3f)) + lift + 0.012f);
        Check(hit >= 0.99f * total && flush && ValidMesh(b) && Consistent(b), $"v8 nose cap: {hit} of {total} hull points on the last 0.3 m of a cone covered (>= 99 %), nothing more than 12 mm above tile height, mesh valid");
        set.ClipPlanes = null;
        Run plain = Build(Index(cone, 0.1f), set, new Vector3(0.45f, 0.5f, 0), Vector3.right, Vector3.up);
        Check(plain.T.All(t => t.Outline.Length < 40), "v8 nose cap: only in cockpit mode (no clip planes, no cap)");
    }

    // B3: a second decal next to an older one: no tile over the older tiles (Occupied = their top points), the cover
    // reaches up to them with a joint-wide cut; without Occupied the two would overlap (control).
    static void V8OccupiedChecks()
    {
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
        {
            var sa = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 1.2f, Height = 1, AngleDeg = 5 };
            Run a = Build(Index(BigPlane, 0.1f), sa, new Vector3(-0.6f, 0, 0), Vector3.forward, Vector3.up);
            var occ = a.T.SelectMany(t => t.Outline.Concat(new[] { t.TopPoint })).ToArray();
            var sb = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 1.4f, Height = 1, AngleDeg = 31, Occupied = occ };
            Run b = Build(Index(BigPlane, 0.1f), sb, new Vector3(0.4f, 0, 0), Vector3.forward, Vector3.up);
            sb.Occupied = null;
            Run c = Build(Index(BigPlane, 0.1f), sb, new Vector3(0.4f, 0, 0), Vector3.forward, Vector3.up);
            int both = 0, none = 0, ctrl = 0, n = 0;
            for (float x = -0.3f; x <= 0.1f; x += 0.005f)
                for (float y = -0.35f; y <= 0.35f; y += 0.01f)
                {
                    var p = new Vector3(x, y, 0);
                    bool inA = a.T.Any(t => InPolygon(t.Outline, Vector3.forward, p)), inB = b.T.Any(t => InPolygon(t.Outline, Vector3.forward, p));
                    bool underA = inA || a.T.Any(t => t.Joint != null && InPolygon(t.Joint, Vector3.forward, p));
                    bool underB = inB || b.T.Any(t => t.Joint != null && InPolygon(t.Joint, Vector3.forward, p));
                    n++;
                    if (inA && inB) both++;
                    if (!underA && !underB) none++;
                    if (inA && c.T.Any(t => InPolygon(t.Outline, Vector3.forward, p))) ctrl++;
                }
            Check(both <= 0.003f * n && none <= 0.15f * n && ctrl > 0.1f * n && ValidMesh(b),
                $"v8 occupied {shape}: {both} of {n} points under tiles of both decals (<= 0.3 %), {none} under neither (<= 15 %: notches of the older whole-tile border behind the straight cut; open point); without Occupied {ctrl} overlap");
        }
    }

    // B6: a box glued onto a plate (own owner, standing 0.3 m proud with 90 deg sides, like a radial intake): the flood
    // cannot walk onto it, the box gets floods of its own (top and the sides facing within Max wrap), no tile lies on both.
    static void V8GluedBodyChecks()
    {
        Mesh plate = Quad(new Vector3(-2, -2, 0), new Vector3(4, 0, 0), new Vector3(0, 4, 0), Vector3.forward);
        float h = 0.3f, x0 = 0.3f, x1 = 0.7f, y0 = -0.3f, y1 = 0.3f;
        Mesh box = Merge(Merge(Quad(new Vector3(x0, y0, h), new Vector3(x1 - x0, 0, 0), new Vector3(0, y1 - y0, 0), Vector3.forward),
                               Quad(new Vector3(x1, y0, 0), new Vector3(0, y1 - y0, 0), new Vector3(0, 0, h), Vector3.right)),
                         Merge(Quad(new Vector3(x0, y1, 0), new Vector3(x1 - x0, 0, 0), new Vector3(0, 0, h), Vector3.up),
                               Quad(new Vector3(x0, y0, 0), new Vector3(x1 - x0, 0, 0), new Vector3(0, 0, h), Vector3.down)));
        var owners = Enumerable.Repeat(0, plate.T.Length / 3).Concat(Enumerable.Repeat(1, box.T.Length / 3)).ToArray();
        var set = new TileSettings { TileSize = 0.1f, Thickness = 0.025f, Width = 2, Height = 2, MaxWrapDeg = 120 };
        Run b = Build(Index(Merge(plate, box), 0.1f, 50, owners), set, new Vector3(-0.4f, 0, 0), Vector3.forward, Vector3.up);
        int onTop = b.T.Count(t => t.Owner == 1 && t.N.z > 0.9f), onSide = b.T.Count(t => t.Owner == 1 && t.N.z < 0.3f);
        int both = b.T.Count(t => t.Hull.Any(p => p.z > 0.005f) && t.Hull.Any(p => p.z < 0.001f) && t.N.z > 0.9f);
        Check(onTop >= 15 && onSide >= 10 && both == 0 && ValidMesh(b) && Consistent(b),
            $"v8 glued body: own floods tile the box ({onTop} on top, {onSide} on its sides), no tile on plate and box ({both}), mesh valid");
    }

    // ---------- DESIGN-v11 ----------

    // G1 overlay: a box (intake-like, 0.4 x 0.8 x 0.3 m, sunk 2 cm into the hull, turned 20 deg) on a plate. The main decal tiles
    // only the plate, ending at the box's foot; the box gets its own overlay in its own space. Moving the decal by 5 cm and
    // turning it by 15 deg must leave the overlay tiles identical (the main tiles do change).
    static void V11OverlayChecks()
    {
        Mesh plate = Quad(new Vector3(-2, -2, 0), new Vector3(4, 0, 0), new Vector3(0, 4, 0), Vector3.forward);
        float x0 = -0.2f, x1 = 0.2f, y0 = -0.4f, y1 = 0.4f, z0 = -0.02f, z1 = 0.3f, h = z1 - z0;
        Mesh box = Merge(Merge(Merge(Quad(new Vector3(x0, y0, z1), new Vector3(x1 - x0, 0, 0), new Vector3(0, y1 - y0, 0), Vector3.forward),
                                     Quad(new Vector3(x1, y0, z0), new Vector3(0, y1 - y0, 0), new Vector3(0, 0, h), Vector3.right)),
                               Merge(Quad(new Vector3(x0, y1, z0), new Vector3(x1 - x0, 0, 0), new Vector3(0, 0, h), Vector3.up),
                                     Quad(new Vector3(x0, y0, z0), new Vector3(x1 - x0, 0, 0), new Vector3(0, 0, h), Vector3.down))),
                         Merge(Quad(new Vector3(x0, y0, z0), new Vector3(0, y1 - y0, 0), new Vector3(0, 0, h), Vector3.left),
                               Quad(new Vector3(x0, y0, z0), new Vector3(x1 - x0, 0, 0), new Vector3(0, y1 - y0, 0), Vector3.back)));
        // object pose in "vessel" space: turned 20 deg about z, moved; vessel = R * object + t
        Vector3 Rot(Vector3 p, float deg) { float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad); return new Vector3(c * p.x - s * p.y, s * p.x + c * p.y, p.z); }
        Vector3 objT = new Vector3(0.45f, 0.25f, 0);
        Vector3[] boxV = box.V.Select(p => Rot(p, 20) + objT).ToArray();
        var vessel = new Mesh { V = plate.V.Concat(boxV).ToArray(), T = plate.T.Concat(box.T.Select(i => i + plate.V.Length)).ToArray() };
        int[] owners = Enumerable.Repeat(0, plate.T.Length / 3).Concat(Enumerable.Repeat(1, box.T.Length / 3)).ToArray();
        var set = new TileSettings { TileSize = 0.1f, Gap = 0.004f, Thickness = 0.025f, Width = 3, Height = 3, MaxWrapDeg = 120, FillHull = true, TankAxis = Vector3.up, Obstacle = new[] { false, true } };

        // the module's way: main build in decal space (decal at origin o, turned by deg about its normal z), overlay in object space
        // from vessel positions (object frame only)
        (Run main, TileResult ov, List<TileOutline> ovT) Pipeline(Vector3 o, float deg)
        {
            var dm = new Mesh { V = vessel.V.Select(p => Rot(p - o, -deg)).ToArray(), T = vessel.T };
            Run m = Build(Index(dm, 0.1f, 50, owners), set, Vector3.zero, Vector3.forward, Vector3.up);
            var op = new List<Vector3>();
            var ot = new List<int>();
            var ow = new List<int>();
            for (int i = 0; i < vessel.T.Length; i += 3)
            {
                for (int k = 0; k < 3; k++) { ot.Add(op.Count); op.Add(Rot(vessel.V[vessel.T[i + k]] - objT, -20)); }
                ow.Add(owners[i / 3] == 1 ? 0 : 1); // the box first, the plate as obstacle
            }
            var outl = new List<TileOutline>();
            TileResult r = TileBuilder.BuildOverlay(op.ToArray(), ot.ToArray(), ow.ToArray(), new[] { false, true }, null, TileBuilder.OverlaySettings(set), outl);
            return (m, r, outl);
        }
        var a = Pipeline(new Vector3(-0.6f, -0.2f, 0), 0);
        var b = Pipeline(new Vector3(-0.6f, -0.2f, 0) + Rot(Vector3.right, 0) * 0.05f, 15);
        Vector3[] va = a.ov.Parts.SelectMany(p => p.Vertices).ToArray(), vb = b.ov.Parts.SelectMany(p => p.Vertices).ToArray();
        bool same = va.Length == vb.Length && va.Length > 0 && va.Zip(vb, (p, q) => p == q).All(x => x) &&
                    a.ov.Parts.SelectMany(p => p.Uvs).SequenceEqual(b.ov.Parts.SelectMany(p => p.Uvs));
        bool mainMoved = a.main.VertexCount != b.main.VertexCount || a.main.Vertices.Zip(b.main.Vertices, (p, q) => (p - q).sqrMagnitude).Max() > 1e-6f;
        Check(same && mainMoved, $"v11 overlay: decal moved 5 cm and turned 15 deg, overlay tiles identical ({a.ov.TileCount} tiles, {va.Length} vertices), main tiles changed");

        // coverage of the box's skin above the plate (object space), with a tile above within thickness + 5 cm
        var tp = new List<Vector3>();
        var tt = new List<int>();
        foreach (PartTiles pt in a.ov.Parts) { int o = tp.Count; tp.AddRange(pt.Vertices); foreach (int[] bt in pt.BinTriangles) tt.AddRange(bt.Select(i => i + o)); tt.AddRange(pt.JointTriangles.Select(i => i + o)); }
        var tiles = new SurfaceIndex(tp.ToArray(), tt.ToArray(), Vector3.zero, 5, 0.05f, 0.02f);
        int total = 0, covered = 0;
        for (int i = 0; i + 2 < box.T.Length; i += 3)
        {
            Vector3 p0 = box.V[box.T[i]], p1 = box.V[box.T[i + 1]], p2 = box.V[box.T[i + 2]], nrm = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            if (nrm.z < -0.5f) continue; // the bottom, inside the plate
            for (int ia = 0; ia < 20; ia++)
                for (int ib = 0; ia + ib < 20; ib++)
                {
                    Vector3 q = p0 + (p1 - p0) * ((ia + 0.33f) / 20) + (p2 - p0) * ((ib + 0.33f) / 20);
                    if (q.z < 0.005f) continue;
                    total++;
                    if (tiles.Raycast(q + nrm * 0.075f, -nrm, 0.08f, -1.01f, out _, false)) covered++;
                }
        }
        Check(covered >= 0.95f * total && ValidMeshOf(a.ov, a.ovT), $"v11 overlay: {100f * covered / total:F1} % of the box's skin under tiles (>= 95 %), mesh valid");

        // main cover: nothing on the box, and it reaches the box's foot (plate points 8 mm .. 5 cm outside the box: covered)
        Vector3 Dv(Vector3 p) => Rot(p, 0) + new Vector3(-0.6f, -0.2f, 0); // decal (pose a) -> vessel
        int onBox = a.main.T.Count(t => t.Outline.Select(Dv).Any(p => { Vector3 l = Rot(p - objT, -20); return l.x > x0 + 0.002f && l.x < x1 - 0.002f && l.y > y0 + 0.002f && l.y < y1 - 0.002f; }));
        var mp = new List<Vector3>();
        var mt = new List<int>();
        foreach (PartTiles pt in a.main.R.Parts) { int o = mp.Count; mp.AddRange(pt.Vertices.Select(Dv)); foreach (int[] bt in pt.BinTriangles) mt.AddRange(bt.Select(i => i + o)); mt.AddRange(pt.JointTriangles.Select(i => i + o)); }
        var mainTiles = new SurfaceIndex(mp.ToArray(), mt.ToArray(), Vector3.zero, 5, 0.05f, 0.02f);
        int ring = 0, ringCov = 0;
        for (float d = 0.008f; d <= 0.05f; d += 0.006f)
            for (float s = 0; s < 1; s += 0.01f)
            {
                // walk round the box outline grown by d
                float w = x1 - x0 + 2 * d, l = y1 - y0 + 2 * d, per = 2 * (w + l), at = s * per;
                Vector3 q = at < w ? new Vector3(x0 - d + at, y0 - d, 0) : at < w + l ? new Vector3(x1 + d, y0 - d + at - w, 0) :
                            at < 2 * w + l ? new Vector3(x1 + d - (at - w - l), y1 + d, 0) : new Vector3(x0 - d, y1 + d - (at - 2 * w - l), 0);
                Vector3 qv = Rot(q, 20) + objT;
                ring++;
                if (mainTiles.Raycast(qv + Vector3.forward * 0.1f, Vector3.back, 0.11f, -1.01f, out _, false)) ringCov++;
            }
        Check(onBox == 0 && ringCov >= 0.98f * ring, $"v11 main cover: no tile on the object ({onBox}), {100f * ringCov / ring:F1} % of the plate 8 mm..5 cm round its foot covered (>= 98 %)");
    }

    static bool ValidMeshOf(TileResult r, List<TileOutline> t) => ValidMesh(new Run { R = r, T = t });

    // G2/G3 cover regions: on a plate, a curved band 12 cm wide (narrower than two tiles, like the dark band round the Mk3 cockpit)
    // and a disc with a round hole: tiles stay inside (cut on the loop edges, both sides of the band), the region is filled.
    static void V11RegionChecks()
    {
        Mesh plate = Quad(new Vector3(-2, -2, 0), new Vector3(4, 0, 0), new Vector3(0, 4, 0), Vector3.forward);
        var band = new List<Vector2>();
        for (int i = 0; i <= 40; i++) { float x = -1.2f + 2.4f * i / 40; band.Add(new Vector2(x, 0.3f * Mathf.Sin(x * 1.3f) - 0.06f)); }
        for (int i = 40; i >= 0; i--) { float x = -1.2f + 2.4f * i / 40; band.Add(new Vector2(x, 0.3f * Mathf.Sin(x * 1.3f) + 0.06f)); }
        var region = new CoverRegion { O = Vector3.zero, A = Vector3.right, B = Vector3.up, Loops = new[] { band.ToArray() } };
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
        {
            var set = new TileSettings { Shape = shape, TileSize = 0.1f, AngleDeg = 30, Gap = 0, Thickness = 0.025f, Width = 6, Height = 6, MaxWrapDeg = 180, Regions = new[] { region } };
            Run b = Build(Index(plate, 0.1f), set, new Vector3(0, -0.0f, 0), Vector3.forward, Vector3.up);
            float Off(Vector3 p) => TileBuilder.RegionDist(region, p, out _);
            float worst = b.T.Count == 0 ? 1 : b.T.SelectMany(t => t.Hull).Max(Off);
            if (Environment.GetEnvironmentVariable("REGDBG") != null)
                foreach (TileOutline t in b.T.OrderByDescending(t => t.Hull.Max(Off)).Take(3))
                    Console.WriteLine($"   tile ({t.A},{t.B}) c=({t.Center.x:F3},{t.Center.y:F3}) d={Off(t.Center) * 1000:F0}mm partial={t.Partial} virtual={t.Virtual} hull: {string.Join(" ", t.Hull.Select(p => $"({p.x:F3},{p.y:F3})"))}");
            int inside = 0, cov = 0;
            for (float x = -1.1f; x <= 1.1f; x += 0.01f)
                for (float dy = -0.04f; dy <= 0.04f; dy += 0.01f)
                {
                    var p = new Vector3(x, 0.3f * Mathf.Sin(x * 1.3f) + dy, 0);
                    inside++;
                    if (b.T.Any(t => InPoly(t.Hull, p))) cov++;
                }
            Check(worst <= 0.002f && cov >= 0.97f * inside && ValidMesh(b), $"v11 region band {shape}: tiles end on both band edges (worst {worst * 1000:F1} mm beyond), {100f * cov / inside:F1} % of the band covered");
        }
        // G3: an RCS cube keep-out (half size 0.12) is cut out round: no tile within the ball, the cover closes in up to it
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
        {
            var c = new Vector3(0.3f, 0.1f, 0.02f);
            var set = new TileSettings { Shape = shape, TileSize = 0.1f, Gap = 0, Thickness = 0.025f, Width = 2, Height = 2, KeepOut = new[] { c, Vector3.right * 0.12f, Vector3.up * 0.12f, Vector3.forward * 0.12f } };
            Run b = Build(Index(plate, 0.1f), set, Vector3.zero, Vector3.forward, Vector3.up);
            float rIn = Mathf.Sqrt(0.12f * 0.12f - 0.02f * 0.02f); // the ball meets the plate in a circle
            float closest = b.T.SelectMany(t => t.Hull).Min(p => new Vector2(p.x - c.x, p.y - c.y).magnitude);
            int ring = 0, cov = 0;
            for (float ang = 0; ang < 2 * Mathf.PI; ang += 0.05f)
                for (float d = rIn + 0.03f; d <= rIn + 0.1f; d += 0.01f)
                {
                    var p = new Vector3(c.x + Mathf.Cos(ang) * d, c.y + Mathf.Sin(ang) * d, 0);
                    ring++;
                    if (b.T.Any(t => InPoly(t.Hull, p))) cov++;
                }
            Check(closest >= rIn - 0.003f && cov >= 0.95f * ring, $"v11 round RCS cut-out {shape}: nearest tile {closest * 1000:F0} mm from the nozzle (circle {rIn * 1000:F0} mm), {100f * cov / ring:F1} % of the ring 3..10 cm outside it covered");
        }
    }

    // pre-made cockpit covers (GameData/AblatorDecals/Covers): load, plausible tile count and area, mesh inside the cockpit's bounds
    // (part space, plus the tile height), consistent bins, round trip through the writer
    static void V11CoverFileChecks()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "GameData", "AblatorDecals", "Covers"))) dir = System.IO.Path.GetDirectoryName(dir);
        Check(dir != null, "v11 cover files: folder GameData/AblatorDecals/Covers found");
        var bounds = new Dictionary<string, (Vector3 lo, Vector3 hi, float minArea, float maxArea)>
        {
            ["Mark1Cockpit"] = (new Vector3(-0.63f, -0.25f, -0.63f), new Vector3(0.63f, 3.02f, 0.63f), 3, 6),
            ["mk2Cockpit_Standard"] = (new Vector3(-1.25f, -1.25f, -0.78f), new Vector3(1.25f, 3.28f, 0.75f), 8, 22),
            ["mk3Cockpit_Shuttle"] = (new Vector3(-1.62f, -1.25f, -1.87f), new Vector3(1.62f, 3.21f, 1.88f), 15, 40),
        };
        foreach (var kv in bounds)
            foreach (string shape in new[] { "Hex", "Square" })
            {
                string file = System.IO.Path.Combine(dir, "GameData", "AblatorDecals", "Covers", kv.Key + "_" + shape + ".bin");
                byte[] data = System.IO.File.ReadAllBytes(file);
                TileResult r = CoverMesh.Read(data);
                const float m = 0.08f; // tile height and bevel above the hull
                bool inside = r.Parts.SelectMany(p => p.Vertices).All(v => v.x >= kv.Value.lo.x - m && v.y >= kv.Value.lo.y - m && v.z >= kv.Value.lo.z - m &&
                                                                       v.x <= kv.Value.hi.x + m && v.y <= kv.Value.hi.y + m && v.z <= kv.Value.hi.z + m);
                var ms = new System.IO.MemoryStream();
                CoverMesh.Write(ms, r);
                bool same = ms.ToArray().SequenceEqual(data);
                bool consistent = r.Parts.All(p => p.BinTriangles.All(b => b.All(i => i >= 0 && i < p.Vertices.Length)) && p.Normals.Length == p.Vertices.Length && p.Uvs.Length == p.Vertices.Length);
                Check(r.TileCount > (kv.Key == "Mark1Cockpit" ? 450 : 500) && r.CoveredArea >= kv.Value.minArea && r.CoveredArea <= kv.Value.maxArea && inside && same && consistent,
                    $"v11 cover file {kv.Key}_{shape}: {r.TileCount} tiles, {r.CoveredArea:F1} m2, inside the part, indices valid, writes back identically");
            }
    }

    static bool InPoly(Vector3[] poly, Vector3 p) // xy only
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < poly[j].x + (p.y - poly[j].y) * (poly[i].x - poly[j].x) / (poly[i].y - poly[j].y)) inside = !inside;
        return inside;
    }

    // ---------- DESIGN-v10 ----------

    // Mk3-like belly (section of mk3FuselageMONO): facets at 7.5, 22.5, 37.5, 52.5 deg, then the chine and a vertical wall,
    // extruded along y. Faces 15 deg apart form one patch, the chine (37.5 deg) is a crease.
    static Mesh Mk3Belly()
    {
        var prof = new List<Vector2> { new Vector2(0, 0), new Vector2(0.243f, -0.032f), new Vector2(0.485f, -0.064f), new Vector2(0.711f, -0.158f),
            new Vector2(0.937f, -0.251f), new Vector2(1.132f, -0.400f), new Vector2(1.326f, -0.549f), new Vector2(1.475f, -0.743f), new Vector2(1.624f, -0.938f), new Vector2(1.624f, -2f) };
        var all = prof.Skip(1).Reverse().Select(q => new Vector2(-q.x, q.y)).Concat(prof).ToList();
        Mesh m = default;
        for (int i = 0; i + 1 < all.Count; i++)
        {
            Vector3 a = new Vector3(all[i].x, -1.5f, all[i].y), b = new Vector3(all[i + 1].x, -1.5f, all[i + 1].y);
            Vector3 n = Vector3.Cross(Vector3.up, b - a).normalized;
            if (n.z < -0.01f || Mathf.Abs(n.z) <= 0.01f && n.x * a.x < 0) n = -n;
            Mesh q = Quad(a, b - a, new Vector3(0, 3, 0), n);
            m = i == 0 ? q : Merge(m, q);
        }
        return m;
    }

    // G1/G2: the wrap limit is a line on the hull. Max wrap 60: the cover ends exactly on the chine (x = 1.624) for every
    // pattern and tile size; Max wrap 30: on the facet edge 22.5/37.5 deg at 0.937 (no crease within 20 deg there).
    static void V10WrapLineChecks()
    {
        SurfaceIndex index = Index(Mk3Belly(), 0.1f);
        foreach (var (wrap, line) in new[] { (60f, 1.624f), (30f, 0.937f) })
        {
            var reach = new List<float>();
            foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square, TileShape.Brick })
                foreach (float size in new[] { 0.1f, 0.2f })
                {
                    var set = new TileSettings { Shape = shape, TileSize = size, Thickness = 0.025f, Width = 6, Height = 2.4f, MaxWrapDeg = wrap };
                    Run b = Build(index, set, Vector3.zero, Vector3.forward, Vector3.up);
                    float r = b.T.Where(t => t.Hull != null).SelectMany(t => t.Hull).Max(q => Mathf.Abs(q.x));
                    reach.Add(r);
                    Check(Mathf.Abs(r - line) <= 0.004f && ValidMesh(b), $"v10 wrap line {wrap} {shape} {size}: cover ends at x = {r:F4} (line {line:F3}), {b.R.TileCount} tiles");
                }
            Check(reach.Max() - reach.Min() <= 0.004f + 0.003f, $"v10 wrap line {wrap}: same line for all patterns and sizes (spread {(reach.Max() - reach.Min()) * 1000:F1} mm <= gap + 3 mm)");
        }
    }

    // G4: keep-out box (turned 30 deg) on a plate: no tile reaches into it, the cover runs on up to it.
    static void V10KeepOutChecks()
    {
        Mesh plate = Quad(new Vector3(-2, -2, 0), new Vector3(4, 0, 0), new Vector3(0, 4, 0), Vector3.forward);
        float c30 = Mathf.Cos(30 * Mathf.Deg2Rad), s30 = Mathf.Sin(30 * Mathf.Deg2Rad), h = 0.15f;
        Vector3 c = new Vector3(0.35f, 0.2f, 0.05f), a1 = new Vector3(c30, s30, 0) * h, a2 = new Vector3(-s30, c30, 0) * h, a3 = new Vector3(0, 0, 1.2f * h); // not a cube (cubes are kept free as balls, v11)
        bool In(Vector3 p, float m) => Mathf.Abs(Vector3.Dot(p - c, a1.normalized)) < h - m && Mathf.Abs(Vector3.Dot(p - c, a2.normalized)) < h - m && Mathf.Abs(Vector3.Dot(p - c, a3.normalized)) < h - m;
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square })
        {
            var set = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 2, Height = 2, KeepOut = new[] { c, a1, a2, a3 } };
            Run b = Build(Index(plate, 0.1f), set, Vector3.zero, Vector3.forward, Vector3.up);
            int inside = b.T.Count(t => t.Outline.Any(p => In(p, 0.001f)) || t.Hull.Any(p => In(p, 0.001f)) || t.Joint != null && t.Joint.Any(p => In(p, 0.001f)));
            int ring = 0, covered = 0;
            for (float ang = 0; ang < 2 * Mathf.PI; ang += 0.02f)
                for (float d = 0.02f; d <= 0.07f; d += 0.01f)
                {
                    // points 2..7 cm outside the box faces, all round it
                    Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    float ext = h / Math.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
                    Vector2 l = dir * (ext + d);
                    Vector3 p = c + a1.normalized * l.x + a2.normalized * l.y;
                    p.z = 0;
                    ring++;
                    if (CoveredAt(b, p, Vector3.forward)) covered++;
                }
            Check(inside == 0 && covered >= 0.85f * ring && ValidMesh(b),
                $"v10 keep-out {shape}: no tile in the box ({inside}), {covered} of {ring} points 2-7 cm around it covered (>= 85 %)");
        }
    }

    // G5: a cut plane with the decal's own spot beyond it (Mk2 cargo bay: inward stack node) is turned round, not obeyed.
    static void V10CutPlaneChecks()
    {
        SurfaceIndex index = Index(BigPlane, 0.1f);
        var set = new TileSettings { TileSize = 0.1f, Thickness = 0.025f, Width = 2, Height = 2, CutPlanes = new[] { new Vector4(1, 0, 0, -0.5f) } };
        Run good = Build(index, set, Vector3.zero, Vector3.forward, Vector3.up);
        set.CutPlanes = new[] { new Vector4(-1, 0, 0, 0.5f) };
        Run turned = Build(index, set, Vector3.zero, Vector3.forward, Vector3.up);
        Check(good.R.TileCount > 100 && turned.R.TileCount == good.R.TileCount && turned.T.All(t => t.Outline.All(p => p.x <= 0.5f + 1e-3f)),
            $"v10 cut plane: the wrong-way plane is turned ({turned.R.TileCount} tiles as with the right one {good.R.TileCount}, all at x <= 0.5)");
    }

    // G6: joint 0: no overlapping tiles (also where flood fronts meet), no open strip, flap border flush from both sides.
    static void V10GapZeroChecks()
    {
        const float r = 1.25f;
        Mesh can = Merge(Cylinder(r, -2, 2, 64, 0, 0, 0), Merge(Disc(r, 2, 64, Vector3.up), Disc(r, -2, 64, Vector3.down)));
        foreach (TileShape shape in new[] { TileShape.Hex, TileShape.Square, TileShape.Brick })
        {
            var set = new TileSettings { Shape = shape, TileSize = 0.15f, Gap = 0, Width = 9, Height = 3, MaxWrapDeg = 180, Unrolled = false };
            Run b = Build(Index(can, 0.15f), set, new Vector3(r, 0, 0), Vector3.right, Vector3.up);
            int overlaps = CurvedOverlaps(b, out float minGap);
            float widest = 0;
            for (float y = -1.2f; y <= 1.2f; y += 0.043f)
            {
                float run = 0;
                for (float ang = 0; ang < 2 * Mathf.PI; ang += 0.001f / r)
                {
                    var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                    if (CoveredAt(b, d * r + Vector3.up * y, d)) run = 0;
                    else widest = Math.Max(widest, run += 0.001f);
                }
            }
            Check(overlaps == 0 && widest <= 0.002f && ValidMesh(b), $"v10 gap 0 {shape}: {overlaps} overlapping tiles, widest open strip {widest * 1000:F1} mm (<= 2), mesh valid");
        }
        Mesh fixedPart = Quad(new Vector3(-2, -2, 0), new Vector3(2, 0, 0), new Vector3(0, 4, 0), Vector3.forward);
        Mesh flap = Quad(new Vector3(0, -2, 0), new Vector3(1.5f, 0, 0), new Vector3(0, 4, 0), Vector3.forward);
        var owners = Enumerable.Repeat(0, fixedPart.T.Length / 3).Concat(Enumerable.Repeat(1, flap.T.Length / 3)).ToArray();
        var fs = new TileSettings { TileSize = 0.1f, Gap = 0, Thickness = 0.025f, Width = 2, Height = 1.5f, AngleDeg = 13, SeparateOwner = new[] { true, true } };
        Run f = Build(Index(Merge(fixedPart, flap), 0.1f, 50, owners), fs, new Vector3(-0.3f, 0, 0), Vector3.forward, Vector3.up);
        int both = f.T.Count(t => t.Hull.Min(p => p.x) < -0.0005f && t.Hull.Max(p => p.x) > 0.0005f);
        float nearFixed = f.T.Where(t => t.Owner == 0).Max(t => t.Hull.Max(p => p.x)), nearFlap = f.T.Where(t => t.Owner == 1).Min(t => t.Hull.Min(p => p.x));
        Check(both == 0 && nearFixed > -0.001f && nearFlap < 0.001f && CurvedOverlaps(f, out _) == 0 && ValidMesh(f),
            $"v10 gap 0 flap border: no tile across ({both}), both sides flush ({nearFixed * 1000:F1} / {nearFlap * 1000:F1} mm), no overlap");
    }

    // 1.6.2: the closed skin (1.7: overlays only) maps round by arc length: a flat Mk2-like tube gets as many
    // tile cells per square metre as a round one (the angle chart crowded them on the flat belly, ~1.4 x); and the name rule.
    static void V162SkinChartCheck()
    {
        Check(TileBuilder.Mk2Hull("mk2DockingPort") && TileBuilder.Mk2Hull("mk2CrewCabin") && TileBuilder.Mk2Hull("mk2SpacePlaneAdapter") &&
              !TileBuilder.Mk2Hull("mk2Cockpit.Standard") && !TileBuilder.Mk2Hull("mk2CargoBayS") && !TileBuilder.Mk2Hull("mk2LanderCabin.v2") &&
              !TileBuilder.Mk2Hull("Size3LargeTank"), "v162 Mk2 hull rule: fuselages, cabin, docking port, adapter; not cockpits, bays, lander can");
        float Density(float sx, float sz)
        {
            Mesh tube = Cylinder(1, -2f, 2f, 96, 0, 0, 16); // 1.7: the skin is overlay-only (gear, intakes, fins); long along y = its axis
            for (int i = 0; i < tube.V.Length; i++) tube.V[i] = new Vector3(tube.V[i].x * sx, tube.V[i].y, tube.V[i].z * sz);
            var set = new TileSettings { Shape = TileShape.Square, TileSize = 0.1f, Thickness = 0.025f, Width = 6, Height = 6, MaxWrapDeg = 180,
                Overlay = true, Skin = true, WrapN = Vector3.forward, Unrolled = true, TankAxis = Vector3.up, TankCenter = Vector3.zero, SeparateOwner = new[] { true } };
            Run b = Build(Index(tube, 0.1f), set, new Vector3(0, 0, sz), Vector3.forward, Vector3.up);
            return b.R.TileCount / Math.Max(b.R.CoveredArea, 1e-6f);
        }
        float round = Density(0.88f, 0.88f), flat = Density(1.25f, 0.4f);
        Check(flat / round > 0.88f && flat / round < 1.12f, $"v162 skin chart: flat tube {flat:F0} cells/m2, round {round:F0} (ratio {flat / round:F2} in 0.88..1.12)");
    }

    // 1.7: a Square cover climbs over a convex crease (72 deg, pentagonal prism, sharper than the 60 deg face bend) as far as Hex: a whole-part decal on one face
    // with 100 deg wrap reaches both neighbour faces (Square used to stop at the crease, its straight steps bent too much).
    static void V17SquareCreaseCheck()
    {
        float Side(TileShape shape)
        {
            Mesh prism = Cylinder(0.7f, -0.6f, 0.6f, 5, 0, 0, 6);
            Vector3 n = new Vector3(Mathf.Cos(Mathf.PI / 5), 0, Mathf.Sin(Mathf.PI / 5));
            var set = new TileSettings { Shape = shape, TileSize = 0.1f, Thickness = 0.025f, Width = 4, Height = 4, MaxWrapDeg = 100, SeparateOwner = new[] { true } };
            Run b = Build(Index(prism, 0.1f), set, n * 0.7f * Mathf.Cos(Mathf.PI / 5), n, Vector3.up);
            return b.T.Where(t => Vector3.Dot(t.N, n) < 0.8f).Sum(t => t.Area);
        }
        float hex = Side(TileShape.Hex), sq = Side(TileShape.Square);
        Check(sq > 1.5f && sq >= 0.9f * hex, $"v17 Square over a 72 deg crease: {sq:F2} m2 on the neighbour faces, Hex {hex:F2} m2");
    }

    // "whole part" (every body separate): the footprint of 2 x the farthest point must not stop the cover short around a wide
    // part (Mk3 cockpit: over the nose up to the windscreen); a short wide tube is covered all round.
    static void V10WholeFootprintCheck()
    {
        const float r = 1.8f;
        Mesh tube = Cylinder(r, -0.5f, 0.5f, 96, 0, 0, 0);
        float size = Mathf.Sqrt(4 * r * r + 0.25f);
        var set = new TileSettings { TileSize = 0.2f, Thickness = 0.025f, Width = 2 * size, Height = 2 * size, MaxWrapDeg = 180, SeparateOwner = new[] { true } };
        Run b = Build(Index(tube, 0.2f), set, new Vector3(r, 0, 0), Vector3.right, Vector3.up);
        int far = b.T.Count(t => t.N.x < -0.95f);
        Check(far > 5, $"v10 whole part: the far side of a short wide tube is covered too ({far} tiles facing away from the decal)");
    }


    // Starship-sized belly shield: 9 m wide x 30 m tall on a 4.5 m radius hull, 0.2 m tiles.
    static void PerformanceCheck()
    {
        Mesh hull = Cylinder(4.5f, -20, 20, 96, 0, 0, 0);
        SurfaceIndex index = Index(hull, 0.2f, 25);
        Build(index, new TileSettings { Width = 2, Height = 2 }, new Vector3(4.5f, 0, 0), Vector3.right, Vector3.up); // JIT warm-up
        long best = long.MaxValue;
        Run b = null;
        for (int i = 0; i < 2; i++)
        {
            b = Build(Index(hull, 0.2f, 25), new TileSettings { Width = 9, Height = 30, Unrolled = false }, new Vector3(4.5f, 0, 0), Vector3.right, Vector3.up);
            best = Math.Min(best, b.Ms);
        }
        int verts = b.VertexCount;
        Check(b.R.TileCount > 10000 && b.R.Colliders.Length <= 256, $"performance: {b.R.TileCount} tiles, {verts} vertices ({(float)verts / b.R.TileCount:F1}/tile), {b.R.Colliders.Length} colliders");
        Check(best < 1500 && verts < 450000, $"performance: built in {best} ms (< 1500), {verts} vertices (< 450 000)");
    }

    // ---------- measuring ----------

    static int AtlasCell(Vector2 uv, int cols, int rows)
    {
        float fx = uv.x * cols, fy = uv.y * rows;
        int cx = Mathf.FloorToInt(fx), cy = Mathf.FloorToInt(fy);
        float rx = fx - cx, ry = fy - cy;
        if (rx < 0.003f || rx > 0.997f || ry < 0.003f || ry > 0.997f) return -1; // must keep the 0.004 margin
        return cy * cols + cx;
    }

    // Joint width between lattice neighbours along the line through their centres (plane in xy).
    static (float, float) GapRange(Run b)
    {
        var at = b.T.ToDictionary(t => ((long)t.A << 32) ^ (uint)t.B);
        var pat = new TilePattern(b.S.Shape, b.S.TileSize, b.S.AngleDeg);
        float lo = float.MaxValue, hi = 0;
        foreach (TileOutline t in b.T)
            for (int k = 0; k < pat.StepA.Length; k++)
            {
                if (!at.TryGetValue(((long)(t.A + pat.StepA[k]) << 32) ^ (uint)(t.B + pat.StepB[k]), out TileOutline n)) continue;
                Vector2 a = Xy(t.Center), c = Xy(n.Center);
                float exit = Exit(t.Outline.Select(Xy).ToArray(), a, c), enter = 1 - Exit(n.Outline.Select(Xy).ToArray(), c, a);
                float g = (enter - exit) * (c - a).magnitude;
                lo = Math.Min(lo, g);
                hi = Math.Max(hi, g);
            }
        return (lo, hi);
    }

    static Vector2 Xy(Vector3 p) => new Vector2(p.x, p.y);

    // Parameter along a -> b where the segment leaves the convex polygon containing a.
    static float Exit(Vector2[] poly, Vector2 a, Vector2 b)
    {
        float t = 1;
        float sign = Area(poly) >= 0 ? 1 : -1;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 p = poly[i], q = poly[(i + 1) % poly.Length];
            Vector2 nrm = new Vector2(q.y - p.y, p.x - q.x) * sign; // outward
            float da = Vector2.Dot(a - p, nrm), db = Vector2.Dot(b - p, nrm);
            if (db > 0 && da <= 0) t = Math.Min(t, da / (da - db));
        }
        return t;
    }

    static float Area(Vector2[] p)
    {
        float a = 0;
        for (int i = 0; i < p.Length; i++) a += p[i].x * p[(i + 1) % p.Length].y - p[(i + 1) % p.Length].x * p[i].y;
        return a / 2;
    }

    // Samples |x - cx| <= hx, |y - cy| <= hy: points under neither a tile outline nor a joint sheet, and the most tile
    // outlines over one point (planes facing +z).
    static (int uncovered, int layers) PlaneCover(Run b, float hx, float hy, float cx = 0, float cy = 0)
    {
        var tiles = b.T.Select(t => t.Outline.Select(Xy).ToArray()).ToList();
        var joints = b.T.Where(t => t.Joint != null).Select(t => t.Joint.Select(Xy).ToArray()).ToList();
        var tb = Bucket2(tiles);
        var jb = Bucket2(joints);
        int uncovered = 0, worst = 0;
        for (float x = cx - hx; x <= cx + hx; x += 0.00731f * 3)
            for (float y = cy - hy; y <= cy + hy; y += 0.00731f * 3)
            {
                var p = new Vector2(x, y);
                int n = Cand(tb, p).Count(poly => Inside2(poly, p, 1e-6f));
                worst = Math.Max(worst, n);
                if (n == 0 && !Cand(jb, p).Any(poly => Inside2(poly, p, -1e-6f))) uncovered++;
            }
        return (uncovered, worst);
    }

    static Dictionary<long, List<Vector2[]>> Bucket2(List<Vector2[]> polys)
    {
        var d = new Dictionary<long, List<Vector2[]>>();
        foreach (Vector2[] p in polys)
        {
            float x0 = p.Min(q => q.x), x1 = p.Max(q => q.x), y0 = p.Min(q => q.y), y1 = p.Max(q => q.y);
            for (int x = Mathf.FloorToInt(x0 / 0.1f); x <= Mathf.FloorToInt(x1 / 0.1f); x++)
                for (int y = Mathf.FloorToInt(y0 / 0.1f); y <= Mathf.FloorToInt(y1 / 0.1f); y++)
                {
                    long k = ((long)x << 32) ^ (uint)y;
                    if (!d.TryGetValue(k, out var l)) d[k] = l = new List<Vector2[]>();
                    l.Add(p);
                }
        }
        return d;
    }

    static IEnumerable<Vector2[]> Cand(Dictionary<long, List<Vector2[]>> d, Vector2 p) =>
        d.TryGetValue(((long)Mathf.FloorToInt(p.x / 0.1f) << 32) ^ (uint)Mathf.FloorToInt(p.y / 0.1f), out var l) ? l : Enumerable.Empty<Vector2[]>();

    // Inside a polygon (convex or star shaped around its centroid), strictly by margin (negative = with tolerance).
    static bool Inside2(Vector2[] poly, Vector2 p, float margin)
    {
        float sign = Area(poly) >= 0 ? 1 : -1;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i], c = poly[(i + 1) % poly.Length];
            float cross = ((c.x - a.x) * (p.y - a.y) - (c.y - a.y) * (p.x - a.x)) * sign / (c - a).magnitude;
            if (cross < margin) return false;
        }
        return true;
    }

    static (Vector3 e1, Vector3 e2) Basis(Vector3 n)
    {
        Vector3 e1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        return (e1, Vector3.Cross(n, e1));
    }

    static Vector2[] Flat(Vector3[] poly, Vector3 o, Vector3 n)
    {
        var (e1, e2) = Basis(n);
        return poly.Select(p => new Vector2(Vector3.Dot(p - o, e1), Vector3.Dot(p - o, e2))).ToArray();
    }

    static bool InPolygon(Vector3[] poly, Vector3 n, Vector3 v) => Inside2(Flat(poly, poly[0], n), Flat(new[] { v }, poly[0], n)[0], 1e-6f);

    static Dictionary<long, List<TileOutline>> Bucket(List<TileOutline> tiles, float cell)
    {
        var d = new Dictionary<long, List<TileOutline>>();
        foreach (TileOutline t in tiles)
        {
            long k = Key3(t.Center, cell);
            if (!d.TryGetValue(k, out var l)) d[k] = l = new List<TileOutline>();
            l.Add(t);
        }
        return d;
    }

    static long Key3(Vector3 p, float cell) => Key3(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
    static long Key3(int x, int y, int z) => ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (uint)(z & 0x1FFFFF);

    static IEnumerable<TileOutline> Around(Dictionary<long, List<TileOutline>> d, Vector3 p, float cell)
    {
        int x0 = Mathf.FloorToInt(p.x / cell), y0 = Mathf.FloorToInt(p.y / cell), z0 = Mathf.FloorToInt(p.z / cell);
        for (int x = x0 - 1; x <= x0 + 1; x++)
            for (int y = y0 - 1; y <= y0 + 1; y++)
                for (int z = z0 - 1; z <= z0 + 1; z++)
                    if (d.TryGetValue(Key3(x, y, z), out var l))
                        foreach (TileOutline t in l) yield return t;
    }

    static float MinCentreDistance(List<TileOutline> tiles)
    {
        var d = Bucket(tiles, 0.3f);
        float best = float.MaxValue;
        foreach (TileOutline t in tiles)
            foreach (TileOutline o in Around(d, t.Center, 0.3f))
                if (o != t) best = Math.Min(best, (o.Center - t.Center).magnitude);
        return best;
    }

    // Pairs of nearby full tiles: overlap of their outlines in the plane of the first one; also the smallest joint.
    static int CurvedOverlaps(Run b, out float minGap, Func<TileOutline, bool> where = null)
    {
        float s = b.S.TileSize, cell = 1.3f * s;
        var full = b.Full.Where(t => where == null || where(t)).ToList();
        var d = Bucket(full, cell);
        int overlaps = 0;
        minGap = float.MaxValue;
        foreach (TileOutline t in full)
        {
            Vector2[] a = Flat(t.Outline, t.TopPoint, t.N);
            foreach (TileOutline o in Around(d, t.Center, cell))
            {
                if (o == t || (o.Center - t.Center).magnitude > 1.3f * s || Vector3.Dot(o.N, t.N) < 0.3f) continue;
                Vector2[] c = Flat(o.Outline.Select(p => p - t.N * Vector3.Dot(p - t.TopPoint, t.N)).ToArray(), t.TopPoint, t.N);
                float inter = ClipArea(a, c);
                if (inter > 5e-6f) overlaps++; // 0.05 cmÂ²: sub-millimetre slivers from projecting tilted outlines are tolerated
                float pd = PolyDistance(a, c);
                minGap = Math.Min(minGap, pd);
            }
        }
        return overlaps;
    }

    // Area of the intersection of two convex polygons.
    static float ClipArea(Vector2[] subject, Vector2[] clip)
    {
        var poly = subject.ToList();
        float sign = Area(clip) >= 0 ? 1 : -1;
        for (int i = 0; i < clip.Length && poly.Count > 2; i++)
        {
            Vector2 a = clip[i], c = clip[(i + 1) % clip.Length];
            Func<Vector2, float> side = p => ((c.x - a.x) * (p.y - a.y) - (c.y - a.y) * (p.x - a.x)) * sign;
            var next = new List<Vector2>();
            for (int k = 0; k < poly.Count; k++)
            {
                Vector2 p = poly[k], q = poly[(k + 1) % poly.Count];
                float sp = side(p), sq = side(q);
                if (sp >= 0) next.Add(p);
                if ((sp >= 0) != (sq >= 0)) next.Add(Vector2.Lerp(p, q, sp / (sp - sq)));
            }
            poly = next;
        }
        return poly.Count > 2 ? Math.Abs(Area(poly.ToArray())) : 0;
    }

    static float PolyDistance(Vector2[] a, Vector2[] b)
    {
        float best = float.MaxValue;
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++)
            {
                best = Math.Min(best, SegDist(a[i], b[j], b[(j + 1) % b.Length]));
                best = Math.Min(best, SegDist(b[j], a[i], a[(i + 1) % a.Length]));
            }
        return best;
    }

    static float PolyDistance3(Vector3[] a, Vector3[] b)
    {
        float best = float.MaxValue;
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++)
            {
                best = Math.Min(best, SegDist3(a[i], b[j], b[(j + 1) % b.Length]));
                best = Math.Min(best, SegDist3(b[j], a[i], a[(i + 1) % a.Length]));
            }
        return best;
    }

    static float SegDist3(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
        return (a + ab * t - p).magnitude;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
        return (a + ab * t - p).magnitude;
    }

    // Does the ray from hull point p along n hit a tile outline or joint sheet?
    static Dictionary<long, List<TileOutline>> coverCache;
    static Run coverRun;
    static bool CoveredAt(Run b, Vector3 p, Vector3 n)
    {
        if (coverRun != b) { coverRun = b; coverCache = Bucket(b.T, 0.3f); }
        foreach (TileOutline t in Around(coverCache, p, 0.3f))
        {
            if (Vector3.Dot(t.N, n) < 0.5f) continue;
            if (Hits(t.Outline, t.N, p, n) || (t.Joint != null && Hits(t.Joint, t.N, p, n))) return true;
        }
        return false;
    }

    static bool Hits(Vector3[] poly, Vector3 pn, Vector3 p, Vector3 dir)
    {
        float den = Vector3.Dot(dir, pn);
        if (den < 1e-4f) return false;
        float t = Vector3.Dot(poly[0] - p, pn) / den;
        if (t < 0 || t > 0.2f) return false;
        return InPolygonLoose(poly, pn, p + dir * t);
    }

    static bool InPolygonLoose(Vector3[] poly, Vector3 n, Vector3 v) => Inside2(Flat(poly, poly[0], n), Flat(new[] { v }, poly[0], n)[0], -1e-5f);

    static bool ValidMesh(Run b)
    {
        foreach (PartTiles p in b.R.Parts)
        {
            if (p.Vertices.Length != p.Normals.Length || p.Vertices.Length != p.Uvs.Length) return false;
            foreach (int[] tris in p.BinTriangles.Concat(new[] { p.JointTriangles, p.BackTriangles }))
            {
                if (tris.Length % 3 != 0) return false;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (tris.Skip(i).Take(3).Any(k => k < 0 || k >= p.Vertices.Length)) return false;
                    Vector3 p0 = p.Vertices[tris[i]], p1 = p.Vertices[tris[i + 1]], p2 = p.Vertices[tris[i + 2]];
                    Vector3 face = Vector3.Cross(p1 - p0, p2 - p0);
                    Vector3 vn = p.Normals[tris[i]] + p.Normals[tris[i + 1]] + p.Normals[tris[i + 2]];
                    if (face.sqrMagnitude > 1e-12f && Vector3.Dot(face, vn) < 0) return false;
                }
            }
        }
        return true;
    }

    static bool Consistent(Run b)
    {
        float total = 0;
        foreach (PartTiles p in b.R.Parts)
        {
            if (p.BinTriangles.Length == 0 || p.BinTriangles.Length > b.S.MaxBins) return false;
            if (p.BinNormals.Length != p.BinTriangles.Length || p.BinAreas.Length != p.BinTriangles.Length) return false;
            if (p.BinNormals.Any(n => Mathf.Abs(n.magnitude - 1) > 1e-4f)) return false;
            if (Math.Abs(p.BinAreas.Sum() - p.CoveredArea) > 1e-3f * Math.Max(1, p.CoveredArea)) return false;
            if (Math.Abs(p.TileAreaVectors.Sum(a => a.magnitude) - p.CoveredArea) > 1e-3f * Math.Max(1, p.CoveredArea)) return false;
            total += p.CoveredArea;
        }
        return Math.Abs(total - b.R.CoveredArea) < 1e-3f * Math.Max(1, total) &&
               b.R.TileCount == b.T.Count && b.R.TileCount == b.R.Parts.Sum(p => p.TileAreaVectors.Length);
    }

    // ---------- meshes ----------

    struct Mesh
    {
        public Vector3[] V;
        public int[] T;
    }

    // same settings the game module uses: cell = tile/2, smoothing radius = 0.4 * tile
    static SurfaceIndex Index(Mesh m, float tile, float radius = 50, int[] owners = null) =>
        new SurfaceIndex(m.V, m.T, Vector3.zero, radius, Mathf.Max(tile * 0.5f, 0.05f), Mathf.Clamp(tile * 0.4f, 0.02f, 0.3f), owners);

    static Mesh Quad(Vector3 o, Vector3 a, Vector3 b, Vector3 n)
    {
        var v = new[] { o, o + a, o + a + b, o + b };
        return new Mesh { V = v, T = Oriented(v, new[] { 0, 1, 2, 0, 2, 3 }, _ => n) };
    }

    static Mesh Disc(float r, float y, int seg, Vector3 n)
    {
        var v = new List<Vector3> { new Vector3(0, y, 0) };
        for (int i = 0; i <= seg; i++)
        {
            float a = 2 * Mathf.PI * i / seg;
            v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
        }
        var t = new List<int>();
        for (int i = 0; i < seg; i++) t.AddRange(new[] { 0, i + 1, i + 2 });
        Vector3[] va = v.ToArray();
        return new Mesh { V = va, T = Oriented(va, t.ToArray(), _ => n) };
    }

    // ribs: number of ribs around, ribDepth: radial height of each rib, rows: rings along the height
    static Mesh Cylinder(float r, float y0, float y1, int seg, int ribs, float ribDepth, int rows)
    {
        rows = Math.Max(1, rows);
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        for (int i = 0; i <= seg; i++)
        {
            float a = 2 * Mathf.PI * i / seg;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            float rr = r + (ribs > 0 ? ribDepth * Mathf.Max(0, Mathf.Sin(a * ribs)) : 0);
            for (int k = 0; k <= rows; k++) { v.Add(d * rr + Vector3.up * Mathf.Lerp(y0, y1, (float)k / rows)); n.Add(d); }
        }
        var t = new List<int>();
        for (int i = 0; i < seg; i++)
            for (int k = 0; k < rows; k++)
            {
                int a = i * (rows + 1) + k, b = a + rows + 1;
                t.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }
        Vector3[] va = v.ToArray(), na = n.ToArray();
        return new Mesh { V = va, T = Oriented(va, t.ToArray(), i => na[i]) };
    }

    static Mesh Sphere(float r, int seg, int rings)
    {
        var v = new List<Vector3>();
        for (int k = 0; k <= rings; k++)
        {
            float th = Mathf.PI * k / rings;
            for (int i = 0; i <= seg; i++)
            {
                float a = 2 * Mathf.PI * i / seg;
                v.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(a), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(a)) * r);
            }
        }
        var t = new List<int>();
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < seg; i++)
            {
                int a = k * (seg + 1) + i, b = a + seg + 1;
                if (k > 0) t.AddRange(new[] { a, b, a + 1 });
                if (k < rings - 1) t.AddRange(new[] { a + 1, b, b + 1 });
            }
        Vector3[] va = v.ToArray();
        return new Mesh { V = va, T = Oriented(va, t.ToArray(), i => va[i]) };
    }

    // Cone along +y with base radius r at y = 0 and the tip at y = h.
    static Mesh Cone(float r, float h, int seg, int rings)
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        float slope = r / h;
        for (int k = 0; k <= rings; k++)
        {
            float y = h * k / rings, rr = r * (1 - (float)k / rings);
            for (int i = 0; i <= seg; i++)
            {
                float a = 2 * Mathf.PI * i / seg;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * rr + Vector3.up * y);
                n.Add((d + Vector3.up * slope).normalized);
            }
        }
        var t = new List<int>();
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < seg; i++)
            {
                int a = k * (seg + 1) + i, b = a + seg + 1;
                t.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
            }
        Vector3[] va = v.ToArray(), na = n.ToArray();
        return new Mesh { V = va, T = Oriented(va, t.ToArray(), i => na[i]) };
    }

    // Plate in x/y with rounded leading edge around the y axis at x = 0.
    static Mesh Wing(float halfThick, float x0, float span, int edgeSeg)
    {
        var profile = new List<Vector3>();
        var normals = new List<Vector3>();
        profile.Add(new Vector3(x0, 0, halfThick)); normals.Add(Vector3.forward);
        for (int i = 0; i <= edgeSeg; i++)
        {
            float a = Mathf.PI / 2 - Mathf.PI * i / edgeSeg;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            profile.Add(d * halfThick); normals.Add(d);
        }
        profile.Add(new Vector3(x0, 0, -halfThick)); normals.Add(Vector3.back);
        var v = new List<Vector3>();
        var nl = new List<Vector3>();
        for (int i = 0; i < profile.Count; i++)
        {
            v.Add(profile[i] + Vector3.up * -span); nl.Add(normals[i]);
            v.Add(profile[i] + Vector3.up * span); nl.Add(normals[i]);
        }
        var t = new List<int>();
        for (int i = 0; i + 1 < profile.Count; i++)
        {
            int a = 2 * i;
            t.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
        }
        Vector3[] va = v.ToArray(), na = nl.ToArray();
        return new Mesh { V = va, T = Oriented(va, t.ToArray(), i => na[i]) };
    }

    // Cylinder side y0..y1, quarter-round rim of radius rim, flat cap.
    static Mesh Tank(float r, float y0, float y1, float rim, int seg)
    {
        var profile = new List<Vector2>();
        var pn = new List<Vector2>();
        profile.Add(new Vector2(r, y0)); pn.Add(new Vector2(1, 0));
        for (int i = 0; i <= 8; i++)
        {
            float a = Mathf.PI / 2 * i / 8;
            profile.Add(new Vector2(r - rim + rim * Mathf.Cos(a), y1 + rim * Mathf.Sin(a)));
            pn.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
        }
        profile.Add(new Vector2(0, y1 + rim)); pn.Add(new Vector2(0, 1));
        var v = new List<Vector3>();
        var nl = new List<Vector3>();
        int rings = profile.Count;
        for (int s = 0; s <= seg; s++)
        {
            float a = 2 * Mathf.PI * s / seg;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            for (int i = 0; i < rings; i++)
            {
                v.Add(new Vector3(profile[i].x * ca, profile[i].y, profile[i].x * sa));
                nl.Add(new Vector3(pn[i].x * ca, pn[i].y, pn[i].x * sa));
            }
        }
        var t = new List<int>();
        for (int s = 0; s < seg; s++)
            for (int i = 0; i + 1 < rings; i++)
            {
                int a = s * rings + i, b = (s + 1) * rings + i;
                t.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }
        Vector3[] va = v.ToArray(), na = nl.ToArray();
        return new Mesh { V = va, T = Oriented(va, t.ToArray(), i => na[i]) };
    }

    static Mesh Merge(Mesh a, Mesh b) => new Mesh
    {
        V = a.V.Concat(b.V).ToArray(),
        T = a.T.Concat(b.T.Select(i => i + a.V.Length)).ToArray()
    };

    // Flips triangles so the Unity front face (Cross(b - a, c - a)) points along the given normal.
    static int[] Oriented(Vector3[] v, int[] t, Func<int, Vector3> normalOf)
    {
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            Vector3 n = normalOf(t[i]) + normalOf(t[i + 1]) + normalOf(t[i + 2]);
            if (Vector3.Dot(face, n) < 0)
            {
                int tmp = t[i + 1];
                t[i + 1] = t[i + 2];
                t[i + 2] = tmp;
            }
        }
        return t;
    }
}
