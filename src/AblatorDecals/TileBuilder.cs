using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AblatorDecals
{
    public enum UvMode { Atlas, Window, Across }

    public class TileSettings            // plain data, safe to copy to a worker thread
    {
        public TileShape Shape = TileShape.Hex;
        public float TileSize = 0.2f;     // lattice pitch incl. joint: hex flat-to-flat, square/brick edge length
        public float AngleDeg;            // pattern rotation about the decal normal
        public float Gap = 0.004f;        // joint width
        public float Thickness = 0.04f;
        public float Width = 2, Height = 2;   // footprint seen along the decal normal
        public float MaxWrapDeg = 120;
        public bool StraightBorder;       // false: whole tiles at the footprint border
        public float TileLoss;            // 0..0.2, fraction of tiles left out (cosmetic)
        public float SurfaceOffset = 0.003f;
        public float BevelWidth = 0.006f, BevelDepth = 0.005f;
        public float CornerBendDeg = 35, FaceBendDeg = 60, CurvatureFactor = 1;
        public UvMode UvMode = UvMode.Window;
        public int AtlasCols = 4, AtlasRows = 4;
        public bool AtlasRandomRotation;
        public float TextureScale = 2;    // m per repeat (Window/Across; 0 with Across = stretch over footprint)
        public float TextureAspect = 1;   // texture height / width
        public int MaxTiles = 40000, MaxBins = 16;
        public float BinAngleDeg = 20;
        public float ColliderPatchSize = 1; public int MaxColliders = 256;
        public int Seed;                  // per-decal salt for all random choices
        public bool Unrolled = true;      // true: Width/Height measured along the surface (like a sticker); false: seen along the decal normal
        public bool[] SeparateOwner;      // per owner id (optional): true = moving body (control surface flap); tiles never cross its border
        public Vector4[] ClipPlanes;      // optional, decal space: keep where dot(xyz, p) + w <= 0 for at least one plane (removed: where all
                                          // planes are > 0, a convex cap such as a canopy); tiles cut straight on the plane (cockpit cover)
        public Vector4[] CutPlanes;       // optional, decal space: keep only where dot(xyz, p) + w <= 0 for every plane (cockpit cover: the
                                          // stack face at the back, which MaxWrap 180 would reach); cut like ClipPlanes
        public Vector3[] Occupied;        // optional, decal space: points on tiles of older decals; new tiles must not cover them (no stacking)
        public Vector3[] KeepOut;         // optional, decal space, groups of 4: box centre, then its three half-axis vectors (orthogonal,
                                          // length = half size). No tile reaches into a box; tiles are cut straight at its faces (RCS
                                          // nozzles, hatches; DESIGN-v10 G4)
        public CoverRegion[] Regions;     // optional, decal space: kept like ClipPlanes (the union of both); tiles cut straight on the loop
                                          // edges (cockpit mask, DESIGN-v11 G2/G3)
        public bool[] Obstacle;           // optional, per owner id: never tiled, only in the way; tiles are cut where they meet it (DESIGN-v11
                                          // overlay). Main build: the attached objects with an overlay of their own (the cover ends at
                                          // their foot); overlay build: the part they sit on
        public bool Overlay;              // overlay build (DESIGN-v11, see TileBuilder.BuildOverlay): every owner that is no obstacle is
                                          // tiled all over; Width/Height, MaxWrap and clip/cut planes are ignored
        public bool[] PassUnder;          // main build, per owner id (optional): an obstacle the cover runs on under (retracted landing gear);
                                          // under any other obstacle (inside its volume) the cover is cut at the obstacle's outline
        public bool[] WrapOwner;          // overlay, per owner id (optional): only faces within MaxWrapDeg of WrapN (the main decal's normal in
        public Vector3 WrapN;             // the object's space) are tiled, as a line on the hull like the main cover (cargo bay doors and
                                          // ramps follow the main decal's Max wrap; CutPlanes, e.g. its height cut, apply in overlays too)
        public bool[] Inward;             // overlay, per owner id (optional): faces turned towards the origin are no skin (cargo bay doors
                                          // and ramps, origin = the bay's middle: their inner side looks into the bay)
        public bool GearSkin;             // gear triangles were visibility-tested in the retracted pose
        public bool LimitedTank;
        public Vector3 TankAxis, TankCenter; // in decal space, for the side-wall joint bed
        public bool FillHull;             // close gaps on continuous whole-part hulls and cockpit masks
        public bool Skin;                 // overlay (gear, intakes, fins, flaps, shielded port base): no tiles but one closed shell, the visible skin lifted by
                                          // the thickness, the tile pattern projected onto it (joints only in the texture; see RunSkin)
        public float CeramicSideDeg;      // > 0 (cockpit covers): smooth untiled black ceramic shell on the nose tip and both sides/chines (|dot(n, decalN)| <= sin(deg))
    }

    // A kept region of a cockpit cover (DESIGN-v11 G2/G3): closed loops drawn on a chart plane and reaching through the part along
    // the chart normal A x B, e.g. the dark panels of the Mk3 cockpit texture seen from above. Plain data.
    public class CoverRegion
    {
        public Vector3 O, A, B;           // chart origin and axes (unit, orthogonal)
        public Vector2[][] Loops;         // closed loops in chart coordinates (m); inside = inside an odd number of loops (holes allowed)
        public Vector4[] Domain;          // the region applies only where dot(xyz, p) + w <= 0 for all of these (one side of the part)
    }

    public class PartTiles               // tiles lying on one owner (SurfaceHit.Owner)
    {
        public int Owner;
        public Vector3[] Vertices, Normals; public Vector2[] Uvs;   // decal part local space
        public int[][] BinTriangles;      // one submesh per glow bin (tile tops, bevels, walls)
        public Vector3[] BinNormals;      // unit, area-weighted mean normal per bin
        public float[] BinAreas;
        public int[] JointTriangles, BackTriangles;
        public Vector3[] TileAreaVectors; // tile normal * covered area, one per tile (thermal)
        public float CoveredArea;
    }

    public class TileResult
    {
        public bool OnSurface;            // false: nothing found near the origin
        public int TileCount, PartialCount; public bool HitTileLimit;
        public float CoveredArea;
        public PartTiles[] Parts;         // only owners that got tiles
        public TileBuilder.Box[] Colliders;   // thin boxes over tile patches, decal space
    }

    // One drawn tile, for tests and the preview only (Build overload with an outline list).
    public class TileOutline
    {
        public int A, B, Owner;
        public bool Partial, Virtual, Strip;
        public Vector3 Center, N, TopPoint;   // surface point under the tile, tile normal, a point of the top plane
        public Vector3[] Outline;             // outer edge (below the bevel) in the tile plane
        public Vector3[] Joint;               // joint sheet polygon, null for partial tiles
        public Vector3[] Hull;                // outline dropped onto the hull
        public float Area;
        public float MaxGap;                  // largest air gap under the tile bottom beyond SurfaceOffset where the cover ends
        public float MaxSag;                  // the same over all edges (curvature under inner tiles; neighbours hide it)
    }

    // Lays rigid flat tiles onto a surface (DESIGN.md section 2).
    // Layout: flood fill over the tile lattice; every tile reaches its neighbours by walking along the surface, so tiles
    // keep their size on curved hulls and any connected shape is filled. Walking stops at tight bends, the end of the
    // surface and Max wrap; neighbours across such an edge become "virtual" tiles clipped against it. Candidates closer
    // than 0.6 tile to an existing tile on the same side are dropped (stops endless laps around closed hulls and stacking).
    // Cells: each tile is cut from the actual positions of its neighbours (convex clipping of half-planes), so tiles stay
    // regular on planes and cylinders and get individually trimmed on doubly curved hulls with a constant joint.
    // Everything is in one coordinate space (the decal part's local space). Pure managed math, see tests/SelfCheck.
    public class TileBuilder
    {
        public struct Box
        {
            public Vector3 Center, Forward, Up, Size; // Size along (Cross(Up, Forward), Up, Forward) like Quaternion.LookRotation
        }

        private struct Walker
        {
            public Vector3 P, N, U, F;  // position, smoothed normal, u direction, face normal under P
            public Vector3 N1, N2, N3, N4; // normals of the last steps (sliding window for the bend test)
            public float D1, D2, D3, D4;   // distance walked since each of them, 0 = unused
        }

        // edge tags: >= 0 pattern edge with neighbours, else
        // Where flood fronts meet, a tile reaches at most this far (in tile pitches) towards a foreign tile, see Cell.
        private const float SeamReach = 1.7f, SeamGrow = 1.6f;
        // Faces meeting at more than this are separate faces (a crease); below it one curved face.
        private const float CreaseDeg = 20;
        // Strip tiles: faces flat within PlanarDeg, at least MinStrip wide and narrower than 0.9 tile (fillets of small
        // facets don't qualify, tiles stay draped over them).
        private const float PlanarDeg = 8, MinStrip = 0.03f;
        // Smallest partial piece kept (fraction of a cell): slivers next to a crease would otherwise leave holes.
        private const float MinPiece = 0.004f, EmptyGrow = 1.5f;
        private const int TagStrip = -5;     // strip edge along its crease: wall, starts no strip
        private const int TagStripSide = -6; // strip edge to the next strip along the crease: a normal joint
        private const int TagBorder = -1, TagForeign = -2, TagFootprint = -3, TagCut = -4;

        private struct Half
        {
            public Vector2 N;
            public float HTile, HMid, Raw; // tile edge, cell midline (clamped), unclamped midline
            public int Tag;
            public Tile Other;             // foreign tile
        }

        private class Tile
        {
            public int A, B;
            public bool Virtual, Floods, Lost, Dead, Strip;
            public bool[] StripCut;        // per outline segment: a strip lies beyond this cut
            public Vector2 SA, SB, SE, SO; // strip: crease ends, crease direction, across the face
            public Tile Parent;            // strip: the tile cut at its crease, segments K0..K1-1 of its outline
            public int K0, K1;
            public Walker W;
            public Vector3 C, N, U, V;     // lattice centre and walker frame
            public Vector3 F;              // real face normal under the walker point (smoothing leans into edges)
            public float Edge;             // expected height of the outer tile edge above C (Prefit)
            public List<Half> Halves;
            public List<Tile>[] EdgeOwners;
            public float MidArea;
            public int State;              // 0 not drawn, 1 full, 2 partial
            public Vector2[] O;            // outline, 2D in (U, V) around C
            public int[] Tags;             // tag of segment O[k] -> O[k+1]
            public Vector2[][] Seg2;       // sample points per segment (from O[k], excluding O[k+1])
            public Vector3[][] SegHull;    // their dropped hull points
            public Vector3 TileN, Hit;
            public float Htop, Area;
            public int Owner, Bin;
            public Vector3 HP, HF;         // home face: hull point and real face normal under C (crease cells, see Cell)
            public bool[] Crease;          // per pattern edge: the cell ends on a crease line (mitred past it onto the far face)
            public int Body = -1;          // owner under C; with SeparateOwner the tile never leaves it
            public int Region;             // flood it came from: 0 main, > 0 an own start on a body the main flood missed
            public bool GapCut;            // second placement: border samples hovering more than gapLimit are cut
            public float Worst;            // largest gap of the last placement at edges the gap cut may trim
            public Vector3 GapN;
            public float GapMin;           // lowest hull height (along GapN, from C) a border sample may have
            public List<SurfaceIndex.ShellTri> Shell; // nose cap: hull triangles too curved for tiles, drawn conformal (see CapShell)
            public int SplitOther = -1;    // separate body its cell reached over (gets a twin piece there, see Twins)
            public Vector2[] O0; public int[] Tags0; // the cell before placing cut it
            public bool Filler;            // 1.6 filler pseudo tile (see Fill): lower, cell pictures per piece
            public bool DrawnFull => State == 1 && !Lost;
            public bool Drawn => State > 0 && !Lost;
        }

        private class Acc
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<List<int>> Bins = new List<List<int>>();
            public readonly List<Vector3> BinSum = new List<Vector3>();
            public readonly List<float> BinArea = new List<float>();
            public readonly List<int> Joint = new List<int>(), Back = new List<int>();
            public readonly List<Vector3> AreaVecs = new List<Vector3>();
            public float Covered;
        }

        private readonly SurfaceIndex surface;
        private readonly TileSettings S;
        private readonly TilePattern pat;
        private readonly Vector3 origin, decalN, decalU, decalV;
        private readonly float vSign, step, s, eps, hashCell;
        private readonly float cosWrap, cosCorner, cosFace, cosCrease, cosPlanar, cosBound, minBendRadius, dropHeight, depthLimit, gapLimit;
        private readonly List<Tile> tiles = new List<Tile>();
        private readonly Dictionary<long, Tile> tileAt = new Dictionary<long, Tile>();
        private readonly Dictionary<long, List<Tile>> hash = new Dictionary<long, List<Tile>>();
        private readonly List<Tile> near = new List<Tile>();
        private readonly Dictionary<long, bool> stripFace = new Dictionary<long, bool>(), broadFace = new Dictionary<long, bool>();
        private readonly Dictionary<long, List<Vector3>> occupied = new Dictionary<long, List<Vector3>>(); // S.Occupied by hash cell
        private HashSet<Tile> empty;                    // sites that came out empty, see Regrow
        private HashSet<int> shellTris;                 // hull triangles under the nose cap: no tile lands there
        private readonly HashSet<int> ownBodies = new HashSet<int>(); // bodies with their own flood: no tile crosses their border
        private int region, regionBody = -1;            // the flood running now and the body it keeps to (-1 any)
        private readonly List<int> regionBodies = new List<int> { -1 }; // body of each region
        private int BodyOf(int r) => regionBodies[r];
        private int TileBody(Tile t) => t.Region > 0 ? regionBodies[t.Region] : t.Body; // main-flood tiles: body set in FloodMissedBodies
        private bool hitLimit;
        private int startBody = -1;                     // body under the decal origin
        private int live;
        private bool[] wrapIn;                          // per hull triangle: within Max wrap (SurfaceIndex.WrapMask)
        private bool InWrap(SurfaceHit h) => wrapIn[h.Triangle];
        private Vector4[] cuts;
        // Unrolled footprint (measured along the hull). Cockpit cover and "whole part" pass 2 x the farthest hull point as "no
        // limit", but around a wide part (Mk3: belly, side, top) the way along the hull is longer: up to pi / 2 times that for
        // convex parts (half a perimeter <= pi / 2 x diameter), so there the footprint is stretched by pi / 2.
        private readonly float footW, footH;                         // S.CutPlanes, each turned so the start lies on its kept side (see Run)

        public static TileResult Build(SurfaceIndex surface, TileSettings s, Vector3 origin, Vector3 n, Vector3 u, Vector3 v) =>
            Build(surface, s, origin, n, u, v, null);

        public static TileResult Build(SurfaceIndex surface, TileSettings s, Vector3 origin, Vector3 n, Vector3 u, Vector3 v,
            List<TileOutline> outlines) =>
            new TileBuilder(surface, s, origin, n, u, v).Run(outlines);

        private TileBuilder(SurfaceIndex surface, TileSettings settings, Vector3 origin, Vector3 n, Vector3 u, Vector3 v)
        {
            this.surface = surface;
            S = settings;
            s = Math.Max(settings.TileSize, 0.01f);
            pat = new TilePattern(settings.Shape, s, settings.AngleDeg);
            this.origin = origin;
            decalN = n; decalU = u; decalV = v;
            vSign = Vector3.Dot(Vector3.Cross(u, n), v) >= 0 ? 1 : -1;
            step = Mathf.Min(s * 0.25f, 0.1f);
            eps = Math.Max(S.Gap / 4, 0.0005f);
            hashCell = SeamReach * s; // >= largest query radius, so the 27 surrounding cells suffice
            if (settings.Occupied != null)
                foreach (Vector3 q in settings.Occupied)
                {
                    HashCell(q, out int x, out int y, out int z);
                    long key = HashKey(x, y, z);
                    if (!occupied.TryGetValue(key, out List<Vector3> list)) occupied[key] = list = new List<Vector3>();
                    list.Add(q);
                }
            float foot = Cockpit ? Mathf.PI / 2 : 1;
            footW = S.Width * foot;
            footH = S.Height * foot;
            cosWrap = Mathf.Cos(S.MaxWrapDeg * Mathf.Deg2Rad);
            // DESIGN-v10 G1/G2: the wrap limit is a line on the hull (face normals, creases within 20 deg of it snap), the same for
            // every pattern; tiles are cut on it like on any edge of the surface
            wrapIn = S.Overlay ? new bool[surface.TriangleSlots] : surface.WrapMask(n, S.MaxWrapDeg, 25, 20);
            // DESIGN-v11: obstacles are never walked on nor landed on (tiles end at them); an overlay takes every other face
            bool[] ownerWrap = S.Overlay && S.WrapOwner != null ? surface.WrapMask(S.WrapN.normalized, S.MaxWrapDeg, 25, 20) : null;
            for (int k = 0; k < wrapIn.Length; k++)
                if (surface.Triangle(k, out _, out _, out _, out _, out int o))
                    wrapIn[k] = S.Overlay ? !Obstacle(o) && (ownerWrap == null || o >= S.WrapOwner.Length || !S.WrapOwner[o] || ownerWrap[k]) : wrapIn[k] && !Obstacle(o);
            cosCorner = Mathf.Cos(S.CornerBendDeg * Mathf.Deg2Rad);
            cosFace = Mathf.Cos(S.FaceBendDeg * Mathf.Deg2Rad);
            cosCrease = Mathf.Cos(CreaseDeg * Mathf.Deg2Rad);
            cosPlanar = Mathf.Cos(PlanarDeg * Mathf.Deg2Rad);
            cosBound = Mathf.Cos(15 * Mathf.Deg2Rad);
            // smoothing turns a sharp corner into a bend of radius ~1.3 * SmoothRadius; stay above that
            minBendRadius = Math.Max(S.CurvatureFactor * s, 2.5f * surface.SmoothRadius);
            dropHeight = Math.Max(0.6f * s, 0.05f) + S.Thickness;
            depthLimit = 0.25f * s;
            // DESIGN-v7 Â§4: a tile bottom may hover at most this far above the hull (beyond SurfaceOffset) at the border
            gapLimit = Math.Max(0.003f, 0.05f * s);
        }

        // Owner ids: with SeparateOwner, tiles never lie on two bodies of which one moves on its own (control surface flap).
        private bool Separate(int owner) => S.SeparateOwner != null && owner >= 0 && owner < S.SeparateOwner.Length && S.SeparateOwner[owner];
        private bool Obstacle(int owner) => S.Obstacle != null && owner >= 0 && owner < S.Obstacle.Length && S.Obstacle[owner];
        private bool Split(int a, int b) => a != b && a >= 0 && b >= 0 && (Separate(a) || Separate(b) || ownBodies.Contains(a) || ownBodies.Contains(b));

        private TileResult Run(List<TileOutline> outlines)
        {
            var result = new TileResult { Parts = new PartTiles[0], Colliders = new Box[0] };
            if (S.Overlay) return RunOverlay(result, outlines);
            if (!surface.Closest(origin, Mathf.Max(0.5f, s), decalN, 0f, out SurfaceHit h0) || !Inside(h0.P, 0)) return result;
            Vector3 u0 = Tangent(decalU, h0.N);
            if (u0 == Vector3.zero) return result;
            result.OnSurface = true;
            // DESIGN-v10 G5: a cut plane with the decal's own spot beyond it points the wrong way (Mk2/Mk3 cargo bays are stacked
            // by inward nodes "top2"/"bottom2"; along their orientation the plane removed the whole bay): turned round
            if (S.CutPlanes != null)
            {
                cuts = (Vector4[])S.CutPlanes.Clone();
                for (int i = 0; i < cuts.Length; i++)
                    if (Vector3.Dot(new Vector3(cuts[i].x, cuts[i].y, cuts[i].z), h0.P) + cuts[i].w > 0) cuts[i] = -cuts[i];
            }
            if (S.Skin) return RunSkin(result, outlines);

            startBody = h0.Owner;
            List<SurfaceIndex.ShellTri> shell = CapShell();
            Flood(new Walker { P = h0.P, N = h0.N, U = u0, F = h0.FaceN }, 0, 0);
            FloodMissedBodies();
            if (S.Regions != null && S.Regions.Length > 0) FloodIslands();
            return Finish(result, outlines, shell);
        }

        private TileResult Finish(TileResult result, List<TileOutline> outlines, List<SurfaceIndex.ShellTri> shell)
        {
            var nominal = new List<Half>();
            foreach (TilePattern.Edge e in pat.Edges) nominal.Add(new Half { N = e.Normal, HTile = e.H, Tag = TagBorder });
            Vector2[] corners = ClipCell(nominal, 0, out _);
            foreach (Tile t in tiles) Prefit(t, corners);
            foreach (Tile t in tiles) Cell(t);
            // tiles whose walker stood on a strip face are left to the strips (neighbours grow over, Regrow), and so are
            // the virtual tiles hung off such a walker (they share its walker point)
            var offStrip = new HashSet<Vector3>();
            foreach (Tile t in tiles)
                if (!t.Virtual && OnStripFace(t)) { t.State = 0; offStrip.Add(t.W.P); }
            foreach (Tile t in tiles)
                if (t.Virtual && offStrip.Contains(t.W.P)) t.State = 0;
            foreach (Tile t in tiles)
                if (!t.Lost && t.State > 0) Place(t);
            Regrow();
            Strips();
            Twins();
            AddCap(shell);
            // Only small overlay tips (e.g. shielded docking port hood) use Fill(). Mk2 hulls use AddTankJointBed in Emit (< 2 ms)
            // instead of 1-2 s Fill() passes; cockpit masks use exact Drop/RegionHalves + AddMaskJointBed / EmitCeramicShell.
            if (S.Overlay && S.FillHull && S.TileLoss == 0 && (S.Occupied == null || S.Occupied.Length == 0)) Fill();
            Emit(result, outlines);
            result.HitTileLimit = hitLimit;
            result.Colliders = Colliders();
            return result;
        }

        private Vector3 V(Walker w) => Vector3.Cross(w.U, w.N) * vSign;

        private bool Inside(Vector3 p, float margin)
        {
            Vector3 d = p - origin;
            return Math.Abs(Vector3.Dot(d, decalU)) <= S.Width / 2 + margin + 1e-4f &&
                   Math.Abs(Vector3.Dot(d, decalV)) <= S.Height / 2 + margin + 1e-4f;
        }

        // Footprint test of a tile centre: Unrolled measures along the surface by the lattice position (x along u, y along
        // v, the flood's unrolled coordinates), else the 3D point seen along the decal normal.
        private bool InFoot(Vector2 lattice, Vector3 c, float margin) =>
            S.Unrolled
                ? Math.Abs(lattice.x) <= footW / 2 + margin + 1e-4f && Math.Abs(lattice.y) <= footH / 2 + margin + 1e-4f
                : Inside(c, margin);

        private bool InFoot(int a, int b, Vector3 c, float margin) => InFoot(pat.Position(a, b), c, margin);

        private bool HasClip => S.ClipPlanes != null && S.ClipPlanes.Length > 0 || S.Regions != null && S.Regions.Length > 0;

        // Cover regions on a coarse chart grid (cell = 2.5 tiles): per cell the loop edges within one cell of it and whether its centre
        // lies inside. A query looks only at its cell's edges: distances beyond a cell are clamped (only signs and near distances
        // matter), inside = the centre's flag flipped by every edge crossing the way from the centre. Mk3 mask: 5 s -> well under 1 s.
        private class RegionGrid
        {
            public float X0, Y0, Cell;
            public int W, H;
            public List<(Vector2 a, Vector2 b)>[] Edges;
            public bool[] Inside;
        }
        private readonly Dictionary<CoverRegion, RegionGrid> regionGrids = new Dictionary<CoverRegion, RegionGrid>();

        private RegionGrid GridOf(CoverRegion r)
        {
            if (regionGrids.TryGetValue(r, out RegionGrid g)) return g;
            float cell = Math.Max(2.5f * s, 0.05f), x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Vector2[] loop in r.Loops) foreach (Vector2 p in loop) { x0 = Math.Min(x0, p.x); y0 = Math.Min(y0, p.y); x1 = Math.Max(x1, p.x); y1 = Math.Max(y1, p.y); }
            x0 -= 2 * cell; y0 -= 2 * cell;
            g = new RegionGrid { X0 = x0, Y0 = y0, Cell = cell, W = (int)((x1 - x0) / cell) + 3, H = (int)((y1 - y0) / cell) + 3 };
            g.Edges = new List<(Vector2, Vector2)>[g.W * g.H];
            g.Inside = new bool[g.W * g.H];
            for (int i = 0; i < g.Edges.Length; i++) g.Edges[i] = new List<(Vector2, Vector2)>();
            foreach (Vector2[] loop in r.Loops)
                for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
                {
                    Vector2 a = loop[j], b = loop[i];
                    int cx0 = Math.Max(0, (int)((Math.Min(a.x, b.x) - x0) / cell) - 1), cx1 = Math.Min(g.W - 1, (int)((Math.Max(a.x, b.x) - x0) / cell) + 1);
                    int cy0 = Math.Max(0, (int)((Math.Min(a.y, b.y) - y0) / cell) - 1), cy1 = Math.Min(g.H - 1, (int)((Math.Max(a.y, b.y) - y0) / cell) + 1);
                    for (int cx = cx0; cx <= cx1; cx++) for (int cy = cy0; cy <= cy1; cy++) g.Edges[cy * g.W + cx].Add((a, b));
                }
            for (int cx = 0; cx < g.W; cx++)
                for (int cy = 0; cy < g.H; cy++)
                {
                    var c = new Vector2(x0 + (cx + 0.5f) * cell, y0 + (cy + 0.5f) * cell);
                    bool inside = false;
                    foreach (Vector2[] loop in r.Loops)
                        for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
                            if ((loop[j].y > c.y) != (loop[i].y > c.y) && c.x < loop[j].x + (c.y - loop[j].y) * (loop[i].x - loop[j].x) / (loop[i].y - loop[j].y)) inside = !inside;
                    g.Inside[cy * g.W + cx] = inside;
                }
            regionGrids[r] = g;
            return g;
        }

        // RegionDist on the grid (distances clamped at one grid cell)
        private float RegionDistFast(CoverRegion r, Vector3 p, out Vector4 plane)
        {
            plane = default;
            float dom = float.MinValue;
            if (r.Domain != null)
                foreach (Vector4 c in r.Domain)
                {
                    float dc = c.x * p.x + c.y * p.y + c.z * p.z + c.w;
                    if (dc > dom) { dom = dc; plane = c; }
                }
            RegionGrid g = GridOf(r);
            Vector3 rel = p - r.O;
            var q = new Vector2(Vector3.Dot(rel, r.A), Vector3.Dot(rel, r.B));
            int cx = (int)Math.Floor((q.x - g.X0) / g.Cell), cy = (int)Math.Floor((q.y - g.Y0) / g.Cell);
            if (cx < 0 || cy < 0 || cx >= g.W || cy >= g.H) return Math.Max(dom, g.Cell); // far outside the loops
            var centre = new Vector2(g.X0 + (cx + 0.5f) * g.Cell, g.Y0 + (cy + 0.5f) * g.Cell);
            bool inside = g.Inside[cy * g.W + cx];
            float best = g.Cell * g.Cell;
            Vector2 e0 = default, e1 = default;
            bool found = false;
            foreach (var (a, b) in g.Edges[cy * g.W + cx])
            {
                if (SegmentsCross(centre, q, a, b)) inside = !inside;
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0;
                float d2 = (a + ab * t - q).sqrMagnitude;
                if (d2 < best) { best = d2; e0 = a; e1 = b; found = true; }
            }
            float sd = (inside ? -1 : 1) * Mathf.Sqrt(best);
            if (dom > 0 && dom > sd) return dom;
            if (!found) { plane = default; return sd; } // no edge near: the plane is never used for a cut this far in or out
            Vector3 n3 = Vector3.Cross(r.A, r.B), p0 = r.O + r.A * e0.x + r.B * e0.y;
            Vector3 en = Vector3.Cross(r.A * (e1.x - e0.x) + r.B * (e1.y - e0.y), n3);
            if (en.sqrMagnitude < 1e-12f) en = r.A * (q.x - e0.x) + r.B * (q.y - e0.y);
            en.Normalize();
            if (inside == Vector3.Dot(en, p - p0) > 0) en = -en;
            plane = new Vector4(en.x, en.y, en.z, -Vector3.Dot(en, p0));
            return sd;
        }

        private static bool SegmentsCross(Vector2 p, Vector2 q, Vector2 a, Vector2 b)
        {
            float Cross(Vector2 o, Vector2 u, Vector2 v) => (u.x - o.x) * (v.y - o.y) - (u.y - o.y) * (v.x - o.x);
            float d1 = Cross(a, b, p), d2 = Cross(a, b, q), d3 = Cross(p, q, a), d4 = Cross(p, q, b);
            return (d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0);
        }

        // Signed distance of p beyond the clip planes and regions: < 0 kept. The kept side is the union of the planes' sides and the
        // regions.
        private float ClipDist(Vector3 p)
        {
            float d = float.MinValue;
            if (HasClip)
            {
                d = float.MaxValue;
                if (S.ClipPlanes != null) foreach (Vector4 c in S.ClipPlanes) d = Math.Min(d, c.x * p.x + c.y * p.y + c.z * p.z + c.w);
                if (S.Regions != null) foreach (CoverRegion r in S.Regions) d = Math.Min(d, RegionDistFast(r, p, out _));
            }
            if (cuts != null)
                foreach (Vector4 c in cuts) d = Math.Max(d, c.x * p.x + c.y * p.y + c.z * p.z + c.w);
            return d;
        }

        private bool InClip(Vector3 p, float margin) => ClipDist(p) <= margin + 1e-4f;

        // Signed distance (chart plane) of p to a cover region: < 0 inside. plane: the cut there, the line of the nearest loop edge
        // extruded along the chart normal (kept side: the region's), or the domain plane p lies beyond.
        public static float RegionDist(CoverRegion r, Vector3 p, out Vector4 plane)
        {
            plane = default;
            float dom = float.MinValue;
            if (r.Domain != null)
                foreach (Vector4 c in r.Domain)
                {
                    float dc = c.x * p.x + c.y * p.y + c.z * p.z + c.w;
                    if (dc > dom) { dom = dc; plane = c; }
                }
            Vector3 rel = p - r.O;
            var q = new Vector2(Vector3.Dot(rel, r.A), Vector3.Dot(rel, r.B));
            bool inside = false;
            float best = float.MaxValue;
            Vector2 e0 = default, e1 = default;
            foreach (Vector2[] loop in r.Loops)
                for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
                {
                    Vector2 a = loop[j], b = loop[i];
                    if ((a.y > q.y) != (b.y > q.y) && q.x < a.x + (q.y - a.y) * (b.x - a.x) / (b.y - a.y)) inside = !inside;
                    Vector2 ab = b - a;
                    float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0;
                    float dist = (a + ab * t - q).sqrMagnitude;
                    if (dist < best) { best = dist; e0 = a; e1 = b; }
                }
            float sd = (inside ? -1 : 1) * Mathf.Sqrt(best);
            if (dom > 0 && dom > sd) return dom;
            Vector3 n3 = Vector3.Cross(r.A, r.B), p0 = r.O + r.A * e0.x + r.B * e0.y;
            Vector3 en = Vector3.Cross(r.A * (e1.x - e0.x) + r.B * (e1.y - e0.y), n3);
            if (en.sqrMagnitude < 1e-12f) en = r.A * (q.x - e0.x) + r.B * (q.y - e0.y); // degenerate edge: away from its point
            en.Normalize();
            float side = Vector3.Dot(en, p - p0);
            if (inside == side > 0) en = -en; // kept side: the region's
            plane = new Vector4(en.x, en.y, en.z, -Vector3.Dot(en, p0));
            return sd;
        }

        private static Vector3 Tangent(Vector3 dir, Vector3 n)
        {
            Vector3 t = dir - n * Vector3.Dot(dir, n);
            return t.sqrMagnitude < 1e-8f ? Vector3.zero : t.normalized;
        }

        private static long LatticeKey(int a, int b) => ((long)a << 32) ^ (uint)b;

        // lattice site of one flood (regions never see each other's sites as neighbours)
        private static long SiteKey(int a, int b, int r) => LatticeKey(a + r * 4000000, b);

        private static long HashKey(int x, int y, int z) =>
            ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (uint)(z & 0x1FFFFF);

        private static int Hash(int a, int b, int salt)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ salt * 83492791;
                h ^= h >> 13;
                h *= 1274126177;
                return (int)((uint)(h ^ (h >> 16)) & 0x7FFFFFFF);
            }
        }

        // ---------- layout ----------

        private void Flood(Walker start, int a0, int b0)
        {
            var visited = new HashSet<long> { LatticeKey(a0, b0) };
            var queue = new Queue<Tile>();
            Tile first = AddTile(a0, b0, start, start.P, false);
            if (first == null) return;
            if (first.Floods) queue.Enqueue(first);
            float margin = S.StraightBorder ? s : 0; // straight cut: lay one more ring, the footprint trims it
            while (queue.Count > 0 && !hitLimit)
            {
                Tile t = queue.Dequeue();
                if (t.Dead) continue;
                for (int k = 0; k < pat.StepVec.Length && !hitLimit && !t.Dead; k++)
                {
                    int na = t.A + pat.StepA[k], nb = t.B + pat.StepB[k];
                    long key = LatticeKey(na, nb);
                    // cockpit cover: a site left with a virtual tile hanging over a crease (or none) is taken over when a
                    // tile on the far face walks there (fronts meeting at the Mk2 chine left zigzag holes)
                    Tile old = null;
                    bool retry = visited.Contains(key);
                    if (retry && (!Cockpit || (tileAt.TryGetValue(SiteKey(na, nb, region), out old) ? !old.Virtual || old.Floods || old.Dead : claimDot == 0))) continue; // empty sites: cockpit only
                    Walker next = t.W;
                    Vector2 d = pat.StepVec[k];
                    float dist = d.magnitude;
                    if (Walk(ref next, d / dist, dist, out float got))
                    {
                        if (retry)
                        {
                            // only on the body the walk started on (on wings and winglets a retry over a body border hovered)
                            if (!InFoot(na, nb, next.P, margin) || !InClip(next.P, s) || Taken(next.P, next.N) || OwnerAt(next) != OwnerAt(t.W) || old != null && Vector3.Dot(old.N, next.N) <= 0) continue;
                            if (old != null) Remove(old);
                            if (!Claim(na, nb, next.P, next.N)) { if (old != null) Revive(old); continue; }
                            Tile r = AddTile(na, nb, next, next.P, false);
                            if (r != null && r.Floods) queue.Enqueue(r);
                            continue;
                        }
                        visited.Add(key);
                        if (!InFoot(na, nb, next.P, margin) || !InClip(next.P, s) || Taken(next.P, next.N) || regionBody >= 0 && Enclosed(next.P, next.N) ||
                            !Claim(na, nb, next.P, next.N)) continue;
                        Tile n = AddTile(na, nb, next, next.P, false);
                        if (n != null && n.Floods) queue.Enqueue(n);
                        continue;
                    }
                    if (retry) continue;
                    // Surface ends between the two centres: virtual tile, clipped against the edge later. It is the
                    // parent's lattice step straight on in the parent's tangent plane: the last good walker point
                    // already leans into the edge (smoothed normal, bevels), a frame from there drapes the tile over it.
                    Walker pw = t.W;
                    Vector3 fu = Tangent(pw.U, pw.F);
                    if (fu != Vector3.zero && Vector3.Dot(pw.F, pw.N) > 0.7f) { pw.N = pw.F; pw.U = fu; }
                    Vector3 c = pw.P + pw.U * d.x + V(pw) * d.y;
                    if (!InFoot(na, nb, c, margin) || !InClip(c, s) || Taken(c, pw.N) || regionBody >= 0 && Enclosed(c, pw.N)) continue;
                    visited.Add(key);
                    bool snapped = Snap(ref pw, ref c, c - pw.P, got);
                    if (!Claim(na, nb, c, pw.N)) continue;
                    Tile vt = AddTile(na, nb, pw, c, true);
                    // snapped onto the broad face beyond a crease (within Max wrap): the cover goes on over that face
                    // (Unrolled: a decal on a Mk3 belly runs over its edge up the side)
                    if (vt != null && snapped && InFoot(na, nb, c, 0) && InClip(c, 0)) { vt.Floods = true; queue.Enqueue(vt); }
                }
            }
            tiles.RemoveAll(x => x.Dead);
        }

        // DESIGN-v8 B6: bodies in reach that the flood never got onto (an intake or flap glued on at ~90 deg, a stacked part
        // behind a step) get floods of their own: each starts at the point of such a body nearest the tiles so far (within
        // 1.5 tiles) that no tile of that body covers yet (0.75 tile), lattice and frame from the nearest tile, so face by
        // face (box-like intakes) the body gets covered. Their tiles keep to that body and no other tile crosses onto it
        // (Split): both sides end at the part border with a cut.
        private void FloodMissedBodies()
        {
            if (!surface.HasOwners) return;
            // bodies of the main flood's tiles. A body near the cover that it never walked onto (at most virtual tiles hung
            // over its edge) is tiled anew by floods of its own; bodies it walked onto stay as they were.
            foreach (Tile t in tiles) t.Body = surface.Closest(t.W.P, 0.02f, t.W.N, 0f, out SurfaceHit h) ? h.Owner : -1;
            // "whole part" (every body separate): no own starts on edges thinner than a tile (wing and flap edges)
            bool allSeparate = S.SeparateOwner != null && Array.TrueForAll(S.SeparateOwner, x => x);
            var walked = new HashSet<int>();
            var candidates = new HashSet<int>();
            foreach (Tile t in tiles) if (!t.Virtual) walked.Add(t.Body);
            for (int k = 0; k < surface.TriangleSlots; k++)
            {
                // DESIGN-v10 G8, "whole part" only: also bodies the main flood walked onto in part (one of two symmetric intakes:
                // the flood stopped at a crease of its outer face); they are tiled anew by floods of their own. Never the body
                // the decal sits on.
                if (!surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int owner) || owner == startBody ||
                    walked.Contains(owner) && !allSeparate || candidates.Contains(owner) || !wrapIn[k]) continue;
                foreach (Vector3 q in new[] { a, b, c, (a + b + c) / 3 })
                    if (InClip(q, 0) && Near(q, 1.5f * s).Count > 0 && !Enclosed(q, n)) { candidates.Add(owner); break; }
            }
            if (candidates.Count == 0) return;
            var failed = new List<Vector3>();
            // the spots to cover, once (raycasts are dear); a spot once covered stays covered and leaves the list
            var spots = new List<(Vector3 q, Vector3 n, int owner)>();
            for (int k = 0; k < surface.TriangleSlots; k++)
            {
                if (!surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int owner) ||
                    !candidates.Contains(owner) || !wrapIn[k]) continue;
                foreach (Vector3 q in new[] { a, b, c, (a + b) / 2, (b + c) / 2, (c + a) / 2, (a + b + c) / 3 })
                {
                    if (InClip(q, 0) && (S.Unrolled || Inside(q, 0)) && !InKeepOut(q) && !Enclosed(q, n) && !(allSeparate && Thin(q, n, owner))) spots.Add((q, n, owner));
                }
            }
            // a body the main flood walked onto keeps those tiles; own floods start only far (1.2 tiles) from any of them and
            // only where a broad face was never reached (at least 20 tile cells of uncovered faces: the outer face of one of
            // two symmetric intakes). The ragged edges of flaps and winglets stay as they are (starts there opened seams).
            Func<Vector3, Vector3, bool> nearMain = (q, n) => Near(q, 1.2f * s).Exists(t => !t.Dead && t.Region == 0 && Vector3.Dot(t.N, n) > 0.5f);
            var bare = new Dictionary<int, float>();
            for (int k = 0; k < surface.TriangleSlots; k++)
            {
                if (!surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int owner) || !walked.Contains(owner) ||
                    !candidates.Contains(owner) || !wrapIn[k]) continue;
                Vector3 m = (a + b + c) / 3;
                if (!InClip(m, 0) || Enclosed(m, n) || nearMain(m, n)) continue;
                bare[owner] = (bare.TryGetValue(owner, out float ar) ? ar : 0) + 0.5f * Vector3.Cross(b - a, c - a).magnitude;
            }
            spots.RemoveAll(sp => walked.Contains(sp.owner) && (!bare.TryGetValue(sp.owner, out float ba) || ba < 20 * pat.CellArea || nearMain(sp.q, sp.n)));
            for (int round = 0; round < 48 && !hitLimit && spots.Count > 0; round++)
            {
                float best = 1.5f * s;
                Vector3 bp = default, bn = default;
                Tile near = null;
                int body = -1;
                for (int i = spots.Count - 1; i >= 0; i--)
                {
                    var (q, n, owner) = spots[i];
                    if (failed.Exists(f => (f - q).sqrMagnitude < 0.5625f * s * s)) { spots.RemoveAt(i); continue; }
                    Tile closest = null;
                    float dc = best;
                    bool covered = false;
                    foreach (Tile t in Near(q, 1.5f * s))
                    {
                        if (t.Dead) continue;
                        float d = (t.C - q).magnitude;
                        if ((BodyOf(t.Region) == owner || t.Region == 0 && walked.Contains(owner)) && d < 0.75f * s && Vector3.Dot(t.N, n) > 0.5f) { covered = true; break; }
                        if (d < dc) { dc = d; closest = t; }
                    }
                    if (covered) { spots.RemoveAt(i); continue; }
                    // the start must lie in the footprint, measured from the nearest tile like the flood would
                    if (closest == null || !InFoot(pat.Position(closest.A, closest.B) + To2(closest, q), q, 0)) continue;
                    best = dc; bp = q; bn = n; near = closest; body = owner;
                }
                if (near == null) break;
                region++;
                regionBodies.Add(body);
                regionBody = body;
                // start on the lattice of the nearest tile, continued in its plane (the pattern runs on over a flush door
                // joint): the site nearest the spot whose centre, dropped along that tile's normal, lands on the body
                Vector2 off = To2(near, bp);
                float det = pat.E1.x * pat.E2.y - pat.E1.y * pat.E2.x;
                int da0 = Mathf.RoundToInt((off.x * pat.E2.y - off.y * pat.E2.x) / det), db0 = Mathf.RoundToInt((pat.E1.x * off.y - pat.E1.y * off.x) / det);
                bool started = false;
                for (int k = -1; k < pat.StepA.Length && !started; k++)
                {
                    int sa = near.A + da0 + (k < 0 ? 0 : pat.StepA[k]), sb = near.B + db0 + (k < 0 ? 0 : pat.StepB[k]);
                    Vector2 d2 = pat.Position(sa, sb) - pat.Position(near.A, near.B);
                    Vector3 c3 = near.C + near.U * d2.x + near.V * d2.y;
                    if (!surface.Raycast(c3 + near.N * (0.5f * s), -near.N, 1.5f * s, 0.17f, out SurfaceHit hs) || hs.Owner != body || Vector3.Dot(hs.FaceN, bn) < 0.9f || Taken(hs.P, hs.N) || Enclosed(hs.P, hs.FaceN)) continue;
                    Vector3 u = Transport(near.U, near.N, hs.N);
                    if (u == Vector3.zero) u = Tangent(decalU, hs.N);
                    if (u == Vector3.zero) continue;
                    Flood(new Walker { P = hs.P, N = hs.N, U = u, F = hs.FaceN }, sa, sb);
                    started = true;
                }
                // a face standing off at an angle (side of an intake): start right at the spot, lattice of the nearest tile
                if (!started && surface.Closest(bp, 0.05f, bn, 0.3f, out SurfaceHit hc) && hc.Owner == body && !Taken(hc.P, hc.N))
                {
                    Vector3 u = Tangent(near.U, hc.N);
                    if (u == Vector3.zero) u = Tangent(decalU, hc.N);
                    if (u != Vector3.zero) Flood(new Walker { P = hc.P, N = hc.N, U = u, F = hc.FaceN }, near.A, near.B);
                }
                if (!tiles.Exists(t => t.Region == region)) { failed.Add(bp); continue; } // nothing came of this start: not again
                if (ownBodies.Add(body) && !walked.Contains(body)) // first own tiles on it: virtual tiles of the main flood hanging onto it go
                    foreach (Tile t in tiles)
                        if (t.Region == 0 && t.Body == body && !t.Dead) Remove(t);
            }
            tiles.RemoveAll(x => x.Dead);
            regionBody = -1;
        }

        // DESIGN-v11 G2: kept regions the flood never walked onto (dark panels of a cockpit texture are islands between light ones)
        // get floods of their own, each at the first uncovered spot well inside a region (hull triangle order), frame along the
        // decal's u.
        private bool islandFlood;

        private void FloodIslands()
        {
            islandFlood = true;
            var spots = new List<(Vector3 q, Vector3 n)>();
            for (int k = 0; k < surface.TriangleSlots; k++)
            {
                if (!surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int o) || !wrapIn[k] || shellTris != null && shellTris.Contains(k)) continue;
                foreach (Vector3 q in new[] { (a + b + c) / 3, (a + b) / 2, (b + c) / 2, (c + a) / 2 })
                    if (ClipDist(q) < -0.3f * s && !InKeepOut(q) && !Enclosed(q, n)) spots.Add((q, n));
            }
            for (int round = 0; round < 64 && spots.Count > 0 && !hitLimit; round++)
            {
                spots.RemoveAll(sp => Near(sp.q, 0.75f * s).Exists(t => !t.Dead && Vector3.Dot(t.N, sp.n) > 0.5f));
                if (spots.Count == 0) break;
                var (q0, n0) = spots[0];
                spots.RemoveAt(0);
                if (!surface.Closest(q0, 0.05f, n0, 0.3f, out SurfaceHit h) || Obstacle(h.Owner) || !wrapIn[h.Triangle]) continue;
                Vector3 u = Tangent(decalU, h.N);
                if (u == Vector3.zero) u = Tangent(decalV, h.N);
                if (u == Vector3.zero) continue;
                region++;
                regionBodies.Add(h.Owner);
                regionBody = h.Owner;
                Flood(new Walker { P = h.P, N = h.N, U = u, F = h.FaceN }, 0, 0);
                if (!tiles.Exists(t => t.Region == region && !t.Dead)) spots.RemoveAll(sp => (sp.q - q0).sqrMagnitude < 0.5625f * s * s);
            }
            tiles.RemoveAll(x => x.Dead);
            regionBody = -1;
            islandFlood = false;
        }

        // Mk2-profile hull parts (fuselages, crew cabin, docking port, adapters): rim fill on a whole-part decal (FillHull). 1.7: real
        // tiles again (the 1.6.2 closed skin drew back from the part seams). Not cockpits (own masks), cargo bays or the lander can.
        public static bool Mk2Hull(string partName)
        {
            string n = (partName ?? "").ToLowerInvariant();
            return n.StartsWith("mk2") && !n.Contains("cockpit") && !n.Contains("cargobay") && !n.Contains("lander");
        }

        // Intake mouth or duct (any space): walls facing in towards the intake axis (the line through the intake transform along its
        // forward, the direction of travel) and faces turned towards the oncoming air that lie recessed behind the intake's front
        // (front = largest dot(p - axisPos, fwd) over the intake, the lip): the hole and the duct are free, the lip is tiled
        // (DESIGN-v11 B; 1.4 left the whole front face of the lip bare). Module and preview share this rule.
        public static bool IntakeOpening(Vector3 a, Vector3 b, Vector3 c, Vector3 axisPos, Vector3 fwd, float front)
        {
            Vector3 n = Vector3.Cross(b - a, c - a); // Unity front face normal
            if (n.sqrMagnitude < 1e-16f) return false;
            n.Normalize();
            Vector3 ctr = (a + b + c) / 3, radial = ctr - axisPos;
            // the wall rule only within 35 cm of the lip: the IntakeRadialLong's axis runs beside its body, so it took outer faces far
            // back and left white patches at its rear end; duct walls deeper inside stay out of an overlay anyway (they look at
            // their own inside, see Enclosed)
            float depth = front - Vector3.Dot(radial, fwd);
            if (Vector3.Dot(n, fwd) > 0.5f && depth > 0.03f) return true;
            if (depth > 0.35f) return false;
            radial -= fwd * Vector3.Dot(radial, fwd);
            return radial.sqrMagnitude > 1e-4f && Vector3.Dot(n, radial.normalized) < -0.3f;
        }

        // ---------- overlay (DESIGN-v11) ----------

        // Settings of an object's overlay from the main decal's: pattern, tile size, joint, thickness, texture and the decal's seed
        // carry over, nothing that depends on where the decal sits, how it is turned or how large it is.
        public static TileSettings OverlaySettings(TileSettings m) => new TileSettings
        {
            Shape = m.Shape, TileSize = m.TileSize, AngleDeg = 0, Gap = m.Gap, Thickness = m.Thickness, Width = 1e4f, Height = 1e4f,
            MaxWrapDeg = 180, StraightBorder = false, TileLoss = m.TileLoss, SurfaceOffset = m.SurfaceOffset, BevelWidth = m.BevelWidth,
            BevelDepth = m.BevelDepth, CornerBendDeg = m.CornerBendDeg, FaceBendDeg = m.FaceBendDeg, CurvatureFactor = m.CurvatureFactor,
            UvMode = m.UvMode, AtlasCols = m.AtlasCols, AtlasRows = m.AtlasRows, AtlasRandomRotation = m.AtlasRandomRotation,
            TextureScale = m.TextureScale, TextureAspect = m.TextureAspect, MaxTiles = m.MaxTiles, MaxBins = Math.Min(m.MaxBins, 8),
            BinAngleDeg = Math.Max(m.BinAngleDeg, 24f), ColliderPatchSize = m.ColliderPatchSize, MaxColliders = m.MaxColliders, Seed = m.Seed,
            Unrolled = true, Overlay = true
        };

        // 1.6 skin overlay (landing gear, intakes; user: "lieber alles zu als alles perfekt gekachelt"): the visible outer skin
        // (as the overlay's: no obstacle, inside wrap/clip, not enclosed) welded by position (1 mm) and lifted along the welded
        // normals by SurfaceOffset + Thickness; walls down to the hull along its open edges (the foot, the intake lip). The tile
        // pattern is projected box-wise in object space (each face along its dominant axis, lattice anchored at the origin) and
        // every face is cut along the cell borders, each piece showing its cell's atlas picture: joints only in the texture, no
        // gaps anywhere, seams where the projection axis changes. Every piece stays on its face's owner (moving gear parts).
        private TileResult RunSkin(TileResult result, List<TileOutline> outlines)
        {
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int k = 0; k < surface.TriangleSlots; k++)
                if (wrapIn[k] && surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out _, out _))
                {
                    lo = Vector3.Min(lo, Vector3.Min(a, Vector3.Min(b, c)));
                    hi = Vector3.Max(hi, Vector3.Max(a, Vector3.Max(b, c)));
                }
            if (lo.x > hi.x) return result;
            enclosedReach = Math.Max(0.5f, 1.1f * (hi - lo).magnitude);
            var keep = new List<(Vector3 a, Vector3 b, Vector3 c, int owner)>();
            var ids = new List<int>();
            var vp = new List<Vector3>();
            var vn = new List<Vector3>();
            var welded = new Dictionary<(int, int, int), int>();
            int Weld(Vector3 p, Vector3 normal)
            {
                var key = (Mathf.RoundToInt(p.x * 10000), Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));
                if (!welded.TryGetValue(key, out int id))
                {
                    welded[key] = id = vp.Count;
                    vp.Add(p);
                    vn.Add(Vector3.zero);
                }
                vn[id] += normal;
                return id;
            }
            // Exact clip classification for skin polygons (model has ~1k tris, so exact RegionDist is < 2 ms and avoids grid clamping edge cases)
            bool ExactInClip(Vector3 p)
            {
                if (cuts != null)
                    foreach (Vector4 c in cuts)
                        if (c.x * p.x + c.y * p.y + c.z * p.z + c.w > 1e-4f) return false;
                if (!HasClip) return true;
                if (S.ClipPlanes != null)
                    foreach (Vector4 c in S.ClipPlanes)
                        if (c.x * p.x + c.y * p.y + c.z * p.z + c.w <= 1e-4f) return true;
                if (S.Regions != null)
                    foreach (CoverRegion r in S.Regions)
                        if (RegionDist(r, p, out _) <= 1e-4f) return true;
                return false;
            }
            for (int k = 0; k < surface.TriangleSlots; k++)
            {
                if (!wrapIn[k] || !surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int owner)) continue;
                // Stack end faces are covered by their neighbour. Retain the exposed nose cap.
                if (!S.Overlay && Vector3.Dot(n, S.TankAxis) < -0.999f) continue;
                if (!S.Overlay && !HasClip && Vector3.Dot(n, S.TankAxis) > 0.999f) continue;
                if (S.Overlay && !S.GearSkin && Enclosed((a+b+c)/3, n)) continue;
                // Hull skins skip faces looking into the part itself (e.g. docking port tunnel, internal cockpit faces)
                if (!S.Overlay &&
                    surface.Raycast((a+b+c)/3 + n * 0.01f, n, 0.5f, -1.01f, out SurfaceHit inner, false) && !Obstacle(inner.Owner)) continue;
                foreach (var piece in SkinPolygons(a, b, c))
                {
                    Vector3 mid = Vector3.zero;
                    foreach (Vector3 p in piece) mid += p;
                    mid /= piece.Count;
                    if (!ExactInClip(mid) || InKeepOut(mid) || (S.LimitedTank && !Inside(mid, 0))) continue;
                    for (int j = 1; j + 1 < piece.Count; j++)
                    {
                        Vector3 an = Vector3.Cross(piece[j]-piece[0], piece[j+1]-piece[0]);
                        if (an.sqrMagnitude < 1e-16f) continue;
                        keep.Add((piece[0], piece[j], piece[j+1], owner));
                        ids.Add(Weld(piece[0], an)); ids.Add(Weld(piece[j], an)); ids.Add(Weld(piece[j+1], an));
                    }
                }
            }
            if (keep.Count == 0) return result;
            result.OnSurface = true;
            var edgeUse = new Dictionary<(int, int), int>();
            for (int i = 0; i < ids.Count; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int p = ids[i + e], q = ids[i + (e + 1) % 3];
                    var key = p < q ? (p, q) : (q, p);
                    edgeUse[key] = (edgeUse.TryGetValue(key, out int u0) ? u0 : 0) + 1;
                }
            float lift = S.SurfaceOffset + S.Thickness;
            var top = new Vector3[vp.Count];
            for (int i = 0; i < vp.Count; i++)
            {
                vn[i] = vn[i].sqrMagnitude > 1e-20f ? vn[i].normalized : Vector3.up;
                Vector3 nLift = vn[i];
                // Keep vertices on a stack cut plane flush with that plane so no gap opens at the part seam
                if (!S.Overlay && cuts != null)
                {
                    foreach (Vector4 cp in cuts)
                    {
                        var cn = new Vector3(cp.x, cp.y, cp.z);
                        if (Mathf.Abs(Vector3.Dot(cn, vp[i]) + cp.w) < 0.005f)
                        {
                            Vector3 proj = nLift - cn * Vector3.Dot(nLift, cn);
                            if (proj.sqrMagnitude > 1e-6f) nLift = proj.normalized;
                        }
                    }
                }
                top[i] = vp[i] + nLift * lift;
            }

            var accs = new Dictionary<int, Acc>();
            var order = new List<int>();
            float cosBin = Mathf.Cos(S.BinAngleDeg * Mathf.Deg2Rad);
            int maxBins = Math.Max(1, S.MaxBins);
            var cellTile = new Tile();
            var mappedCells = new HashSet<(int owner, int a, int b, bool end)>();
            float det = pat.E1.x * pat.E2.y - pat.E1.y * pat.E2.x;
            var poly = new List<Vector2>();
            var next = new List<Vector2>();
            Vector3 size = hi - lo;
            Vector3 axis = !S.Overlay ? S.TankAxis.normalized :
                size.y >= size.x && size.y >= size.z ? Vector3.up : size.x >= size.z ? Vector3.right : Vector3.forward;
            Vector3 center = S.Overlay ? (lo + hi) / 2 : S.TankCenter;
            Vector3 front = Vector3.ProjectOnPlane(S.Overlay ? S.WrapN : decalN, axis).normalized;
            if (front.sqrMagnitude < 0.5f) front = Tangent(Math.Abs(axis.x) < 0.8f ? Vector3.right : Vector3.up, axis);
            Vector3 lateral = Vector3.Cross(axis, front).normalized;
            // 1.6.2 chart: round about the axis by ARC LENGTH of the hull's outline (max radius per 1 deg), so tiles keep their size
            // on flat Mk2 bellies and narrow intake flanks (angle alone stretched them sideways); the outline length is rounded to
            // whole tiles, so the chart closes without a seam. Flat objects (fins, winglets, flaps: thinnest extent under 0.3 of the
            // middle one) are projected straight through their thin axis instead.
            const int Nb = 360;
            var rim = new float[Nb];
            void RimAt(Vector3 q)
            {
                Vector3 d = q - center;
                float ang = Mathf.Atan2(Vector3.Dot(d, lateral), Vector3.Dot(d, front));
                int bi = Mathf.Clamp(Mathf.FloorToInt((ang + Mathf.PI) / (2 * Mathf.PI) * Nb), 0, Nb - 1);
                rim[bi] = Math.Max(rim[bi], Vector3.ProjectOnPlane(d, axis).magnitude);
            }
            foreach (var (ka, kb, kc, _) in keep) // edges sampled densely: large flat faces have their corners far apart
                foreach (var (e0, e1) in new[] { (ka, kb), (kb, kc), (kc, ka) })
                {
                    int ns = Mathf.Clamp(Mathf.CeilToInt((e1 - e0).magnitude / (0.25f * s)), 1, 200);
                    for (int i = 0; i <= ns; i++) RimAt(Vector3.Lerp(e0, e1, (float)i / ns));
                }
            float rMax = rim.Max();
            if (rMax <= 0) for (int bi = 0; bi < Nb; bi++) rim[bi] = 1;
            for (int pass = 0; pass < Nb && rim.Contains(0f); pass++) // empty bins: from their neighbours
            {
                var was = (float[])rim.Clone();
                for (int bi = 0; bi < Nb; bi++)
                    if (was[bi] == 0) rim[bi] = Math.Max(was[(bi + Nb - 1) % Nb], was[(bi + 1) % Nb]);
            }
            for (int pass = 0; pass < 2; pass++) // smoothed (+-3 deg): a jagged outline would lengthen the arc
            {
                var was = (float[])rim.Clone();
                for (int bi = 0; bi < Nb; bi++)
                {
                    float sum = 0;
                    for (int j = -3; j <= 3; j++) sum += was[(bi + j + Nb) % Nb];
                    rim[bi] = sum / 7;
                }
            }
            var arc = new float[Nb + 1];
            float dTh = 2 * Mathf.PI / Nb;
            for (int bi = 0; bi < Nb; bi++)
            {
                float r0 = rim[bi], r1 = rim[(bi + 1) % Nb];
                arc[bi + 1] = arc[bi] + Mathf.Sqrt(0.25f * (r0 + r1) * (r0 + r1) * dTh * dTh + (r1 - r0) * (r1 - r0));
            }
            float period = Math.Max(3, Mathf.RoundToInt(arc[Nb] / s)) * s, arcScale = period / Math.Max(arc[Nb], 1e-6f);
            float ArcAt(float th) // th any angle (unwrapped), arc length from -pi
            {
                float f = (th + Mathf.PI) / dTh;
                int turns = Mathf.FloorToInt(f / Nb);
                f -= turns * Nb;
                int bi = Mathf.Clamp(Mathf.FloorToInt(f), 0, Nb - 1);
                return Mathf.Lerp(arc[bi], arc[bi + 1], f - bi) + turns * arc[Nb];
            }
            float RimAtAngle(float th)
            {
                float f = (th + Mathf.PI) / dTh - 0.5f;
                int b0 = Mathf.FloorToInt(f);
                float w = f - b0;
                return Mathf.Lerp(rim[((b0 % Nb) + Nb) % Nb], rim[(((b0 + 1) % Nb) + Nb) % Nb], w);
            }
            float arc0 = ArcAt(0);
            // x: arc length from the front meridian, scaled by the local radius over the outline's (a tapering nose or the round end
            // of an adapter keeps its tile size instead of fanning out); thRef: the angle of the triangle's first corner, so a
            // triangle across the back seam stays in one piece
            Vector2 Map(Vector3 p, ref float thRef, bool first)
            {
                Vector3 d = p - center;
                float th = Mathf.Atan2(Vector3.Dot(d, lateral), Vector3.Dot(d, front));
                if (first) thRef = th;
                else th += Mathf.Round((thRef - th) / (2 * Mathf.PI)) * 2 * Mathf.PI;
                float ratio = Mathf.Clamp(Vector3.ProjectOnPlane(d, axis).magnitude / Math.Max(RimAtAngle(th), 1e-4f), 0.05f, 1.5f);
                return new Vector2((ArcAt(th) - arc0) * arcScale * ratio, Vector3.Dot(d, axis));
            }
            int thin = -1, wide = -1;
            if (S.Overlay)
            {
                int[] byExtent = new[] { 0, 1, 2 }.OrderBy(i => size[i]).ToArray();
                if (size[byExtent[0]] < 0.3f * size[byExtent[1]]) { thin = byExtent[0]; wide = byExtent[2]; }
            }
            Vector3 Unit(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            for (int i = 0; i < keep.Count; i++)
            {
                var (a, b, c, o) = keep[i];
                Vector3 fn = Vector3.Cross(b - a, c - a);
                float area = 0.5f * fn.magnitude;
                fn.Normalize();
                if (!accs.TryGetValue(o, out Acc acc)) { accs[o] = acc = new Acc(); order.Add(o); }
                int bin = -1;
                float bestDot = -2;
                for (int j = 0; j < acc.BinSum.Count; j++)
                {
                    float d = Vector3.Dot(acc.BinSum[j].normalized, fn);
                    if (d > bestDot) { bestDot = d; bin = j; }
                }
                if (bin < 0 || (bestDot < cosBin && acc.BinSum.Count < maxBins))
                {
                    acc.Bins.Add(new List<int>()); acc.BinSum.Add(Vector3.zero); acc.BinArea.Add(0);
                    bin = acc.Bins.Count - 1;
                }
                acc.BinSum[bin] += fn * area;
                acc.BinArea[bin] += area;
                acc.AreaVecs.Add(fn * area);
                acc.Covered += area;
                result.CoveredArea += area;

                List<int> tris = acc.Bins[bin];
                int ia = ids[3 * i], ib = ids[3 * i + 1], ic = ids[3 * i + 2];
                Vector2 pa, pb, pc;
                bool endChart;
                if (thin >= 0)
                {
                    // flat object: both broad faces straight through the thin axis, the narrow rim along its own side
                    Vector3 tn = Unit(thin), e1 = Unit(wide), e2 = Unit(3 - thin - wide);
                    endChart = Math.Abs(Vector3.Dot(fn, tn)) < 0.35f;
                    Vector3 cx = !endChart ? e1 : Math.Abs(Vector3.Dot(fn, e1)) > Math.Abs(Vector3.Dot(fn, e2)) ? e2 : e1, cy = !endChart ? e2 : tn;
                    pa = new Vector2(Vector3.Dot(a, cx), Vector3.Dot(a, cy));
                    pb = new Vector2(Vector3.Dot(b, cx), Vector3.Dot(b, cy));
                    pc = new Vector2(Vector3.Dot(c, cx), Vector3.Dot(c, cy));
                }
                else
                {
                    float thA = 0;
                    // Upper nose on tapered cockpit covers (opposite the belly meridian): flat top chart avoids azimuthal shear
                    // where (ArcAt(+-pi) - arc0) * ratio would otherwise drag tiles diagonally as the nose narrows.
                    bool topChart = !S.Overlay && HasClip && Math.Abs(Vector3.Dot(fn, axis)) <= 0.7f && Vector3.Dot(fn, front) < -0.2f;
                    if (topChart)
                    {
                        endChart = true;
                        pa = new Vector2(Vector3.Dot(a - center, lateral), Vector3.Dot(a - center, axis));
                        pb = new Vector2(Vector3.Dot(b - center, lateral), Vector3.Dot(b - center, axis));
                        pc = new Vector2(Vector3.Dot(c - center, lateral), Vector3.Dot(c - center, axis));
                    }
                    else
                    {
                        pa = Map(a, ref thA, true); pb = Map(b, ref thA, false); pc = Map(c, ref thA, false);
                        // caps (faces turned more than 45 deg towards the axis): one flat chart across the axis, continuous over the cap
                        // (the round chart fanned out there into ever thinner wedges)
                        endChart = Math.Abs(Vector3.Dot(fn, axis)) > 0.7f || Math.Abs((pb.x-pa.x)*(pc.y-pa.y)-(pc.x-pa.x)*(pb.y-pa.y)) < 1e-8f;
                        if (endChart)
                        {
                            pa = new Vector2(Vector3.Dot(a - center, lateral), Vector3.Dot(a - center, front));
                            pb = new Vector2(Vector3.Dot(b - center, lateral), Vector3.Dot(b - center, front));
                            pc = new Vector2(Vector3.Dot(c - center, lateral), Vector3.Dot(c - center, front));
                        }
                    }
                }
                float d2 = (pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x);
                if (Math.Abs(d2) > 1e-12f)
                {
                    Vector2 mn = Vector2.Min(pa, Vector2.Min(pb, pc)), mx = Vector2.Max(pa, Vector2.Max(pb, pc));
                    // lattice range: (x, y) = E1 la + E2 lb
                    int la0 = int.MaxValue, la1 = int.MinValue, lb0 = int.MaxValue, lb1 = int.MinValue;
                    foreach (Vector2 q in new[] { mn, mx, new Vector2(mn.x, mx.y), new Vector2(mx.x, mn.y) })
                    {
                        float fa = (q.x * pat.E2.y - q.y * pat.E2.x) / det, fb = (pat.E1.x * q.y - pat.E1.y * q.x) / det;
                        la0 = Math.Min(la0, Mathf.FloorToInt(fa) - 1); la1 = Math.Max(la1, Mathf.CeilToInt(fa) + 1);
                        lb0 = Math.Min(lb0, Mathf.FloorToInt(fb) - 1); lb1 = Math.Max(lb1, Mathf.CeilToInt(fb) + 1);
                    }
                    for (int la = la0; la <= la1; la++)
                        for (int lb = lb0; lb <= lb1; lb++)
                        {
                            Vector2 cc = pat.Position(la, lb);
                            if (cc.x < mn.x - s || cc.y < mn.y - s || cc.x > mx.x + s || cc.y > mx.y + s) continue;
                            poly.Clear(); poly.Add(pa); poly.Add(pb); poly.Add(pc);
                            foreach (TilePattern.Edge e in pat.Edges)
                            {
                                next.Clear();
                                for (int k = 0; k < poly.Count; k++)
                                {
                                    Vector2 p = poly[k], q = poly[(k + 1) % poly.Count];
                                    float dp = Vector2.Dot(p - cc, e.Normal) - e.H, dq = Vector2.Dot(q - cc, e.Normal) - e.H;
                                    if (dp <= 0) next.Add(p);
                                    if ((dp < 0) != (dq < 0) && dp != dq) next.Add(Vector2.Lerp(p, q, dp / (dp - dq)));
                                }
                                var sw = poly; poly = next; next = sw;
                                if (poly.Count < 3) break;
                            }
                            if (poly.Count < 3) continue;
                            mappedCells.Add((o, la, lb, endChart));
                            cellTile.A = la; cellTile.B = lb;
                            int i0 = acc.V.Count;
                            foreach (Vector2 p in poly)
                            {
                                float wb = ((p.x - pa.x) * (pc.y - pa.y) - (p.y - pa.y) * (pc.x - pa.x)) / d2;
                                float wc = ((pb.x - pa.x) * (p.y - pa.y) - (pb.y - pa.y) * (p.x - pa.x)) / d2, wa = 1 - wb - wc;
                                Add(acc, top[ia] * wa + top[ib] * wb + top[ic] * wc, (vn[ia] * wa + vn[ib] * wb + vn[ic] * wc).normalized, Uv(cellTile, p - cc));
                            }
                            for (int k = 1; k + 1 < poly.Count; k++) Tri(acc, tris, i0, i0 + k, i0 + k + 1, fn);
                        }
                }
                // walls down to the hull along open edges (plain: the middle of one atlas cell)
                Vector3 mid = (a + b + c) / 3;
                cellTile.A = cellTile.B = 0;
                Vector2 plain = Uv(cellTile, Vector2.zero);
                for (int e = 0; e < 3; e++)
                {
                    int p = ids[3 * i + e], q = ids[3 * i + (e + 1) % 3];
                    if (edgeUse[p < q ? (p, q) : (q, p)] > 1) continue;
                    Vector3 outward = Vector3.Cross(vp[q] - vp[p], fn);
                    if (Vector3.Dot(outward, vp[p] - mid) < 0) outward = -outward;
                    outward.Normalize();
                    int w0 = Add(acc, top[p], outward, plain);
                    Add(acc, top[q], outward, plain);
                    Add(acc, vp[p], outward, plain);
                    Add(acc, vp[q], outward, plain);
                    Tri(acc, tris, w0, w0 + 1, w0 + 3, outward);
                    Tri(acc, tris, w0, w0 + 3, w0 + 2, outward);
                }
                outlines?.Add(new TileOutline
                {
                    A = i, B = 0, Owner = o, Virtual = true, Center = mid, N = fn, TopPoint = mid + fn * lift,
                    Outline = new[] { top[ia], top[ib], top[ic] }, Hull = new[] { a, b, c }, Area = area
                });
            }
            result.TileCount = mappedCells.Count;
            result.Parts = new PartTiles[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                Acc a = accs[order[i]];
                var bn = new Vector3[a.BinSum.Count];
                for (int b = 0; b < bn.Length; b++) bn[b] = a.BinSum[b].sqrMagnitude > 1e-12f ? a.BinSum[b].normalized : decalN;
                var bt = new int[a.Bins.Count][];
                for (int b = 0; b < bt.Length; b++) bt[b] = a.Bins[b].ToArray();
                result.Parts[i] = new PartTiles
                {
                    Owner = order[i], Vertices = a.V.ToArray(), Normals = a.N.ToArray(), Uvs = a.Uv.ToArray(),
                    BinTriangles = bt, BinNormals = bn, BinAreas = a.BinArea.ToArray(),
                    JointTriangles = a.Joint.ToArray(), BackTriangles = a.Back.ToArray(),
                    TileAreaVectors = a.AreaVecs.ToArray(), CoveredArea = a.Covered
                };
            }
            if (!S.Overlay)
            {
                // Flight uses coarse patches, the editor uses the render mesh. Keep each box on its own part.
                var boxes = new List<Box>();
                foreach (var group in keep.GroupBy(t => (t.owner, Mathf.FloorToInt((t.a.x+t.b.x+t.c.x)/3),
                    Mathf.FloorToInt((t.a.y+t.b.y+t.c.y)/3), Mathf.FloorToInt((t.a.z+t.b.z+t.c.z)/3))))
                {
                    var points = group.SelectMany(t => new[] { t.a, t.b, t.c }).ToArray();
                    Vector3 normal = group.Aggregate(Vector3.zero, (v,t) => v + Vector3.Cross(t.b-t.a,t.c-t.a)).normalized;
                    if (normal.sqrMagnitude < 0.5f) normal = decalN;
                    Vector3 up = Tangent(Math.Abs(normal.y)<0.8f ? Vector3.up : Vector3.right, normal);
                    Vector3 right = Vector3.Cross(up, normal);
                    Vector3 min = Vector3.one * float.MaxValue, max = -min;
                    foreach (var p in points)
                    {
                        var q = new Vector3(Vector3.Dot(p,right),Vector3.Dot(p,up),Vector3.Dot(p,normal));
                        min = Vector3.Min(min,q); max = Vector3.Max(max,q);
                    }
                    max.z += lift;
                    Vector3 mid = (min+max)/2;
                    boxes.Add(new Box { Center=right*mid.x+up*mid.y+normal*mid.z, Up=up, Forward=normal, Size=Vector3.Max(max-min,Vector3.one*0.001f) });
                }
                result.Colliders = boxes.Take(Math.Max(1,S.MaxColliders)).ToArray();
            }
            else result.Colliders = new Box[0];
            return result;
        }

        // Partition a model face at actual mask edges. Classifying the resulting convex pieces
        // preserves sharp steps, holes and unions even when several edges cross the same triangle.
        private List<List<Vector3>> SkinPolygons(Vector3 a, Vector3 b, Vector3 c)
        {
            var pieces = new List<List<Vector3>> { new List<Vector3> { a,b,c } };
            void Split(Vector3 normal, float w)
            {
                for (int j = pieces.Count - 1; j >= 0; j--)
                {
                    var poly = pieces[j];
                    float lo = float.MaxValue, hi = -lo;
                    foreach (var p in poly) { float d = Vector3.Dot(normal,p)+w; lo = Math.Min(lo,d); hi = Math.Max(hi,d); }
                    if (lo >= -0.00001f || hi <= 0.00001f) continue;
                    var pos = new List<Vector3>(); var neg = new List<Vector3>();
                    for (int k=0;k<poly.Count;k++)
                    {
                        var p=poly[k]; var q=poly[(k+1)%poly.Count];
                        float dp=Vector3.Dot(normal,p)+w, dq=Vector3.Dot(normal,q)+w;
                        if(dp>=0) pos.Add(p);
                        if(dp<=0) neg.Add(p);
                        if(dp*dq<0) { var v=Vector3.Lerp(p,q,dp/(dp-dq)); pos.Add(v); neg.Add(v); }
                    }
                    pieces[j]=neg; pieces.Add(pos);
                }
            }
            void Planes(Vector4[] planes) { if (planes != null) foreach (var plane in planes) Split((Vector3)plane,plane.w); }
            Planes(cuts); Planes(S.ClipPlanes);
            if (S.LimitedTank)
            {
                foreach (int sign in new[] {-1,1}) { Split(sign*decalU,-Vector3.Dot(origin,sign*decalU)-S.Width/2); Split(sign*decalV,-Vector3.Dot(origin,sign*decalV)-S.Height/2); }
            }
            if (S.Regions != null) foreach (var region in S.Regions)
            {
                Planes(region.Domain);
                Vector2 Chart(Vector3 p) => new Vector2(Vector3.Dot(region.A,p-region.O),Vector3.Dot(region.B,p-region.O));
                Vector2 lower=Vector2.Min(Chart(a),Vector2.Min(Chart(b),Chart(c))), upper=Vector2.Max(Chart(a),Vector2.Max(Chart(b),Chart(c)));
                foreach (var loop in region.Loops) for (int k=0;k<loop.Length;k++)
                {
                    var p=loop[k];var q=loop[(k+1)%loop.Length];
                    if((q-p).sqrMagnitude < 1e-12f || Math.Max(p.x,q.x)<lower.x || Math.Min(p.x,q.x)>upper.x || Math.Max(p.y,q.y)<lower.y || Math.Min(p.y,q.y)>upper.y) continue;
                    Vector3 normal=((q.y-p.y)*region.A-(q.x-p.x)*region.B).normalized;
                    Split(normal,-Vector3.Dot(normal,region.O+p.x*region.A+p.y*region.B));
                }
            }
            if(S.KeepOut != null) for(int k=0;k+3<S.KeepOut.Length;k+=4)
                for(int j=1;j<=3;j++) { var v=S.KeepOut[k+j]; var n=v.normalized; Split(n,-Vector3.Dot(n,S.KeepOut[k])-v.magnitude); Split(n,-Vector3.Dot(n,S.KeepOut[k])+v.magnitude); }
            return pieces;
        }

        // 1.6: whether the main cover reaches an attached object: a vertex of the main tiles within dist of one of the object's
        // points (same space). An object beside the cover stays bare (gear doors swung open black out of a white wing).
        public static bool CoverReaches(TileResult main, List<Vector3> objectPoints, float dist)
        {
            if (main?.Parts == null || objectPoints.Count == 0) return false;
            var cells = new HashSet<(int, int, int)>();
            (int, int, int) Cell(Vector3 q) => (Mathf.FloorToInt(q.x / dist), Mathf.FloorToInt(q.y / dist), Mathf.FloorToInt(q.z / dist));
            foreach (PartTiles pt in main.Parts) foreach (Vector3 q in pt.Vertices) cells.Add(Cell(q));
            foreach (Vector3 q in objectPoints)
            {
                var (x, y, z) = Cell(q);
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
                    if (cells.Contains((x + dx, y + dy, z + dz))) return true; // ponytail: cell test, reach up to 2 x dist
            }
            return false;
        }

        // One object's overlay, all in the object's own space (origin and axes of its transform): pos/tri/owner are its triangles
        // and those of the part it sits on, obstacle[owner] marks the latter (inward: see TileSettings.Inward). The result depends only
        // on these and the settings.
        public static TileResult BuildOverlay(Vector3[] pos, int[] tri, int[] owner, bool[] obstacle, bool[] inward, TileSettings overlay,
            List<TileOutline> outlines = null)
        {
            float r = 0;
            foreach (Vector3 p in pos) r = Math.Max(r, p.magnitude);
            float s = Math.Max(overlay.TileSize, 0.01f);
            overlay.Obstacle = obstacle;
            overlay.Inward = inward;
            overlay.Overlay = true;
            var index = new SurfaceIndex(pos, tri, Vector3.zero, r + 1, Mathf.Max(s * 0.5f, 0.05f), Mathf.Clamp(s * 0.4f, 0.02f, 0.3f), owner);
            return Build(index, overlay, Vector3.zero, Vector3.forward, Vector3.right, Vector3.up, outlines);
        }

        // Every owner that is no obstacle, owner by owner: its face pieces (joined across bends under 30 deg), largest first, each
        // start on the lattice site nearest the piece's middle, the lattice anchored at the origin along u (v where u stands on the
        // face); more starts where a flood did not reach (the far side of a flap behind its trailing edge, faces past a sharp edge).
        // Pieces smaller than half a tile (edges, lips) get none: the tiles of the faces beside them end there or strips cover them.
        private TileResult RunOverlay(TileResult result, List<TileOutline> outlines)
        {
            if (S.CutPlanes != null && S.CutPlanes.Length > 0) cuts = (Vector4[])S.CutPlanes.Clone(); // the main decal's height cut (doors)
            if (S.Skin) return RunSkin(result, outlines);
            int[] patch = surface.Patches(30, true);
            var area = new Dictionary<int, float>();
            var mid = new Dictionary<int, Vector3>();
            var bodyOf = new Dictionary<int, int>();
            var spots = new Dictionary<int, List<(Vector3 q, Vector3 n)>>();
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int k = 0; k < surface.TriangleSlots; k++)
            {
                if (!surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int o) || !wrapIn[k]) continue;
                float ar = 0.5f * Vector3.Cross(b - a, c - a).magnitude;
                int p = patch[k];
                area[p] = (area.TryGetValue(p, out float a0) ? a0 : 0) + ar;
                mid[p] = (mid.TryGetValue(p, out Vector3 m0) ? m0 : Vector3.zero) + (a + b + c) / 3 * ar;
                bodyOf[p] = o;
                lo = Vector3.Min(lo, Vector3.Min(a, Vector3.Min(b, c)));
                hi = Vector3.Max(hi, Vector3.Max(a, Vector3.Max(b, c)));
            }
            if (area.Count == 0) return result;
            result.OnSurface = true;
            enclosedReach = Math.Max(0.5f, 1.1f * (hi - lo).magnitude);
            for (int k = 0; k < surface.TriangleSlots; k++)
            {
                if (!surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out _) || !wrapIn[k] || area[patch[k]] < 0.5f * pat.CellArea) continue;
                if (!spots.TryGetValue(patch[k], out var list)) spots[patch[k]] = list = new List<(Vector3, Vector3)>();
                foreach (Vector3 q in new[] { a, b, c, (a + b) / 2, (b + c) / 2, (c + a) / 2, (a + b + c) / 3 })
                    if (!InKeepOut(q) && InClip(q, 0) && !Enclosed(q, n)) list.Add((q, n));
            }
            var order = new List<int>(spots.Keys);
            order.Sort((x, y) => bodyOf[x] != bodyOf[y] ? bodyOf[x].CompareTo(bodyOf[y]) : area[x] != area[y] ? area[y].CompareTo(area[x]) : x.CompareTo(y));
            foreach (int p in order)
            {
                int body = bodyOf[p];
                ownBodies.Add(body);
                Vector3 centre = mid[p] / area[p];
                List<(Vector3 q, Vector3 n)> list = spots[p];
                for (int round = 0; round < 12 && !hitLimit; round++)
                {
                    list.RemoveAll(sp => Near(sp.q, 0.75f * s).Exists(t => !t.Dead && TileBody(t) == body && Vector3.Dot(t.N, sp.n) > 0.5f));
                    if (list.Count == 0) break;
                    int best = 0;
                    for (int i = 1; i < list.Count; i++)
                        if ((list[i].q - centre).sqrMagnitude < (list[best].q - centre).sqrMagnitude) best = i;
                    var (sq, sn) = list[best];
                    if (!StartOverlay(body, sq, sn)) list.RemoveAll(sp => (sp.q - sq).sqrMagnitude < 0.5625f * s * s); // nothing came of it: not here again
                }
            }
            tiles.RemoveAll(x => x.Dead);
            regionBody = -1;
            return tiles.Count == 0 ? result : Finish(result, outlines, null);
        }

        // An own flood on body starting near q (face normal n), on the anchored lattice: the site nearest q whose centre, dropped
        // along n, lands on the same face of the body. True if it laid tiles.
        private bool StartOverlay(int body, Vector3 q, Vector3 n)
        {
            region++;
            regionBodies.Add(body);
            regionBody = body;
            Vector3 u0 = Mathf.Abs(Vector3.Dot(decalU, n)) < 0.9f ? Tangent(decalU, n) : Tangent(decalV, n);
            Vector3 v0 = Vector3.Cross(u0, n) * vSign;
            var qq = new Vector2(Vector3.Dot(q - origin, u0), Vector3.Dot(q - origin, v0));
            float det = pat.E1.x * pat.E2.y - pat.E1.y * pat.E2.x;
            int a0 = Mathf.RoundToInt((qq.x * pat.E2.y - qq.y * pat.E2.x) / det), b0 = Mathf.RoundToInt((pat.E1.x * qq.y - pat.E1.y * qq.x) / det);
            var sites = new List<(int a, int b, float d)>();
            for (int da = -1; da <= 1; da++)
                for (int db = -1; db <= 1; db++)
                    sites.Add((a0 + da, b0 + db, (pat.Position(a0 + da, b0 + db) - qq).sqrMagnitude));
            sites.Sort((x, y) => x.d != y.d ? x.d.CompareTo(y.d) : x.a != y.a ? x.a.CompareTo(y.a) : x.b.CompareTo(y.b));
            foreach (var (sa, sb, _) in sites)
            {
                Vector2 d2 = pat.Position(sa, sb) - qq;
                Vector3 c3 = q + u0 * d2.x + v0 * d2.y;
                if (!surface.Raycast(c3 + n * (0.5f * s), -n, s, 0.17f, out SurfaceHit hs) || hs.Owner != body || !wrapIn[hs.Triangle] ||
                    Vector3.Dot(hs.FaceN, n) < cosCrease || Enclosed(hs.P, hs.FaceN)) continue;
                Vector3 u = Tangent(u0, hs.N);
                if (u == Vector3.zero) continue;
                Flood(new Walker { P = hs.P, N = hs.N, U = u, F = hs.FaceN }, sa, sb);
                return tiles.Exists(t => t.Region == region && !t.Dead);
            }
            // a face narrower than a tile around q: start right there (the lattice then runs through q)
            if (!surface.Closest(q, 0.05f, n, 0.3f, out SurfaceHit hc) || hc.Owner != body || !wrapIn[hc.Triangle]) return false;
            Vector3 uc = Tangent(u0, hc.N);
            if (uc == Vector3.zero) return false;
            Flood(new Walker { P = hc.P, N = hc.N, U = uc, F = hc.FaceN }, a0, b0);
            return tiles.Exists(t => t.Region == region && !t.Dead);
        }

        // The edge of a body thinner than a tile (wing or flap edge, seen from its side): no start of its own there; the tiles
        // of the broad faces end at it as before.
        // Only the body's own far side counts (an intake's flank stands a few cm over the fuselage it is glued on).
        private bool Thin(Vector3 q, Vector3 n, int owner) => surface.Raycast(q - n * 0.005f, -n, s, -1.01f, out SurfaceHit h, false) && h.Owner == owner;

        // A face that looks into another part (hidden faces inside a nose cone, the inner side of a joint) is no hull.
        // Overlay: hidden only if no ray of a fan (along n and tilted by 50 deg four ways) gets farther than the object is wide: the
        // inner side of a cargo bay door looks across the bay at the other door or the floor, but the flank of an intake low on a
        // Mk3 side, whose normal ray meets the fuselage bulging out below, sees the sky upwards.
        private bool Enclosed(Vector3 q, Vector3 n)
        {
            if (!S.Overlay) return surface.Raycast(q + n * 0.01f, n, 0.5f, -1.01f, out _, false);
            if (S.Inward != null && surface.Closest(q, 0.01f, n, 0.9f, out SurfaceHit hq) && hq.Owner >= 0 && hq.Owner < S.Inward.Length && S.Inward[hq.Owner])
                return Vector3.Dot(n, q - origin) < 0 || surface.Raycast(q + n * 0.01f, n, 0.5f, -1.01f, out _, false);
            // a face whose normal ray meets the object itself looks at its own inside (inner shell of an intake): hidden
            if (surface.Raycast(q + n * 0.01f, n, enclosedReach, -1.01f, out SurfaceHit hn, false) && (!Obstacle(hn.Owner) || (hn.P - q).sqrMagnitude < 0.25f)) return true;
            Vector3 a = Mathf.Abs(Vector3.Dot(decalU, n)) < 0.9f ? Tangent(decalU, n) : Tangent(decalV, n), b = Vector3.Cross(n, a);
            const float c = 0.6428f, sn = 0.7660f;
            foreach (Vector3 d in new[] { n, n * c + a * sn, n * c - a * sn, n * c + b * sn, n * c - b * sn })
                if (!surface.Raycast(q + n * 0.01f, d, enclosedReach, -1.01f, out _, false)) return false;
            return true;
        }
        private float enclosedReach = 0.5f;

        // A virtual tile over a broad face beyond a crease the walker could not cross (facet edges of 35-40Â° on low-poly
        // hulls) moves onto that face: hovering in the parent's plane it would be cut down to a sliver and leave a hole
        // in the middle of the cover. Not at the end of the surface or over faces narrower than a tile: the face must go
        // on ahead (away from the parent) for half a tile.
        private bool Snap(ref Walker w, ref Vector3 c, Vector3 ahead, float crease)
        {
            float rise = s;
            bool unfolded = false;
            SurfaceHit h = default;
            // 1.7 (Square) / 1.7.2 (cockpit masks, any pattern): past a convex crease the step is folded about the crease line (the walk
            // ended there, `crease` from the walker), so the lattice runs on over the edge as on a flat hull: one grid over the Mk2
            // chine, no shifted second front, no stretched tiles (a drop along the old normal shifted them, or missed beside the hull)
            Vector3 dir = Tangent(ahead, w.F);
            if ((pat.Shape == TileShape.Square || HasClip) && dir != Vector3.zero && crease >= 0)
            {
                // the edge lies within the walker's last step past `crease`: probe in 5 mm steps for the first face beyond it
                Vector3 cp = w.P + dir * crease;
                SurfaceHit e = default;
                bool found = false;
                for (float k = 0.005f; k <= step + 0.03f && !found; k += 0.005f)
                {
                    if (!surface.Raycast(cp + dir * k + w.N * 0.05f, -w.N, 0.25f, 0.17f, out e, false)) break;
                    if (Vector3.Dot(e.FaceN, w.F) <= cosCrease) { found = true; cp += dir * (k - 0.0025f); }
                }
                if (found)
                {
                    Vector3 axis = Vector3.Cross(w.F, e.FaceN);
                    if (axis.sqrMagnitude > 1e-8f)
                    {
                        axis.Normalize();
                        Vector3 d = c - cp, dPar = axis * Vector3.Dot(axis, d), dPerp = d - dPar;
                        Vector3 f2 = Vector3.Cross(e.FaceN, axis);
                        if (Vector3.Dot(f2, dPerp) < 0) f2 = -f2;
                        Vector3 q = cp + dPar + f2 * dPerp.magnitude;
                        unfolded = surface.Raycast(q + e.FaceN * rise, -e.FaceN, 2 * rise, 0.17f, out h);
                    }
                }
            }
            if (!unfolded && !surface.Raycast(c + w.N * rise, -w.N, 2 * rise, 0.17f, out h)) return false;
            if (!InWrap(h) || Vector3.Dot(h.FaceN, w.F) < (unfolded ? 0.05f : cosFace) || regionBody >= 0 && h.Owner != regionBody) return false;
            Vector3 u = Transport(w.U, w.N, h.N), fwd = Tangent(ahead, h.FaceN);
            if (u == Vector3.zero || fwd == Vector3.zero || !SameFace(h.P + fwd * (0.5f * s), h.FaceN)) return false;
            w.U = u;
            w.P = c = h.P;
            w.N = h.N;
            w.F = h.FaceN;
            w.N1 = w.N2 = w.N3 = w.N4 = Vector3.zero; // a fresh start on the new face for the bend test
            w.D1 = w.D2 = w.D3 = w.D4 = 0;
            return true;
        }

        private bool SameFace(Vector3 q, Vector3 f)
        {
            float rise = 0.25f * s;
            return surface.Raycast(q + f * rise, -f, 2 * rise, 0.17f, out SurfaceHit h, false) && Vector3.Dot(h.FaceN, f) >= cosCrease;
        }

        // Dedupe: a candidate closer than 0.6 tile to tiles on the same side (stacking, or another flood front after a
        // lap around a closed hull) goes to whichever lies nearer the start in lattice terms (|lattice position|, the
        // unrolled distance on developable hulls). Fronts that meet thus split the hull on one line half way, instead
        // of first-come interleaving over a wide ragged band. Losing tiles are removed (their lattice site stays visited).
        // Cockpit cover: fronts reach a chine from both faces (one over the chine by Snap); tiles on faces at a crease
        // (> 35 deg) are not stacked, so neither front gives way there (left zigzag holes along the Mk2 chine).
        // ponytail: cockpit mode only, the v7 scenes keep the old rule (any tile on the same side).
        private float claimDot => HasClip || S.Overlay ? Mathf.Cos(35 * Mathf.Deg2Rad) : 0;

        private bool Claim(int a, int b, Vector3 p, Vector3 n)
        {
            float mine = pat.Position(a, b).sqrMagnitude;
            // 1.7: an island flood keeps a whole cell (0.75 tile) clear of every tile laid before, whatever its body: island tiles wove
            // in between the main cover (the Voronoi Square cockpit of 1.6.0; a misaligned Square site can lie 0.71 tile off all)
            List<Tile> close = Near(p, (islandFlood ? 0.75f : 0.6f) * s);
            for (int i = close.Count - 1; i >= 0; i--)
            {
                Tile o = close[i];
                if (Vector3.Dot(o.N, n) <= claimDot || !islandFlood && regionBody >= 0 && TileBody(o) != regionBody || regionBody < 0 && o.Region != 0) close.RemoveAt(i); // other bodies
                else if (pat.Position(o.A, o.B).sqrMagnitude <= mine + 1e-6f) return false;
            }
            if (islandFlood && close.Count > 0) return false; // an island flood never takes over tiles already laid
            foreach (Tile o in close.ToArray()) Remove(o);
            return true;
        }

        private void Remove(Tile t)
        {
            t.Dead = true;
            live--;
            long key = SiteKey(t.A, t.B, t.Region);
            if (tileAt.TryGetValue(key, out Tile at) && at == t) tileAt.Remove(key);
            HashCell(t.C, out int x, out int y, out int z);
            hash[HashKey(x, y, z)].Remove(t);
        }

        // cockpit cover and "whole part" mode (every body separate): fronts meeting at a chine take over each other's sites
        private int OwnerAt(Walker w) => surface.Closest(w.P, 0.01f, w.N, 0f, out SurfaceHit h) ? h.Owner : -1;

        private bool Cockpit => HasClip || S.SeparateOwner != null && S.SeparateOwner.Length > 0 && Array.TrueForAll(S.SeparateOwner, x => x);

        private void Revive(Tile t)
        {
            t.Dead = false;
            live++;
            tileAt[SiteKey(t.A, t.B, t.Region)] = t;
            AddToHash(t);
        }

        private Tile AddTile(int a, int b, Walker w, Vector3 c, bool virt)
        {
            if (live >= S.MaxTiles)
            {
                hitLimit = true;
                return null;
            }
            var t = new Tile
            {
                A = a, B = b, Virtual = virt, W = w, C = c, N = w.N, U = w.U, V = V(w), F = w.F,
                // the clip planes stop the flood (DESIGN-v8 B1); along a narrow region (a band of a cockpit mask) it may run on half a
                // tile outside, or a square lattice turned against the band could not follow it
                Floods = !virt && InFoot(a, b, c, 0) && InClip(c, S.Regions != null && S.Regions.Length > 0 ? 0.5f * s : 0),
                Lost = S.TileLoss > 0 && Hash(a, b, S.Seed * 31 + 7) % 10000 < S.TileLoss * 10000,
                State = 1, Region = region
            };
            tiles.Add(t);
            live++;
            tileAt[SiteKey(a, b, region)] = t;
            AddToHash(t);
            return t;
        }

        private void AddToHash(Tile t)
        {
            HashCell(t.C, out int x, out int y, out int z);
            long key = HashKey(x, y, z);
            if (!hash.TryGetValue(key, out List<Tile> list)) hash[key] = list = new List<Tile>(2);
            list.Add(t);
        }

        private void HashCell(Vector3 p, out int x, out int y, out int z)
        {
            x = Mathf.FloorToInt(p.x / hashCell);
            y = Mathf.FloorToInt(p.y / hashCell);
            z = Mathf.FloorToInt(p.z / hashCell);
        }

        // Tiles with their centre within r (<= hashCell) of p. Reuses one list.
        private List<Tile> Near(Vector3 p, float r)
        {
            near.Clear();
            HashCell(p, out int x0, out int y0, out int z0);
            float r2 = r * r;
            for (int x = x0 - 1; x <= x0 + 1; x++)
            for (int y = y0 - 1; y <= y0 + 1; y++)
            for (int z = z0 - 1; z <= z0 + 1; z++)
            {
                if (!hash.TryGetValue(HashKey(x, y, z), out List<Tile> list)) continue;
                foreach (Tile t in list)
                    if ((t.C - p).sqrMagnitude < r2) near.Add(t);
            }
            return near;
        }

        // Moves along the surface by distance in a fixed direction of the walker's (U, V) frame. On failure the
        // walker stays at the last good point and got tells how far it came.
        private bool Walk(ref Walker w, Vector2 dir, float distance, out float got)
        {
            got = 0;
            float remaining = distance;
            while (remaining > 1e-5f)
            {
                float len = Math.Min(step, remaining);
                Vector3 heading = w.U * dir.x + V(w) * dir.y;
                Vector3 guess = w.P + heading * len;
                // drop the step straight onto the surface: past an edge the ray misses, so the walker can't slide along it
                float rise = len + 0.02f;
                if (!surface.Raycast(guess + w.N * rise, -w.N, 2 * rise, 0.34f, out SurfaceHit h)) return false;
                if (!InWrap(h) || OnCap(h) || regionBody >= 0 && h.Owner != regionBody) return false;
                // a strip face (chamfer narrower than a tile) is left to the strip tiles
                // (only next to a broad face like Drop: the valleys between ribs are narrow flat faces too)
                if (Vector3.Dot(h.FaceN, w.F) < cosCrease && BroadFace(w.P, w.F) && StripFace(h, w.F)) return false;
                // Bend radius over the last few steps: smoothing spreads a sharp corner over several steps,
                // so a per-step test alone would let the walker creep around it.
                if (TooBent(h.N, w.N, len) || TooBent(h.N, w.N1, w.D1 + len) || TooBent(h.N, w.N2, w.D2 + len) ||
                    TooBent(h.N, w.N3, w.D3 + len) || TooBent(h.N, w.N4, w.D4 + len))
                    return false;                                                // corner or rim
                if ((h.P - guess).magnitude > len * 0.75f + 0.005f) return false; // walked off the surface
                // A step that snaps back onto an edge, or slides along it, has not arrived: demand real progress and
                // no sideways slip in the tangent plane (height changes from ribs and curvature are fine).
                if (Vector3.Dot(h.P - w.P, heading) < len * 0.9f) return false;
                Vector3 slip = h.P - guess;
                slip -= w.N * Vector3.Dot(slip, w.N);
                if (slip.magnitude > len * 0.2f + 0.006f) return false;
                Vector3 nu = Transport(w.U, w.N, h.N);
                if (nu == Vector3.zero) return false;
                w.N4 = w.N3; w.D4 = w.D3 > 0 ? w.D3 + len : 0;
                w.N3 = w.N2; w.D3 = w.D2 > 0 ? w.D2 + len : 0;
                w.N2 = w.N1; w.D2 = w.D1 > 0 ? w.D1 + len : 0;
                w.N1 = w.N; w.D1 = len;
                w.P = h.P;
                w.N = h.N;
                w.F = h.FaceN;
                w.U = nu;
                got += len;
                remaining -= len;
            }
            return true;
        }

        // Parallel transport of tangent u from normal n1 to n2: the smallest rotation taking n1 to n2. Plain projection
        // onto the new plane drags u towards the direction of least turning, so frames drift on long wraps.
        private static Vector3 Transport(Vector3 u, Vector3 n1, Vector3 n2)
        {
            float c = Vector3.Dot(n1, n2);
            if (c < -0.9f) return Tangent(u, n2);
            return Tangent(u - (n1 + n2) * (Vector3.Dot(u, n2) / (1 + c)), n2);
        }

        private bool TooBent(Vector3 n, Vector3 before, float distance) =>
            distance > 0 && before != Vector3.zero &&
            Mathf.Acos(Mathf.Clamp(Vector3.Dot(n, before), -1f, 1f)) > distance / minBendRadius;

        // Frame normal from the hull under the nominal cell corners (Newell fit). The smoothed walker normal leans
        // into nearby edges; cutting the cell in that frame and laying the tile in another opens cracks.
        // Also estimates where the tile's outer edge will sit (Edge), see Top.
        private void Prefit(Tile t, Vector2[] corners)
        {
            // home face and body: the hull right under the lattice centre (virtual tiles hang beside their walker point)
            float rise = 0.25f * s + 0.02f;
            if (surface.Raycast(t.C + t.N * rise, -t.N, 2 * rise, 0.17f, out SurfaceHit hh, false)) { t.HP = hh.P; t.HF = hh.FaceN; t.Body = hh.Owner; }
            else
            {
                t.HP = t.W.P; t.HF = t.W.F;
                t.Body = surface.Closest(t.W.P, 0.05f, t.W.N, 0f, out SurfaceHit hw) ? hw.Owner : -1;
            }
            if (t.Region > 0) t.Body = BodyOf(t.Region); // an own flood keeps to its body
            else if (Obstacle(t.Body)) t.Body = OwnerAt(t.W); // a virtual tile hanging over an obstacle belongs to its walker's body
            Vector3 fit = Vector3.zero;
            var hits = new List<Vector3>(corners.Length);
            foreach (Vector2 c in corners)
                if (Drop(t, c, out SurfaceHit h))
                {
                    if (hits.Count > 0) fit += Newell(hits[hits.Count - 1], h.P);
                    hits.Add(h.P);
                }
            float lift = 0;
            if (hits.Count >= 3)
            {
                fit += Newell(hits[hits.Count - 1], hits[0]);
                Vector3 n = fit.sqrMagnitude > 1e-12f ? fit.normalized : t.N;
                if (Vector3.Dot(n, t.N) < 0) n = -n;
                Vector3 u = Tangent(t.U, n);
                if (Vector3.Dot(n, t.N) >= 0.8f && u != Vector3.zero)
                {
                    t.N = n;
                    t.U = u;
                    t.V = Vector3.Cross(u, n) * vSign;
                }
                lift = float.MinValue;
                foreach (Vector3 p in hits) lift = Math.Max(lift, Vector3.Dot(p - t.C, t.N));
            }
            t.Edge = lift + S.SurfaceOffset + S.Thickness - Math.Max(S.BevelDepth, 0);
        }

        // Cells are cut between the tiles' outer edges, not their feet: thick rigid tiles on a bend (low-poly hull
        // facets meet at 30-40Â°) would otherwise gape by thickness * tan(bend) at the top. Feet meet under the tiles.
        private static Vector3 Top(Tile t) => t.C + t.N * t.Edge;

        // Where the two tile tops meet, measured along n in this tile's plane: half the distance between the tops,
        // stretched by |d|Â² / |d in plane|Â² because the neighbour's top lies above or below this plane (on a
        // 35Â° facet crease the plain projection would leave a 1-2 cm joint). 1 on a plane.
        private static float Meet(Tile t, Vector3 d, Vector3 n)
        {
            Vector3 inPlane = d - t.N * Vector3.Dot(d, t.N);
            float stretch = Math.Min(d.sqrMagnitude / Math.Max(inPlane.sqrMagnitude, 1e-12f), 1.5f);
            return Vector3.Dot(d, n) / 2 * stretch;
        }

        // Crease between t and its neighbour j (home faces more than CreaseDeg apart): the line where the two home planes
        // meet, as a half-plane q . n <= h in t's frame (q dropped along t.N onto t's home plane), moved to where the tile
        // tops meet (thickness * tan(bend / 2) beyond the crease on a convex bend, before it on a concave one). Both tiles
        // compute the same line, so their cells close without overlap. False if no crease lies between the two centres.
        private bool CreaseCut(Tile t, Tile j, Vector2 toward, out Vector2 n, out float h)
        {
            n = default;
            h = 0;
            float c = Vector3.Dot(t.HF, j.HF), den = Vector3.Dot(t.N, t.HF);
            if (c >= cosCrease || den < 0.3f) return false;
            // only tiles standing on their home face: a virtual tile hanging in its parent's plane over the next face
            // (not snapped onto it) is cut by Drop against its parent's face and leaves that crease to the strips
            if (Vector3.Dot(t.F, t.HF) < cosCrease || Vector3.Dot(j.F, j.HF) < cosCrease) return false;
            // only between broad flat faces (Mk3 belly and its chamfer, Mk2 chine band): among the narrow facets of a
            // curved hull some pairs would qualify and their neighbours not, and the cells would no longer meet
            if (!BroadFace(t.HP, t.HF) || !BroadFace(j.HP, j.HF)) return false;
            Vector3 x0 = t.C + t.N * (Vector3.Dot(t.HP - t.C, t.HF) / den);
            float g0 = Vector3.Dot(x0 - j.HP, j.HF); // height of t's foot over j's home plane: < 0 on a convex bend
            Vector3 du = t.U - t.N * (Vector3.Dot(t.U, t.HF) / den), dv = t.V - t.N * (Vector3.Dot(t.V, t.HF) / den);
            var grad = new Vector2(Vector3.Dot(du, j.HF), Vector3.Dot(dv, j.HF));
            float gl = grad.magnitude;
            if (gl < 1e-4f || Math.Abs(g0) < 1e-6f) return false;
            n = grad * (-Math.Sign(g0) / gl);
            h = Math.Abs(g0) / gl;
            // any neighbour beyond the crease line (also one along it at a slant) ends at the same line
            if (Vector2.Dot(n, toward.normalized) < 0.1f || h > toward.magnitude) return false;
            // a real, straight crease: both home faces run flat up to within 4 mm of the line, here and one tile to either
            // side along it. On a curved hull of narrow facets the planes of two facets meet over other facets; along a
            // curved crease (engine adapter) each pair gets its own line and triangle holes open where cells meet.
            const float probe = 0.004f;
            var along = new Vector2(-n.y, n.x);
            for (int i = -1; i <= 1; i++)
                if (!FaceAt(t, n * Math.Max(h - probe, 0) + along * (i * s), t.HP, t.HF) ||
                    !FaceAt(t, n * (h + probe) + along * (i * s), j.HP, j.HF)) return false;
            float half = Mathf.Acos(Mathf.Clamp(c, -1, 1)) / 2;
            float e = half < 40 * Mathf.Deg2Rad ? (S.SurfaceOffset + S.Thickness - Math.Max(S.BevelDepth, 0)) * Mathf.Tan(half) : 0;
            h += g0 < 0 ? e : -e;
            return true;
        }

        // Does the flat face (point fp, normal f) lie under q of t's frame (q dropped along t.N onto that plane)?
        private bool FaceAt(Tile t, Vector2 q, Vector3 fp, Vector3 f)
        {
            Vector3 x = t.C + t.U * q.x + t.V * q.y;
            float den = Vector3.Dot(t.N, f);
            if (den < 0.2f) return false;
            x += t.N * (Vector3.Dot(fp - x, f) / den);
            const float rise = 0.01f;
            return surface.Raycast(x + f * rise, -f, 2 * rise, 0.17f, out SurfaceHit h, false) && Vector3.Dot(h.FaceN, f) >= cosPlanar;
        }

        private static Vector3 Newell(Vector3 a, Vector3 b) =>
            new Vector3((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y));

        // ---------- cells ----------

        // Convex clipping (Sutherland-Hodgman) of a large start square against p . n <= h for every half-plane; each
        // result vertex carries the tag of the edge starting at it (clip lines take the tag of their half-plane).
        // which: 0 tile edge, 1 cell midline, else dist.
        private Vector2[] ClipCell(List<Half> halves, int which, out int[] tags, Func<Half, float> dist = null)
        {
            var pin = new List<Vector2>(10);
            var pout = new List<Vector2>(10);
            var tin = new List<int>(10);
            var tout = new List<int>(10);
            float r = 2.5f * s;
            pin.Add(new Vector2(-r, -r)); pin.Add(new Vector2(r, -r)); pin.Add(new Vector2(r, r)); pin.Add(new Vector2(-r, r));
            tin.Add(TagBorder); tin.Add(TagBorder); tin.Add(TagBorder); tin.Add(TagBorder);
            foreach (Half hp in halves)
            {
                float h = which == 0 ? hp.HTile : which == 1 ? hp.HMid : dist(hp);
                pout.Clear();
                tout.Clear();
                int n = pin.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = pin[i], q = pin[(i + 1) % n];
                    float dp = Vector2.Dot(p, hp.N) - h, dq = Vector2.Dot(q, hp.N) - h;
                    if (dp <= 0)
                    {
                        pout.Add(p); tout.Add(tin[i]);
                        if (dq > 0) { pout.Add(Vector2.Lerp(p, q, dp / (dp - dq))); tout.Add(hp.Tag); }
                    }
                    else if (dq <= 0)
                    {
                        pout.Add(Vector2.Lerp(p, q, dp / (dp - dq))); tout.Add(tin[i]);
                    }
                }
                var sw = pin; pin = pout; pout = sw;
                var tw = tin; tin = tout; tout = tw;
                if (pin.Count < 3) break;
            }
            tags = tin.ToArray();
            return pin.Count < 3 ? null : pin.ToArray();
        }

        private void Cell(Tile t)
        {
            float gap2 = S.Gap / 2;
            t.Halves = new List<Half>(8);
            t.EdgeOwners = new List<Tile>[pat.Edges.Length];
            t.Crease = new bool[pat.Edges.Length];
            for (int e = 0; e < pat.Edges.Length; e++)
            {
                TilePattern.Edge edge = pat.Edges[e];
                Vector3 n3 = t.U * edge.Normal.x + t.V * edge.Normal.y;
                float raw = float.MaxValue;
                List<Tile> owners = null;
                bool toEmpty = false;
                foreach (int k in edge.Owners)
                {
                    if (!tileAt.TryGetValue(SiteKey(t.A + pat.StepA[k], t.B + pat.StepB[k], t.Region), out Tile j)) continue;
                    if (empty != null && empty.Contains(j)) { toEmpty = true; continue; }
                    float lim = 1.6f * pat.StepVec[k].magnitude;
                    if ((j.C - t.C).sqrMagnitude >= lim * lim) continue;
                    raw = Math.Min(raw, Meet(t, Top(j) - Top(t), n3));
                    (owners = owners ?? new List<Tile>(2)).Add(j);
                }
                t.EdgeOwners[e] = owners;
                float h = edge.H;
                if (owners != null)
                {
                    Vector2 nrm = edge.Normal;
                    bool crease = false;
                    if (owners.Count == 1 && edge.Owners.Length == 1)
                    {
                        // single neighbour: cut along the true direction to it, so both sides of the joint stay parallel
                        // although the transported frames of neighbours differ a little on doubly curved hulls
                        Vector3 d = owners[0].C - t.C; // direction from the feet, distance from the tops (below)
                        var w = new Vector2(Vector3.Dot(d, t.U), Vector3.Dot(d, t.V));
                        if (w.sqrMagnitude > 1e-8f && Vector2.Dot(w.normalized, nrm) > 0.7f) // within 45Â°
                        {
                            nrm = w.normalized;
                            raw = Meet(t, Top(owners[0]) - Top(t), t.U * nrm.x + t.V * nrm.y);
                            // a crease between the two: both cells end on the crease line (where the tops meet), so
                            // neither tile straddles it and hovers over the far face (DESIGN-v7 Â§4)
                            if (CreaseCut(t, owners[0], w, out Vector2 cn, out float ch)) { nrm = cn; raw = ch; crease = true; }
                        }
                    }
                    float hc = crease ? Mathf.Clamp(raw, 0.05f * h, 2 * h) : Mathf.Clamp(raw, 0.35f * h, 1.5f * h);
                    t.Crease[e] = crease;
                    t.Halves.Add(new Half { N = nrm, HTile = hc - gap2, HMid = hc, Raw = Math.Max(raw, hc - gap2), Tag = e });
                }
                else
                {
                    if (toEmpty) h *= EmptyGrow;
                    t.Halves.Add(new Half { N = edge.Normal, HTile = h - gap2, HMid = h, Raw = h - gap2, Tag = TagBorder });
                }
            }

            // foreign tiles: close, same side, but no lattice neighbour (seams where flood fronts meet). The dedupe in
            // Claim leaves up to 1.6 pitches between the two fronts, so a nominal edge facing a foreign tile may
            // grow towards it (up to SeamGrow * h); the bisector below then closes the joint to Gap.
            int nominalEdges = t.Halves.Count;
            foreach (Tile j in Near(t.C, SeamReach * s))
            {
                int da = j.A - t.A, db = j.B - t.B;
                if (j == t || empty != null && empty.Contains(j) || Vector3.Dot(j.N, t.N) <= 0.3f || Split(t.Body, j.Body) ||
                    j.Region == t.Region && (pat.StepIndex(da, db) >= 0 || (Math.Abs(da) <= 1 && Math.Abs(db) <= 1))) continue;
                Vector3 d = Top(j) - Top(t);
                var w = new Vector2(Vector3.Dot(d, t.U), Vector3.Dot(d, t.V));
                float beta = w.magnitude;
                if (beta < 1e-4f) continue;
                Vector2 dir = w / beta;
                bool facing = false;
                for (int e = 0; e < nominalEdges; e++)
                {
                    Half he = t.Halves[e];
                    if (he.Tag != TagBorder || Vector2.Dot(he.N, dir) < 0.85f) continue;
                    facing = true;
                    float hg = SeamGrow * pat.Edges[e].H;
                    he.HTile = hg - gap2; he.HMid = hg; he.Raw = he.HTile;
                    t.Halves[e] = he;
                }
                // beyond 1.25 pitches the nominal cells cannot touch: only a tile in front of an open edge matters
                if (!facing && (j.C - t.C).sqrMagnitude > 1.5625f * s * s) continue;
                float m = Meet(t, d, t.U * dir.x + t.V * dir.y);
                // DESIGN-v10 G6: where fronts meet, each side's midline is its own estimate; with joint 0 the slightly tilted
                // tops would overlap by a fraction of a mm, so keep 0.3 mm back on either side (the joint sheet closes it)
                t.Halves.Add(new Half { N = dir, HTile = m - Math.Max(gap2, 0.0003f), HMid = m, Raw = m, Tag = TagForeign, Other = j });
            }

            if (S.StraightBorder && S.Unrolled && !t.Strip)
            {
                // straight cut in the unrolled lattice coordinates: the tile frame runs along the lattice axes
                Vector2 lp = pat.Position(t.A, t.B);
                t.Halves.Add(new Half { N = new Vector2(1, 0), HTile = footW / 2 - lp.x, HMid = footW / 2 - lp.x, Raw = footW / 2 - lp.x, Tag = TagFootprint });
                t.Halves.Add(new Half { N = new Vector2(-1, 0), HTile = footW / 2 + lp.x, HMid = footW / 2 + lp.x, Raw = footW / 2 + lp.x, Tag = TagFootprint });
                t.Halves.Add(new Half { N = new Vector2(0, 1), HTile = footH / 2 - lp.y, HMid = footH / 2 - lp.y, Raw = footH / 2 - lp.y, Tag = TagFootprint });
                t.Halves.Add(new Half { N = new Vector2(0, -1), HTile = footH / 2 + lp.y, HMid = footH / 2 + lp.y, Raw = footH / 2 + lp.y, Tag = TagFootprint });
            }
            else if (S.StraightBorder)
            {
                float lift = S.SurfaceOffset + S.Thickness; // trim the top, not the base
                for (int i = 0; i < 4; i++)
                {
                    Vector3 axis = i < 2 ? decalU : decalV;
                    if ((i & 1) == 1) axis = -axis;
                    float half = (i < 2 ? S.Width : S.Height) / 2;
                    var n2 = new Vector2(Vector3.Dot(t.U, axis), Vector3.Dot(t.V, axis));
                    float len = n2.magnitude;
                    if (len < 0.05f) continue;
                    float h = (half - Vector3.Dot(t.C - origin, axis) - lift * Vector3.Dot(t.N, axis)) / len;
                    t.Halves.Add(new Half { N = n2 / len, HTile = h, HMid = h, Raw = h, Tag = TagFootprint });
                }
            }

            bool gone = false;
            if (HasClip)
            {
                // union: the plane (or region edge) the tile centre lies deepest inside (near the corner of two, the tile keeps one side)
                Vector4 best = default;
                float bd = float.MaxValue;
                CoverRegion bestRegion = null;
                if (S.ClipPlanes != null)
                    foreach (Vector4 c in S.ClipPlanes)
                    {
                        float dc = c.x * t.C.x + c.y * t.C.y + c.z * t.C.z + c.w;
                        if (dc < bd) { bd = dc; best = c; }
                    }
                if (S.Regions != null)
                    foreach (CoverRegion r in S.Regions)
                    {
                        float dr = RegionDistFast(r, t.C, out Vector4 pl);
                        if (dr < bd) { bd = dr; best = pl; bestRegion = r; }
                    }
                if (bestRegion != null && bd < s) gone |= RegionHalves(t, bestRegion, bd >= 0);
                else if (best == default) gone |= bd > 0; // far outside every region (no edge near)
                else gone |= PlaneHalf(t, best);
            }
            if (cuts != null)
                foreach (Vector4 c in cuts) gone |= PlaneHalf(t, c);
            OccupiedHalves(t);
            gone |= KeepOutHalves(t);
            t.O = gone ? null : ClipCell(t.Halves, 0, out t.Tags);
            Vector2[] mid = ClipCell(t.Halves, 1, out _);
            t.MidArea = mid != null ? Math.Abs(Shoelace(mid)) : 0;
            bool atCut = t.Tags != null && Array.IndexOf(t.Tags, TagFootprint) >= 0;
            if (t.O == null || Math.Abs(Shoelace(t.O)) < (atCut ? 0.006f : 0.02f) * pat.CellArea) t.State = 0;
        }

        // DESIGN-v8 B1: straight cut at a clip or cut plane like the footprint (wall, bevel; the top is trimmed, the foot
        // stays inside). True if the tile lies wholly beyond a plane parallel to it.
        private bool PlaneHalf(Tile t, Vector4 pl)
        {
            var n = new Vector3(pl.x, pl.y, pl.z);
            var n2 = new Vector2(Vector3.Dot(t.U, n), Vector3.Dot(t.V, n));
            float len = n2.magnitude, slope = Vector3.Dot(t.N, n);
            // Shallow tapers/shoulders near stack seams (0 < slope <= 0.5) must not pull the cut back behind the seam plane;
            // steeper faces still trim the tile top so it does not overhang, and negative slopes still extend to close V-gaps.
            float liftOff = slope < 0 ? (S.SurfaceOffset + S.Thickness) * slope : slope > 0.5f ? (S.SurfaceOffset + S.Thickness) * (slope - 0.5f) : 0;
            float top = Vector3.Dot(n, t.C) + pl.w + liftOff;
            if (len < 0.05f) return top > 0;
            float h = -top / len;
            if (h < 2.5f * s) t.Halves.Add(new Half { N = n2 / len, HTile = h, HMid = h, Raw = h, Tag = TagFootprint });
            return false;
        }

        // A tile whose centre lies in a cover region: cut straight on every loop edge near it that has the centre on its kept side
        // (loops run with the region on their left), unless just beyond the edge another region or clip plane keeps the hull (charts
        // overlap where the hull turns from one to the next). Narrow strips (the dark band round the Mk3 cockpit) get both sides.
        // A tile centred just outside (it hangs over the region's border) is cut on every edge near it that has the centre beyond it
        // as well (at a corner of the region both; in a notch of it that takes a little too much).
        private bool RegionHalves(Tile t, CoverRegion r, bool outside)
        {
            bool gone = false;
            float reach = 1.15f * s + S.Thickness;
            Vector3 rel = t.C - r.O;
            var q = new Vector2(Vector3.Dot(rel, r.A), Vector3.Dot(rel, r.B));
            // candidate edges nearest first; one cut per direction (outward normals more than ~18 deg apart): both sides of a band
            // and both edges at a corner and along a bend, but not the many nearly parallel short edges of a wavy border (each a
            // cut that takes a sliver; RCS cut-outs are balls, see KeepOutHalves)
            var cand = new List<(float d, Vector2 dir, Vector4 plane)>();
            RegionGrid g = GridOf(r);
            int gx = Mathf.Clamp((int)Math.Floor((q.x - g.X0) / g.Cell), 0, g.W - 1), gy = Mathf.Clamp((int)Math.Floor((q.y - g.Y0) / g.Cell), 0, g.H - 1);
            foreach (var (a, b) in g.Edges[gy * g.W + gx])
            {
                Vector2 ab = b - a;
                if (ab.sqrMagnitude < 1e-12f) continue;
                float u = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                Vector2 near2 = a + ab * u;
                float d2 = (near2 - q).sqrMagnitude;
                if (d2 > reach * reach) continue;
                bool right = ab.x * (q.y - a.y) - ab.y * (q.x - a.x) < 0; // centre beyond the edge
                if (right && !outside) continue;
                Vector3 n = (r.A * ab.y - r.B * ab.x).normalized, p0 = r.O + r.A * a.x + r.B * a.y;
                if (!right)
                {
                    Vector3 beyond = r.O + r.A * near2.x + r.B * near2.y + n * 0.01f;
                    beyond += t.N * Vector3.Dot(t.C - beyond, t.N); // at the tile's height above the hull
                    if (KeptElsewhere(beyond, r)) continue;
                }
                cand.Add((d2, new Vector2(ab.y, -ab.x).normalized, new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, p0))));
            }
            cand.Sort((x, y) => x.d.CompareTo(y.d));
            var taken = new List<Vector2>();
            foreach (var (_, dir, plane) in cand)
            {
                if (taken.Exists(o => Vector2.Dot(o, dir) > 0.985f)) continue;
                taken.Add(dir);
                gone |= PlaneHalf(t, plane);
            }
            // the domain's border too (where the next chart takes over), unless that chart keeps the hull beyond it
            if (r.Domain != null)
                foreach (Vector4 c in r.Domain)
                {
                    var cn = new Vector3(c.x, c.y, c.z);
                    float dc = Vector3.Dot(cn, t.C) + c.w;
                    if (dc > 0 || dc < -reach) continue;
                    Vector3 beyond = t.C - cn * (dc - 0.01f);
                    if (!KeptElsewhere(beyond, r)) gone |= PlaneHalf(t, c);
                }
            return gone;
        }

        private bool KeptElsewhere(Vector3 p, CoverRegion except)
        {
            if (S.ClipPlanes != null)
                foreach (Vector4 c in S.ClipPlanes) if (c.x * p.x + c.y * p.y + c.z * p.z + c.w <= 0) return true;
            foreach (CoverRegion o in S.Regions)
                if (o != except && RegionDistFast(o, p, out _) < 0) return true;
            return false;
        }

        // ---------- keep-out boxes (DESIGN-v10 G4) ----------

        // Straight cut at the face of each keep-out box near the tile: the face the tile centre lies farthest outside of. The
        // box lies wholly behind any of its face planes, so the cut tile never reaches into it (at a box corner the cut follows
        // one face). A tile centred inside a box is dropped; its neighbours then grow towards the empty site (Regrow) and are
        // cut at the box in turn. True if the tile is gone.
        private bool KeepOutHalves(Tile t)
        {
            if (S.KeepOut == null) return false;
            bool gone = false;
            for (int i = 0; i + 3 < S.KeepOut.Length; i += 4)
            {
                Vector3 c = S.KeepOut[i], d = t.C - c;
                float h1 = S.KeepOut[i + 1].magnitude, h2 = S.KeepOut[i + 2].magnitude, h3 = S.KeepOut[i + 3].magnitude;
                if (h1 > 1e-6f && Math.Abs(h2 - h1) < 0.01f * h1 && Math.Abs(h3 - h1) < 0.01f * h1)
                {
                    // DESIGN-v11 G3: a cube (RCS nozzle) is kept free as a ball: a round cut-out, each tile cut once on the plane
                    // touching the ball towards the tile centre
                    float dist = d.magnitude;
                    if (dist <= h1) return true;
                    if (dist > h1 + 0.8f * s + S.Thickness) continue;
                    Vector3 dir = d / dist;
                    gone |= PlaneHalf(t, new Vector4(-dir.x, -dir.y, -dir.z, Vector3.Dot(dir, c) + h1));
                    continue;
                }
                float best = float.MinValue, out2 = 0;
                Vector4 plane = default;
                for (int k = 1; k <= 3; k++)
                {
                    Vector3 a = S.KeepOut[i + k];
                    float h = a.magnitude;
                    if (h < 1e-6f) continue;
                    Vector3 n = a / h;
                    float along = Vector3.Dot(d, n), e = Math.Abs(along) - h;
                    if (e > 0) out2 += e * e;
                    if (e <= best) continue;
                    best = e;
                    if (along < 0) n = -n;
                    plane = new Vector4(-n.x, -n.y, -n.z, Vector3.Dot(n, c) + h); // kept: outside this face
                }
                if (best == float.MinValue || out2 > Sq(0.8f * s + S.Thickness)) continue; // degenerate or too far to touch the cell
                if (best <= 0) return true;
                gone |= PlaneHalf(t, plane);
            }
            return gone;
        }

        private bool InKeepOut(Vector3 p)
        {
            if (S.KeepOut == null) return false;
            for (int i = 0; i + 3 < S.KeepOut.Length; i += 4)
            {
                bool inside = true;
                for (int k = 1; k <= 3 && inside; k++)
                {
                    Vector3 a = S.KeepOut[i + k];
                    inside = Math.Abs(Vector3.Dot(p - S.KeepOut[i], a)) <= a.sqrMagnitude;
                }
                if (inside) return true;
            }
            return false;
        }

        private static float Sq(float x) => x * x;

        // ---------- older decals (DESIGN-v8 B3) ----------

        // Points of older decals' tiles on this surface (same side: within half a tile along the normal) within r of p.
        private IEnumerable<Vector3> OccupiedNear(Vector3 p, Vector3 n, float r)
        {
            if (occupied.Count == 0) yield break;
            HashCell(p, out int x0, out int y0, out int z0);
            for (int x = x0 - 1; x <= x0 + 1; x++)
            for (int y = y0 - 1; y <= y0 + 1; y++)
            for (int z = z0 - 1; z <= z0 + 1; z++)
            {
                if (!occupied.TryGetValue(HashKey(x, y, z), out List<Vector3> list)) continue;
                foreach (Vector3 q in list)
                    if ((q - p).sqrMagnitude < r * r && Math.Abs(Vector3.Dot(q - p, n)) < 0.5f * s) yield return q;
            }
        }

        // An older decal lies on this spot: no tile here, and the flood stops.
        private bool Taken(Vector3 p, Vector3 n)
        {
            foreach (Vector3 _ in OccupiedNear(p, n, 0.4f * s)) return true; // on an older tile (inside one the straight cut leaves at most a sliver)
            return false;
        }

        // The cell ends a joint before the older tiles around it: one straight cut (wall, like the footprint) across the
        // direction to them, at the nearest of their points. Older tiles are convex, so with all their points beyond the
        // line they lie beyond it; in the notches of their border the cut leaves a gap (ponytail: one line per tile).
        private void OccupiedHalves(Tile t)
        {
            Vector2 dir = Vector2.zero;
            var pts = new List<Vector2>();
            foreach (Vector3 q in OccupiedNear(t.C, t.N, s))
            {
                var q2 = new Vector2(Vector3.Dot(q - t.C, t.U), Vector3.Dot(q - t.C, t.V));
                pts.Add(q2);
                dir += q2.normalized / Math.Max(q2.magnitude, 0.1f * s);
            }
            if (pts.Count == 0 || dir.sqrMagnitude < 1e-12f) return;
            dir.Normalize();
            float h = float.MaxValue;
            foreach (Vector2 q2 in pts) h = Math.Min(h, Vector2.Dot(q2, dir));
            h -= S.Gap;
            t.Halves.Add(new Half { N = dir, HTile = h, HMid = h, Raw = h, Tag = TagFootprint });
        }

        // ---------- nose cap ----------

        // DESIGN-v8 B1, cockpit cover only (clip planes set): the tip of a pointed nose is too curved both ways for rigid tiles
        // (holes, blocks). The hull there (Gaussian curvature within 2 tiles like a sphere of radius < 3 tiles, or normals
        // turning by 45 deg within a tile: the rounded rim of the Mk2 nose) gets a conformal cap at tile height; tiles end at
        // its border like at a cut. Tuned on Mk1/Mk2 cockpits (preview/v8). ponytail: cockpits only; nose cones would
        // profit too, enable when their scenes are rechecked.
        private List<SurfaceIndex.ShellTri> CapShell()
        {
            // Only true doubly-curved sharp nose tips (Gaussian curvature, plus local normal-turn extension right at the tip),
            // never long 1D creases/chines or large flat belly faces.
            if (!HasClip || S.Regions != null && Array.Exists(S.Regions, r => r.Domain != null && r.Domain.Length > 0)) return null;
            List<SurfaceIndex.ShellTri> shell = surface.CurvedShell(2 * s, Mathf.PI / 2.25f, s, Mathf.Cos(45 * Mathf.Deg2Rad), (a, b, c) =>
                ClipDist(a) <= 0 && ClipDist(b) <= 0 && ClipDist(c) <= 0 && Vector3.Dot(Vector3.Cross(b - a, c - a).normalized, decalN) >= cosWrap &&
                Vector3.Cross(b - a, c - a).magnitude <= 4 * s * s);
            if (S.CeramicSideDeg > 0)
            {
                // Smooth untiled ceramic on nose tip + both sides/chines (|dot(faceN, decalN)| <= sin(CeramicSideDeg));
                // belly and upper nose ahead of the windows remain real 3D tiles. Emitted mask-clipped in EmitCeramicShell.
                float sideSin = Mathf.Sin(S.CeramicSideDeg * Mathf.Deg2Rad);
                shellTris = new HashSet<int>();
                foreach (SurfaceIndex.ShellTri st in shell) shellTris.Add(st.Index);
                Vector3 axis = S.TankAxis.sqrMagnitude > 0.5f ? S.TankAxis.normalized : Vector3.zero;
                for (int k = 0; k < surface.TriangleSlots; k++)
                {
                    if (!wrapIn[k] || !surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 fn, out int o) || Obstacle(o)) continue;
                    if (axis.sqrMagnitude > 0.5f && Vector3.Dot(fn, axis) < -0.999f) continue;
                    if (Math.Abs(Vector3.Dot(fn, decalN)) > sideSin) continue;
                    if (surface.Raycast((a + b + c) / 3 + fn * 0.01f, fn, 0.5f, -1.01f, out SurfaceHit inner, false) && !Obstacle(inner.Owner)) continue;
                    shellTris.Add(k);
                }
                return null;
            }
            if (shell.Count == 0) return null;
            shellTris = new HashSet<int>();
            foreach (SurfaceIndex.ShellTri st in shell) shellTris.Add(st.Index);
            return shell;
        }

        private bool OnCap(SurfaceHit h) => shellTris != null && shellTris.Contains(h.Triangle);

        // The cap as one pseudo tile per owner (bins, areas and thermal like a tile; no collider).
        private void AddCap(List<SurfaceIndex.ShellTri> shell)
        {
            if (shell == null) return;
            var byOwner = new Dictionary<int, Tile>();
            foreach (SurfaceIndex.ShellTri st in shell)
            {
                if (!byOwner.TryGetValue(st.Owner, out Tile t))
                    byOwner[st.Owner] = t = new Tile { Shell = new List<SurfaceIndex.ShellTri>(), Owner = st.Owner, Body = st.Owner, State = 1, Virtual = true };
                t.Shell.Add(st);
                Vector3 an = Vector3.Cross(st.B - st.A, st.C - st.A) * 0.5f;
                t.TileN += an;
                t.Area += an.magnitude;
                t.Hit += (st.A + st.B + st.C) * (an.magnitude / 3);
            }
            foreach (Tile t in byOwner.Values)
            {
                t.Hit /= Math.Max(t.Area, 1e-9f);
                t.TileN = t.TileN.sqrMagnitude > 1e-12f ? t.TileN.normalized : decalN;
                t.C = t.Hit; t.N = t.F = t.TileN;
                t.U = Tangent(Math.Abs(t.TileN.y) < 0.9f ? Vector3.up : Vector3.right, t.TileN);
                t.V = Vector3.Cross(t.U, t.TileN) * vSign;
                t.MidArea = t.Area;
                tiles.Add(t);
            }
        }

        // Cap: hull triangles lifted along the welded vertex normals to the tile top, walls down to the hull at its border.
        private void EmitCap(Tile t, Acc acc, List<TileOutline> outlines)
        {
            List<int> tris = acc.Bins[t.Bin];
            float lift = S.SurfaceOffset + S.Thickness * (t.Filler ? 0.9f : 1f), r = 1e-4f;
            foreach (SurfaceIndex.ShellTri st in t.Shell)
                foreach (Vector3 q in new[] { st.A, st.B, st.C }) r = Math.Max(r, (q - t.C).magnitude);
            float k = 0.1f * s / r; // the whole cap shows the middle of one atlas cell: plain, no stretched tile picture
            Vector2 Flat(Vector3 q) => new Vector2(Vector3.Dot(q - t.C, t.U), Vector3.Dot(q - t.C, t.V)) * k;
            var top = new List<Vector3>();
            var hull = new List<Vector3>();
            foreach (SurfaceIndex.ShellTri st in t.Shell)
            {
                Vector3 fn = Vector3.Cross(st.B - st.A, st.C - st.A), a = st.A + st.NA * lift, b = st.B + st.NB * lift, c = st.C + st.NC * lift;
                // 1.7.2: plain filler pieces take the middle of their cell's picture (projected far out they sampled the atlas margins
                // and showed as white dots along the mask edge)
                int i0 = Add(acc, a, st.NA, st.HasUv ? st.UA : t.Filler ? Uv(t, Vector2.zero) : Uv(t, Flat(st.A)));
                Add(acc, b, st.NB, st.HasUv ? st.UB : t.Filler ? Uv(t, Vector2.zero) : Uv(t, Flat(st.B)));
                Add(acc, c, st.NC, st.HasUv ? st.UC : t.Filler ? Uv(t, Vector2.zero) : Uv(t, Flat(st.C)));
                Tri(acc, tris, i0, i0 + 1, i0 + 2, fn);
                top.Add(a); top.Add(b); top.Add(c);
                hull.Add(st.A); hull.Add(st.B); hull.Add(st.C);
                Vector3 mid = (st.A + st.B + st.C) / 3;
                for (int e = 0; e < 3; e++)
                {
                    if (!(e == 0 ? st.OpenAB : e == 1 ? st.OpenBC : st.OpenCA)) continue;
                    Vector3 p = e == 0 ? st.A : e == 1 ? st.B : st.C, q = e == 0 ? st.B : e == 1 ? st.C : st.A;
                    Vector3 np = e == 0 ? st.NA : e == 1 ? st.NB : st.NC, nq = e == 0 ? st.NB : e == 1 ? st.NC : st.NA;
                    Vector3 outward = Vector3.Cross(q - p, fn);
                    if (Vector3.Dot(outward, p - mid) < 0) outward = -outward;
                    outward.Normalize();
                    int w0 = Add(acc, p + np * lift, outward, Uv(t, Flat(p)));
                    Add(acc, q + nq * lift, outward, Uv(t, Flat(q)));
                    Add(acc, p, outward, Uv(t, Flat(p)));
                    Add(acc, q, outward, Uv(t, Flat(q)));
                    Tri(acc, tris, w0, w0 + 1, w0 + 3, outward);
                    Tri(acc, tris, w0, w0 + 3, w0 + 2, outward);
                }
            }
            outlines?.Add(new TileOutline
            {
                A = t.A, B = t.B, Owner = t.Owner, Virtual = true, Center = t.Hit, N = t.TileN, TopPoint = t.Hit + t.TileN * lift,
                Outline = top.ToArray(), Hull = hull.ToArray(), Area = t.Area
            });
        }

        // ---------- fill (1.6) ----------

        // User: "lieber alles zu als alles perfekt gekachelt". After placing, hull left bare where it should be covered gets filler
        // pieces: the hull triangle cut into pieces of half a tile (partly covered ones split down to 1/16 tile); a piece under
        // no tile (the middle of a joint counts as covered: its sheet closes it; lost tiles count as there) that should be
        // covered is lifted along the face normal to 90 % of the tile height (so tiles win where both overlap: it reads as a
        // joint), walls down to the hull along the hull triangle's edges,
        // textured with the cell picture of the lattice of the nearest tile continued (joints only in the texture). Should be
        // covered: a cockpit mask (clip planes/regions): inside it; else a spot within 4 tiles of the cover, inside the footprint
        // by half a tile (measured from the nearest tile like the flood; the ragged rim of a free border stays), within the
        // wrap line. Never on obstacles, keep-outs, the nose cap, enclosed or under-object faces. Closes the triangular notches
        // where hexagons meet a crease (Mk2 chine) and faces the flood did not walk onto (a Square cover stopping at the Mk2
        // side face that Hex climbed). One pseudo tile per hull triangle (bins by its normal).
        // 1.7.2: a mask band narrower than about a tile (the Mk3 chin band) where the tile sits: the mask ends on both sides of it along
        // one of its axes. Such bands are filled plain and dark instead of with slivers cut on both sides.
        private bool NarrowBand(Tile t)
        {
            float r = 0.6f * s;
            foreach (Vector3 a in new[] { t.U, t.V, (t.U + t.V).normalized, (t.U - t.V).normalized })
                if (ClipDist(t.C + a * r) > 0 && ClipDist(t.C - a * r) > 0) return true;
            return false;
        }

        // 1.7.3: a tile near the mask contour (1.5 tiles) on a face turned more than 30 deg from a lattice neighbour that lies deeper in the
        // mask: the row over the Mk2 chine. The tiles end straight at the crease, the strip beyond is filled plain and dark (trim strip).
        private bool OverCrease(Tile t)
        {
            float d = ClipDist(t.C);
            if (d < -1.5f * s) return false;
            foreach (Tile o in Near(t.C, 1.5f * s))
                if (o != t && !o.Dead && o.Shell == null && ClipDist(o.C) < d - 0.2f * s && Vector3.Dot(o.TileN, t.TileN) < 0.866f) return true;
            return false;
        }

        private const int MaxFillPieces = 40000;
        private void Fill()
        {
            // 1.7.1, cockpit masks: a tile reaching more than 5 mm out of the mask goes, the rim fill closes the mask exactly along its
            // contour instead (the jagged edges of 1.7.0; user: "für die schwierigen Formen kannst du es auch einfach schwarz machen")
            if (HasClip && S.Regions != null && S.Regions.Length > 0)
                foreach (Tile t in tiles)
                    if (t.Shell == null && t.State > 0 && t.O != null && (Array.Exists(t.O, o => ClipDist(OnPlane(t, o, 0)) > 0.005f) || NarrowBand(t) || OverCrease(t))) t.State = 0;
            var grid = new Dictionary<(int, int, int), List<Tile>>();
            float gc = s;
            (int, int, int) G(Vector3 q) => (Mathf.FloorToInt(q.x / gc), Mathf.FloorToInt(q.y / gc), Mathf.FloorToInt(q.z / gc));
            foreach (Tile t in tiles)
            {
                if (t.Shell != null || t.O == null || t.O.Length < 3 || t.Dead || !(t.State > 0 || t.Lost)) continue;
                var key = G(t.C);
                if (!grid.TryGetValue(key, out List<Tile> l)) grid[key] = l = new List<Tile>();
                l.Add(t);
            }
            if (grid.Count == 0) return;
            Vector3 lo = Vector3.one * float.MaxValue, hi = -lo;
            foreach (List<Tile> l in grid.Values) foreach (Tile t in l) { lo = Vector3.Min(lo, t.C); hi = Vector3.Max(hi, t.C); }
            lo -= Vector3.one * 4 * s; hi += Vector3.one * 4 * s;
            bool mask = HasClip;
            // tiles on a coarse grid (4 tiles): which spots are within reach of the cover at all, cheaply
            var coarse = new HashSet<(int, int, int)>();
            (int, int, int) Coarse(Vector3 q) => (Mathf.FloorToInt(q.x / (4 * s)), Mathf.FloorToInt(q.y / (4 * s)), Mathf.FloorToInt(q.z / (4 * s)));
            foreach (List<Tile> l in grid.Values) foreach (Tile t in l) coarse.Add(Coarse(t.C));
            float tol = 0.5f * S.Gap + 0.001f, piece = s / 2; // the middle of a joint counts as covered (its sheet closes it)
            Tile Nearest(Vector3 q, Vector3 n, int rings)
            {
                Tile best = null;
                float bd = float.MaxValue;
                var (x0, y0, z0) = G(q);
                for (int r = 0; r <= rings; r++)
                {
                    for (int x = x0 - r; x <= x0 + r; x++)
                        for (int y = y0 - r; y <= y0 + r; y++)
                            for (int z = z0 - r; z <= z0 + r; z++)
                            {
                                if (Math.Max(Math.Abs(x - x0), Math.Max(Math.Abs(y - y0), Math.Abs(z - z0))) != r || !grid.TryGetValue((x, y, z), out List<Tile> l)) continue;
                                foreach (Tile t in l)
                                {
                                    if (Vector3.Dot(t.TileN, n) < (mask ? -0.2f : 0.3f)) continue; // 1.7.1 mask: also over a chine (the strip up to the contour)
                                    float d = (t.C - q).sqrMagnitude;
                                    if (d < bd) { bd = d; best = t; }
                                }
                            }
                    if (best != null && r >= 1 && bd < (r * gc) * (r * gc)) break; // nothing nearer in further rings
                }
                return best;
            }
            bool CoveredAt(Vector3 q, Vector3 n)
            {
                // 1.7.2, mask rim (3 cm in from the contour): only 12 mm inside a tile counts as covered, so the rim fill reaches in under
                // the cut tiles' edges (no hull sliver between tile and fill; the tiles stand higher and hide it)
                float rimTol = mask && ClipDist(q) > -0.05f ? -0.02f : tol;
                var (x0, y0, z0) = G(q);
                for (int x = x0 - 1; x <= x0 + 1; x++)
                    for (int y = y0 - 1; y <= y0 + 1; y++)
                        for (int z = z0 - 1; z <= z0 + 1; z++)
                        {
                            if (!grid.TryGetValue((x, y, z), out List<Tile> l)) continue;
                            foreach (Tile t in l)
                            {
                                // 1.7.3 mask: a tile on the face across a chine (30 deg and more) does not cover the strip beyond it
                                if (Vector3.Dot(t.TileN, n) <= (mask || S.Overlay ? 0.866f : 0.5f) || (t.C - q).sqrMagnitude >= s * s) continue;
                                // under it, the tile above within its drop range (walls close the sag of curved hulls); the top point
                                // straight above q (outlines are drawn along the walker normal, which leans on virtual tiles)
                                float h = Vector3.Dot(q - t.C, t.TileN);
                                if (h > -0.5f * s && h < S.Thickness + 0.02f && Under(t, q + t.TileN * (t.Htop - h), rimTol)) return true;
                            }
                        }
                return false;
            }
            var cellTile = new Tile();
            var fillers = new List<Tile>();
            // 1.7.2, overlay fill (the shield of a capped docking port; object space, +y = the port's axis): the top 10 cm are closed even
            // where the doors meet ("lieber zu als perfekt": the slits between the door tips counted as enclosed)
            float apex = float.MinValue;
            if (S.Overlay)
                for (int k = 0; k < surface.TriangleSlots; k++)
                    if (surface.Triangle(k, out Vector3 ta, out Vector3 tb, out Vector3 tc, out _, out int to) && !Obstacle(to)) apex = Math.Max(apex, Math.Max(ta.y, Math.Max(tb.y, tc.y)));
            int pieces = 0;
            for (int k = 0; k < surface.TriangleSlots; k++)
            {
                if (shellTris != null && shellTris.Contains(k) || !surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int o) || Obstacle(o)) continue;
                // 1.7.3: at the port's shield tip every face is filled, wrap or not (the door edges in the slits where the doors meet)
                bool tip = S.Overlay && Math.Max(a.y, Math.Max(b.y, c.y)) > apex - 0.3f;
                if (!wrapIn[k] && !tip) continue;
                Vector3 fn = Vector3.Cross(b - a, c - a);
                if (fn.sqrMagnitude < 1e-14f) continue;
                fn.Normalize();
                float edge = Math.Max((b - a).magnitude, Math.Max((c - b).magnitude, (a - c).magnitude));
                // quick reject: no tile within the triangle's reach (mask: the whole mask is filled, any distance)
                Vector3 tl = Vector3.Min(a, Vector3.Min(b, c)), th = Vector3.Max(a, Vector3.Max(b, c));
                if (!mask && (th.x < lo.x || th.y < lo.y || th.z < lo.z || tl.x > hi.x || tl.y > hi.y || tl.z > hi.z)) continue;
                if (!mask)
                {
                    bool any = false;
                    var (cx0, cy0, cz0) = Coarse(tl - Vector3.one * 4 * s);
                    var (cx1, cy1, cz1) = Coarse(th + Vector3.one * 4 * s);
                    for (int x = cx0; x <= cx1 && !any; x++) for (int y = cy0; y <= cy1 && !any; y++) for (int z = cz0; z <= cz1 && !any; z++) any = coarse.Contains((x, y, z));
                    if (!any) continue;
                }
                int m = Mathf.Clamp(Mathf.CeilToInt(edge / piece), 1, 48);
                var pts = new Vector3[(m + 1) * (m + 2) / 2];
                int Idx(int i, int j) => i * (2 * m + 3 - i) / 2 + j;
                for (int i = 0; i <= m; i++) for (int j = 0; i + j <= m; j++) pts[Idx(i, j)] = a + (b - a) * ((float)i / m) + (c - a) * ((float)j / m);
                List<SurfaceIndex.ShellTri> list = null;
                Tile filler = null;
                bool Rim(int ia, int ja, int ib, int jb) => ia == 0 && ib == 0 || ja == 0 && jb == 0 || ia + ja == m && ib + jb == m;
                // a piece partly under tiles is split in four (down to 1/16 tile): the notches between hexagons are thinner than a piece
                bool NearAny(Vector3 q)
                {
                    var (x0, y0, z0) = Coarse(q);
                    for (int x = x0 - 1; x <= x0 + 1; x++) for (int y = y0 - 1; y <= y0 + 1; y++) for (int z = z0 - 1; z <= z0 + 1; z++) if (coarse.Contains((x, y, z))) return true;
                    return false;
                }
                // 1.7: only at the rim, within 1.5 tiles of a real tile (user: real tiles, filling only at the edge); faces the flood
                // never reached stay bare rather than get a stretched picture
                bool WantedCheap(Vector3 q, Tile near, float dq) =>
                    (near != null && dq <= 0 && (mask || (near.C - q).magnitude <= 1.5f * s) && (mask ||
                     (S.Unrolled ? InFoot(pat.Position(near.A, near.B) + To2(near, q), q, -0.5f * s) : Inside(q, -0.5f * s)))) && !InKeepOut(q);
                // whether the spot should be covered is asked once per half-tile piece (its middle), its parts inherit the answer; the
                // raycasts (under an object, enclosed) only once a part of it really is bare
                int wanted = -1;   // of the current half-tile piece: -1 raycasts not asked yet, 0 no, 1 yes
                Vector3 wantAt = default;
                void Piece(Vector3 p0, Vector3 p1, Vector3 p2, bool r01, bool r12, bool r20, int depth)
                {
                    if (pieces >= MaxFillPieces) return; // hard ceiling against pathological hulls; the rest stays bare
                    Vector3 q = (p0 + p1 + p2) / 3;
                    bool cq = CoveredAt(q, fn), c0 = CoveredAt(p0, fn), c1 = CoveredAt(p1, fn), c2 = CoveredAt(p2, fn);
                    if (depth == 0)
                    {
                        // the edge middles too: a notch a third of a tile wide fits between corners half a tile apart
                        bool e01 = CoveredAt((p0 + p1) / 2, fn), e12 = CoveredAt((p1 + p2) / 2, fn), e20 = CoveredAt((p2 + p0) / 2, fn);
                        if (cq && c0 && c1 && c2 && e01 && e12 && e20) return;
                        // mask: a piece across the contour counts (it is cut along it below)
                        float dq = mask ? Math.Min(ClipDist(q), Math.Min(ClipDist(p0), Math.Min(ClipDist(p1), ClipDist(p2)))) : ClipDist(q);
                        if (!tip && (!mask && !NearAny(q) || !WantedCheap(q, Nearest(q, fn, mask ? 6 : 4), dq))) return;
                        wanted = -1; wantAt = q;
                        c0 |= e01 || e12 || e20; // partly covered: split
                    }
                    else if (cq && c0 && c1 && c2 || wanted == 0) return;
                    if ((cq || c0 || c1 || c2) && depth < (mask ? 3 : 2))
                    {
                        Vector3 m01 = (p0 + p1) / 2, m12 = (p1 + p2) / 2, m20 = (p2 + p0) / 2;
                        Piece(p0, m01, m20, r01, false, r20, depth + 1);
                        Piece(m01, p1, m12, r01, r12, false, depth + 1);
                        Piece(m20, m12, p2, false, r12, r20, depth + 1);
                        Piece(m01, m12, m20, false, false, false, depth + 1);
                        return;
                    }
                    if (cq) return;
                    // 1.7.3 mask: only a face looking straight into the part counts as inside (the 5-ray test took the Mk2 strip above the
                    // chine, its slanted rays hit the canopy)
                    if (wanted < 0) wanted = !UnderObstacle(wantAt, fn) && (S.Overlay && wantAt.y > apex - 0.3f ||
                        (mask ? !surface.Raycast(wantAt + fn * 0.01f, fn, 0.5f, -1.01f, out _, false) : !Enclosed(wantAt, fn))) ? 1 : 0;
                    if (wanted == 0 || InKeepOut(q)) return;
                    if (mask)
                    {
                        // 1.7.1: cut along the mask contour (distances interpolated along the edges), a plain dark piece with a wall there
                        float e0 = ClipDist(p0), e1 = ClipDist(p1), e2 = ClipDist(p2);
                        if (e0 > 0 && e1 > 0 && e2 > 0) return;
                        if (e0 > 0 || e1 > 0 || e2 > 0)
                        {
                            var cp = new List<(Vector3 p, bool cut)>();
                            Vector3[] pv = { p0, p1, p2 };
                            float[] ev = { e0, e1, e2 };
                            bool[] rim = { r01, r12, r20 };
                            for (int i = 0; i < 3; i++)
                            {
                                int j = (i + 1) % 3;
                                if (ev[i] <= 0) cp.Add((pv[i], !rim[i] ? false : true));
                                if (ev[i] <= 0 != ev[j] <= 0) cp.Add((Vector3.Lerp(pv[i], pv[j], ev[i] / (ev[i] - ev[j])), ev[i] <= 0 ? true : rim[i]));
                            }
                            // cp[k].cut: the edge from cp[k] to cp[k+1] is open (wall): the contour or the hull triangle's own edge
                            if (cp.Count < 3) return;
                            pieces++;
                            if (list == null)
                            {
                                list = new List<SurfaceIndex.ShellTri>();
                                filler = new Tile { Shell = list, Owner = o, Body = o, State = 1, Virtual = true, Filler = true };
                            }
                            for (int t2 = 1; t2 + 1 < cp.Count; t2++)
                            {
                                float ar = 0.5f * Vector3.Cross(cp[t2].p - cp[0].p, cp[t2 + 1].p - cp[0].p).magnitude;
                                filler.Area += ar;
                                filler.Hit += (cp[0].p + cp[t2].p + cp[t2 + 1].p) / 3 * ar;
                                list.Add(new SurfaceIndex.ShellTri { Index = k, Owner = o, A = cp[0].p, B = cp[t2].p, C = cp[t2 + 1].p, NA = fn, NB = fn, NC = fn,
                                    OpenAB = t2 == 1 && cp[0].cut, OpenBC = cp[t2].cut, OpenCA = t2 + 2 == cp.Count && cp[cp.Count - 1].cut });
                            }
                            return;
                        }
                    }
                    else if (!InClip(q, 0)) return;
                    if (S.Obstacle != null && (UnderObstacle(p0, fn) || UnderObstacle(p1, fn) || UnderObstacle(p2, fn))) return;
                    Tile near = Nearest(q, fn, mask ? 6 : 4);
                    pieces++;
                    if (list == null)
                    {
                        list = new List<SurfaceIndex.ShellTri>();
                        filler = new Tile { Shell = list, Owner = o, Body = o, State = 1, Virtual = true, Filler = true, A = near?.A ?? 0, B = near?.B ?? 0 };
                    }
                    float area = 0.5f * Vector3.Cross(p1 - p0, p2 - p0).magnitude;
                    filler.Area += area;
                    filler.Hit += q * area;
                    // walls only along the hull triangle's own edges (inside it they stood in zigzags along the tiles' edges)
                    if (near == null)
                    {
                        list.Add(new SurfaceIndex.ShellTri { Index = k, Owner = o, A = p0, B = p1, C = p2, NA = fn, NB = fn, NC = fn, OpenAB = r01, OpenBC = r12, OpenCA = r20 });
                        return;
                    }
                    // the nearest tile's lattice continued over the piece, cut along its cell borders: each part shows its cell's picture
                    Vector2 basePos = pat.Position(near.A, near.B);
                    Vector2 q0 = basePos + To2(near, p0), q1 = basePos + To2(near, p1), q2 = basePos + To2(near, p2);
                    float d2 = (q1.x - q0.x) * (q2.y - q0.y) - (q1.y - q0.y) * (q2.x - q0.x);
                    // seen edge-on from the nearest tile's plane (projection squashed below a tenth of its area): one plain piece
                    // 1.7.3 mask: on a face turned away from the nearest tile (the strip over a chine) too, plain and dark
                    if (Math.Abs(d2) < 0.2f * Vector3.Cross(p1 - p0, p2 - p0).magnitude || mask && Vector3.Dot(near.TileN, fn) < 0.866f)
                    {
                        list.Add(new SurfaceIndex.ShellTri { Index = k, Owner = o, A = p0, B = p1, C = p2, NA = fn, NB = fn, NC = fn, OpenAB = r01, OpenBC = r12, OpenCA = r20 });
                        return;
                    }
                    if (depth >= 2) // slivers along a tile edge: one cell picture each, not worth cutting
                    {
                        Vector2 qm = (q0 + q1 + q2) / 3;
                        float dt = pat.E1.x * pat.E2.y - pat.E1.y * pat.E2.x;
                        int la = Mathf.RoundToInt((qm.x * pat.E2.y - qm.y * pat.E2.x) / dt), lb = Mathf.RoundToInt((pat.E1.x * qm.y - pat.E1.y * qm.x) / dt);
                        cellTile.A = la; cellTile.B = lb;
                        Vector2 cc = pat.Position(la, lb);
                        list.Add(new SurfaceIndex.ShellTri { Index = k, Owner = o, A = p0, B = p1, C = p2, NA = fn, NB = fn, NC = fn, HasUv = true,
                            UA = Uv(cellTile, q0 - cc), UB = Uv(cellTile, q1 - cc), UC = Uv(cellTile, q2 - cc), OpenAB = r01, OpenBC = r12, OpenCA = r20 });
                        return;
                    }
                    Vector3 At(Vector2 x)
                    {
                        float wb = Mathf.Clamp01(((x.x - q0.x) * (q2.y - q0.y) - (x.y - q0.y) * (q2.x - q0.x)) / d2);
                        float wc = Mathf.Clamp(((q1.x - q0.x) * (x.y - q0.y) - (q1.y - q0.y) * (x.x - q0.x)) / d2, 0, 1 - wb);
                        return p0 * (1 - wb - wc) + p1 * wb + p2 * wc;
                    }
                    bool OnSeg(Vector2 x, Vector2 a2, Vector2 b2)
                    {
                        Vector2 e = b2 - a2;
                        float len2 = e.sqrMagnitude;
                        if (len2 < 1e-14f) return false;
                        float cr = e.x * (x.y - a2.y) - e.y * (x.x - a2.x);
                        return cr * cr <= 1e-10f * len2;
                    }
                    bool RimEdge(Vector2 x, Vector2 y) => r01 && OnSeg(x, q0, q1) && OnSeg(y, q0, q1) || r12 && OnSeg(x, q1, q2) && OnSeg(y, q1, q2) || r20 && OnSeg(x, q2, q0) && OnSeg(y, q2, q0);
                    ForCells(q0, q1, q2, (la, lb, poly) =>
                    {
                        cellTile.A = la; cellTile.B = lb;
                        Vector2 cc = pat.Position(la, lb);
                        for (int t = 1; t + 1 < poly.Count; t++)
                        {
                            Vector2 x0 = poly[0], x1 = poly[t], x2 = poly[t + 1];
                            list.Add(new SurfaceIndex.ShellTri
                            {
                                Index = k, Owner = o, A = At(x0), B = At(x1), C = At(x2), NA = fn, NB = fn, NC = fn, HasUv = true,
                                UA = Uv(cellTile, x0 - cc), UB = Uv(cellTile, x1 - cc), UC = Uv(cellTile, x2 - cc),
                                OpenAB = RimEdge(x0, x1), OpenBC = RimEdge(x1, x2), OpenCA = RimEdge(x2, x0)
                            });
                        }
                    });
                }
                for (int i = 0; i < m; i++)
                    for (int j = 0; i + j < m; j++)
                    {
                        Piece(pts[Idx(i, j)], pts[Idx(i + 1, j)], pts[Idx(i, j + 1)], Rim(i, j, i + 1, j), Rim(i + 1, j, i, j + 1), Rim(i, j + 1, i, j), 0);
                        if (i + j < m - 1)
                            Piece(pts[Idx(i + 1, j)], pts[Idx(i + 1, j + 1)], pts[Idx(i, j + 1)], Rim(i + 1, j, i + 1, j + 1), Rim(i + 1, j + 1, i, j + 1), Rim(i, j + 1, i + 1, j), 0);
                    }
                if (filler == null || list.Count == 0 || filler.Area <= 0) continue;
                if (tip) // 1.7.3: at the shield tip also the reverse side, so the slits where the doors meet are closed from either side
                    foreach (SurfaceIndex.ShellTri st in list.ToArray())
                        list.Add(new SurfaceIndex.ShellTri { Index = st.Index, Owner = st.Owner, A = st.A, B = st.C, C = st.B, NA = -st.NA, NB = -st.NC, NC = -st.NB,
                            HasUv = st.HasUv, UA = st.UA, UB = st.UC, UC = st.UB });
                filler.Hit /= filler.Area;
                filler.C = filler.Hit;
                filler.TileN = filler.N = filler.F = fn;
                filler.U = Tangent(Math.Abs(fn.y) < 0.9f ? Vector3.up : Vector3.right, fn);
                filler.V = Vector3.Cross(filler.U, fn) * vSign;
                filler.MidArea = filler.Area;
                fillers.Add(filler);
            }
            tiles.AddRange(fillers);
        }

        // The triangle (lattice plane, pattern coordinates) cut by the cells of the lattice: piece(la, lb, polygon) for each
        // non-empty part (polygon reused between calls).
        private readonly List<Vector2> cellPoly = new List<Vector2>(), cellNext = new List<Vector2>();
        private void ForCells(Vector2 pa, Vector2 pb, Vector2 pc, Action<int, int, List<Vector2>> piece)
        {
            float det = pat.E1.x * pat.E2.y - pat.E1.y * pat.E2.x;
            Vector2 mn = Vector2.Min(pa, Vector2.Min(pb, pc)), mx = Vector2.Max(pa, Vector2.Max(pb, pc));
            int la0 = int.MaxValue, la1 = int.MinValue, lb0 = int.MaxValue, lb1 = int.MinValue;
            foreach (Vector2 q in new[] { mn, mx, new Vector2(mn.x, mx.y), new Vector2(mx.x, mn.y) })
            {
                float fa = (q.x * pat.E2.y - q.y * pat.E2.x) / det, fb = (pat.E1.x * q.y - pat.E1.y * q.x) / det;
                la0 = Math.Min(la0, Mathf.FloorToInt(fa) - 1); la1 = Math.Max(la1, Mathf.CeilToInt(fa) + 1);
                lb0 = Math.Min(lb0, Mathf.FloorToInt(fb) - 1); lb1 = Math.Max(lb1, Mathf.CeilToInt(fb) + 1);
            }
            if (!(mx.x - mn.x < 8 * s && mx.y - mn.y < 8 * s)) return; // a piece is a quarter tile at most (also catches NaN)
            for (int la = la0; la <= la1; la++)
                for (int lb = lb0; lb <= lb1; lb++)
                {
                    Vector2 cc = pat.Position(la, lb);
                    if (cc.x < mn.x - s || cc.y < mn.y - s || cc.x > mx.x + s || cc.y > mx.y + s) continue;
                    List<Vector2> poly = cellPoly, next = cellNext;
                    poly.Clear(); poly.Add(pa); poly.Add(pb); poly.Add(pc);
                    foreach (TilePattern.Edge e in pat.Edges)
                    {
                        next.Clear();
                        for (int k = 0; k < poly.Count; k++)
                        {
                            Vector2 p = poly[k], q = poly[(k + 1) % poly.Count];
                            float dp = Vector2.Dot(p - cc, e.Normal) - e.H, dq = Vector2.Dot(q - cc, e.Normal) - e.H;
                            if (dp <= 0) next.Add(p);
                            if ((dp < 0) != (dq < 0) && dp != dq) next.Add(Vector2.Lerp(p, q, dp / (dp - dq)));
                        }
                        var sw = poly; poly = next; next = sw;
                        if (poly.Count < 3) break;
                    }
                    if (poly.Count >= 3) piece(la, lb, poly);
                }
        }

        // p under tile t: inside its outline grown by tol (in t's frame)
        private static bool Under(Tile t, Vector3 p, float tol)
        {
            Vector2 q = To2(t, p);
            float sign = Shoelace(t.O) >= 0 ? 1 : -1;
            for (int k = 0; k < t.O.Length; k++)
            {
                Vector2 a = t.O[k], b = t.O[(k + 1) % t.O.Length], e = b - a;
                float len = e.magnitude;
                if (len < 1e-6f) continue;
                if ((e.x * (q.y - a.y) - e.y * (q.x - a.x)) * sign < -tol * len) return false;
            }
            return true;
        }

        // ---------- placing ----------

        // Drops a point of the tile plane onto the surface; false if it misses or lands on a spot too steep for this tile.
        private bool Drop(Tile t, Vector2 p, out SurfaceHit h, int gap = 0)
        {
            Vector3 x = t.C + t.U * p.x + t.V * p.y;
            if (!surface.Raycast(x + t.N * dropHeight, -t.N, 2 * dropHeight, 0.17f, out h, false) || OnCap(h)) return false;
            // never onto another body when one of them moves on its own (control surface flap): cut at its border
            if (Split(h.Owner, t.Body)) { t.SplitOther = h.Owner; return false; }
            // border samples of a tile that hovered there (second placement, see Place): cut where the hull falls away
            // (inner edges only where the hull also turns away by more than CreaseDeg: the rest of the cover runs on there)
            if (gap > 0 && t.GapCut && Vector3.Dot(h.P - t.C, t.GapN) < t.GapMin && (gap == 1 || Vector3.Dot(h.FaceN, t.GapN) < cosCrease)) return false;
            // a rigid tile cannot hover far above a hull that falls away (past a chamfer, down a steep face): every tile
            // ends where the hull leaves its plane by DepthLimit, so cut edges of neighbours line up whatever their frames
            if (Math.Abs(Vector3.Dot(h.P - t.C, t.N)) > depthLimit) return false;
            if (!InWrap(h) || UnderObstacle(h.P, t.N)) return false;
            // Cockpit masks: enforce the exact mask contour on every tile drop sample so PlaceOnce's bisection trims border
            // tiles smoothly along curved/concave contours without overhanging spikes or deleting whole border rows.
            if (HasClip && !InClip(h.P, 0.001f)) return false;
            if (t.Strip) return Vector3.Dot(h.FaceN, t.F) >= cosPlanar;
            // a flat face narrower than a tile beyond a crease (chamfer of a wing edge) gets strip tiles of its own:
            // the tile ends exactly on the crease line instead of hovering over the chamfer
            if (Vector3.Dot(h.FaceN, t.F) < cosCrease && BroadFace(t.W.P, t.F) && StripFace(h, t.F)) return false;
            bool faceOk = Vector3.Dot(h.FaceN, t.F) >= cosFace;
            if (faceOk) return true; // clear case, skip smoothing
            h.N = surface.Smooth(h);
            return Vector3.Dot(h.N, t.N) >= cosCorner;
        }

        // DESIGN-v11: a hull point inside an object standing on it (intake, fin): the ray along n meets an obstacle within 1.5 m and so do
        // four rays tilted by 60 deg (beside the object one of them gets out). The drop rays pass through such objects (their walls
        // face sideways), so without this the cover ran on under them. Not under retracted gear (PassUnder).
        private bool UnderObstacle(Vector3 p, Vector3 n)
        {
            if (S.Obstacle == null || S.Overlay) return false;
            Vector3 q = p + n * 0.002f;
            if (!surface.Raycast(q, n, 1.5f, -1.01f, out SurfaceHit h, false) || !Obstacle(h.Owner) ||
                S.PassUnder != null && h.Owner < S.PassUnder.Length && S.PassUnder[h.Owner]) return false;
            Vector3 a = Tangent(decalU, n);
            if (a == Vector3.zero) a = Tangent(decalV, n);
            Vector3 b = Vector3.Cross(n, a);
            foreach (Vector3 d in new[] { n * 0.5f + a * 0.866f, n * 0.5f - a * 0.866f, n * 0.5f + b * 0.866f, n * 0.5f - b * 0.866f })
                if (!surface.Raycast(q, d, 1.5f, -1.01f, out SurfaceHit hd, false) || !Obstacle(hd.Owner)) return false;
            return true;
        }

        private static bool OnCrease(Tile t, int tag) => tag >= 0 && t.Crease != null && tag < t.Crease.Length && t.Crease[tag];

        // Border edges: no neighbour continues the cover there (cell border, footprint, end of the surface), or only a
        // virtual tile hanging over the end of the surface (it is cut back itself, so cutting here opens no hole).
        // Gap cut per edge (second placement): 1 border edge (no neighbour: cell border, footprint, cut), else 0. Cutting
        // edges towards neighbours (also virtual ones over a rim) opened holes next to neighbours that stayed whole
        // (Dynawing wings and cockpit, measured), so the cover over a rim much tighter than a tile keeps some sag there.
        private int GapMode(Tile t, int tag) => tag == TagBorder || tag == TagFootprint || tag == TagCut ? 1 : 0;

        private bool BorderEdge(Tile t, int tag)
        {
            if (tag == TagBorder || tag == TagFootprint || tag == TagCut) return true;
            if (tag < 0 || t.EdgeOwners == null || tag >= t.EdgeOwners.Length || t.EdgeOwners[tag] == null) return false;
            foreach (Tile o in t.EdgeOwners[tag])
                if (!o.Virtual || o.Floods) return false;
            return true;
        }

        // Places the tile; a tile that hovers more than gapLimit over a hull that falls away (rim of a cockpit, chine, edge
        // of a fuselage) is placed again with those samples cut, at the line where the gap reaches the limit (DESIGN-v7
        // §4). Edges towards neighbours are cut only where the hull turns away by more than CreaseDeg (the neighbour is
        // cut on the same line); plain curvature under inner edges stays, so no holes open inside the cover.
        private void Place(Tile t)
        {
            t.GapCut = false;
            Vector2[] o0 = t.O;
            int[] tags0 = t.Tags;
            t.O0 = o0; t.Tags0 = tags0;
            PlaceOnce(t);
            // the refit plane after a cut can tilt a little: up to four rounds, each against the latest plane and stricter
            for (int round = 0; round < 4 && t.State > 0 && !t.Strip; round++)
            {
                float bottom = t.Htop - S.Thickness - S.SurfaceOffset;
                if (t.Worst <= gapLimit) return;
                t.GapCut = true;
                if (round == 0 && t.HF != Vector3.zero && Vector3.Dot(t.HF, t.TileN) >= cosCrease)
                {
                    // first against the face under the tile centre: a plane fitted over the falling hull is tilted
                    // towards it, and cutting against that converges badly
                    t.GapN = t.HF;
                    t.GapMin = Vector3.Dot(t.HP - t.C, t.HF) - gapLimit;
                }
                else
                {
                    t.GapN = t.TileN;
                    t.GapMin = bottom - gapLimit * (1 - 0.2f * round);
                }
                t.O = o0;
                t.Tags = tags0;
                t.State = 1;
                PlaceOnce(t);
            }
        }

        private void PlaceOnce(Tile t)
        {
            int m = t.O.Length;
            var s2 = new Vector2[3 * m];
            var s3 = new Vector3[3 * m];
            var sf = new Vector3[3 * m];
            var ok = new bool[3 * m];
            bool all = true;
            for (int k = 0; k < 3 * m; k++)
            {
                s2[k] = Vector2.Lerp(t.O[k / 3], t.O[(k / 3 + 1) % m], (k % 3) / 3f);
                ok[k] = Drop(t, s2[k], out SurfaceHit h, GapMode(t, t.Tags[k / 3]));
                s3[k] = h.P;
                sf[k] = h.FaceN;
                all &= ok[k];
            }
            Vector3[] faces;

            float outlineArea = Math.Abs(Shoelace(t.O));
            Vector3[] ring;
            if (all)
            {
                t.Seg2 = new Vector2[m][];
                t.SegHull = new Vector3[m][];
                for (int k = 0; k < m; k++)
                {
                    t.Seg2[k] = new[] { s2[3 * k], s2[3 * k + 1], s2[3 * k + 2] };
                    t.SegHull[k] = new[] { s3[3 * k], s3[3 * k + 1], s3[3 * k + 2] };
                }
                ring = s3;
                faces = sf;
                t.Area = t.MidArea;
            }
            else
            {
                // keep the good samples; where the outline leaves good surface, insert the crossing found along it
                var p2 = new List<Vector2>(24);
                var p3 = new List<Vector3>(24);
                var tg = new List<int>(24);
                var pf = new List<Vector3>(24);
                int n = 3 * m;
                for (int k = 0; k < n; k++)
                {
                    int k1 = (k + 1) % n, tag = t.Tags[k / 3];
                    if (ok[k]) { p2.Add(s2[k]); p3.Add(s3[k]); tg.Add(tag); pf.Add(sf[k]); }
                    if (ok[k] == ok[k1]) continue;
                    Vector2 good = ok[k] ? s2[k] : s2[k1], bad = ok[k] ? s2[k1] : s2[k];
                    Vector3 hitP = ok[k] ? s3[k] : s3[k1], hitF = ok[k] ? sf[k] : sf[k1];
                    float lo = 0, hi = 1;
                    int border = GapMode(t, t.Tags[k / 3]);
                    for (int i = 0; i < 10; i++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        if (Drop(t, Vector2.Lerp(good, bad, mid), out SurfaceHit h, border)) { lo = mid; hitP = h.P; hitF = h.FaceN; }
                        else hi = mid;
                    }
                    p2.Add(Vector2.Lerp(good, bad, lo));
                    p3.Add(hitP);
                    pf.Add(hitF);
                    tg.Add(ok[k] ? TagCut : tag); // leaving good surface starts the cut
                }
                Simplify(p2, p3, tg, pf);
                if (p2.Count >= 3 && Math.Abs(Shoelace(p2.ToArray())) < MinPiece * pat.CellArea) p2.Clear();
                // good surface only inside the outline (tip of a narrowing face between two chamfers): trace it by rays
                if (p2.Count < 3)
                {
                    if (!Radial(t, p2, p3, tg)) { t.State = 0; return; }
                    pf.Clear();
                    for (int k = 0; k < p3.Count; k++) pf.Add(t.N); // faces unknown: no inner gap check
                }
                faces = pf.ToArray();
                t.O = p2.ToArray();
                t.Tags = tg.ToArray();
                float area = Math.Abs(Shoelace(t.O));
                if (area < MinPiece * pat.CellArea) { t.State = 0; return; }
                t.Seg2 = new Vector2[t.O.Length][];
                t.SegHull = new Vector3[t.O.Length][];
                for (int k = 0; k < t.O.Length; k++)
                {
                    t.Seg2[k] = new[] { p2[k] };
                    t.SegHull[k] = new[] { p3[k] };
                }
                ring = p3.ToArray();
                t.Area = outlineArea > 0 ? area * t.MidArea / outlineArea : area;
                t.State = 2;
            }

            Vector2 centroid = Centroid(t.O);
            if (Drop(t, centroid, out SurfaceHit hc)) { t.Hit = hc.P; t.Owner = hc.Owner; }
            else
            {
                Vector3 avg = Vector3.zero;
                foreach (Vector3 p in ring) avg += p;
                t.Hit = avg / ring.Length;
                if (surface.Closest(t.Hit, s, t.N, 0f, out SurfaceHit ha)) t.Owner = Obstacle(ha.Owner) && t.Body >= 0 ? t.Body : ha.Owner;
            }

            // Tile orientation from where its outline landed (Newell plane fit): exact next to creases, averaged over
            // ribs. The smoothed walker normal only steers the drop direction. Points of a mitred crease edge lie on the
            // far face and would tilt the tile off its own face: left out.
            var fitRing = new List<Vector3>(ring.Length);
            int segs = t.O.Length;
            for (int k = 0; k < ring.Length; k++)
            {
                int seg = all ? k / 3 : k, prev = (seg + segs - 1) % segs;
                bool corner = !all || k % 3 == 0;
                if (OnCrease(t, t.Tags[seg]) || corner && OnCrease(t, t.Tags[prev])) continue;
                fitRing.Add(ring[k]);
            }
            if (fitRing.Count < 3) fitRing = new List<Vector3>(ring);
            Vector3 fit = Vector3.zero;
            for (int k = 0; k < fitRing.Count; k++)
            {
                Vector3 a = fitRing[k], b = fitRing[(k + 1) % fitRing.Count];
                fit += new Vector3((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y));
            }
            Vector3 tileN = fit.sqrMagnitude > 1e-12f ? fit.normalized : t.N;
            if (Vector3.Dot(tileN, t.N) < 0) tileN = -tileN;
            if (Vector3.Dot(tileN, t.N) < 0.5f) tileN = t.N; // degenerate outline
            t.TileN = tileN;
            // rigid flat tile: top plane above the highest hull point under it (hull facets must never poke through)
            float lift = float.MinValue;
            foreach (Vector3 p in ring) lift = Math.Max(lift, Vector3.Dot(p - t.C, tileN));
            t.Htop = lift + S.SurfaceOffset + S.Thickness;

            // largest gap the second placement would cut (see Place, Drop): border edges, and inner edges where the hull
            // turns away by more than CreaseDeg
            t.Worst = 0;
            float bottom = t.Htop - S.Thickness - S.SurfaceOffset;
            for (int k = 0; k < ring.Length; k++)
            {
                int seg = all ? k / 3 : k, prev = (seg + segs - 1) % segs;
                bool corner = !all || k % 3 == 0;
                int mode = GapMode(t, t.Tags[seg]);
                if (corner)
                {
                    // a corner belongs to both edges: the stricter rule wins, a mitred crease edge excludes it
                    int pm = GapMode(t, t.Tags[prev]);
                    if (OnCrease(t, t.Tags[prev]) || OnCrease(t, t.Tags[seg])) mode = 0;
                    else if (pm != 0) mode = mode == 0 ? pm : Math.Min(mode, pm);
                }
                if (mode == 0 || mode == 2 && Vector3.Dot(faces[k], tileN) >= cosCrease) continue;
                t.Worst = Math.Max(t.Worst, bottom - Vector3.Dot(ring[k] - t.C, tileN));
            }
        }

        // Cleans a partial outline for the bevel: a crossing right at a sample duplicates it (segments under 3 mm are
        // dropped, keeping the cut tag), and samples in the middle of a straight edge go (they gain nothing and make
        // the mitre fragile). Segment k runs from point k to k + 1 with tag tg[k].
        private static void Simplify(List<Vector2> p2, List<Vector3> p3, List<int> tg, List<Vector3> pf)
        {
            for (bool changed = true; changed && p2.Count > 3;)
            {
                changed = false;
                for (int k = p2.Count - 1; k >= 0 && p2.Count > 3; k--)
                {
                    int n = p2.Count, k1 = (k + 1) % n, k0 = (k + n - 1) % n;
                    Vector2 a = p2[k0], b = p2[k], c = p2[k1];
                    bool shortSeg = (c - b).sqrMagnitude < 0.003f * 0.003f;
                    bool straight = tg[k0] == tg[k] && (b - a).sqrMagnitude > 1e-10f && (c - b).sqrMagnitude > 1e-10f &&
                                    Vector2.Dot((b - a).normalized, (c - b).normalized) > 0.99995f;
                    if (shortSeg)
                    {
                        // drop point k1's predecessor segment: point k goes, segment k0 takes over (a cut wins)
                        if (tg[k] == TagCut) tg[k0] = TagCut;
                        p2.RemoveAt(k); p3.RemoveAt(k); tg.RemoveAt(k); pf.RemoveAt(k);
                        changed = true;
                    }
                    else if (straight)
                    {
                        p2.RemoveAt(k); p3.RemoveAt(k); tg.RemoveAt(k); pf.RemoveAt(k);
                        changed = true;
                    }
                }
            }
        }

        // Outline of the good surface inside t.O seen from a good point near its centre: 24 rays, each ending at the
        // outline (keeping its tag) or where the surface stops being good (cut). False if no good point is found.
        private bool Radial(Tile t, List<Vector2> p2, List<Vector3> p3, List<int> tg)
        {
            p2.Clear(); p3.Clear(); tg.Clear();
            Vector2 c = Centroid(t.O), lo = t.O[0], hi = t.O[0];
            foreach (Vector2 q in t.O) { lo = Vector2.Min(lo, q); hi = Vector2.Max(hi, q); }
            Vector2 seed = c;
            bool found = Drop(t, c, out _);
            for (int i = 0; i < 7 && !found; i++)
                for (int j = 0; j < 7 && !found; j++)
                {
                    var q = new Vector2(Mathf.Lerp(lo.x, hi.x, (i + 0.5f) / 7), Mathf.Lerp(lo.y, hi.y, (j + 0.5f) / 7));
                    if (InOutline(t.O, q) && Drop(t, q, out _)) { seed = q; found = true; }
                }
            if (!found) return false;
            const int rays = 24;
            var hitTag = new int[rays];
            for (int r = 0; r < rays; r++)
            {
                float ang = 2 * Mathf.PI * r / rays;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float exit = RayExit(t.O, seed, dir, out int seg);
                Vector2 end = seed + dir * exit;
                int border = GapMode(t, t.Tags[seg]);
                if (Drop(t, end, out SurfaceHit he, border)) { p2.Add(end); p3.Add(he.P); hitTag[r] = t.Tags[seg]; continue; }
                float a = 0, b = 1;
                Drop(t, seed, out SurfaceHit hs);
                Vector3 hp = hs.P;
                for (int i = 0; i < 10; i++)
                {
                    float mid = (a + b) / 2;
                    if (Drop(t, Vector2.Lerp(seed, end, mid), out SurfaceHit hm, border)) { a = mid; hp = hm.P; } else b = mid;
                }
                p2.Add(Vector2.Lerp(seed, end, a)); p3.Add(hp); hitTag[r] = TagCut;
            }
            // segment r -> r+1 keeps an outline tag only if both ends sit on that outline edge
            for (int r = 0; r < rays; r++) tg.Add(hitTag[r] == hitTag[(r + 1) % rays] ? hitTag[r] : TagCut);
            return true;
        }

        private static bool InOutline(Vector2[] o, Vector2 q)
        {
            float sign = Shoelace(o) >= 0 ? 1 : -1;
            for (int k = 0; k < o.Length; k++)
            {
                Vector2 a = o[k], b = o[(k + 1) % o.Length];
                if (((b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x)) * sign < 0) return false;
            }
            return true;
        }

        // Distance from p (inside the convex outline) along dir to the outline, and the segment it leaves through.
        private static float RayExit(Vector2[] o, Vector2 p, Vector2 dir, out int seg)
        {
            float best = float.MaxValue;
            seg = 0;
            for (int k = 0; k < o.Length; k++)
            {
                Vector2 a = o[k], e = o[(k + 1) % o.Length] - a;
                float den = dir.x * e.y - dir.y * e.x;
                if (Math.Abs(den) < 1e-12f) continue;
                Vector2 w = a - p;
                float tt = (w.x * e.y - w.y * e.x) / den, u = (w.x * dir.y - w.y * dir.x) / den;
                if (tt > 1e-6f && u >= -1e-4f && u <= 1 + 1e-4f && tt < best) { best = tt; seg = k; }
            }
            return best == float.MaxValue ? 0 : best;
        }

        private static float Shoelace(Vector2[] p)
        {
            float a = 0;
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 q = p[(i + 1) % p.Length];
                a += p[i].x * q.y - q.x * p[i].y;
            }
            return a * 0.5f;
        }

        // Bevel triangle a b c (inner edge, then outer): its 2D winding is opposite to the outline (oSign), and the
        // (U, V) frame maps to -N * vSign, so its 3D face must point along TileN * oSign * vSign.
        private bool BevelUp(Tile t, Vector3 a, Vector3 b, Vector3 c, float oSign) =>
            Vector3.Dot(Vector3.Cross(b - a, c - a), t.TileN) * oSign * vSign > 0;

        private static Vector2 Centroid(Vector2[] p)
        {
            Vector2 c = Vector2.zero;
            foreach (Vector2 q in p) c += q;
            return c / p.Length;
        }

        // Lattice sites whose tile came out empty (a virtual tile beyond a crease with only a sliver on good surface):
        // the neighbours grow towards them (EmptyGrow) instead of stopping at the shared midline, which left a gap there.
        private void Regrow()
        {
            empty = new HashSet<Tile>();
            foreach (Tile t in tiles)
                if (t.State == 0 && !t.Lost) empty.Add(t);
            if (empty.Count == 0) return;
            foreach (Tile t in tiles)
            {
                if (!t.Drawn || t.Strip) continue;
                bool next = false;
                foreach (List<Tile> owners in t.EdgeOwners)
                    if (owners != null && owners.TrueForAll(empty.Contains)) next = true;
                if (!next) continue;
                t.State = 1;
                Cell(t);
                if (t.State > 0) Place(t);
            }
        }

        // DESIGN-v9 G1/G2: a cell over the border of two separate bodies (wing and flap, two parts in "whole part" mode) is
        // cut there; the part on the other body becomes a second piece of the same lattice site, placed on that body. Both
        // pieces end on the border line with a wall, the pattern runs on over it (same site, same atlas cell).
        private void Twins()
        {
            foreach (Tile t in tiles.ToArray())
            {
                if (!t.Drawn || t.SplitOther < 0 || t.Strip || t.Shell != null || t.O0 == null || !Separate(t.SplitOther) && !Separate(t.Body) || Obstacle(t.SplitOther)) continue; // flaps and part seams, not own-flood borders nor obstacles
                var w = new Tile
                {
                    A = t.A, B = t.B, Virtual = t.Virtual, W = t.W, C = t.C, N = t.N, U = t.U, V = t.V, F = t.F, Edge = t.Edge,
                    Halves = t.Halves, EdgeOwners = t.EdgeOwners, Crease = t.Crease, MidArea = t.MidArea, O = t.O0, Tags = t.Tags0,
                    Body = t.SplitOther, Region = t.Region, HP = t.HP, HF = t.HF, Lost = t.Lost, State = 1
                };
                Place(w);
                if (w.State == 0) continue;
                tiles.Add(w);
                AddToHash(w);
            }
        }

        // ---------- strips ----------

        // A tile the walker carried onto a strip face (it slipped over the crease before the bend showed).
        private bool OnStripFace(Tile t)
        {
            if (t.State == 0) return false;
            bool crease = false; // cheap test first: a neighbour stands on a face at a crease to this one
            for (int k = 0; k < pat.StepA.Length && !crease; k++)
                crease = tileAt.TryGetValue(SiteKey(t.A + pat.StepA[k], t.B + pat.StepB[k], t.Region), out Tile j) && Vector3.Dot(j.F, t.W.F) < cosCrease;
            if (!crease || !surface.Raycast(t.W.P + t.W.N * 0.02f, -t.W.N, 0.04f, 0.17f, out SurfaceHit h, false) || BroadFace(h.P, h.FaceN)) return false;
            for (int k = 0; k < pat.StepA.Length; k++)
            {
                if (!tileAt.TryGetValue(SiteKey(t.A + pat.StepA[k], t.B + pat.StepB[k], t.Region), out Tile j)) continue;
                if (Vector3.Dot(j.F, h.FaceN) < cosCrease && BroadFace(j.W.P, j.F) && StripFace(h, j.F)) return true;
            }
            return false;
        }

        // Is the flat face under h, beyond a crease to a face with normal other, a strip face: flat (PlanarDeg), at least
        // MinStrip but less than 0.9 tile across, ending in creases (> 15Â°) on both sides, and at least two tiles long along the crease (wing and fuselage edges;
        // the short facets of a ring around a cap would get a jagged collar of strips meeting at angles)? Cached.
        private bool StripFace(SurfaceHit h, Vector3 other)
        {
            long key = FaceKey(h);
            if (stripFace.TryGetValue(key, out bool known)) return known;
            bool ok = false;
            Vector3 along = Vector3.Cross(h.FaceN, other);
            if (along.sqrMagnitude > 1e-6f && InWrap(h))
            {
                along.Normalize();
                Vector3 across = Vector3.Cross(along, h.FaceN);
                float e1 = Extent(h.P, h.FaceN, across, s), e2 = Extent(h.P, h.FaceN, -across, s), w = e1 + e2;
                ok = w >= MinStrip && w < 0.9f * s && Extent(h.P, h.FaceN, along, s) + Extent(h.P, h.FaceN, -along, s) >= 1.9f * s &&
                     Bounded(h.P + across * (e1 + 0.005f), h.FaceN) && Bounded(h.P - across * (e2 + 0.005f), h.FaceN);
            }
            return stripFace[key] = ok;
        }

        // Strips only next to a broad flat face (the top of a wing): at least 0.9 tile both ways (PlanarDeg). On ribs or the
        // facets of a curved hull every facet is narrow, and those must not be cut into strips. Cached.
        private bool BroadFace(Vector3 p, Vector3 f)
        {
            if (!surface.Raycast(p + f * 0.02f, -f, 0.04f, 0.17f, out SurfaceHit h, false)) return false;
            long key = FaceKey(h);
            if (broadFace.TryGetValue(key, out bool known)) return known;
            Vector3 a = Tangent(Mathf.Abs(f.y) < 0.9f ? Vector3.up : Vector3.right, h.FaceN), b = Vector3.Cross(h.FaceN, a);
            float r = 0.9f * s;
            bool ok = Extent(h.P, h.FaceN, a, r) + Extent(h.P, h.FaceN, -a, r) >= 0.9f * s - 0.001f &&
                      Extent(h.P, h.FaceN, b, r) + Extent(h.P, h.FaceN, -b, r) >= 0.9f * s - 0.001f;
            return broadFace[key] = ok;
        }

        // Cache key for face measurements: triangle and a 5 cm cell (a big triangle can be wide here and narrow there).
        private static long FaceKey(SurfaceHit h) =>
            HashKey(Mathf.FloorToInt(h.P.x / 0.05f), Mathf.FloorToInt(h.P.y / 0.05f), Mathf.FloorToInt(h.P.z / 0.05f)) * 31 + h.Triangle;

        // Does the face (normal f) end at q in a real crease (or the end of the surface), not in the next facet of a
        // curved hull?
        private bool Bounded(Vector3 q, Vector3 f)
        {
            float rise = 0.03f;
            return !surface.Raycast(q + f * rise, -f, 2 * rise, 0.17f, out SurfaceHit h, false) || Vector3.Dot(h.FaceN, f) < cosBound;
        }

        // How far the flat face (normal f) through p goes on along dir, up to max.
        private float Extent(Vector3 p, Vector3 f, Vector3 dir, float max)
        {
            float good = 0, stepLen = 0.005f;
            while (good < max)
            {
                float next = Math.Min(good + stepLen, max);
                if (!Flat(p + dir * next, f)) break;
                good = next;
            }
            if (good >= max) return max;
            float lo = good, hi = Math.Min(good + stepLen, max);
            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) / 2;
                if (Flat(p + dir * mid, f)) lo = mid; else hi = mid;
            }
            return lo;
        }

        private bool Flat(Vector3 q, Vector3 f)
        {
            float rise = 0.02f;
            return surface.Raycast(q + f * rise, -f, 2 * rise, 0.17f, out SurfaceHit h, false) && Vector3.Dot(h.FaceN, f) >= cosPlanar &&
                   Math.Abs(Vector3.Dot(h.P - q, f)) < 0.002f;
        }

        // Strip tiles: where a tile was cut at a crease and a strip face lies beyond (chamfer, edge or tip of a wing),
        // a rigid flat tile on that face from the crease to its far crease, as long as the cut. Strips of neighbouring
        // tiles touch; the far edge of a strip may start the next one (chamfer, then the steep face).
        private void Strips()
        {
            var parents = new List<Tile>(tiles);
            var all = new List<Tile>();
            for (int gen = 0; gen < 4 && parents.Count > 0; gen++)
            {
                var made = new List<Tile>();
                foreach (Tile p in parents)
                {
                    if (!p.Drawn || p.State != 2) continue;
                    int m = p.O.Length;
                    // chains of cut segments along one crease: a chain also ends where the cut turns by more than 30Â°
                    bool Joins(int k) => p.Tags[k] == TagCut && p.Tags[(k + m - 1) % m] == TagCut &&
                                         Vector2.Dot((p.O[(k + 1) % m] - p.O[k]).normalized, (p.O[k] - p.O[(k + m - 1) % m]).normalized) > 0.866f;
                    for (int k0 = 0; k0 < m; k0++)
                    {
                        if (p.Tags[k0] != TagCut || Joins(k0)) continue;
                        int k1 = k0 + 1;
                        while (k1 - k0 < m && Joins(k1 % m)) k1++;
                        Tile strip = MakeStrip(p, p.SegHull[k0][0], p.SegHull[k1 % m][0]);
                        if (strip == null) continue;
                        strip.Parent = p; strip.K0 = k0; strip.K1 = k1;
                        made.Add(strip);
                        p.StripCut = p.StripCut ?? new bool[m];
                        for (int k = k0; k < k1; k++) p.StripCut[k % m] = true;
                    }
                }
                JoinStrips(made);
                tiles.AddRange(made);
                all.AddRange(made);
                parents = made;
            }
            foreach (Tile t in all) MeetAtCrease(t);
        }

        // Thick rigid tiles on both sides of a convex crease, each cut on the crease line, leave a V groove between their
        // tops (thickness * tan(bend / 2) on each side). Both edges move out to where the tops meet, less half a joint,
        // so the crease shows a joint like any other.
        private void MeetAtCrease(Tile t)
        {
            Tile p = t.Parent;
            float c = Mathf.Clamp(Vector3.Dot(p.TileN, t.TileN), -1, 1);
            float half = Mathf.Acos(c) / 2;
            float d = (S.SurfaceOffset + S.Thickness) * Mathf.Tan(half) - S.Gap / 2;
            if (d <= 0 || half > 40 * Mathf.Deg2Rad) return;
            // parent: the chain's vertices out across the chord
            int m = p.O.Length;
            Vector2 a = p.O[t.K0 % m], b = p.O[t.K1 % m], e = (b - a).normalized, nrm = new Vector2(e.y, -e.x);
            if (Vector2.Dot(nrm, (a + b) * 0.5f - Centroid(p.O)) < 0) nrm = -nrm;
            for (int k = t.K0; k <= t.K1; k++) p.O[k % m] += nrm * d;
            Resample(p);
            // strip: its near edge back towards the parent
            for (int k = 0; k < t.O.Length; k++)
                if (t.Tags[k] == TagStrip)
                {
                    t.O[k] -= t.SO * d;
                    t.O[(k + 1) % t.O.Length] -= t.SO * d;
                }
            Resample(t);
        }

        // Wall sample points follow a moved outline.
        private static void Resample(Tile t)
        {
            int m = t.O.Length;
            for (int k = 0; k < m; k++)
            {
                int n = t.Seg2[k].Length;
                for (int i = 0; i < n; i++) t.Seg2[k][i] = Vector2.Lerp(t.O[k], t.O[(k + 1) % m], (float)i / n);
            }
        }

        private Tile MakeStrip(Tile p, Vector3 a3, Vector3 b3)
        {
            Vector3 n = p.TileN, chord = b3 - a3;
            chord -= n * Vector3.Dot(chord, n);
            Vector3 mid = (a3 + b3) * 0.5f, centre = p.Hit;
            if (chord.magnitude < 0.3f * s) return null; // shorter cuts: the neighbouring strips run on over them
            Vector3 out3 = Vector3.Cross(chord.normalized, n);
            if (Vector3.Dot(out3, mid - centre) < 0) out3 = -out3;
            // the face beyond the crease, just past the cut
            if (!surface.Raycast(mid + out3 * 0.004f + n * 0.02f, -n, s, 0.17f, out SurfaceHit h, false)) return null;
            Vector3 f = h.FaceN;
            if (Vector3.Dot(f, p.F) >= cosCrease || Vector3.Dot(f, p.TileN) >= cosCrease || !InFoot(pat.Position(p.A, p.B) + To2(p, h.P), h.P, 0) || !InClip(h.P, 0) || Taken(h.P, f) || InKeepOut(h.P)) return null;
            Vector3 across = Tangent(out3, f);
            if (across == Vector3.zero || !StripFace(h, p.F)) return null;
            Vector3 inFace = h.P + across * MinStrip; // a bit further in: tiles cut at this crease end right before it
            foreach (Tile o in Near(h.P, s))
                if (o != p && o.Drawn && Covers(o, inFace, f)) return null;

            var t = new Tile
            {
                A = p.A, B = p.B, Virtual = true, Strip = true, Lost = p.Lost, State = 1, Body = p.Body,
                C = h.P, N = f, F = f, U = Tangent(p.U, f), Halves = new List<Half>(), EdgeOwners = new List<Tile>[pat.Edges.Length]
            };
            if (t.U == Vector3.zero) return null;
            t.V = Vector3.Cross(t.U, f) * vSign;
            t.W = new Walker { P = h.P, N = f, U = t.U, F = f };
            Vector2 a = To2(t, a3), b = To2(t, b3), e = (b - a).normalized;
            Vector2 o2 = new Vector2(Vector3.Dot(across, t.U), Vector3.Dot(across, t.V));
            o2 = (o2 - e * Vector2.Dot(o2, e)).normalized;
            // the parents' joint lies between the ends of neighbouring strips: a normal joint between them too
            t.SA = a;
            t.SB = b;
            t.SE = e;
            t.SO = o2;
            if (!Shape(t, true)) return null;
            AddToHash(t);
            return t;
        }

        // Strip outline from its crease ends SA, SB across the face (SO) to the far crease, placed on the face.
        private bool Shape(Tile t, bool fresh)
        {
            const float inset = 0.0005f;
            Vector2 o2 = t.SO, a0 = t.SA + o2 * inset, b0 = t.SB + o2 * inset, m0 = (a0 + b0) * 0.5f;
            Vector3 across = (t.U * o2.x + t.V * o2.y).normalized;
            float wa = Extent(To3(t, a0), t.F, across, s), wb = Extent(To3(t, b0), t.F, across, s), wm = Extent(To3(t, m0), t.F, across, s);
            if (fresh && (Math.Max(wa, Math.Max(wb, wm)) < MinStrip || Math.Min(wa, Math.Min(wb, wm)) >= 0.9f * s)) return false;
            float w = Math.Max(wa, Math.Max(wb, wm));
            // a tapering end (wing tip) can measure 0 right at its corner: keep the shape, the face cut trims it
            if (wa < inset) wa = w;
            if (wb < inset) wb = w;
            var poly = new[] { a0, b0, b0 + o2 * Math.Max(wb - inset, 0), m0 + o2 * Math.Max(wm - inset, 0), a0 + o2 * Math.Max(wa - inset, 0) };
            var tags = new[] { TagStrip, TagStripSide, TagCut, TagCut, TagStripSide };
            if (Shoelace(poly) < 0)
            {
                Array.Reverse(poly);
                tags = new[] { TagCut, TagCut, TagStripSide, TagStrip, TagStripSide }; // segment k: poly[k] -> poly[k + 1]
            }
            t.O = poly;
            t.Tags = tags;
            t.MidArea = Math.Abs(Shoelace(poly));
            if (t.MidArea < MinPiece * pat.CellArea) return false;
            t.State = 1;
            Place(t);
            if (t.State == 0) return false;
            t.State = 2; // ragged outline: fan from the centroid
            return true;
        }

        // Strips on one face along one crease: an end that stops short of the next strip moves up to a joint's width
        // from it, an end without a next strip runs on along the face (tip of a wing).
        private void JoinStrips(List<Tile> strips)
        {
            foreach (Tile t in strips)
            {
                bool moved = false;
                for (int side = 0; side < 2; side++)
                {
                    Vector2 end = side == 0 ? t.SA : t.SB, dir2 = side == 0 ? -t.SE : t.SE;
                    Vector3 end3 = To3(t, end), dir = (t.U * dir2.x + t.V * dir2.y).normalized;
                    float reach = float.MaxValue;
                    foreach (Tile o in strips)
                    {
                        if (o == t || Vector3.Dot(o.F, t.F) < 0.999f) continue;
                        foreach (Vector2 oe in new[] { o.SA, o.SB })
                        {
                            Vector3 d = To3(o, oe) - end3;
                            float along = Vector3.Dot(d, dir);
                            if ((d - dir * along).magnitude < 0.01f && along > -0.01f) reach = Math.Min(reach, Math.Max(along - S.Gap, 0));
                        }
                    }
                    if (reach == float.MaxValue)
                    {
                        // free end: as far as the face goes on along the crease, measured half way across
                        Vector3 across = (t.U * t.SO.x + t.V * t.SO.y).normalized;
                        reach = Math.Max(Extent(end3 + across * MinStrip * 0.5f, t.F, dir, 0.5f * s) - 0.001f, 0);
                    }
                    if (reach < 0.001f) continue;
                    if (side == 0) t.SA += dir2 * reach; else t.SB += dir2 * reach;
                    moved = true;
                }
                if (moved)
                {
                    Tile keep = Copy(t);
                    if (!Shape(t, false)) Restore(t, keep);
                }
            }
        }

        private static Tile Copy(Tile t) => new Tile { O = t.O, Tags = t.Tags, Seg2 = t.Seg2, SegHull = t.SegHull, Area = t.Area, MidArea = t.MidArea, TileN = t.TileN, Htop = t.Htop, Hit = t.Hit, Owner = t.Owner, State = t.State };

        private static void Restore(Tile t, Tile k)
        {
            t.O = k.O; t.Tags = k.Tags; t.Seg2 = k.Seg2; t.SegHull = k.SegHull; t.Area = k.Area; t.MidArea = k.MidArea;
            t.TileN = k.TileN; t.Htop = k.Htop; t.Hit = k.Hit; t.Owner = k.Owner; t.State = k.State;
        }

        private static Vector2 To2(Tile t, Vector3 q) => new Vector2(Vector3.Dot(q - t.C, t.U), Vector3.Dot(q - t.C, t.V));
        private static Vector3 To3(Tile t, Vector2 q) => t.C + t.U * q.x + t.V * q.y;

        // Does tile o lie over p (outline in o's plane, normals alike)?
        private static bool Covers(Tile o, Vector3 p, Vector3 n)
        {
            if (Vector3.Dot(o.TileN, n) < 0.3f) return false;
            Vector2 q = To2(o, p);
            float sign = Shoelace(o.O) >= 0 ? 1 : -1;
            for (int k = 0; k < o.O.Length; k++)
            {
                Vector2 a = o.O[k], b = o.O[(k + 1) % o.O.Length];
                if (((b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x)) * sign < 0) return false;
            }
            return true;
        }

        // ---------- mesh ----------

        // 2D outline point lifted along the drop direction onto the tile plane at the given height: the footprint
        // stays the cell neighbours computed theirs against, even when the tile plane tilts.
        private Vector3 OnPlane(Tile t, Vector2 o, float height)
        {
            Vector3 p = t.C + t.U * o.x + t.V * o.y;
            return p + t.N * ((height - Vector3.Dot(p - t.C, t.TileN)) / Vector3.Dot(t.N, t.TileN));
        }

        private Vector3 Dir3(Tile t, Vector2 d) => Tangent(t.U * d.x + t.V * d.y, t.TileN);

        private bool OverHull(Tile t, Vector2 p)
        {
            Vector3 x = t.C + t.U * p.x + t.V * p.y;
            return surface.Raycast(x + t.N * dropHeight, -t.N, 2 * dropHeight, 0.17f, out _, false);
        }

        private static bool AllFull(Tile t)
        {
            foreach (List<Tile> owners in t.EdgeOwners)
            {
                if (owners == null) return false;
                foreach (Tile o in owners)
                    if (!o.DrawnFull) return false;
            }
            return true;
        }

        // Is there another strip on the same face at p (its neighbour along the crease, a joint away)?
        private bool NextStrip(Tile t, Vector3 p)
        {
            foreach (Tile o in Near(p, s))
                if (o != t && o.Strip && o.Drawn && Vector3.Dot(o.TileN, t.TileN) > 0.99f && Covers(o, p, t.TileN)) return true;
            return false;
        }

        private static bool NeedsWall(Tile t, int tag)
        {
            if (tag < 0) return true;
            List<Tile> owners = t.EdgeOwners[tag];
            if (owners == null) return true;
            foreach (Tile o in owners)
                if (!o.DrawnFull) return true;
            return false;
        }

        // A rigid tile that straddles a crease sits on its highest hull point and can stand well above its neighbour's
        // plane. Its edge then needs a wall, or the hull shows through the open step at grazing angles.
        // p: point on the tile's outer edge, bevel: whether the outer edge sits one bevel depth below the top.
        private bool StandsProud(Tile t, int tag, Vector3 p, bool bevel)
        {
            foreach (Tile o in t.EdgeOwners[tag])
                if (Vector3.Dot(p - o.C, o.TileN) - (o.Htop - (bevel ? S.BevelDepth : 0)) > 0.002f) return true;
            return false;
        }

        // Joint sheet distance of a half-plane, knowing which neighbours were drawn. Towards a drawn neighbour the sheet
        // reaches past the joint under its edge (JointReach), so neighbouring sheets overlap and close the joint from every
        // view, also at corners where 3-4 tiles meet and on bends. It stays below the neighbour's bevel: where the
        // neighbour sits lower (proud step), only the midline + eps. Towards holes and the border it stays inside.
        private const float JointReach = 0.005f;

        private float JointDistance(Tile t, Half hp)
        {
            float inside = hp.HTile - 0.0005f;
            switch (hp.Tag)
            {
                case TagBorder:
                case TagFootprint:
                case TagCut:
                    return inside;
                case TagForeign:
                    return hp.Other.Drawn && !Split(t.Body, hp.Other.Body) ? Reach(t, hp, hp.Other) : inside;
            }
            float d = float.MaxValue;
            foreach (Tile o in t.EdgeOwners[hp.Tag])
            {
                if (!o.Drawn || Split(t.Body, o.Body)) return inside;  // hole or flap border: no dark sheet sticking out
                d = Math.Min(d, Reach(t, hp, o));
            }
            return d;
        }

        private float Reach(Tile t, Half hp, Tile o)
        {
            float near = hp.Raw + eps, far = Math.Max(hp.Raw, hp.HTile) + S.Gap / 2 + JointReach;
            bool bevel = S.BevelWidth > 0 && S.BevelDepth > 0;
            float hj = (bevel ? t.Htop - S.BevelDepth : t.Htop) - 0.001f;
            // the neighbour's surface over the sheet's end: its bevel, JointReach in from its outer edge
            float cover = (bevel ? o.Htop - S.BevelDepth * (1 - Math.Min(1, JointReach / S.BevelWidth)) : o.Htop) - 0.0005f;
            // sheet height over the neighbour's plane is linear in the reach: go as far as it stays under that cover
            float h0 = Vector3.Dot(OnPlane(t, hp.N * near, hj) - o.C, o.TileN);
            float h1 = Vector3.Dot(OnPlane(t, hp.N * far, hj) - o.C, o.TileN);
            if (h1 <= cover) return far;
            if (h0 >= cover) return near;
            return near + (far - near) * (cover - h0) / (h1 - h0);
        }

        private Vector2 Uv(Tile t, Vector2 o)
        {
            switch (S.UvMode)
            {
                case UvMode.Atlas:
                {
                    int cols = Math.Max(1, S.AtlasCols), rows = Math.Max(1, S.AtlasRows);
                    int cell = Hash(t.A, t.B, S.Seed * 31 + 1) % (cols * rows);
                    Vector2 q = pat.Unrotate(o);
                    if (S.AtlasRandomRotation)
                    {
                        float ang = 2 * Mathf.PI / pat.RotationSteps * (Hash(t.A, t.B, S.Seed * 31 + 2) % pat.RotationSteps);
                        float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        q = new Vector2(q.x * c - q.y * sn, q.x * sn + q.y * c);
                    }
                    float ux = Mathf.Clamp(q.x / pat.AtlasEdge + 0.5f, 0.004f, 0.996f);
                    float uy = Mathf.Clamp(q.y / pat.AtlasEdge + 0.5f, 0.004f, 0.996f);
                    return new Vector2((cell % cols + ux) / cols, (cell / cols + uy) / rows);
                }
                case UvMode.Across:
                {
                    Vector2 d = pat.Position(t.A, t.B) + o;
                    return S.TextureScale > 0
                        ? new Vector2(d.x / S.TextureScale + 0.5f, d.y / (S.TextureScale * S.TextureAspect) + 0.5f)
                        : new Vector2(d.x / S.Width + 0.5f, d.y / S.Height + 0.5f);
                }
                default:
                {
                    float rep = S.TextureScale > 0 ? S.TextureScale : 2f;
                    var jitter = new Vector2(Hash(t.A, t.B, S.Seed * 31 + 3) % 1000 / 1000f, Hash(t.A, t.B, S.Seed * 31 + 4) % 1000 / 1000f);
                    return new Vector2(o.x / rep, o.y / (rep * S.TextureAspect)) + jitter;
                }
            }
        }

        // Outline moved inward by w (mitre, limited); dirs = inward direction of each vertex.
        private static Vector2[] Inset(Vector2[] o, float w, out Vector2[] dirs)
        {
            int m = o.Length;
            var r = new Vector2[m];
            dirs = new Vector2[m];
            float sign = Shoelace(o) >= 0 ? 1 : -1;
            Vector2 c = Centroid(o);
            for (int k = 0; k < m; k++)
            {
                Vector2 p = o[(k + m - 1) % m], q = o[(k + 1) % m];
                Vector2 e1 = (o[k] - p).normalized, e2 = (q - o[k]).normalized;
                Vector2 n1 = new Vector2(-e1.y, e1.x) * sign, n2 = new Vector2(-e2.y, e2.x) * sign;
                Vector2 dir = n1 + n2;
                dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : (c - o[k]).normalized;
                float len = w / Math.Max(Vector2.Dot(dir, n1), 0.4f);
                len = Math.Min(len, 0.45f * (c - o[k]).magnitude);
                dirs[k] = dir;
                r[k] = o[k] + dir * len;
            }
            return r;
        }

        // The dark joint bed sits just above the Mk2 tank skin. Both patterns end on the
        // same model edges, even where short filler pieces meet at a sharp chine.
        // It has no thermal area of its own.
        private void AddTankJointBed(Dictionary<int, Acc> accs)
        {
            var bedCuts = new List<Vector4>(cuts ?? new Vector4[0]);
            if (S.LimitedTank)
            {
                // The unrolled footprint is defined by the walked lattice. Its filler already follows it.
                if (S.Unrolled) return;
                float inset = S.StraightBorder ? 0 : s;
                void Limit(Vector3 axis, float extent)
                {
                    float h = extent / 2 - inset;
                    bedCuts.Add(new Vector4(axis.x, axis.y, axis.z, -Vector3.Dot(origin, axis) - h));
                    bedCuts.Add(new Vector4(-axis.x, -axis.y, -axis.z, Vector3.Dot(origin, axis) - h));
                }
                Limit(decalU, S.Width);
                Limit(decalV, S.Height);
            }
            bool excluded = S.Obstacle != null && S.Obstacle.Any(x => x) ||
                            S.KeepOut != null && S.KeepOut.Length > 0;
            for (int k = 0; k < wrapIn.Length; k++)
            {
                if (!wrapIn[k] || !surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int owner) ||
                    !accs.TryGetValue(owner, out Acc acc) || Mathf.Abs(Vector3.Dot(n, S.TankAxis)) > 0.99f) continue;
                var poly = new List<Vector3> { a, b, c };
                foreach (Vector4 plane in bedCuts)
                {
                    var next = new List<Vector3>();
                    for (int j = 0; j < poly.Count; j++)
                    {
                        Vector3 u = poly[j], v = poly[(j + 1) % poly.Count];
                        float du = Vector3.Dot((Vector3)plane, u) + plane.w, dv = Vector3.Dot((Vector3)plane, v) + plane.w;
                        if (du <= 0) next.Add(u);
                        if ((du < 0) != (dv < 0)) next.Add(Vector3.Lerp(u, v, du / (du - dv)));
                    }
                    poly = next;
                }
                Vector3 Lift(Vector3 p) => p + Vector3.ProjectOnPlane(p - S.TankCenter, S.TankAxis).normalized * 0.0015f;
                void Face(Vector3 u, Vector3 v, Vector3 w)
                {
                    if (excluded)
                    {
                        float span = Math.Max((u - v).sqrMagnitude, Math.Max((v - w).sqrMagnitude, (w - u).sqrMagnitude));
                        if (span > Math.Max(0.01f, s * s))
                        {
                            Vector3 uv = (u + v) / 2, vw = (v + w) / 2, wu = (w + u) / 2;
                            Face(u, uv, wu);
                            Face(uv, v, vw);
                            Face(wu, vw, w);
                            Face(uv, vw, wu);
                            return;
                        }
                        Vector3 center = (u + v + w) / 3;
                        if (InKeepOut(center) || InKeepOut(u) || InKeepOut(v) || InKeepOut(w) ||
                            UnderObstacle(center, n) || UnderObstacle(u, n) || UnderObstacle(v, n) || UnderObstacle(w, n)) return;
                    }
                    int first = Add(acc, Lift(u), n, Vector2.zero);
                    Add(acc, Lift(v), n, Vector2.zero);
                    Add(acc, Lift(w), n, Vector2.zero);
                    Tri(acc, acc.Joint, first, first + 1, first + 2, n);
                }
                for (int j = 1; j + 1 < poly.Count; j++) Face(poly[0], poly[j], poly[j + 1]);
            }
        }

        // Recessed dark joint underlay for cockpit masks: exact to the mask contour (SkinPolygons) and sitting at
        // SurfaceOffset (well below the 3D tile tops, so zero Z-fighting and zero flat blotches over tiles), closing any
        // tiny micro-crevices between rigid 3D tiles on doubly-curved cockpit hulls. No thermal area of its own.
        private void AddMaskJointBed(Dictionary<int, Acc> accs)
        {
            bool ExactInMask(Vector3 p)
            {
                if (cuts != null)
                    foreach (Vector4 c in cuts)
                        if (c.x * p.x + c.y * p.y + c.z * p.z + c.w > 1e-4f) return false;
                if (S.ClipPlanes != null)
                    foreach (Vector4 c in S.ClipPlanes)
                        if (c.x * p.x + c.y * p.y + c.z * p.z + c.w <= 1e-4f) return true;
                if (S.Regions != null)
                    foreach (CoverRegion r in S.Regions)
                        if (RegionDist(r, p, out _) <= 1e-4f) return true;
                return false;
            }
            float lift = Math.Max(0.0015f, S.SurfaceOffset * 0.75f);
            for (int k = 0; k < wrapIn.Length; k++)
            {
                if (!wrapIn[k] || !surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int owner) ||
                    !accs.TryGetValue(owner, out Acc acc)) continue;
                if (S.TankAxis.sqrMagnitude > 0.5f && Vector3.Dot(n, S.TankAxis.normalized) < -0.999f) continue;
                if (surface.Raycast((a + b + c) / 3 + n * 0.01f, n, 0.5f, -1.01f, out SurfaceHit inner, false) && !Obstacle(inner.Owner)) continue;
                foreach (var piece in SkinPolygons(a, b, c))
                {
                    Vector3 mid = Vector3.zero;
                    foreach (Vector3 p in piece) mid += p;
                    mid /= piece.Count;
                    if (!ExactInMask(mid) || InKeepOut(mid) || UnderObstacle(mid, n)) continue;
                    int i0 = acc.V.Count;
                    for (int j = 0; j < piece.Count; j++) Add(acc, piece[j] + n * lift, n, Vector2.zero);
                    for (int j = 1; j + 1 < piece.Count; j++) Tri(acc, acc.Joint, i0, i0 + j, i0 + j + 1, n);
                }
            }
        }

        // Smooth untiled ceramic shell for cockpit nose tip + sides/chines (S.CeramicSideDeg > 0):
        // clipped cleanly to the exact mask contour and stack cuts via SkinPolygons, smoothly welded, lifted to tile top
        // height with side walls along open edges, and clustered into per-normal glow bins so nose and sides glow realistically.
        private void EmitCeramicShell(Dictionary<int, Acc> accs, List<int> order, TileResult result, List<TileOutline> outlines)
        {
            bool ExactInMask(Vector3 p)
            {
                if (cuts != null)
                    foreach (Vector4 c in cuts)
                        if (c.x * p.x + c.y * p.y + c.z * p.z + c.w > 1e-4f) return false;
                if (S.ClipPlanes != null)
                    foreach (Vector4 c in S.ClipPlanes)
                        if (c.x * p.x + c.y * p.y + c.z * p.z + c.w <= 1e-4f) return true;
                if (S.Regions != null)
                    foreach (CoverRegion r in S.Regions)
                        if (RegionDist(r, p, out _) <= 1e-4f) return true;
                return false;
            }
            var keep = new List<(Vector3 a, Vector3 b, Vector3 c, int owner)>();
            var ids = new List<int>();
            var vp = new List<Vector3>();
            var vn = new List<Vector3>();
            var welded = new Dictionary<(int, int, int), int>();
            int Weld(Vector3 p, Vector3 normal)
            {
                var key = (Mathf.RoundToInt(p.x * 10000), Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));
                if (!welded.TryGetValue(key, out int id))
                {
                    welded[key] = id = vp.Count;
                    vp.Add(p);
                    vn.Add(Vector3.zero);
                }
                vn[id] += normal;
                return id;
            }
            Vector3 axis = S.TankAxis.sqrMagnitude > 0.5f ? S.TankAxis.normalized : Vector3.zero;
            foreach (int k in shellTris)
            {
                if (!surface.Triangle(k, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 n, out int owner) || Obstacle(owner)) continue;
                if (axis.sqrMagnitude > 0.5f && Vector3.Dot(n, axis) < -0.999f) continue;
                foreach (var piece in SkinPolygons(a, b, c))
                {
                    Vector3 mid = Vector3.zero;
                    foreach (Vector3 p in piece) mid += p;
                    mid /= piece.Count;
                    if (!ExactInMask(mid) || InKeepOut(mid) || UnderObstacle(mid, n)) continue;
                    for (int j = 1; j + 1 < piece.Count; j++)
                    {
                        Vector3 an = Vector3.Cross(piece[j] - piece[0], piece[j + 1] - piece[0]);
                        if (an.sqrMagnitude < 1e-16f) continue;
                        keep.Add((piece[0], piece[j], piece[j + 1], owner));
                        ids.Add(Weld(piece[0], an)); ids.Add(Weld(piece[j], an)); ids.Add(Weld(piece[j + 1], an));
                    }
                }
            }
            if (keep.Count == 0) return;
            var edgeUse = new Dictionary<(int, int), int>();
            for (int i = 0; i < ids.Count; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int p = ids[i + e], q = ids[i + (e + 1) % 3];
                    var key = p < q ? (p, q) : (q, p);
                    edgeUse[key] = (edgeUse.TryGetValue(key, out int u0) ? u0 : 0) + 1;
                }
            float lift = S.SurfaceOffset + S.Thickness;
            var top = new Vector3[vp.Count];
            for (int i = 0; i < vp.Count; i++)
            {
                vn[i] = vn[i].sqrMagnitude > 1e-20f ? vn[i].normalized : decalN;
                Vector3 nLift = vn[i];
                if (cuts != null)
                {
                    foreach (Vector4 cp in cuts)
                    {
                        var cn = new Vector3(cp.x, cp.y, cp.z);
                        if (Mathf.Abs(Vector3.Dot(cn, vp[i]) + cp.w) < 0.005f)
                        {
                            Vector3 proj = nLift - cn * Vector3.Dot(nLift, cn);
                            if (proj.sqrMagnitude > 1e-6f) nLift = proj.normalized;
                        }
                    }
                }
                top[i] = vp[i] + nLift * lift;
            }
            // Spotless matte ceramic sample point (upper center of atlas cell 0,0, clear of SquareBlack serial numbers at lower half).
            Vector2 ceramicUv = S.UvMode == UvMode.Atlas
                ? new Vector2(0.5f / Math.Max(1, S.AtlasCols), 0.625f / Math.Max(1, S.AtlasRows))
                : new Vector2(0.5f, 0.5f);
            float cosBin = Mathf.Cos(S.BinAngleDeg * Mathf.Deg2Rad);
            int maxBins = Math.Max(1, S.MaxBins);
            for (int i = 0; i < keep.Count; i++)
            {
                var (a, b, c, o) = keep[i];
                Vector3 fn = Vector3.Cross(b - a, c - a);
                float area = 0.5f * fn.magnitude;
                if (area < 1e-10f) continue;
                fn.Normalize();
                if (!accs.TryGetValue(o, out Acc acc)) { accs[o] = acc = new Acc(); order.Add(o); }
                int bin = -1;
                float bestDot = -2;
                for (int j = 0; j < acc.BinSum.Count; j++)
                {
                    float d = Vector3.Dot(acc.BinSum[j].normalized, fn);
                    if (d > bestDot) { bestDot = d; bin = j; }
                }
                if (bin < 0 || (bestDot < cosBin && acc.BinSum.Count < maxBins))
                {
                    acc.Bins.Add(new List<int>()); acc.BinSum.Add(Vector3.zero); acc.BinArea.Add(0);
                    bin = acc.Bins.Count - 1;
                }
                acc.BinSum[bin] += fn * area;
                acc.BinArea[bin] += area;
                acc.AreaVecs.Add(fn * area);
                acc.Covered += area;
                result.CoveredArea += area;
                List<int> tris = acc.Bins[bin];
                int ia = ids[3 * i], ib = ids[3 * i + 1], ic = ids[3 * i + 2];
                int v0 = Add(acc, top[ia], vn[ia], ceramicUv);
                Add(acc, top[ib], vn[ib], ceramicUv);
                Add(acc, top[ic], vn[ic], ceramicUv);
                Tri(acc, tris, v0, v0 + 1, v0 + 2, fn);
                Vector3 mid = (a + b + c) / 3;
                int[] ci = { ia, ib, ic };
                for (int e = 0; e < 3; e++)
                {
                    int p = ci[e], q = ci[(e + 1) % 3];
                    var key = p < q ? (p, q) : (q, p);
                    if (edgeUse[key] != 1) continue;
                    Vector3 outward = Vector3.Cross(vp[q] - vp[p], fn);
                    if (Vector3.Dot(outward, vp[p] - mid) < 0) outward = -outward;
                    if (outward.sqrMagnitude < 1e-16f) continue;
                    outward.Normalize();
                    int w0 = Add(acc, top[p], outward, ceramicUv);
                    Add(acc, top[q], outward, ceramicUv);
                    Add(acc, vp[p], outward, ceramicUv);
                    Add(acc, vp[q], outward, ceramicUv);
                    Tri(acc, tris, w0, w0 + 1, w0 + 3, outward);
                    Tri(acc, tris, w0, w0 + 3, w0 + 2, outward);
                }
            }
            result.TileCount++;
        }

        private void Emit(TileResult result, List<TileOutline> outlines)
        {
            var accs = new Dictionary<int, Acc>();
            var order = new List<int>();
            float cosBin = Mathf.Cos(S.BinAngleDeg * Mathf.Deg2Rad);
            int maxBins = Math.Max(1, S.MaxBins);
            foreach (Tile t in tiles)
            {
                if (!t.Drawn) continue;
                if (!accs.TryGetValue(t.Owner, out Acc acc)) { accs[t.Owner] = acc = new Acc(); order.Add(t.Owner); }
                // greedy normal clustering into glow bins
                int best = -1;
                float bestDot = -2;
                for (int i = 0; i < acc.BinSum.Count; i++)
                {
                    float d = Vector3.Dot(acc.BinSum[i].normalized, t.TileN);
                    if (d > bestDot) { bestDot = d; best = i; }
                }
                if (best < 0 || (bestDot < cosBin && acc.BinSum.Count < maxBins))
                {
                    acc.Bins.Add(new List<int>()); acc.BinSum.Add(Vector3.zero); acc.BinArea.Add(0);
                    best = acc.Bins.Count - 1;
                }
                t.Bin = best;
                acc.BinSum[best] += t.TileN * Math.Max(t.Area, 1e-6f);
                acc.BinArea[best] += t.Area;
                acc.AreaVecs.Add(t.TileN * t.Area);
                acc.Covered += t.Area;
                EmitTile(t, acc, outlines);
                result.TileCount++;
                if (t.State == 2) result.PartialCount++;
                result.CoveredArea += t.Area;
            }

            if (S.CeramicSideDeg > 0 && shellTris != null && shellTris.Count > 0)
                EmitCeramicShell(accs, order, result, outlines);
            if (S.FillHull && !HasClip && !S.Overlay && S.TileLoss == 0 && (S.Occupied == null || S.Occupied.Length == 0))
                AddTankJointBed(accs);
            else if (S.FillHull && HasClip && !S.Overlay && S.TileLoss == 0 && (S.Occupied == null || S.Occupied.Length == 0))
                AddMaskJointBed(accs);
            result.Parts = new PartTiles[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                Acc a = accs[order[i]];
                var bn = new Vector3[a.BinSum.Count];
                for (int b = 0; b < bn.Length; b++) bn[b] = a.BinSum[b].sqrMagnitude > 1e-12f ? a.BinSum[b].normalized : decalN;
                var bt = new int[a.Bins.Count][];
                for (int b = 0; b < bt.Length; b++) bt[b] = a.Bins[b].ToArray();
                result.Parts[i] = new PartTiles
                {
                    Owner = order[i], Vertices = a.V.ToArray(), Normals = a.N.ToArray(), Uvs = a.Uv.ToArray(),
                    BinTriangles = bt, BinNormals = bn, BinAreas = a.BinArea.ToArray(),
                    JointTriangles = a.Joint.ToArray(), BackTriangles = a.Back.ToArray(),
                    TileAreaVectors = a.AreaVecs.ToArray(), CoveredArea = a.Covered
                };
            }
        }

        private void EmitTile(Tile t, Acc acc, List<TileOutline> outlines)
        {
            if (t.Shell != null) { EmitCap(t, acc, outlines); return; }
            List<int> tris = acc.Bins[t.Bin];
            Vector3 n = t.TileN;
            Vector2[] o = t.O;
            int m = o.Length;
            Vector2 c2 = Centroid(o);
            // 1.6.2 / 1.7.3: no bevel on a tile cut straight by a plane/mask edge or ending right next to a stack seam cut:
            // its top runs flush to the seam, so two parts' tiles meet without a V-groove gap.
            bool nearCut = false;
            if (cuts != null && Array.IndexOf(t.Tags, TagCut) >= 0)
            {
                for (int k = 0; k < m && !nearCut; k++)
                {
                    if (t.Tags[k] != TagCut) continue;
                    Vector3 ep = OnPlane(t, (o[k] + o[(k + 1) % m]) * 0.5f, 0);
                    foreach (Vector4 cp in cuts)
                        if (Mathf.Abs(cp.x * ep.x + cp.y * ep.y + cp.z * ep.z + cp.w) < 0.08f) { nearCut = true; break; }
                }
            }
            bool bevel = S.BevelWidth > 0 && S.BevelDepth > 0 && Array.IndexOf(t.Tags, TagFootprint) < 0 && !nearCut;
            float hOut = bevel ? t.Htop - S.BevelDepth : t.Htop;

            // top: fan over the inset outline
            Vector2[] inner = o, dirs = null;
            if (bevel)
            {
                // narrow or acute pieces (cut tiles at a crease): a narrower bevel, so their joints look like all others;
                // where even that would fold (faces inside out), none
                bevel = false;
                float oSign = Shoelace(o) >= 0 ? 1 : -1;
                for (float w = S.BevelWidth; w >= S.BevelWidth / 4.01f && !bevel; w /= 2)
                {
                    inner = Inset(o, w, out dirs);
                    bevel = true;
                    for (int k = 0; k < m && bevel; k++)
                    {
                        int k1 = (k + 1) % m;
                        Vector2 e = o[k1] - o[k];
                        bevel = Vector2.Dot(inner[k1] - inner[k], e) > 0.25f * e.sqrMagnitude &&
                                Mathf.Abs(e.x * (c2.y - o[k].y) - e.y * (c2.x - o[k].x)) > 2.5f * w * e.magnitude &&
                                // both bevel triangles face up: on a tile tilted against its drop direction the outer
                                // ring shifts sideways by more than the inset and the bevel folds
                                BevelUp(t, OnPlane(t, inner[k], t.Htop), OnPlane(t, inner[k1], t.Htop), OnPlane(t, o[k1], hOut), oSign) &&
                                BevelUp(t, OnPlane(t, inner[k], t.Htop), OnPlane(t, o[k1], hOut), OnPlane(t, o[k], hOut), oSign);
                    }
                }
                if (!bevel) { inner = o; hOut = t.Htop; }
            }
            // convex full tiles fan from a corner; ragged partial pieces from their centroid
            bool convex = t.State == 1;
            int c0 = convex ? -1 : Add(acc, OnPlane(t, c2, t.Htop), n, Uv(t, c2));
            int i0 = acc.V.Count;
            for (int k = 0; k < m; k++) Add(acc, OnPlane(t, inner[k], t.Htop), n, Uv(t, inner[k]));
            Fan(acc, tris, c0, i0, m, n);

            // bevel: ring from the top edge (shared vertices, smooth shading) down to the outer edge
            var outer = new Vector3[m];
            for (int k = 0; k < m; k++) outer[k] = OnPlane(t, o[k], hOut);
            if (bevel)
            {
                int o0 = acc.V.Count;
                for (int k = 0; k < m; k++) Add(acc, outer[k], (n + Dir3(t, -dirs[k])).normalized, Uv(t, o[k]));
                for (int k = 0; k < m; k++)
                {
                    int k1 = (k + 1) % m;
                    Vector3 refN = n + Dir3(t, (o[k] + o[k1]) * 0.5f - c2);
                    Tri(acc, tris, i0 + k, i0 + k1, o0 + k1, refN);
                    Tri(acc, tris, i0 + k, o0 + k1, o0 + k, refN);
                }
            }

            // side walls, only where no full drawn neighbour hides them
            for (int k = 0; k < m; k++)
            {
                int k1 = (k + 1) % m;
                bool proud = false; // wall only because the edge stands above a drawn neighbour: one quad is enough
                if (!NeedsWall(t, t.Tags[k]))
                {
                    if (!StandsProud(t, t.Tags[k], OnPlane(t, (o[k] + o[k1]) * 0.5f, hOut), bevel)) continue;
                    proud = true;
                }
                Vector3 outward = Dir3(t, (o[k] + o[k1]) * 0.5f - c2);
                if (outward == Vector3.zero) continue;
                if (t.Tags[k] == TagStripSide && NextStrip(t, OnPlane(t, (o[k] + o[k1]) * 0.5f, 0) + outward * (S.Gap + 0.002f))) continue;
                // walls meeting in the groove over a strip crease reach into the hull, so its bottom stays closed
                bool sink = t.StripCut != null && t.StripCut[k] || t.Strip && t.Tags[k] == TagStrip;
                Vector2[] sp = t.Seg2[k];
                Vector3[] sh = t.SegHull[k];
                int q = proud ? 1 : sp.Length;
                int w0 = acc.V.Count;
                for (int i = 0; i <= q; i++)
                {
                    Vector2 p2 = i < q ? sp[i] : o[k1];
                    Vector3 hull = i < q ? sh[i] : t.SegHull[k1][0];
                    Vector2 uv = Uv(t, p2);
                    Add(acc, OnPlane(t, p2, hOut), outward, uv);
                    Add(acc, hull + n * (sink ? -0.002f : S.SurfaceOffset), outward, uv);
                }
                for (int i = 0; i < q; i++)
                {
                    int a = w0 + 2 * i;
                    Tri(acc, tris, a, a + 2, a + 3, outward);
                    Tri(acc, tris, a, a + 3, a + 1, outward);
                }
            }

            // dark joint sheet 1 mm below the outer edge, closing the joints to the neighbours
            Vector3[] joint = null;
            {
                // partial tiles too (watertight underlay), but never past their cut, where the hull falls away, unless
                // a strip lies beyond: then under the strip's edge, closing the crease. Strips: their outline.
                List<Half> halves = t.Halves;
                if (t.State == 2)
                {
                    halves = new List<Half>(t.Halves);
                    float sign = Shoelace(o) >= 0 ? 1 : -1;
                    for (int k = 0; k < m; k++)
                    {
                        if (t.Tags[k] != TagCut && !t.Strip) continue; // (strips: every edge bounds the sheet)
                        Vector2 e = o[(k + 1) % m] - o[k];
                        if (e.sqrMagnitude < 1e-10f) continue;
                        Vector2 nrm = new Vector2(e.y, -e.x).normalized * sign;
                        bool under = t.StripCut != null && t.StripCut[k];
                        float h = Vector2.Dot(o[k], nrm) + (under ? JointReach + 0.0005f : t.Tags[k] == TagStripSide ? S.Gap / 2 + JointReach + 0.0005f : 0);
                        halves.Add(new Half { N = nrm, HTile = h, HMid = h, Raw = h, Tag = TagCut });
                    }
                }
                Vector2[] jp = ClipCell(halves, 2, out _, hp => JointDistance(t, hp));
                if (jp != null && (t.State == 2 || !AllFull(t)))
                {
                    // at the end of the cover the sheet never reaches past the hull's edge
                    for (int k = 0; k < jp.Length; k++)
                    {
                        if (OverHull(t, jp[k])) continue;
                        float lo = 0, hi = 1;
                        for (int i = 0; i < 8; i++)
                        {
                            float mid = (lo + hi) / 2;
                            if (OverHull(t, Vector2.Lerp(c2, jp[k], mid))) lo = mid; else hi = mid;
                        }
                        jp[k] = Vector2.Lerp(c2, jp[k], lo);
                    }
                }
                if (jp != null)
                {
                    float hj = hOut - 0.001f;
                    joint = new Vector3[jp.Length];
                    int g0 = acc.V.Count;
                    for (int k = 0; k < jp.Length; k++)
                    {
                        joint[k] = OnPlane(t, jp[k], hj);
                        Add(acc, joint[k], n, Uv(t, jp[k]));
                    }
                    Fan(acc, acc.Joint, -1, g0, jp.Length, n);
                }
            }

            // white underside at hull height
            int b0 = convex ? -1 : Add(acc, t.Hit + n * S.SurfaceOffset, -n, Uv(t, c2));
            int r0 = acc.V.Count;
            for (int k = 0; k < m; k++) Add(acc, t.SegHull[k][0] + n * S.SurfaceOffset, -n, Uv(t, o[k]));
            Fan(acc, acc.Back, b0, r0, m, -n);

            if (outlines != null)
            {
                var hullRing = new Vector3[m];
                for (int k = 0; k < m; k++) hullRing[k] = t.SegHull[k][0];
                outlines.Add(new TileOutline
                {
                    A = t.A, B = t.B, Owner = t.Owner, Partial = t.State == 2, Virtual = t.Virtual, Strip = t.Strip, Center = t.Hit, N = n,
                    TopPoint = OnPlane(t, c2, t.Htop), Outline = outer, Joint = joint, Hull = hullRing, Area = t.Area,
                    MaxGap = MaxGap(t, true), MaxSag = MaxGap(t, false)
                });
            }
        }

        // Air under the tile bottom beyond SurfaceOffset at its outline samples (0 at the highest hull point); border:
        // only edges where the cover ends (no neighbour: border, footprint, cut), else all but mitred crease edges (they reach
        // over the far face under the neighbour's edge, hidden).
        private float MaxGap(Tile t, bool border)
        {
            float bottom = t.Htop - S.Thickness - S.SurfaceOffset, gap = 0;
            int m = t.SegHull.Length;
            for (int k = 0; k < m; k++)
            {
                if (OnCrease(t, t.Tags[k]) || border && GapMode(t, t.Tags[k]) != 1) continue;
                Vector3[] seg = t.SegHull[k];
                for (int i = OnCrease(t, t.Tags[(k + m - 1) % m]) ? 1 : 0; i < seg.Length; i++)
                    gap = Math.Max(gap, bottom - Vector3.Dot(seg[i] - t.C, t.TileN));
            }
            return gap;
        }

        // Polygon ring first..first+count-1: fan from centre (if >= 0) or, for convex rings, from the first corner.
        private static void Fan(Acc acc, List<int> tris, int centre, int first, int count, Vector3 n)
        {
            if (centre >= 0)
                for (int k = 0; k < count; k++) Tri(acc, tris, centre, first + k, first + (k + 1) % count, n);
            else
                for (int k = 1; k + 1 < count; k++) Tri(acc, tris, first, first + k, first + k + 1, n);
        }

        private static int Add(Acc a, Vector3 p, Vector3 n, Vector2 uv)
        {
            a.V.Add(p);
            a.N.Add(n);
            a.Uv.Add(uv);
            return a.V.Count - 1;
        }

        // Adds a triangle whose front face (Unity: Cross(b - a, c - a)) points along reference; drops slivers.
        private static void Tri(Acc acc, List<int> tris, int a, int b, int c, Vector3 reference)
        {
            Vector3 face = Vector3.Cross(acc.V[b] - acc.V[a], acc.V[c] - acc.V[a]);
            if (face.sqrMagnitude < 1e-14f) return;
            tris.Add(a);
            if (Vector3.Dot(face, reference) >= 0) { tris.Add(b); tris.Add(c); }
            else { tris.Add(c); tris.Add(b); }
        }

        // ---------- physics ----------

        // Thin boxes over patches of tiles; one part, a handful of colliders.
        private Box[] Colliders()
        {
            List<Tile> drawn = tiles.FindAll(t => t.Drawn && t.Shell == null);
            float patch = Math.Max(S.ColliderPatchSize, s);
            while (true)
            {
                int kp = Math.Max(1, Mathf.RoundToInt(patch / s));
                var groups = new Dictionary<long, List<Tile>>();
                foreach (Tile t in drawn)
                {
                    long key = SiteKey(FloorDiv(t.A, kp), FloorDiv(t.B, kp), TileBody(t) + 1);
                    if (!groups.TryGetValue(key, out List<Tile> g)) groups[key] = g = new List<Tile>();
                    g.Add(t);
                }
                if (groups.Count > S.MaxColliders && patch < 1000)
                {
                    patch *= 1.5f;
                    continue;
                }
                var boxes = new List<Box>(groups.Count);
                foreach (List<Tile> g in groups.Values) boxes.Add(FitBox(g));
                return boxes.ToArray();
            }
        }

        private Box FitBox(List<Tile> group)
        {
            Vector3 mean = Vector3.zero;
            foreach (Tile t in group) mean += t.Hit;
            mean /= group.Count;
            Tile rep = group[0];
            foreach (Tile t in group)
                if ((t.Hit - mean).sqrMagnitude < (rep.Hit - mean).sqrMagnitude) rep = t;

            Vector3 fwd = rep.TileN, tu = Tangent(rep.U, fwd);
            if (tu == Vector3.zero) tu = rep.U;
            Vector3 up = Vector3.Cross(tu, fwd) * vSign, right = Vector3.Cross(up, fwd);
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            foreach (Tile t in group)
                foreach (Vector3[] seg in t.SegHull)
                    for (int layer = 0; layer < 2; layer++)
                    {
                        Vector3 p = seg[0] + t.TileN * (layer == 0 ? 0 : S.SurfaceOffset + S.Thickness) - rep.Hit;
                        var local = new Vector3(Vector3.Dot(p, right), Vector3.Dot(p, up), Vector3.Dot(p, fwd));
                        lo = Vector3.Min(lo, local);
                        hi = Vector3.Max(hi, local);
                    }
            Vector3 mid = (lo + hi) * 0.5f;
            return new Box
            {
                Center = rep.Hit + right * mid.x + up * mid.y + fwd * mid.z,
                Forward = fwd,
                Up = up,
                Size = hi - lo
            };
        }

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
