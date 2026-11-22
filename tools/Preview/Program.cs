using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AblatorDecals;
using UnityEngine;
using Color = UnityEngine.Color;

// Offline preview of ModuleAblativeDecal: loads a .craft with the real part models from GameData (via
// ModuleManager.ConfigCache), runs the same TileBuilder as the game and renders each decal to PNG.
// Usage: Preview <craft> <outDir> [key=value ...]
//        Preview --stack <gameData> partA,partB,... <decal height> <outDir> [key=value ...]   (vertical stack, decal on +X)
// Keys: width height tileSize (hexSize, default 0.1) tileGap (hexGap) thickness (0.025) maxWrap shape=Hex|Square|Brick angle
//       straightBorder unrolled (default true) separate (flap bodies separate, default true) tileLoss texture=<png> cells=4x4
//       uv=atlas|window|across textureScale spanParts seed views=front,left,...
//       side=x,y,z (--stack only: horizontal direction the decal faces, default 1,0,0)
//       views also: hinge (flap border, with control surfaces); mark (paint hull seen through slits magenta); view "yaw": yaw=<deg about v> zoom=<distance factor> at=<decal-local point>
// Prints per view the number of hull pixels seen through slits inside the tiled area (should be ~0), the largest air gap
// under a tile bottom at its outline (limit max(3 mm, 0.05 tile)) and, with control surfaces, tiles lying over a flap border.
// Bodies as in the module: each ModuleControlSurface transform (transformName, every match) is its own owner, SeparateOwner.
// Debug keys: gaps=<n> (list the n tiles with the largest border gap), leaks=1, pick=x:y, section=<decal-local x>.
// v8: cover=<CockpitCover.cfg> (cockpit cover as the module: planes, cut planes, part size, MaxWrap 180, only the parent);
//     stock=1 (Squad parts from their unpatched cfg, i.e. without ReStock); glue=part@y@azimuth;... (--stack: surface-attached
//     parts, e.g. intakes); intake=off (no intake opening filter); front= wall= frontR= (intake filter thresholds);
//     dirs=name:x,y,z[:distance[:at x,y,z]];... (views from vessel-space directions); perf=1 (warm build time, memory,
//     index size); flat=1 (the module's flat stand-in); debug: intakemark=1 (freed intake faces magenta), bare=1, meshes=1.
//     showAll=1 (render parts that are not covered, e.g. landing gear); gear glued on a covered part adds its flush faces
//     as a SeparateOwner body like the module (default .mu pose, no animation).
static class Program
{
    static string gameData;
    static readonly Dictionary<string, (Node part, string dir)> partConfigs = new Dictionary<string, (Node, string)>();
    static readonly Dictionary<string, MuModel> models = new Dictionary<string, MuModel>();
    static readonly string[] SkipShaders = { "DepthMask", "Particles", "Transparent", "Translucent", "FX" };
    static readonly string[] NotHull = { "ModuleEngines", "ModuleDecouple", "ModuleAnchoredDecoupler", "ModuleDockingNode", "ModuleProceduralFairing", "ModuleAblativeDecal" };
    static Dictionary<string, string> overrides;
    static readonly List<Tri> accTiles = new List<Tri>();

    static int Main(string[] args)
    {
        if (args[0] == "--mask") return MaskMode(args);
        if (args[0] == "--scan-windows") return ScanWindows(args[1], args[2]); // --scan-windows <gameData> <outFile>: ABLATORDECAL_WINDOWS boxes
        if (args[0] == "--mudump") // --mudump <.mu>: meshes (path, bounds, shader) and animation clips (moved transforms), debug
        {
            MuModel mu = MuModel.Load(args[1]);
            foreach (MuMesh m in mu.Meshes)
                Console.WriteLine($"mesh {string.Join("/", m.Path)} v={m.Vertices.Length} renderer={m.HasRenderer} lo={m.Vertices.Aggregate(Vector3.Min)} hi={m.Vertices.Aggregate(Vector3.Max)} shader={string.Join(",", m.MaterialIds.Select(i => i < mu.Shaders.Count ? mu.Shaders[i] : "?"))}");
            foreach (MuClip c in mu.Clips)
                Console.WriteLine($"clip {c.Name} len={c.Length} moves {string.Join(", ", c.Curves.Select(cv => cv.path + ":" + cv.prop).Distinct().Take(40))}");
            return 0;
        }
        if (args[0] == "--bake-cover") // --bake-cover <gameData> <part> <outDir> [keys]: pre-made cockpit covers, Hex and Square
        {
            foreach (string shape in new[] { "Hex", "Square" })
            {
                string tex = Path.Combine(Directory.GetCurrentDirectory(), "GameData", "AblatorDecals", "Textures", shape + "Black.png");
                var a = new List<string> { "--stack", args[1], args[2], "0", Path.Combine(args[3], "bake-" + shape), $"shape={shape}", "tileSize=0.1", "tileGap=0",
                    "thickness=0.025", "binAngle=18", "maxBins=12", $"texture={tex}", "cells=4x4", "side=0,0,1", "whole=1", "views=none", $"bake={Path.Combine(args[3], args[2] + "_" + shape + ".bin")}" };
                a.AddRange(args.Skip(4));
                partConfigs.Clear();
                maskCov = -1;
                if (Main(a.ToArray()) != 0) return 1;
                if (!(maskCov >= 0.99f)) { Console.WriteLine($"  bake check FAILED: mask coverage {maskCov * 100:F2}% < 99%"); return 1; }
            }
            return 0;
        }
        string craftText, outDir;
        bool stack = args[0] == "--stack";
        overrides = args.Skip(stack ? 5 : 2).Select(a => a.Split(new[] { '=' }, 2)).ToDictionary(a => a[0], a => a[1]);
        if (stack)
        {
            gameData = args[1];
            LoadConfigCache(Path.Combine(gameData, "ModuleManager.ConfigCache"));
            craftText = StackCraft(args[2].Split(','), Flt(args[3]), overrides.TryGetValue("side", out string side) ? Vec(side) : Vector3.right);
            outDir = args[4];
        }
        else
        {
            craftText = File.ReadAllText(args[0]);
            outDir = args[1];
            gameData = overrides.TryGetValue("gameData", out string gdo) ? gdo : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(args[0]), "..", "..", "..", "..", "GameData"));
            LoadConfigCache(Path.Combine(gameData, "ModuleManager.ConfigCache"));
        }
        Directory.CreateDirectory(outDir);

        var parts = Node.Parse(craftText).Nodes.Where(n => n.Name == "PART").Select(n => new CraftPart(n)).ToList();
        var byId = parts.ToDictionary(p => p.Id);
        foreach (CraftPart p in parts)
            foreach (string link in p.Node.All("link"))
                if (byId.TryGetValue(link, out CraftPart child)) { child.Parent = p; p.Children.Add(child); }

        Node decalCfg = partConfigs[Norm("ablativeDecal")].part.Nodes.First(n => n.Name == "MODULE" && n.Get("name") == "ModuleAblativeDecal");
        int index = 0;
        foreach (CraftPart decal in parts.Where(p => p.Name == "ablativeDecal"))
        {
            index++;
            if (overrides.TryGetValue("only", out string onlyList) && !onlyList.Split(',').Contains(index.ToString())) continue; // only=3,5: these decals
            // place=<part id>:<x,y,z>: this decal moved onto that part, where a ray from direction (vessel space) hits its hull
            if (overrides.TryGetValue("place", out string place) && byId.TryGetValue(place.Split(':')[0], out CraftPart target))
            {
                var pp = new List<Vector3>();
                var pt = new List<int>();
                foreach (var (verts, tris, _) in HullMeshes(target)) { int st = pp.Count; pp.AddRange(verts); pt.AddRange(tris.Select(i => i + st)); }
                Vector3 dir = Vec(place.Split(':')[1]).normalized, c0 = target.Xform.Pos + (place.Split(':').Length > 2 ? Vec(place.Split(':')[2]) : Vector3.zero);
                var pidx = new SurfaceIndex(pp.ToArray(), pt.ToArray(), c0, 60, 0.25f, 0.05f);
                if (pidx.Raycast(c0 + dir * 30, -dir, 30, 0.1f, out SurfaceHit ph, false))
                {
                    Vector3 nLocal = Vec(decalCfg.Get("shieldNormal") ?? "0,1,0").normalized;
                    decal.Xform = new Xform { Pos = ph.P, Rot = FromTo(nLocal, ph.FaceN), Scale = Vector3.one };
                    decal.Parent?.Children.Remove(decal);
                    decal.Parent = target;
                    target.Children.Add(decal);
                }
                else Console.WriteLine("  place: no hit");
            }
            // nudge=<m>,<deg>: the decal moved along its u and turned about its normal (DESIGN-v11: overlays must not change)
            if (overrides.TryGetValue("nudge", out string nudge))
            {
                float[] nv = nudge.Split(',').Select(Flt).ToArray();
                Vector3 nl = Vec(decalCfg.Get("shieldNormal") ?? "0,1,0").normalized;
                Vector3 upl = Mathf.Abs(Vector3.Dot(nl, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right;
                Vector3 ul = Vector3.Cross(nl, Vector3.Cross(nl, Vector3.Cross(upl, nl).normalized));
                float half = (nv.Length > 1 ? nv[1] : 0) * Mathf.Deg2Rad / 2;
                decal.Xform.Pos += decal.Xform.ApplyDir(ul.normalized * nv[0]);
                decal.Xform.Rot = decal.Xform.Rot * new Quaternion(nl.x * Mathf.Sin(half), nl.y * Mathf.Sin(half), nl.z * Mathf.Sin(half), Mathf.Cos(half));
            }
            Node mod = decal.Node.Nodes.First(n => n.Name == "MODULE" && n.Get("name") == "ModuleAblativeDecal");
            // overrides first, then the craft, then the part config; each under its v6 or v5 name
            Func<string, string, string> str = (def, keys) =>
            {
                string[] names = keys.Split('|');
                foreach (string k in names) if (overrides.TryGetValue(k, out string o)) return o;
                foreach (string k in names) if (mod.Get(k) is string m) return m;
                foreach (string k in names) if (decalCfg.Get(k) is string c) return c;
                return def;
            };
            Func<string, string, float> f = (def, keys) => Flt(str(def, keys));

            var shape = (TileShape)Enum.Parse(typeof(TileShape), str("Hex", "shape|pattern"), true);
            var s = new TileSettings
            {
                Shape = shape,
                Width = f("2", "width"), Height = f("2", "height"),
                TileSize = f("0.1", "tileSize|hexSize"),
                AngleDeg = f("0", "angle|patternAngle"),
                Gap = f("0.004", "tileGap|hexGap"),
                Thickness = f("0.025", "thickness"),
                MaxWrapDeg = f("120", "maxWrap"),
                StraightBorder = str("False", "straightBorder").Equals("true", StringComparison.OrdinalIgnoreCase),
                Unrolled = str("True", "unrolled").Equals("true", StringComparison.OrdinalIgnoreCase),
                TileLoss = f("0", "tileLoss"),
                SurfaceOffset = f("0.003", "surfaceOffset"),
                CornerBendDeg = f("35", "cornerBendAngle"), FaceBendDeg = f("60", "faceBendAngle"), CurvatureFactor = f("1", "curvatureFactor"),
                TextureScale = f("2", "textureScale|textureRepeat"),
                BinAngleDeg = f("18", "binAngle"),
                MaxBins = (int)f("12", "maxBins"),
                Seed = (int)f(index.ToString(), "seed"),
            };

            // texture: explicit png (atlas only with cells=), else the project's atlas for the shape
            string texPath = str(null, "texture");
            string cells = str(null, "cells");
            if (texPath == null)
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "GameData", "AblatorDecals", "Textures");
                string file = Path.Combine(dir, shape == TileShape.Hex ? "HexBlack.png" : "SquareBlack.png");
                if (File.Exists(file)) { texPath = file; cells = cells ?? "4x4"; }
            }
            string uvMode = str(cells != null ? "atlas" : "window", "uv");
            s.UvMode = (UvMode)Enum.Parse(typeof(UvMode), uvMode, true);
            if (cells != null)
            {
                string[] c = cells.ToLowerInvariant().Split('x', ',');
                s.AtlasCols = int.Parse(c[0].Trim());
                s.AtlasRows = int.Parse(c[1].Trim());
            }
            s.AtlasRandomRotation = str(shape == TileShape.Hex ? "True" : "False", "randomRotation").Equals("true", StringComparison.OrdinalIgnoreCase);
            Texture tex = texPath != null ? Texture.Load(texPath) : null;
            if (tex != null) s.TextureAspect = (float)tex.H / tex.W;
            // 1.6.2: glow=<emission png> glowK=<tile temperature>: every tile glows with the one vessel-wide colour of the module's
            // default ramp (UpdateGlow), times the emission texture, as in the game
            // 1.7.1: glowFlow=x,y,z (vessel-space direction of travel): each tile face by how it faces the flow (TileGlow.LocalWeight
            // between glowSkin, default 0.6 x glowK, and glowK); joints glow with TileGlow.JointGlow, as in the game
            if (str(null, "glow") is string glowFile)
            {
                glowTex = Texture.Load(glowFile);
                glowK = Flt(str("1100", "glowK"));
                glowSkin = Flt(str((0.6f * glowK).ToString(CultureInfo.InvariantCulture), "glowSkin"));
                glowFlow = str(null, "glowFlow") is string gf ? Vec(gf).normalized : Vector3.zero;
            }

            // decal frame: n = shield normal; v = the part's "up" on the hull, u = n x v so that v = u x n
            Vector3 n = Vec(decalCfg.Get("shieldNormal") ?? "0,1,0").normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right;
            Vector3 v = Vector3.Cross(n, Vector3.Cross(up, n).normalized);
            Vector3 u = Vector3.Cross(n, v);

            // cockpit cover (DESIGN-v8 A8/B2) as the module does it: cover=<CockpitCover.cfg>; planes of the parent part (part
            // space) into decal space, Width/Height from the part size, MaxWrap 180, only the parent as target
            bool cover = false, keepRcs = false; // keepRcs: M3 (1.5), RCS nozzles stay free only on cockpits whose cover entry says so
            // whole=1 (the module's default mode): the cockpit cover applies by itself unless the decal has cockpitShape = False
            string coverDefault = overrides.ContainsKey("whole") && !str("True", "cockpitShape").Equals("false", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Directory.GetCurrentDirectory(), "GameData", "AblatorDecals", "CockpitCover.cfg") : null;
            if (str(coverDefault, "cover") is string coverFile && File.Exists(coverFile))
                foreach (Node cn in Node.Parse(File.ReadAllText(coverFile)).Nodes.Where(c => c.Name.StartsWith("ABLATORDECAL_COVER") && Norm(c.Get("part") ?? "") == Norm(decal.Parent.Name)))
                {
                    cover = true;
                    keepRcs = (cn.Get("keepRcs") ?? "").Trim().ToLowerInvariant() == "true";
                    if (cn.Get("ceramicSideDeg") is string csd) s.CeramicSideDeg = Flt(csd);
                    if ((cn.Get("ceramicAll") ?? "").Trim().ToLowerInvariant() == "true") s.CeramicSideDeg = 90f; // 1.9 as the module
                    s.CeramicAxis = (cn.Get("ceramicAxis") ?? "").Trim().ToLowerInvariant() == "true";
                    var planes = new List<Vector4>();
                    var cuts = new List<Vector4>();
                    foreach (Node pn in cn.Nodes.Where(c => c.Name == "PLANE"))
                    {
                        Vector3 pn3 = decal.Xform.InverseApplyDir(decal.Parent.Xform.ApplyDir(Vec(pn.Get("normal")))).normalized;
                        Vector3 pp3 = decal.Xform.InverseApply(decal.Parent.Xform.Apply(Vec(pn.Get("point"))));
                        ((pn.Get("cut") ?? "").Trim().ToLowerInvariant() == "true" ? cuts : planes).Add(new Vector4(pn3.x, pn3.y, pn3.z, -Vector3.Dot(pn3, pp3)));
                    }
                    s.ClipPlanes = planes.ToArray();
                    s.CutPlanes = cuts.ToArray();
                    // DESIGN-v11 G2/G3 mask regions, part space -> decal space as the module does it
                    Xform pX = decal.Parent.Xform;
                    s.Regions = cn.Nodes.Where(c => c.Name == "REGION").Select(rn => new CoverRegion
                    {
                        O = decal.Xform.InverseApply(pX.Apply(Vec(rn.Get("origin") ?? "0,0,0"))),
                        A = decal.Xform.InverseApplyDir(pX.ApplyDir(Vec(rn.Get("u")))).normalized,
                        B = decal.Xform.InverseApplyDir(pX.ApplyDir(Vec(rn.Get("v")))).normalized,
                        Loops = rn.All("loop").Select(l => l.Split(',').Select(pt => pt.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).Select(xy => new Vector2(Flt(xy[0]), Flt(xy[1]))).ToArray()).ToArray(),
                        Domain = rn.All("domain").Select(dv =>
                        {
                            float[] q = dv.Split(',').Select(Flt).ToArray();
                            Vector3 dn = decal.Xform.InverseApplyDir(pX.ApplyDir(new Vector3(q[0], q[1], q[2]))).normalized, dp = decal.Xform.InverseApply(pX.Apply(new Vector3(q[3], q[4], q[5])));
                            return new Vector4(dn.x, dn.y, dn.z, -Vector3.Dot(dn, dp));
                        }).ToArray()
                    }).ToArray();
                    float size = 0;
                    foreach (var (verts, _, _) in HullMeshes(decal.Parent))
                        foreach (Vector3 q in verts) size = Mathf.Max(size, decal.Parent.Xform.InverseApply(q).magnitude);
                    // unrolled footprint: half way round and the full length fit in twice the part's radius from its origin
                    if (!overrides.ContainsKey("width")) s.Width = s.Height = 2 * size;
                    if (!overrides.ContainsKey("maxWrap")) s.MaxWrapDeg = 180;
                    break;
                }
            float reach = 0.5f * Mathf.Sqrt(s.Width * s.Width + s.Height * s.Height) + 0.5f * Mathf.Max(s.Width, s.Height) + 2 * s.TileSize + 1f;
            Vector3 center = decal.Xform.Pos;
            bool span = str("True", "spanParts").Equals("true", StringComparison.OrdinalIgnoreCase);
            // DESIGN-v10 M1: "span parts" is gone; whole part = the parent plus the flaps and intakes glued onto it
            List<CraftPart> targets = Targets(decal.Parent, span && !cover && !overrides.ContainsKey("whole"), center, reach, overrides.ContainsKey("whole") || cover);
            // DESIGN-v9 M1 "Ganzes Teil" (whole=1, unless the decal has areaLimited = True): footprint = 2 x the farthest target
            // point, SeparateOwner for every body, a cut plane at every occupied stack node (the stack face stays free)
            bool whole = !cover && overrides.ContainsKey("whole") && !str("False", "areaLimited").Equals("true", StringComparison.OrdinalIgnoreCase);
            var stackCuts = new List<Vector4>();
            s.FillHull = str("on", "fill") != "off" && (cover || TileBuilder.Mk2Hull(decal.Parent.Name));
            s.LimitedTank = !whole && !cover;
            s.TankAxis = decal.Xform.InverseApplyDir(decal.Parent.Xform.ApplyDir(Vector3.up)).normalized;
            s.TankCenter = decal.Xform.InverseApply(decal.Parent.Xform.Pos);
            // 1.8: keepHatches=0|1 (default 0 on the Mk3 crew cabin with whole=1, as the module), smoothHull=0|1|auto (default 0)
            string kh = str(whole && Norm(decal.Parent.Name).Equals("mk3CrewCabin", StringComparison.OrdinalIgnoreCase) ? "0" : "1", "keepHatches").Trim().ToLowerInvariant();
            bool keepHatches = kh == "1" || kh == "true";
            // 1.9: keepWindows=0|1 (default 1 on all parts, as the module), boxes from windows=<cfg> or the repo's WindowKeepOuts.cfg
            string kw = str("1", "keepWindows").Trim().ToLowerInvariant();
            bool keepWindows = kw == "1" || kw == "true";
            string winFile = str(Path.Combine(Directory.GetCurrentDirectory(), "GameData", "AblatorDecals", "WindowKeepOuts.cfg"), "windows");
            var windowNodes = keepWindows && File.Exists(winFile)
                ? Node.Parse(File.ReadAllText(winFile)).Nodes.Where(wn => wn.Name == "ABLATORDECAL_WINDOWS").ToList() : new List<Node>();
            string smoothMode = str("auto", "smoothHull").Trim().ToLowerInvariant(); // 1.9: default auto, as the module
            if (smoothMode == "true") smoothMode = "1";
            if (whole)
            {
                float far = 0;
                foreach (CraftPart tp in targets)
                    foreach (var (verts, _, _) in HullMeshes(tp))
                        foreach (Vector3 q in verts) far = Mathf.Max(far, (q - center).magnitude);
                s.Width = s.Height = Mathf.PI * far + 2 * s.TileSize; // 1.8: as the module (half way round a short wide tank)
                reach = 0.5f * Mathf.Sqrt(2) * s.Width + 0.5f * s.Width + 2 * s.TileSize + 1f;
                // as the module: every stack node of a target with a part on it that is no target (craft attN lines name the
                // node actually used; cargo bays attach by inward nodes "top2"/"bottom2"), plane along the node orientation
                foreach (CraftPart tp in targets)
                {
                    Node tc = partConfigs[Norm(tp.Name)].part;
                    float rs = Flt(tc.Get("rescaleFactor") ?? "1.25");
                    // 1.9 as the module: every stack node, occupied or not (flat stack faces, docking faces never tiled), except
                    // nodes joined to another target
                    var joined = new HashSet<string>();
                    var used = new HashSet<string>();
                    foreach (string attn in tp.Node.All("attN"))
                    {
                        string[] f2 = attn.Split(',');
                        used.Add(f2[0]);
                        string idPart = f2[1].Split('|')[0], otherId = idPart.Substring(0, idPart.LastIndexOf('_'));
                        if (targets.Any(t => t.Id == otherId)) joined.Add(f2[0]);
                    }
                    foreach (var nkv in tc.Values.Where(v => v.Key.StartsWith("node_stack_")).ToList())
                    {
                        if (joined.Contains(nkv.Key.Substring("node_stack_".Length))) continue;
                        float[] nd = nkv.Value.Split(',').Select(Flt).ToArray();
                        if (nd.Length < 6) continue;
                        Vector3 nPos = new Vector3(nd[0], nd[1], nd[2]);
                        Vector3 nDir = new Vector3(nd[3], nd[4], nd[5]).normalized;
                        string nodeId = nkv.Key.Substring("node_stack_".Length);
                        if (!used.Contains(nodeId) && !TileBuilder.FreeNodeCuts(nodeId, nDir)) continue; // 1.9.1 as the module
                        if (Vector3.Dot(nDir, nPos) < 0 && !overrides.ContainsKey("rawNodes"))
                        {
                            nDir = -nDir;
                            float outer = Vector3.Dot(nDir, nPos);
                            foreach (var kv in tc.Values.Where(v => v.Key.StartsWith("node_stack_")))
                            {
                                float[] on = kv.Value.Split(',').Select(Flt).ToArray();
                                if (on.Length < 6) continue;
                                Vector3 od = new Vector3(on[3], on[4], on[5]).normalized;
                                if (Vector3.Dot(od, nDir) > 0.9f)
                                {
                                    Vector3 op = new Vector3(on[0], on[1], on[2]);
                                    float proj = Vector3.Dot(nDir, op);
                                    if (proj > outer) { outer = proj; nPos = op; }
                                }
                            }
                        }
                        Vector3 nd3 = decal.Xform.InverseApplyDir(tp.Xform.ApplyDir(nDir)).normalized;
                        Vector3 np3 = decal.Xform.InverseApply(tp.Xform.Apply(nPos * rs)) + nd3 * 0.0005f;
                        stackCuts.Add(new Vector4(nd3.x, nd3.y, nd3.z, -Vector3.Dot(nd3, np3)));
                    }
                }
                if (stackCuts.Count > 0) s.CutPlanes = stackCuts.ToArray();
            }

            // bodies as in the module: one per part, plus one per control surface flap transform and bay door (SeparateOwner).
            // DESIGN-v11 overlay (overlay=off: as 1.4): glued parts (with their flaps), the parent's flaps and doors and landing gear
            // are objects of their own: obstacles in the main build, each tiled by its own overlay in its own space (groups)
            bool overlayOn = !str("on", "overlay").Equals("off", StringComparison.OrdinalIgnoreCase);
            var pos = new List<Vector3>();
            var tri = new List<int>();
            var owner = new List<int>();
            var bodyFlap = new List<bool>();
            var bodyName = new List<string>();
            var bodyGroup = new List<int>();                       // -1: tiled by the main build
            var gearBody = new HashSet<int>();                      // retracted gear: the main cover runs on under it
            var portOf = new Dictionary<int, int>();                // 1.7.1: bodies of a shielded port -> its target index
            var doorBody = new HashSet<int>();                      // bay doors: inner side is no skin (TileSettings.Inward)
            var vpos = new List<Vector3>();                         // the same triangles in vessel space
            var groups = new List<(string name, Xform frame)>();
            var groupSkin = new List<bool>();                      // 1.6: gear and intakes get a closed skin (TileSettings.Skin)
            bool InAttachmentArea(CraftPart p)
            {
                if (!overrides.ContainsKey("whole") || whole || cover) return true;
                Node cfg = partConfigs[Norm(p.Name)].part;
                string[] attach = (cfg.Get("node_attach") ?? "0,0,0").Split(',');
                Vector3 point = new Vector3(Flt(attach[0]), Flt(attach[1]), Flt(attach[2])) * Flt(cfg.Get("rescaleFactor") ?? "1.25");
                Vector3 q = decal.Xform.InverseApply(p.Xform.Apply(point));
                return Mathf.Abs(Vector3.Dot(q, u)) <= s.Width / 2 + s.TileSize && Mathf.Abs(Vector3.Dot(q, v)) <= s.Height / 2 + s.TileSize;
            }
            int parentMoving = -1;
            var doorSkinGroup = new HashSet<int>(); // 1.9 as the module: radial service bay doors get a closed outer skin
            for (int ti = 0; ti < targets.Count; ti++)
            {
                int main = bodyFlap.Count;
                bool glued = overlayOn && ti > 0 && targets[ti].Parent == decal.Parent && (Glued(targets[ti]) || ShieldedPort(targets[ti]));
                int group = -1;
                if (glued && !InAttachmentArea(targets[ti])) group = -2;
                else if (glued)
                {
                    group = groups.Count;
                    groups.Add((targets[ti].Name, targets[ti].Xform));
                    groupSkin.Add(ShieldedPort(targets[ti]) || partConfigs[Norm(targets[ti].Name)].part.Nodes.Any(m => m.Name == "MODULE" && m.Get("name") == "ModuleResourceIntake"));
                }
                int shieldGroup = -1; // 1.7: a shielded port's base gets the closed skin, its shield doors real tiles (an object of their own)
                bodyFlap.Add(false);
                bodyName.Add(targets[ti].Name);
                bodyGroup.Add(group);
                if (ShieldedPort(targets[ti])) portOf[main] = ti;
                var flapIds = new Dictionary<string, int>();
                // 1.9 as the module: a radial service bay (cargo bay with >= 2 doors, not Mk2/Mk3) keeps only its doors' outer shell
                var hullMeshes = HullMeshes(targets[ti]).ToList();
                bool isBay = partConfigs[Norm(targets[ti].Name)].part.Nodes.Any(m => m.Name == "MODULE" && m.Get("name") == "ModuleCargoBay");
                bool IsDoor(string fl) => fl != null && isBay && !FlapNames(targets[ti]).Any(fn => fl.Contains(fn));
                bool radialBay = isBay && hullMeshes.Select(hm => hm.flap).Where(IsDoor).Distinct().Count() >= 2 &&
                                 !targets[ti].Name.StartsWith("mk2", StringComparison.OrdinalIgnoreCase) && !targets[ti].Name.StartsWith("mk3", StringComparison.OrdinalIgnoreCase);
                float maxDoorR = 0;
                if (radialBay)
                    foreach (var hm in hullMeshes.Where(hm => IsDoor(hm.flap)))
                        foreach (Vector3 q in hm.verts) { Vector3 l = targets[ti].Xform.InverseApply(q); maxDoorR = Mathf.Max(maxDoorR, Mathf.Sqrt(l.x * l.x + l.z * l.z)); }
                int doorDropped = 0;
                foreach (var (verts, trisAll, flap) in hullMeshes)
                {
                    int[] tris = trisAll;
                    if (radialBay && (flap == null || IsDoor(flap))) // 1.9 as the module: doors and frame keep only their outer shell
                    {
                        var kept = new List<int>();
                        for (int i = 0; i + 2 < trisAll.Length; i += 3)
                        {
                            CraftPart bp = targets[ti];
                            if (TileBuilder.DoorOuterFace(bp.Xform.InverseApply(verts[trisAll[i]]), bp.Xform.InverseApply(verts[trisAll[i + 1]]), bp.Xform.InverseApply(verts[trisAll[i + 2]]), maxDoorR))
                            { kept.Add(trisAll[i]); kept.Add(trisAll[i + 1]); kept.Add(trisAll[i + 2]); }
                            else doorDropped++;
                        }
                        tris = kept.ToArray();
                        if (tris.Length == 0) continue;
                    }
                    int bi = main;
                    if (flap != null && !flapIds.TryGetValue(flap, out bi))
                    {
                        flapIds[flap] = bi = bodyFlap.Count;
                        bodyFlap.Add(true);
                        bodyName.Add(targets[ti].Name + "/" + flap);
                        int fg = group;
                        if (group >= 0 && ShieldedPort(targets[ti]))
                        {
                            if (shieldGroup < 0) { shieldGroup = groups.Count; groups.Add((targets[ti].Name + "/shield", targets[ti].Xform)); groupSkin.Add(false); }
                            fg = shieldGroup;
                        }
                        if (overlayOn && ti == 0)
                        {
                            if (parentMoving < 0) { parentMoving = groups.Count; groups.Add((targets[0].Name + "/moving", targets[0].Xform)); groupSkin.Add(false); }
                            fg = parentMoving;
                        }
                        bodyGroup.Add(fg);
                        if (ShieldedPort(targets[ti])) portOf[bi] = ti;
                        if ((ShieldedPort(targets[ti]) || partConfigs[Norm(targets[ti].Name)].part.Nodes.Any(m => m.Name == "MODULE" && m.Get("name") == "ModuleCargoBay")) && !FlapNames(targets[ti]).Any(fl => flap.Contains(fl))) doorBody.Add(bi);
                        if (radialBay && IsDoor(flap) && fg >= 0) doorSkinGroup.Add(fg);
                    }
                    int start = pos.Count;
                    pos.AddRange(verts.Select(p => decal.Xform.InverseApply(p)));
                    vpos.AddRange(verts);
                    foreach (int i in tris) tri.Add(start + i);
                    for (int k = 0; k < tris.Length / 3; k++) owner.Add(bi);
                }
                if (radialBay) Console.WriteLine($"  radial bay {targets[ti].Name}: door radius {maxDoorR:F3} m, inner door faces dropped {doorDropped}");
                if (radialBay && ti == 0 && !str("on", "bayFrameSkin").Equals("off", StringComparison.OrdinalIgnoreCase)) s.Skin = true; // 1.9 as the module: closed frame skin
            }
            if (str(null, "whichBody") is string wb) // debug: bodies of hull triangles within 1 cm of a decal-local point
                for (int i = 0; i < tri.Count; i += 3)
                {
                    Vector3 cpt = SurfaceIndex.ClosestPointOnTriangle(Vec(wb), pos[tri[i]], pos[tri[i + 1]], pos[tri[i + 2]], out _, out _, out _);
                    if ((cpt - Vec(wb)).magnitude < 0.01f) Console.WriteLine($"   body {bodyName[owner[i / 3]]} group {bodyGroup[owner[i / 3]]} n {Vector3.Cross(pos[tri[i + 1]] - pos[tri[i]], pos[tri[i + 2]] - pos[tri[i]]).normalized}");
                }
            if (overrides.ContainsKey("section")) // debug: hull cut by the decal-local plane x = section, as (y, z) points
            {
                float sx = Flt(overrides["section"]);
                var pts = new List<Vector2>();
                for (int i = 0; i < tri.Count; i += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        Vector3 a = pos[tri[i + e]], b = pos[tri[i + (e + 1) % 3]];
                        if ((a.x - sx) * (b.x - sx) >= 0) continue;
                        Vector3 q = Vector3.Lerp(a, b, (sx - a.x) / (b.x - a.x));
                        if (q.magnitude < 2) pts.Add(new Vector2(q.y, q.z));
                    }
                Console.WriteLine("  section " + string.Join(" ", pts.Distinct().OrderBy(q => Mathf.Atan2(q.y + 1, q.x)).Select(q => $"({q.x:F3},{q.y:F3})")));
            }
            // landing gear glued on a covered part, as the module (AddGear, 1.4): every mesh (skinned too) in the RETRACTED pose of
            // its ModuleWheelDeployment clip, each a body of its own; dropped are faces towards or 2 cm into the hull and faces hidden
            // by the gear's own skin (a ray along the normal hits it within 0.5 m). v11: each gear is an overlay object
            var gearScene = new List<Tri>();
            if (pos.Count > 0)
            {
                var hullIndex = new SurfaceIndex(vpos.ToArray(), tri.ToArray(), decal.Xform.Pos, reach, 0.25f, Mathf.Clamp(s.TileSize * 0.4f, 0.02f, 0.3f));
                foreach (CraftPart gp in parts.Where(q => !targets.Contains(q) && q.Parent != null && targets.Contains(q.Parent) && !q.Stack &&
                             partConfigs.TryGetValue(Norm(q.Name), out var gc) && gc.part.Nodes.Any(m => m.Name == "MODULE" && (m.Get("name") ?? "").StartsWith("ModuleWheel"))))
                {
                    if (!InAttachmentArea(gp)) continue;
                    Node dep = partConfigs[Norm(gp.Name)].part.Nodes.FirstOrDefault(m => m.Name == "MODULE" && m.Get("name") == "ModuleWheelDeployment");
                    string clip = dep?.Get("animationStateName");
                    float retracted = 1 - (dep?.Get("deployedPosition") is string dp ? Flt(dp) : 1);
                    var pieces = new List<(string name, Vector3[] v, int[] t)>();
                    foreach (var (mu, w) in PartModels(gp))
                    {
                        Xform[] pose = mu.Pose(clip, retracted);
                        foreach (MuMesh m in mu.Meshes)
                        {
                            if (!m.HasRenderer || m.Vertices == null || string.Equals(m.Name, "flare", StringComparison.OrdinalIgnoreCase)) continue;
                            Vector3[] gv = mu.Posed(m, pose).Select(q => w.Apply(q)).ToArray();
                            bool mir = w.Mirrored ^ (pose[m.Node].Scale.x * pose[m.Node].Scale.y * pose[m.Node].Scale.z < 0 && !m.Skinned);
                            var t = new List<int>();
                            for (int slot = 0; slot < m.Submeshes.Count; slot++)
                            {
                                int mat = slot < m.MaterialIds.Length ? m.MaterialIds[slot] : -1;
                                if (mat >= 0 && mat < mu.Shaders.Count && SkipShaders.Any(mu.Shaders[mat].Contains)) continue;
                                int[] sm = m.Submeshes[slot];
                                for (int i = 0; i + 2 < sm.Length; i += 3) { t.Add(sm[i]); t.Add(mir ? sm[i + 2] : sm[i + 1]); t.Add(mir ? sm[i + 1] : sm[i + 2]); }
                            }
                            pieces.Add((gp.Name + "/" + m.Name, gv, t.ToArray()));
                        }
                    }
                    var occP = new List<Vector3>();
                    var occT = new List<int>();
                    foreach (var pc in pieces) { int o = occP.Count; occP.AddRange(pc.v); occT.AddRange(pc.t.Select(k => k + o)); }
                    if (occT.Count == 0) continue;
                    for (int i = 0; i < occT.Count; i += 3) gearScene.Add(new Tri { A = occP[occT[i]], B = occP[occT[i + 1]], C = occP[occT[i + 2]], Kind = 0 });
                    var own = new SurfaceIndex(occP.ToArray(), occT.ToArray(), gp.Xform.Pos, 20f, 0.1f, 0.02f);
                    {
                        // 1.6.2 as the module (GearOnDecalSide): gear on the far side of the part (a wing's other face) is not this decal's
                        string[] na = (partConfigs[Norm(gp.Name)].part.Get("node_attach") ?? "0,0,0,0,-1,0").Split(',');
                        Vector3 gOut = -gp.Xform.ApplyDir(new Vector3(Flt(na[3]), Flt(na[4]), Flt(na[5]))).normalized;
                        if (overrides.ContainsKey("gearDebug")) Console.WriteLine($"  gear side dot {Vector3.Dot(gOut, decal.Xform.ApplyDir(n).normalized):F2}");
                        if (Vector3.Dot(gOut, decal.Xform.ApplyDir(n).normalized) < -0.3f) { Console.WriteLine($"  gear {gp.Name}: on the far side, not this decal's"); continue; }
                    }
                    int gGroup = overlayOn ? groups.Count : -1, faces = 0, gBodies = 0;
                    if (overlayOn) { groups.Add((gp.Name, gp.Xform)); groupSkin.Add(true); }
                    Node gearConfig = partConfigs[Norm(gp.Name)].part;
                    string[] mount = (gearConfig.Get("node_attach") ?? "0,0,0,0,-1,0").Split(',');
                    Vector3 attach = gp.Xform.Apply(new Vector3(Flt(mount[0]),Flt(mount[1]),Flt(mount[2])) * Flt(gearConfig.Get("rescaleFactor") ?? "1.25"));
                    Vector3 outside = -gp.Xform.ApplyDir(new Vector3(Flt(mount[3]),Flt(mount[4]),Flt(mount[5]))).normalized;
                    if (hullIndex.Closest(attach, 0.6f, outside, 0.7f, out SurfaceHit baseFace)) { attach = baseFace.P; outside = baseFace.FaceN; }
                    foreach (var pc in pieces)
                    {
                        int gb = bodyFlap.Count, before = tri.Count;
                        for (int i = 0; i + 2 < pc.t.Length; i += 3)
                        {
                            Vector3 a = pc.v[pc.t[i]], b = pc.v[pc.t[i + 1]], c = pc.v[pc.t[i + 2]];
                            Vector3 fn = Vector3.Cross(b - a, c - a);
                            if (fn.sqrMagnitude < 1e-16f) continue;
                            fn.Normalize();
                            Vector3 ctr = (a + b + c) / 3;
                            if (Mathf.Max(Vector3.Dot(a-attach,outside), Mathf.Max(Vector3.Dot(b-attach,outside), Vector3.Dot(c-attach,outside))) < -0.012f || Vector3.Dot(fn,outside) < -0.45f) continue; // 1.6.2: any corner out, as the module
                            if (own.Raycast(ctr + fn * 0.002f, fn, 0.5f, -1f, out _, false)) continue;
                            int st = pos.Count;
                            vpos.Add(a); vpos.Add(b); vpos.Add(c);
                            pos.Add(decal.Xform.InverseApply(a)); pos.Add(decal.Xform.InverseApply(b)); pos.Add(decal.Xform.InverseApply(c));
                            tri.Add(st); tri.Add(st + 1); tri.Add(st + 2);
                            owner.Add(gb);
                        }
                        if (tri.Count == before) continue;
                        bodyFlap.Add(true); bodyName.Add(pc.name); bodyGroup.Add(gGroup); gearBody.Add(gb);
                        faces += (tri.Count - before) / 3; gBodies++;
                    }
                    Console.WriteLine($"  gear {gp.Name}: clip={clip ?? "none"}@{retracted} retracted faces={faces} bodies={gBodies}");
                }
            }
            // DESIGN-v10 M4/G4 keep-out boxes as the module: every RCS thruster transform of a target or of a part glued onto one
            // (cube of rcsKeepOut half size along the thruster's axes) and every collider tagged "Airlock" on a target (+5 cm)
            if (!str("on", "keepOut").Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                var keep = new List<Vector3>();
                float rcsR = Flt(str("0.12", "rcsKeepOut"));
                foreach (CraftPart tp in targets)
                    foreach (CraftPart rp in new[] { tp }.Concat(tp.Children.Where(ch => !ch.Stack)))
                    {
                        if (!partConfigs.TryGetValue(Norm(rp.Name), out var rc)) continue;
                        var thr = new HashSet<string>(rc.part.Nodes.Where(m => m.Name == "MODULE" && (m.Get("name") ?? "").StartsWith("ModuleRCS")).Select(m => m.Get("thrusterTransformName") ?? "RCSthruster"));
                        foreach (var (mu, w) in PartModels(rp))
                        {
                            foreach (var (tn, tw) in mu.Transforms)
                            {
                                if (!thr.Contains(tn) || !keepRcs) continue;
                                Xform x = w.Then(tw);
                                keep.Add(decal.Xform.InverseApply(x.Apply(Vector3.zero)));
                                foreach (Vector3 ax in new[] { Vector3.right, Vector3.up, Vector3.forward }) keep.Add(decal.Xform.InverseApplyDir(x.ApplyDir(ax)).normalized * rcsR);
                            }
                            if (rp != tp || !keepHatches) continue;
                            foreach (var (aw, lo, hi) in mu.Airlocks)
                            {
                                Xform x = w.Then(aw);
                                keep.Add(decal.Xform.InverseApply(x.Apply((lo + hi) / 2)));
                                Vector3 hh = TileBuilder.HatchKeepHalf(new Vector3((hi - lo).x / 2 * Mathf.Abs(x.Scale.x), (hi - lo).y / 2 * Mathf.Abs(x.Scale.y), (hi - lo).z / 2 * Mathf.Abs(x.Scale.z)));
                                for (int k = 0; k < 3; k++)
                                {
                                    Vector3 ax = k == 0 ? Vector3.right : k == 1 ? Vector3.up : Vector3.forward;
                                    keep.Add(decal.Xform.InverseApplyDir(x.ApplyDir(ax)).normalized * hh[k]);
                                }
                            }
                        }
                    }
                int wins = 0;
                foreach (CraftPart tp in targets) // 1.9 as the module: registered windows (part space), padded by TileBuilder.WindowKeepHalf
                    foreach (Node wn in windowNodes.Where(wn => Norm(wn.Get("part") ?? "") == Norm(tp.Name)))
                        foreach (Node w in wn.Nodes.Where(x => x.Name == "WINDOW"))
                        {
                            Vector3 half = TileBuilder.WindowKeepHalf(Vec(w.Get("size")) / 2);
                            keep.Add(decal.Xform.InverseApply(tp.Xform.Apply(Vec(w.Get("center")))));
                            for (int k = 0; k < 3; k++)
                            {
                                Vector3 ax = k == 0 ? Vector3.right : k == 1 ? Vector3.up : Vector3.forward;
                                keep.Add(decal.Xform.InverseApplyDir(tp.Xform.ApplyDir(ax)).normalized * half[k]);
                            }
                            wins++;
                        }
                if (wins > 0) Console.WriteLine($"  window keep-outs {wins} ({winFile})");
                if (keep.Count > 0) { s.KeepOut = keep.ToArray(); Console.WriteLine($"  keep-out boxes {keep.Count / 4}" + (overrides.ContainsKey("keepList") ? ": " + string.Join(" ", Enumerable.Range(0, keep.Count / 4).Select(i => decal.Xform.Apply(keep[4 * i])).Select(q => $"({q.x:F2},{q.y:F2},{q.z:F2})").Distinct()) : "")); }
            }
            if (bodyFlap.Contains(true) && str("True", "separate").Equals("true", StringComparison.OrdinalIgnoreCase)) s.SeparateOwner = bodyFlap.ToArray();
            if (whole) s.SeparateOwner = bodyFlap.Select(_ => true).ToArray();
            if (bodyGroup.Any(g => g != -1))
            {
                s.Obstacle = bodyGroup.Select(g => g != -1).ToArray();
                s.PassUnder = Enumerable.Range(0, bodyGroup.Count).Select(b => gearBody.Contains(b)).ToArray();
            }
            if (overrides.ContainsKey("flat")) // the module's stand-in when nothing is under the decal: a quad of the decal's size
            {
                Vector3 hu = u * (s.Width / 2 + s.TileSize), hv = v * (s.Height / 2 + s.TileSize);
                pos = new List<Vector3> { -hu - hv, hu - hv, hu + hv, -hu + hv };
                tri = Vector3.Dot(Vector3.Cross(pos[1] - pos[0], pos[2] - pos[0]), n) >= 0 ? new List<int> { 0, 1, 2, 0, 2, 3 } : new List<int> { 0, 2, 1, 0, 3, 2 };
                owner = new List<int> { 0, 0 };
            }
            long mem0 = GC.GetTotalMemory(true);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // 1.8 smooth hull, as ModuleAblativeDecal.Work: 1 = forced fit, auto = only round parts
            // as the module: auto only without a cockpit cover, on a parent without flaps/doors or inflatables
            if (smoothMode == "auto" && (cover || bodyName.Any(nm => nm.StartsWith(targets[0].Name + "/")) ||
                partConfigs[Norm(targets[0].Name)].part.Nodes.Any(m => m.Name == "MODULE" && ((m.Get("name") ?? "").Contains("Centrifuge") || (m.Get("name") ?? "").Contains("DeployableHabitat")))))
                smoothMode = "0";
            SurfaceIndex.SmoothHullResult smooth = smoothMode == "1" || smoothMode == "auto"
                ? SurfaceIndex.BuildSmoothHull(pos.ToArray(), tri.ToArray(), owner.ToArray(), 0, s.TankAxis, s.TankCenter, smoothMode == "1")
                : null;
            if (smooth != null) { pos = smooth.Pos.ToList(); tri = smooth.Tri.ToList(); owner = smooth.Owner.ToList(); s.SmoothAxisLock = true; s.FillHull = str("on", "fill") != "off"; } // 1.9 as the module: dark joint bed
            if (smoothMode != "0") Console.WriteLine($"  smooth hull ({smoothMode}): {(smooth != null ? $"on, {smooth.Radius.Length} rings, r {smooth.Radius.Min():F3}..{smooth.Radius.Max():F3} m, {smooth.ProtrusionTri.Length / 3} protrusion tris" : "off (not round / too small)")}");
            var surface = new SurfaceIndex(pos.ToArray(), tri.ToArray(), Vector3.zero, reach, Mathf.Max(s.TileSize * 0.5f, 0.05f), Mathf.Clamp(s.TileSize * 0.4f, 0.02f, 0.3f), owner.ToArray());
            long indexMs = watch.ElapsedMilliseconds;
            var outlines = new List<TileOutline>();
            Dl = decal.Xform.InverseApply;
            TileResult r = TileBuilder.Build(surface, s, Vector3.zero, n, u, v, outlines);
            // 1.8 as the module: decal glued onto the parent's own door (no hull under it closed): tile from the nearest face turned the
            // decal's way; the parent's moving parts are tiled even where the cover does not reach them
            bool doorFallback = !(r.OnSurface && r.TileCount > 0) && parentMoving >= 0;
            if (doorFallback && !s.LimitedTank)
            {
                float best = 9f;
                Vector3 altOrigin = Vector3.zero;
                bool found = false;
                for (int ft = 0; ft < owner.Count; ft++)
                {
                    int o = owner[ft];
                    if (s.Obstacle != null && o >= 0 && o < s.Obstacle.Length && s.Obstacle[o]) continue;
                    Vector3 a = pos[tri[3 * ft]], b = pos[tri[3 * ft + 1]], c = pos[tri[3 * ft + 2]];
                    Vector3 fn = Vector3.Cross(b - a, c - a);
                    if (fn.sqrMagnitude < 1e-16f || Vector3.Dot(fn.normalized, n) <= 0.35f) continue;
                    Vector3 mid = (a + b + c) / 3;
                    if (mid.sqrMagnitude >= best) continue;
                    best = mid.sqrMagnitude; altOrigin = mid; found = true;
                }
                if (found) { outlines.Clear(); r = TileBuilder.Build(surface, s, altOrigin, n, u, v, outlines); Console.WriteLine($"  door fallback: origin {altOrigin}, {r.TileCount} tiles"); }
            }
            if (smooth != null && r.OnSurface && r.TileCount > 0 && smooth.ProtrusionTri.Length > 0)
            {
                Vector4[] skinCuts = s.CutPlanes?.Select(c => c.w > 0 ? -c : c).ToArray();
                PartTiles skin = TileBuilder.BuildVisualSkin(smooth.ProtrusionPos, smooth.ProtrusionTri, 0, s, n, skinCuts, Vector3.zero, u, v);
                if (skin != null) r.Parts = r.Parts.Append(skin).ToArray();
                Console.WriteLine($"  protrusion skin: {(skin != null ? skin.BinTriangles.Sum(b => b.Length) / 3 : 0)} tris wrapped");
            }
            watch.Stop();
            if (str(null, "bake") is string bakeFile) // pre-made cover (DESIGN-v11): the tiles in the parent's part space
            {
                Xform px = decal.Parent.Xform, dx = decal.Xform;
                Quaternion q = new Quaternion(-px.Rot.x, -px.Rot.y, -px.Rot.z, px.Rot.w) * dx.Rot;
                Vector3 ex = q * Vector3.right, ey = q * Vector3.up, ez = q * Vector3.forward, t = px.InverseApply(dx.Pos);
                var m = Matrix4x4.identity;
                m.m00 = ex.x; m.m10 = ex.y; m.m20 = ex.z; m.m01 = ey.x; m.m11 = ey.y; m.m21 = ey.z; m.m02 = ez.x; m.m12 = ez.y; m.m22 = ez.z;
                m.m03 = t.x; m.m13 = t.y; m.m23 = t.z;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(bakeFile)));
                using (var fs = File.Create(bakeFile)) CoverMesh.Write(fs, CoverMesh.Transform(r, m));
                Console.WriteLine($"  baked {bakeFile}: {r.TileCount} tiles, {r.CoveredArea:F2} m2, {new FileInfo(bakeFile).Length / 1024} KiB");
            }
            if (str(null, "loadBaked") is string lb) // show a pre-made cover file as the module loads it (part space -> decal space)
            {
                Xform px = decal.Parent.Xform, dx = decal.Xform;
                Quaternion q = new Quaternion(-dx.Rot.x, -dx.Rot.y, -dx.Rot.z, dx.Rot.w) * px.Rot;
                Vector3 ex = q * Vector3.right, ey = q * Vector3.up, ez = q * Vector3.forward, t = dx.InverseApply(px.Pos);
                var m = Matrix4x4.identity;
                m.m00 = ex.x; m.m10 = ex.y; m.m20 = ex.z; m.m01 = ey.x; m.m11 = ey.y; m.m21 = ey.z; m.m02 = ez.x; m.m12 = ez.y; m.m22 = ez.z;
                m.m03 = t.x; m.m13 = t.y; m.m23 = t.z;
                r = CoverMesh.Transform(CoverMesh.Read(File.ReadAllBytes(lb)), m);
                outlines.Clear();
                Console.WriteLine($"  loaded {lb}: {r.TileCount} tiles, {r.CoveredArea:F2} m2");
            }
            // DESIGN-v11 overlays: each object in its own space (frame = its part transform), its bodies tiled, the main bodies
            // near it as obstacles; nothing here reads the decal's pose
            var overlays = new List<(string name, Xform frame, TileResult r, List<TileOutline> o, Vector3[] p, int[] t, int[] w, int tileable, bool[] inw)>();
            var mainBodies = Enumerable.Range(0, bodyGroup.Count).Where(b => bodyGroup[b] == -1).ToList();
            for (int g = 0; g < groups.Count; g++)
            {
                Xform fr = groups[g].frame;
                var mine = Enumerable.Range(0, bodyGroup.Count).Where(b => bodyGroup[b] == g).ToList();
                var op = new List<Vector3>();
                var ot = new List<int>();
                var ow = new List<int>();
                Vector3 lo = Vector3.one * 1e9f, hi = -lo;
                for (int i = 0; i < tri.Count; i += 3)
                {
                    int bi = mine.IndexOf(owner[i / 3]);
                    if (bi < 0) continue;
                    for (int k = 0; k < 3; k++) { Vector3 q = fr.InverseApply(vpos[tri[i + k]]); op.Add(q); ot.Add(op.Count - 1); lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q); }
                    ow.Add(bi);
                }
                if (ot.Count == 0) continue;
                int tileTris = ot.Count;
                lo -= Vector3.one; hi += Vector3.one;
                for (int i = 0; i < tri.Count; i += 3) // obstacles: the main bodies' triangles within 1 m of the object
                {
                    int ob = owner[i / 3];
                    // 1.7.1 as the module: a shielded port's base and doors see each other as obstacles (nothing under the hood)
                    if (!mainBodies.Contains(ob) && !(portOf.TryGetValue(ob, out int pt) && portOf.TryGetValue(mine[0], out int pm) && pt == pm && bodyGroup[ob] != g)) continue;
                    Vector3 a = fr.InverseApply(vpos[tri[i]]), b = fr.InverseApply(vpos[tri[i + 1]]), c = fr.InverseApply(vpos[tri[i + 2]]);
                    Vector3 tl = Vector3.Min(a, Vector3.Min(b, c)), th = Vector3.Max(a, Vector3.Max(b, c));
                    if (tl.x > hi.x || tl.y > hi.y || tl.z > hi.z || th.x < lo.x || th.y < lo.y || th.z < lo.z) continue;
                    op.Add(a); op.Add(b); op.Add(c);
                    ot.Add(op.Count - 3); ot.Add(op.Count - 2); ot.Add(op.Count - 1);
                    ow.Add(mine.Count);
                }
                var obst = Enumerable.Range(0, mine.Count + 1).Select(k => k == mine.Count).ToArray();
                var oo = new List<TileOutline>();
                var ow0 = System.Diagnostics.Stopwatch.StartNew();
                var inw = Enumerable.Range(0, mine.Count + 1).Select(k => k < mine.Count && doorBody.Contains(mine[k])).ToArray();
                TileSettings os = TileBuilder.OverlaySettings(s);
                // 1.6 as the module: an object the main cover does not reach stays bare
                var foot = new List<Vector3>();
                for (int i = 0; i < tileTris; i++) foot.Add(decal.Xform.InverseApply(fr.Apply(op[ot[i]])));
                if (!cover && !overrides.ContainsKey("ovAll") && !(doorFallback && g == parentMoving) && !TileBuilder.CoverReaches(r, foot, 2 * s.TileSize + 0.05f)) { Console.WriteLine($"  overlay {groups[g].name}: not reached by the cover, bare"); continue; }
                if (doorSkinGroup.Contains(g)) // 1.9 as the module: radial service bay doors, closed outer skin within Max wrap and cuts
                {
                    os.Skin = true;
                    os.WrapOwner = obst.Select(x => !x).ToArray();
                    os.WrapN = fr.InverseApplyDir(decal.Xform.ApplyDir(n)).normalized;
                    os.MaxWrapDeg = s.MaxWrapDeg;
                    if (!cover && s.CutPlanes != null)
                        os.CutPlanes = s.CutPlanes.Select(c =>
                        {
                            Vector3 cn = new Vector3(c.x, c.y, c.z), cp = -c.w * cn;
                            Vector3 on = fr.InverseApplyDir(decal.Xform.ApplyDir(cn)).normalized, op2 = fr.InverseApply(decal.Xform.Apply(cp));
                            return new Vector4(on.x, on.y, on.z, -Vector3.Dot(on, op2));
                        }).ToArray();
                }
                else if (groupSkin[g]) { os.WrapN = fr.InverseApplyDir(decal.Xform.ApplyDir(n)).normalized; os.Skin = true; os.GearSkin = mine.Any(gearBody.Contains); if (os.GearSkin) { os.WrapOwner = obst.Select(x => !x).ToArray(); os.MaxWrapDeg = Flt(str("95", "gearWrap")); } } // gear, intakes: closed skin
                else // doors, ramps, flaps, fins, winglets follow the decal's Max wrap and height (its turn only), as the module
                {
                    os.Skin = !inw.Any(x => x);
                    os.WrapOwner = obst.Select(x => !x).ToArray();
                    os.WrapN = fr.InverseApplyDir(decal.Xform.ApplyDir(n)).normalized;
                    os.MaxWrapDeg = inw.Any(x => x) ? s.MaxWrapDeg : 150f;
                    os.FillHull = groups[g].name.EndsWith("/shield"); // 1.7.2 as the module: the port's shield tip closed by the rim fill
                    // the stack faces as in the module, decal -> object space; 1.7.1: doors only (a winglet over the seam to the next part is
                    // tiled on both sides of it)
                    if (!cover && s.CutPlanes != null && inw.Any(x => x))
                        os.CutPlanes = s.CutPlanes.Select(c =>
                        {
                            Vector3 cn = new Vector3(c.x, c.y, c.z), cp = -c.w * cn;
                            Vector3 on = fr.InverseApplyDir(decal.Xform.ApplyDir(cn)).normalized, op2 = fr.InverseApply(decal.Xform.Apply(cp));
                            return new Vector4(on.x, on.y, on.z, -Vector3.Dot(on, op2));
                        }).ToArray();
                }
                TileResult orr = TileBuilder.BuildOverlay(op.ToArray(), ot.ToArray(), ow.ToArray(), obst, inw, os, oo);
                if (os.WrapOwner != null && !inw.Any(x => x) && oo.Count > 0)
                    Console.WriteLine($"  {groups[g].name}: {oo.Count(t => Vector3.Dot(t.N, os.WrapN) < -0.7f)} tiles facing the opposite side");
                overlays.Add((groups[g].name + (os.Skin ? " skin" : "") + $" ({ow0.ElapsedMilliseconds} ms)", fr, orr, oo, op.ToArray(), ot.ToArray(), ow.ToArray(), tileTris, inw));
            }
            if (overrides.ContainsKey("perf"))
            {
                long mem1 = GC.GetTotalMemory(false);
                var w2 = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 3; i++) TileBuilder.Build(new SurfaceIndex(pos.ToArray(), tri.ToArray(), Vector3.zero, reach, Mathf.Max(s.TileSize * 0.5f, 0.05f), Mathf.Clamp(s.TileSize * 0.4f, 0.02f, 0.3f), owner.ToArray()), s, Vector3.zero, n, u, v);
                Console.WriteLine($"  perf: {s.Width}x{s.Height} m, index {surface.TriangleCount} tris {surface.CellCount} cells, build+index {w2.ElapsedMilliseconds / 3} ms (warm), allocated ~{(mem1 - mem0) / 1048576.0:F1} MB, tiles {r.TileCount}, vertices {r.Parts.Sum(p => p.Vertices.Length)}");
            }
            int vertCount = r.Parts.Sum(p => p.Vertices.Length);
            string label = $"decal{index} on {decal.Parent.Name}";
            Console.WriteLine($"{label}: parts=[{string.Join(", ", targets.Select(t => t.Name))}] onSurface={r.OnSurface} tiles={r.TileCount} partial={r.PartialCount} " +
                              $"covered={r.CoveredArea:F2}m2 vertices={vertCount} colliders={r.Colliders.Length} limit={r.HitTileLimit} " +
                              $"{watch.ElapsedMilliseconds}ms (index {indexMs}ms)  ({s.Width}x{s.Height} m, {s.Shape} {s.TileSize} @{s.AngleDeg}°, wrap {s.MaxWrapDeg}, {s.UvMode}, unrolled {s.Unrolled}, straight {s.StraightBorder})");
            // edge quality: air under the tile bottoms (beyond SurfaceOffset) at their outline, and tiles lying on two
            // bodies of which one is a flap
            float gapLimit = Mathf.Max(0.003f, 0.05f * s.TileSize);
            int over = outlines.Count(t => t.MaxGap > gapLimit), straddle = 0;
            foreach (TileOutline t in outlines)
            {
                var seen = new HashSet<int>();
                // points 5 mm inside the outline, dropped along the tile normal (a tile cut on the border line touches it
                // with its edge, which is fine; one lying across it has inner points on both bodies)
                foreach (Vector3 q in t.Hull)
                {
                    Vector3 inner = q + (t.Center - q).normalized * 0.005f;
                    if (surface.Raycast(inner + t.N * 0.05f, -t.N, 0.1f, 0.05f, out SurfaceHit hq, false)) seen.Add(hq.Owner);
                }
                if (seen.Count > 1 && seen.Any(o => bodyFlap[o])) straddle++;
            }
            Console.WriteLine($"  max gap under tile borders {(outlines.Count > 0 ? outlines.Max(t => t.MaxGap) : 0) * 1000:F1} mm, {over} tiles over {gapLimit * 1000:F1} mm" +
                              $" (inner sag max {(outlines.Count > 0 ? outlines.Max(t => t.MaxSag) : 0) * 1000:F1} mm)" +
                              (bodyName.Count > 1 ? $"; bodies [{string.Join(", ", bodyName.Select((b, i) => (bodyFlap[i] ? b + "*" : b) + ": " + (r.Parts.FirstOrDefault(p => p.Owner == i)?.TileAreaVectors.Length ?? 0) + " tiles"))}], tiles over a flap border {straddle}" : ""));
            if (s.Regions != null && s.Regions.Length > 0) // DESIGN-v11 G2: tile corners outside every kept region and clip plane
            {
                float Out(Vector3 q) => Mathf.Min(s.Regions.Min(rg => TileBuilder.RegionDist(rg, q, out _)), s.ClipPlanes != null && s.ClipPlanes.Length > 0 ? s.ClipPlanes.Min(c => c.x * q.x + c.y * q.y + c.z * q.z + c.w) : float.MaxValue);
                var bad = outlines.Where(t => t.Hull.Any(q => Out(q) > Flt(Str("0.01", "maskTol")))).ToList();
                if (overrides.ContainsKey("maskDebug"))
                    foreach (TileOutline bt in bad.OrderByDescending(x => x.Hull.Max(Out)).Take(4))
                    {
                        Vector3 wq = bt.Hull.OrderByDescending(Out).First();
                        Console.WriteLine($"   bad tile c=({bt.Center.x:F3},{bt.Center.y:F3},{bt.Center.z:F3}) n=({bt.N.x:F2},{bt.N.y:F2},{bt.N.z:F2}) worst=({wq.x:F3},{wq.y:F3},{wq.z:F3}) regions at c: {string.Join(" ", s.Regions.Select(rg => (TileBuilder.RegionDist(rg, bt.Center, out _) * 1000).ToString("F0")))} at worst: {string.Join(" ", s.Regions.Select(rg => (TileBuilder.RegionDist(rg, wq, out _) * 1000).ToString("F0")))}");
                    }
                Console.WriteLine($"  mask: {bad.Count} of {outlines.Count} tiles reach more than 1 cm out of the kept regions" + (bad.Count > 0 ? $", worst {bad.Max(t => t.Hull.Max(Out)) * 1000:F0} mm, e.g. at ({bad[0].Center.x:F2},{bad[0].Center.y:F2},{bad[0].Center.z:F2}) partial={bad[0].Partial} strip={bad[0].Strip} virtual={bad[0].Virtual}" : ""));
                // 1.6 (user: "lieber alles zu als perfekt gekachelt"): share of the visible main hull inside the mask (2 cm in from
                // its border, outside keep-outs) that has a tile above it; the bake wants >= 99 %
                toPart = q => decal.Parent.Xform.InverseApply(decal.Xform.Apply(q));
                maskCov = MaskCoverage(pos, tri, owner, bodyFlap, s, r, Out);
                Console.WriteLine($"  mask coverage {maskCov * 100:F2}% of the visible hull inside the mask");
            }
            if (outlines.Count > 0) // where the cover ends along u and v (decal space; DESIGN-v10 G2: the same for every pattern)
            {
                var allPts = outlines.Where(t => t.Hull != null).SelectMany(t => t.Hull).ToList(); // the outline dropped onto the hull
                if (s.FillHull && whole)
                {
                    bool[] side = surface.WrapMask(n, s.MaxWrapDeg, 25, 20);
                    float cov = MaskCoverage(pos, tri, owner, bodyFlap, s, r, q => -1f, side);
                    Console.WriteLine($"  coverage of tank side within wrap: {100f * cov:F2}%");
                }
                Console.WriteLine($"  reach u [{allPts.Min(q => Vector3.Dot(q, u)):F3}, {allPts.Max(q => Vector3.Dot(q, u)):F3}] v [{allPts.Min(q => Vector3.Dot(q, v)):F3}, {allPts.Max(q => Vector3.Dot(q, v)):F3}]");
            }
            // overlays: tiles, share of the visible outer skin under tiles, the cover's gap at the object's foot, and a digest of the
            // tiles in vessel space (equal digests with nudge= prove the overlay does not depend on the decal's pose)
            foreach (var ov in overlays)
            {
                var (cov, tot) = OverlayCoverage(ov.p, ov.t, ov.w, ov.inw, ov.tileable, ov.r, s.Thickness);
                Func<Vector3, Vector3> toObj = q => ov.frame.InverseApply(decal.Xform.Apply(q));
                float foot = FootGap(ov.p, ov.t, ov.w, ov.tileable, r, toObj, q => decal.Xform.InverseApply(ov.frame.Apply(q)), s, ov.frame.InverseApplyDir(decal.Xform.ApplyDir(n)).normalized);
                long dig = 17;
                foreach (TileOutline t in ov.o)
                    foreach (Vector3 q in t.Outline)
                    {
                        Vector3 w = ov.frame.Apply(q);
                        dig = unchecked(dig * 31 + Mathf.RoundToInt(w.x * 1e4f) * 73856093L ^ Mathf.RoundToInt(w.y * 1e4f) * 19349663L ^ Mathf.RoundToInt(w.z * 1e4f) * 83492791L);
                    }
                Console.WriteLine($"  overlay {ov.name}: tiles={ov.r.TileCount} partial={ov.r.PartialCount} covered={ov.r.CoveredArea:F2}m2 skin={tot:F2}m2 coverage={(tot > 0 ? cov / tot : 0) * 100:F1}% footBare={(band > 0 ? 100f * bareBand / band : 0):F1}% of {band} band pts (max {(foot < 0 ? "-" : (foot * 1000).ToString("F0") + "mm")}) digest={dig & 0xFFFFFFFFFFFL:X11}");
            }
            if (overrides.ContainsKey("sideTiles")) Console.WriteLine($"  nan uv {r.Parts.Sum(pt => pt.Uvs.Count(q => float.IsNaN(q.x) || float.IsNaN(q.y)))} nan pos {r.Parts.Sum(pt => pt.Vertices.Count(q => float.IsNaN(q.x)))} uv out {r.Parts.Sum(pt => pt.Uvs.Count(q => q.x < 0 || q.x > 1 || q.y < 0 || q.y > 1))}");
            if (overrides.ContainsKey("sideTiles")) // debug: tiles standing across the decal normal (side faces), by kind
                Console.WriteLine($"  side tiles: {outlines.Count(t => Mathf.Abs(Vector3.Dot(t.N, n)) < 0.35f)} (virtual {outlines.Count(t => t.Virtual && Mathf.Abs(Vector3.Dot(t.N, n)) < 0.35f)}, strip {outlines.Count(t => t.Strip && Mathf.Abs(Vector3.Dot(t.N, n)) < 0.35f)}) area {outlines.Where(t => Mathf.Abs(Vector3.Dot(t.N, n)) < 0.35f).Sum(t => t.Area):F2}");
            if (str(null, "near") is string nearAt) // debug: tiles within 0.15 m of a decal-local point
                foreach (TileOutline t in outlines.Where(t => (t.Center - Vec(nearAt)).magnitude < 0.15f))
                    Console.WriteLine($"   near c=({t.Center.x:F3},{t.Center.y:F3},{t.Center.z:F3}) n=({t.N.x:F2},{t.N.y:F2},{t.N.z:F2}) top-c={Vector3.Dot(t.TopPoint - t.Center, t.N):F3} partial={t.Partial} virtual={t.Virtual} strip={t.Strip} area={t.Area:F4} joint={t.Joint != null}");
            if (overrides.ContainsKey("gaps"))
                foreach (TileOutline t in outlines.OrderByDescending(t => t.MaxGap).Take(int.Parse(overrides["gaps"])))
                    Console.WriteLine($"   gap {t.MaxGap * 1000:F1} mm at ({t.A},{t.B}) centre ({t.Center.x:F3},{t.Center.y:F3},{t.Center.z:F3}) n ({t.N.x:F2},{t.N.y:F2},{t.N.z:F2}) partial={t.Partial}");

            // scene: covered parts in grey (double sided), tiles textured, joints dark, undersides white (single sided)
            var scene = new List<Tri>();
            foreach (CraftPart p in targets) // only the covered parts, so nothing else blocks the view
                if (overrides.ContainsKey("intakemark")) // debug: intake faces the filter leaves free, magenta
                {
                    var kept = new HashSet<Vector3>();
                    foreach (var (kv, kt, _) in HullMeshes(p)) for (int i = 0; i < kt.Length; i += 3) kept.Add((kv[kt[i]] + kv[kt[i + 1]] + kv[kt[i + 2]]) / 3);
                    foreach (var (kv, kt, _) in HullMeshes(p, true, false))
                        for (int i = 0; i < kt.Length; i += 3)
                            if (!kept.Contains((kv[kt[i]] + kv[kt[i + 1]] + kv[kt[i + 2]]) / 3))
                                scene.Add(new Tri { A = kv[kt[i]], B = kv[kt[i + 1]], C = kv[kt[i + 2]], Kind = 5 });
                }
            foreach (CraftPart p in overrides.ContainsKey("showAll") ? parts.Where(q => q.Name != "ablativeDecal") : targets) // showAll=1: also parts not covered (gear)
                foreach (var (verts, tris, _) in HullMeshes(p, false))
                    for (int i = 0; i < tris.Length; i += 3)
                        scene.Add(new Tri { A = verts[tris[i]], B = verts[tris[i + 1]], C = verts[tris[i + 2]], Kind = 0 });
            if (str(null, "hullTex") is string htFile) // hullTex=<.dds of the parent>: the parent drawn with its texture (windows)
            {
                hullTex = Texture.LoadDds(htFile);
                foreach (var (mu, w) in PartModels(decal.Parent))
                    foreach (MuMesh m in mu.Meshes)
                    {
                        if (!m.HasRenderer || m.Uv == null) continue;
                        foreach (int[] sm in m.Submeshes)
                            for (int i = 0; i + 2 < sm.Length; i += 3)
                            {
                                Vector3 Lift(Vector3 q) => q + (q - decal.Parent.Xform.Pos).normalized * 0.002f;
                                scene.Add(new Tri { A = Lift(w.Apply(m.Vertices[sm[i]])), B = Lift(w.Apply(m.Vertices[sm[i + 1]])), C = Lift(w.Apply(m.Vertices[sm[i + 2]])),
                                    UA = m.Uv[sm[i]], UB = m.Uv[sm[i + 1]], UC = m.Uv[sm[i + 2]], Kind = 6 });
                            }
                    }
            }
            if (str(null, "lum") is string lumFile) // debug: hull points lit in the parent's emissive map (lum=<dds>) or, lum=glass with
            {                                       // hullTex, bluish in its texture (windows): ASCII maps in part space
                float[,] lum = lumFile == "glass" ? GlassMap(hullTex) : Dxt1(lumFile);
                int lw = lum.GetLength(0), lh = lum.GetLength(1);
                var wpts = new List<Vector3>();
                foreach (var (mu, w) in PartModels(decal.Parent))
                    foreach (MuMesh m in mu.Meshes)
                    {
                        if (!m.HasRenderer || m.Uv == null) continue;
                        foreach (int[] sm in m.Submeshes)
                            for (int i = 0; i + 2 < sm.Length; i += 3)
                            {
                                Vector3 A = w.Apply(m.Vertices[sm[i]]), B = w.Apply(m.Vertices[sm[i + 1]]), Cc = w.Apply(m.Vertices[sm[i + 2]]);
                                const int N = 24;
                                for (int ia = 0; ia <= N; ia++)
                                    for (int ib = 0; ia + ib <= N; ib++)
                                    {
                                        float fa = (float)ia / N, fb = (float)ib / N, fc = 1 - fa - fb;
                                        Vector2 uvc = m.Uv[sm[i]] * fa + m.Uv[sm[i + 1]] * fb + m.Uv[sm[i + 2]] * fc;
                                        int x = Mathf.Clamp((int)((uvc.x - Mathf.Floor(uvc.x)) * lw), 0, lw - 1), y = Mathf.Clamp((int)((1 - (uvc.y - Mathf.Floor(uvc.y))) * lh), 0, lh - 1);
                                        if (lum[x, y] < Flt(str("0.25", "lumMin"))) continue;
                                        wpts.Add(decal.Parent.Xform.InverseApply(A * fa + B * fb + Cc * fc));
                                    }
                            }
                    }
                if (wpts.Count > 0)
                {
                    Console.WriteLine($"  lit faces: {wpts.Count / 3}, part space x {wpts.Min(q => q.x):F3}..{wpts.Max(q => q.x):F3} y {wpts.Min(q => q.y):F3}..{wpts.Max(q => q.y):F3} z {wpts.Min(q => q.z):F3}..{wpts.Max(q => q.z):F3}");
                    // side view (x > 0): y to the right, z up; top view (z < 0): y to the right, x up; 0.1 m per character
                    float g = Flt(str("0.1", "lumGrid"));
                    for (int view = 0; view < 2; view++)
                        for (float zz = 2f; zz >= -2f; zz -= g)
                        {
                            var row = new System.Text.StringBuilder($"   {(view == 0 ? "z" : "x")} {zz,5:F2} ");
                            for (float yy = -1.3f; yy <= 3.3f; yy += g)
                                row.Append(wpts.Any(q => (view == 0 ? q.x > 0 && q.z >= zz && q.z < zz + g : q.z < 0 && q.x >= zz && q.x < zz + g) && q.y >= yy && q.y < yy + g) ? '#' : '.');
                            Console.WriteLine(row);
                        }
                }
            }
            if (overrides.ContainsKey("maskShow") && s.Regions != null) // maskShow=1: hull inside the kept regions magenta (no tiles)
            {
                scene.RemoveAll(q => q.Kind != 0 && q.Kind != 6);
                foreach (var (verts, tris, _) in HullMeshes(decal.Parent))
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                        int N = int.Parse(Str("12", "maskN"));
                        for (int ia = 0; ia < N; ia++)
                            for (int ib = 0; ia + ib < N; ib++)
                                foreach (int half in new[] { 0, 1 })
                                {
                                    if (half == 1 && ia + ib + 1 >= N) continue;
                                    Vector3 P(int x, int y) => a + (b - a) * ((float)x / N) + (c - a) * ((float)y / N);
                                    Vector3 p0 = half == 0 ? P(ia, ib) : P(ia + 1, ib), p1 = half == 0 ? P(ia + 1, ib) : P(ia + 1, ib + 1), p2 = P(ia, ib + 1);
                                    Vector3 cd = decal.Xform.InverseApply((p0 + p1 + p2) / 3);
                                    bool kept = s.Regions.Any(rg => TileBuilder.RegionDist(rg, cd, out _) < 0) || s.ClipPlanes != null && s.ClipPlanes.Any(cp => cp.x * cd.x + cp.y * cd.y + cp.z * cd.z + cp.w <= 0);
                                    Vector3 lift = Vector3.Cross(p1 - p0, p2 - p0).normalized * 0.004f;
                                    if (kept) scene.Add(new Tri { A = p0 + lift, B = p1 + lift, C = p2 + lift, Kind = 5 });
                                }
                    }
            }
            if (overrides.ContainsKey("showKeep") && s.KeepOut != null) // showKeep=1: keep-out boxes magenta
                for (int i = 0; i + 3 < s.KeepOut.Length; i += 4)
                {
                    Vector3 c0 = s.KeepOut[i], a1 = s.KeepOut[i + 1], a2 = s.KeepOut[i + 2], a3 = s.KeepOut[i + 3];
                    Vector3 Cn(int k) => decal.Xform.Apply(c0 + ((k & 1) != 0 ? a1 : -a1) + ((k & 2) != 0 ? a2 : -a2) + ((k & 4) != 0 ? a3 : -a3));
                    foreach (int[] q in new[] { new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 } })
                    {
                        scene.Add(new Tri { A = Cn(q[0]), B = Cn(q[1]), C = Cn(q[2]), Kind = 5 });
                        scene.Add(new Tri { A = Cn(q[0]), B = Cn(q[2]), C = Cn(q[3]), Kind = 5 });
                    }
                }
            foreach (PartTiles pt in overrides.ContainsKey("bare") ? new PartTiles[0] : r.Parts) // bare=1: hull only
            {
                int before = scene.Count;
                for (int b = 0; b < pt.BinTriangles.Length; b++) AddTris(scene, pt, pt.BinTriangles[b], decal.Xform, 1, b);
                if (overrides.ContainsKey("tint") && pt.Owner < bodyFlap.Count && bodyFlap[pt.Owner])
                    for (int i = before; i < scene.Count; i++) { Tri tt = scene[i]; tt.Tint = true; scene[i] = tt; }
                AddTris(scene, pt, pt.JointTriangles, decal.Xform, 2, 0);
                AddTris(scene, pt, pt.BackTriangles, decal.Xform, 3, 0);
            }
            if (!overrides.ContainsKey("showAll")) scene.AddRange(gearScene); // retracted gear (showAll draws the default pose)
            if (overrides.ContainsKey("ovOnly")) // ovOnly=1: only the overlay objects (their tiled bodies) and their tiles
            {
                scene.Clear();
                foreach (var ov in overlays)
                    for (int i = 0; i + 2 < ov.tileable; i += 3)
                        scene.Add(new Tri { A = ov.frame.Apply(ov.p[ov.t[i]]), B = ov.frame.Apply(ov.p[ov.t[i + 1]]), C = ov.frame.Apply(ov.p[ov.t[i + 2]]), Kind = 0 });
            }
            foreach (var ov in overrides.ContainsKey("bare") ? overlays.Take(0) : overlays)
                foreach (PartTiles pt in ov.r.Parts)
                {
                    int before = scene.Count;
                    for (int b = 0; b < pt.BinTriangles.Length; b++) AddTris(scene, pt, pt.BinTriangles[b], ov.frame, 1, b);
                    if (overrides.ContainsKey("tint"))
                        for (int i = before; i < scene.Count; i++) { Tri tt = scene[i]; tt.Tint = true; scene[i] = tt; }
                    AddTris(scene, pt, pt.JointTriangles, ov.frame, 2, 0);
                    AddTris(scene, pt, pt.BackTriangles, ov.frame, 3, 0);
                }

            if (overrides.ContainsKey("acc")) // acc=1: tiles of the decals rendered before are drawn as well (seams between parts)
            {
                var mine = scene.Where(q => q.Kind >= 1 && q.Kind <= 3).ToList();
                scene.AddRange(accTiles);
                accTiles.AddRange(mine);
            }
            Vector3 nW = decal.Xform.ApplyDir(n), uW = decal.Xform.ApplyDir(u), vW = decal.Xform.ApplyDir(v);
            // frame the tiles that were actually built
            Vector3 focus = decal.Xform.Apply(Vector3.zero);
            float extent = Mathf.Max(s.Width, s.Height);
            if (outlines.Count > 0)
            {
                Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
                foreach (TileOutline t in outlines) { lo = Vector3.Min(lo, t.Center); hi = Vector3.Max(hi, t.Center); }
                focus = decal.Xform.Apply((lo + hi) * 0.5f);
                extent = Mathf.Max(0.5f, (hi - lo).magnitude);
            }
            if (cover && vpos.Count > 0) // 1.9: a cockpit cover shields only part of the part: frame the whole part, never a crop of it
            {
                Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
                foreach (Vector3 q in vpos) { lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q); }
                focus = (lo + hi) * 0.5f;
                extent = Mathf.Max(0.5f, (hi - lo).magnitude);
            }
            float dist = 0.75f * extent / Mathf.Tan(20 * Mathf.Deg2Rad) + 0.5f;
            // macro view: the tile nearest the decal origin, seen along its own normal
            Vector3 spot = focus, spotN = nW;
            if (outlines.Count > 0)
            {
                TileOutline near = outlines.OrderBy(t => t.Center.sqrMagnitude).First();
                spot = decal.Xform.Apply(near.Center);
                spotN = decal.Xform.ApplyDir(near.N);
            }
            // hinge view: the closest pair of tiles on a flap and on another body (the flap border), nearest the decal origin
            Vector3 hinge = spot;
            if (bodyFlap.Contains(true))
            {
                var flapTiles = outlines.Where(t => t.Owner < bodyFlap.Count && bodyFlap[t.Owner]).ToList();
                var fixedTiles = outlines.Where(t => t.Owner < bodyFlap.Count && !bodyFlap[t.Owner]).ToList();
                float best = float.MaxValue;
                foreach (TileOutline a in flapTiles)
                    foreach (TileOutline b in fixedTiles)
                    {
                        float d = (a.Center - b.Center).magnitude;
                        if (d > 1.5f * s.TileSize) continue;
                        float score = ((a.Center + b.Center) * 0.5f).magnitude;
                        if (score < best) { best = score; hinge = decal.Xform.Apply((a.Center + b.Center) * 0.5f); }
                    }
            }
            // seen from outside: camera right = u (decal frame convention), so views turn about v and u
            var views = new (string, Vector3, float)[]
            {
                ("front", nW, dist),
                ("left", (nW * Mathf.Cos(0.9f) - uW * Mathf.Sin(0.9f)).normalized, dist),
                ("right", (nW * Mathf.Cos(0.9f) + uW * Mathf.Sin(0.9f)).normalized, dist),
                ("top", (nW * Mathf.Cos(0.9f) + vW * Mathf.Sin(0.9f)).normalized, dist),
                ("close", (nW * Mathf.Cos(0.6f) - uW * Mathf.Sin(0.6f)).normalized, dist * 0.35f),
                ("back", -nW, dist),
                ("yaw", (nW * Mathf.Cos(Flt(str("0", "yaw")) * Mathf.Deg2Rad) - uW * Mathf.Sin(Flt(str("0", "yaw")) * Mathf.Deg2Rad)).normalized, dist * Flt(str("0.3", "zoom"))),
                ("backclose", (-nW * Mathf.Cos(0.3f) - uW * Mathf.Sin(0.3f)).normalized, dist * 0.2f),
                ("macro", (spotN * Mathf.Cos(0.5f) - uW * Mathf.Sin(0.5f)).normalized, Mathf.Max(0.8f, 6 * s.TileSize)),
                ("hinge", (nW * Mathf.Cos(0.6f) - uW * Mathf.Sin(0.6f)).normalized, Mathf.Max(0.6f, 10 * s.TileSize) * Flt(str("1", "hingeDist"))),
            };
            // dirs=name:x,y,z[:distance factor[:at x,y,z]];... extra views from vessel-space directions (vessel up = camera up when it can be)
            var viewList = views.ToList();
            var atList = new Dictionary<string, Vector3>();
            string dirs = str(null, "dirs");
            if (dirs != null)
                foreach (string e in dirs.Split(';'))
                {
                    string[] fv = e.Split(':');
                    viewList.Add((fv[0], Vec(fv[1]).normalized, dist * (fv.Length > 2 ? Flt(fv[2]) : 1)));
                    if (fv.Length > 3) atList[fv[0]] = Vec(fv[3]);
                }
            // edgeL/edgeR: close views of where the cover ends along -u/+u (DESIGN-v10 G1: the Mk3 chine), half way along v
            var edgeAt = new Dictionary<string, Vector3>();
            if (outlines.Count > 0)
            {
                var pts = outlines.SelectMany(t => t.Outline).ToList();
                float vMid = (pts.Min(q => Vector3.Dot(q, v)) + pts.Max(q => Vector3.Dot(q, v))) / 2 + Flt(str("0", "edgeV"));
                var band = pts.Where(q => Mathf.Abs(Vector3.Dot(q, v) - vMid) < 0.3f).ToList();
                if (band.Count > 0)
                    foreach (int sg in new[] { -1, 1 })
                    {
                        Vector3 e = band.OrderBy(q => -sg * Vector3.Dot(q, u)).First();
                        string nm = sg < 0 ? "edgeL" : "edgeR";
                        edgeAt[nm] = decal.Xform.Apply(e);
                        viewList.Add((nm, (nW * Flt(str("0.35", "edgeN")) + uW * sg + vW * 0.25f).normalized, Mathf.Max(0.7f, 8 * s.TileSize)));
                    }
                // 1.6.2 edgeB/edgeT: where the cover ends along -v/+v (part seams, stack faces), in the middle across u
                float uMid = (pts.Min(q => Vector3.Dot(q, u)) + pts.Max(q => Vector3.Dot(q, u))) / 2;
                var bandU = pts.Where(q => Mathf.Abs(Vector3.Dot(q, u) - uMid) < 0.3f).ToList();
                if (bandU.Count > 0)
                    foreach (int sg in new[] { -1, 1 })
                    {
                        Vector3 e = bandU.OrderBy(q => -sg * Vector3.Dot(q, v)).First();
                        string nm = sg < 0 ? "edgeB" : "edgeT";
                        edgeAt[nm] = decal.Xform.Apply(e);
                        viewList.Add((nm, (nW * 0.6f + vW * sg + uW * 0.3f).normalized, Mathf.Max(0.5f, 5 * s.TileSize)));
                    }
            }
            // ov: each overlay object seen along the mean normal of its tiles and from the other side (ov<k>a, ov<k>b)
            bool ovViews = str("", "views").Split(',').Contains("ov");
            for (int k = 0; k < overlays.Count && ovViews; k++)
            {
                var ov = overlays[k];
                Vector3 mn = Vector3.zero, lo3 = Vector3.one * 1e9f, hi3 = -lo3;
                foreach (PartTiles pt in ov.r.Parts) foreach (Vector3 a in pt.TileAreaVectors) mn += ov.frame.ApplyDir(a);
                for (int i = 0; i < ov.tileable; i++) { Vector3 q = ov.frame.Apply(ov.p[ov.t[i]]); lo3 = Vector3.Min(lo3, q); hi3 = Vector3.Max(hi3, q); }
                if (mn.sqrMagnitude < 1e-9f) mn = nW;
                mn.Normalize();
                Vector3 side = Vector3.Cross(mn, Mathf.Abs(mn.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                float od = Mathf.Max(0.8f, 1.3f * (hi3 - lo3).magnitude) * Flt(str("1", "ovZoom"));
                Console.WriteLine($"  view ov{k + 1}: {ov.name} at ({(lo3 + hi3).x / 2:F2},{(lo3 + hi3).y / 2:F2},{(lo3 + hi3).z / 2:F2}) size ({(hi3 - lo3).x:F2},{(hi3 - lo3).y:F2},{(hi3 - lo3).z:F2}) mean normal ({mn.x:F2},{mn.y:F2},{mn.z:F2})");
                foreach (var (sfx, d) in new[] { ("a", (mn + 0.35f * side).normalized), ("b", (-mn + 0.35f * side).normalized), ("c", (side + 0.3f * mn).normalized), ("d", (-side + 0.3f * mn).normalized) })
                {
                    viewList.Add(($"ov{k + 1}{sfx}", d, od));
                    atList[$"ov{k + 1}{sfx}"] = (lo3 + hi3) / 2;
                }
            }
            views = viewList.ToArray();
            string[] wanted = (str("front,left,right,top,close,macro", "views") + (dirs != null ? "," + string.Join(",", dirs.Split(';').Select(e => e.Split(':')[0])) : "")).Split(',');
            bool mark = overrides.ContainsKey("mark");
            var leaks = new List<string>();
            foreach (var (name, dir, d) in views)
                if (wanted.Contains(name) || ovViews && name.StartsWith("ov"))
                {
                    Vector3 at = atList.TryGetValue(name, out Vector3 av) || edgeAt.TryGetValue(name, out av) ? av : name == "macro" ? spot : name == "hinge" ? hinge : name != "yaw" ? focus : overrides.ContainsKey("at") ? decal.Xform.Apply(Vec(overrides["at"])) : focus;
                    Vector3 upHint = atList.Count > 0 || dirs != null && !"front,left,right,top,close,back,yaw,backclose,macro,hinge".Split(',').Contains(name)
                        ? (Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.forward) : vW;
                    if (str(null, "up") is string upOv) upHint = Vec(upOv); // up=x,y,z: camera up (vessel space)
                    int leak = Render(scene, tex, at + dir * d, at, upHint, Path.Combine(outDir, $"decal{index}-{name}.png"),
                        $"{label} / {name}  tiles {r.TileCount}  {watch.ElapsedMilliseconds} ms", mark);
                    leaks.Add($"{name}={leak}");
                }
            Console.WriteLine($"  hull visible inside the tiled area (px): {string.Join(" ", leaks)}");
        }
        return 0;
    }

    // ---------- cockpit mask (DESIGN-v11 G2/G3) ----------

    // --mask <gameData> <part> <dds> <out.cfg> [keys]: REGION nodes for CockpitCover.cfg from the part's texture. The hull (part
    // space, with UVs) is seen orthographically from each chart direction; a cell is kept where the nearest surface shows dark grey
    // (lo < luminance < hi, saturation < sat) or faces the belly (belly=x,y,z:cos); window glass (bluish) and grow= cells around
    // it are free. Majority filter, small specks (minArea m2) removed, contours by marching squares, Chaikin-smoothed and
    // simplified (tol m): smooth loops, no stairs. Keys: charts=name:dx,dy,dz:nx,ny,nz,px,py,pz[/...]|-;... (domain planes: the chart applies where dot(n, x - p) <= 0) cell lo hi sat belly grow
    // minArea tol debug=1 (charts as PNG next to the output)
    static int MaskMode(string[] args)
    {
        gameData = args[1];
        overrides = args.Skip(5).Select(a => a.Split(new[] { '=' }, 2)).ToDictionary(a => a[0], a => a[1]);
        LoadConfigCache(Path.Combine(gameData, "ModuleManager.ConfigCache"));
        var part = new CraftPart(Node.Parse($"PART\n{{\npart = {args[2]}_1\npos = 0,0,0\nrot = 0,0,0,1\nattm = 0\n}}\n").Nodes[0]);
        Texture tex = Texture.LoadDds(args[3]);
        float cell = Flt(Str("0.015", "cell")), lo = Flt(Str("0.22", "lo")), hi = Flt(Str("0.66", "hi")), satMax = Flt(Str("0.12", "sat"));
        float minArea = Flt(Str("0.01", "minArea")), tol = Flt(Str("0.004", "tol"));
        int grow = int.Parse(Str("2", "grow"));
        Vector3 bellyDir = Vector3.zero;
        float bellyCos = 2;
        if (overrides.TryGetValue("belly", out string bs)) { bellyDir = Vec(bs.Split(':')[0]).normalized; bellyCos = Flt(bs.Split(':')[1]); }
        // samples: point, normal, class (1 kept, 0 bare, -1 window glass)
        var samples = new List<(Vector3 p, Vector3 n, int k)>();
        foreach (var (mu, w) in PartModels(part))
            foreach (MuMesh m in mu.Meshes)
            {
                if (!m.HasRenderer || m.Uv == null || m.Skinned) continue;
                for (int s = 0; s < m.Submeshes.Count; s++)
                {
                    int mat = s < m.MaterialIds.Length ? m.MaterialIds[s] : -1;
                    if (mat >= 0 && mat < mu.Shaders.Count && SkipShaders.Any(mu.Shaders[mat].Contains)) continue;
                    int[] t = m.Submeshes[s];
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        Vector3 a = w.Apply(m.Vertices[t[i]]), b = w.Apply(m.Vertices[t[i + 1]]), c = w.Apply(m.Vertices[t[i + 2]]);
                        Vector3 n = Vector3.Cross(b - a, c - a);
                        if (n.sqrMagnitude < 1e-12f) continue;
                        n = (w.Mirrored ? -n : n).normalized;
                        int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max((b - a).magnitude, Mathf.Max((c - a).magnitude, (c - b).magnitude)) / (0.4f * cell)), 1, 400);
                        for (int ia = 0; ia <= steps; ia++)
                            for (int ib = 0; ia + ib <= steps; ib++)
                            {
                                float fa = (float)ia / steps, fb = (float)ib / steps;
                                Vector3 p = a + (b - a) * fa + (c - a) * fb;
                                Vector2 uv = m.Uv[t[i]] + (m.Uv[t[i + 1]] - m.Uv[t[i]]) * fa + (m.Uv[t[i + 2]] - m.Uv[t[i]]) * fb;
                                Color col = tex.Sample(uv);
                                float l = (col.r + col.g + col.b) / 3, sat = Mathf.Max(col.r, Mathf.Max(col.g, col.b)) - Mathf.Min(col.r, Mathf.Min(col.g, col.b));
                                int k = col.b > col.r + 0.08f && col.b > 0.25f ? -1 : (l > lo && l < hi && sat < satMax) || Vector3.Dot(n, bellyDir) > bellyCos ? 1 : 0;
                                samples.Add((p, n, k));
                            }
                    }
                }
            }
        Console.WriteLine($"mask {args[2]}: bounds x {samples.Min(q => q.p.x):F2}..{samples.Max(q => q.p.x):F2} y {samples.Min(q => q.p.y):F2}..{samples.Max(q => q.p.y):F2} z {samples.Min(q => q.p.z):F2}..{samples.Max(q => q.p.z):F2}, {samples.Count} samples, kept {samples.Count(x => x.k == 1)}, glass {samples.Count(x => x.k < 0)}");
        if (overrides.ContainsKey("profile")) // slices along y: hull extent, chine (widest point), glass and dark samples
        {
            for (float y = -1.2f; y <= 3.3f; y += 0.1f)
            {
                var sl = samples.Where(q => Mathf.Abs(q.p.y - y) < 0.02f).ToList();
                if (sl.Count == 0) continue;
                var wide = sl.OrderByDescending(q => Mathf.Abs(q.p.x)).First();
                var gl = sl.Where(q => q.k < 0).ToList();
                var top = sl.Where(q => q.n.z < -0.3f && q.k == 1).ToList();
                Console.WriteLine($"  y {y:F1}: x max {Mathf.Abs(wide.p.x):F2} at z {wide.p.z:F2}, z {sl.Min(q => q.p.z):F2}..{sl.Max(q => q.p.z):F2}" +
                    (gl.Count > 0 ? $", glass x {gl.Min(q => q.p.x):F2}..{gl.Max(q => q.p.x):F2} z {gl.Min(q => q.p.z):F2}..{gl.Max(q => q.p.z):F2}" : "") +
                    (top.Count > 0 ? $", dark top-facing z >= {top.Min(q => q.p.z):F2}" : ""));
            }
            return 0;
        }
        var sb = new System.Text.StringBuilder();
        foreach (string chart in Str("top:0,0,-1:-", "charts").Split(';'))
        {
            string[] f = chart.Split(':');
            Vector3 d = Vec(f[1]).normalized;
            Vector3 A = Vector3.Cross(Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.forward, d).normalized, B = Vector3.Cross(d, A);
            var facing = samples.Where(x => Vector3.Dot(x.n, d) > 0.05f).ToList();
            if (facing.Count == 0) continue;
            float x0 = facing.Min(x => Vector3.Dot(x.p, A)) - 2 * cell, y0 = facing.Min(x => Vector3.Dot(x.p, B)) - 2 * cell;
            int W = Mathf.CeilToInt((facing.Max(x => Vector3.Dot(x.p, A)) - x0) / cell) + 3, H = Mathf.CeilToInt((facing.Max(x => Vector3.Dot(x.p, B)) - y0) / cell) + 3;
            var depth = new float[W, H];
            var cls = new int[W, H];
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) { depth[x, y] = float.MinValue; cls[x, y] = -2; }
            foreach (var (p, n, k) in facing)
            {
                int x = (int)((Vector3.Dot(p, A) - x0) / cell), y = (int)((Vector3.Dot(p, B) - y0) / cell);
                float h = Vector3.Dot(p, d);
                if (h > depth[x, y] + 0.002f) { depth[x, y] = h; cls[x, y] = k; } // the surface nearest the viewer
            }
            // kept cells: majority of the 3 x 3 neighbourhood (twice); free round the glass; no specks or pin holes
            var keep = new bool[W, H];
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) keep[x, y] = cls[x, y] == 1;
            for (int pass = 0; pass < 2; pass++)
            {
                var nk = new bool[W, H];
                for (int x = 1; x + 1 < W; x++)
                    for (int y = 1; y + 1 < H; y++)
                    {
                        if (cls[x, y] == -2) continue;
                        int cnt = 0, known = 0;
                        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) { if (cls[x + dx, y + dy] != -2) known++; if (keep[x + dx, y + dy]) cnt++; }
                        nk[x, y] = 2 * cnt > known;
                    }
                keep = nk;
            }
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (cls[x, y] == -1)
                        for (int dx = -grow; dx <= grow; dx++) for (int dy = -grow; dy <= grow; dy++)
                            if (x + dx >= 0 && y + dy >= 0 && x + dx < W && y + dy < H) keep[x + dx, y + dy] = false;
            int minCells = Mathf.CeilToInt(minArea / (cell * cell));
            foreach (bool val in new[] { true, false }) // specks of kept, then holes in it (cells outside the silhouette count as free)
            {
                var seen = new bool[W, H];
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                    {
                        if (seen[x, y] || keep[x, y] != val) continue;
                        var comp = new List<(int, int)>();
                        var stack = new Stack<(int, int)>();
                        stack.Push((x, y));
                        seen[x, y] = true;
                        bool border = false;
                        while (stack.Count > 0)
                        {
                            var (cx, cy) = stack.Pop();
                            comp.Add((cx, cy));
                            if (cx == 0 || cy == 0 || cx == W - 1 || cy == H - 1 || cls[cx, cy] == -2) border = true;
                            foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                                if (nx >= 0 && ny >= 0 && nx < W && ny < H && !seen[nx, ny] && keep[nx, ny] == val) { seen[nx, ny] = true; stack.Push((nx, ny)); }
                        }
                        if (comp.Count < minCells && (val || !border)) foreach (var (cx, cy) in comp) keep[cx, cy] = !val;
                    }
            }
            List<List<Vector2>> loops = Contours(keep, W, H).Select(lp => lp.Select(q => new Vector2(x0 + (q.x + 0.5f) * cell, y0 + (q.y + 0.5f) * cell)).ToList())
                .Select(lp => Simplify(Chaikin(Chaikin(Chaikin(lp))), tol)).Where(lp => lp.Count >= 3 && Mathf.Abs(Area(lp)) >= minArea / 4).ToList();
            Console.WriteLine($"  chart {f[0]}: {W}x{H} cells, kept {Enumerable.Range(0, W * H).Count(i => keep[i % W, i / W])}, loops {loops.Count} ({loops.Sum(lp => lp.Count)} points)");
            string I(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
            string V3(Vector3 v) => $"{I(v.x)}, {I(v.y)}, {I(v.z)}";
            sb.Append($"\tREGION // {f[0]}: dark panels seen along {V3(d)}\n\t{{\n\t\torigin = 0, 0, 0\n\t\tu = {V3(A)}\n\t\tv = {V3(B)}\n");
            if (f.Length > 2 && f[2] != "-") foreach (string dom in f[2].Split('/')) sb.Append($"\t\tdomain = {string.Join(", ", dom.Split(',').Select(Flt).Select(I))}\n");
            foreach (List<Vector2> lp in loops) sb.Append("\t\tloop = " + string.Join(", ", lp.Select(q => I(q.x) + " " + I(q.y))) + "\n");
            sb.Append("\t}\n");
            if (overrides.ContainsKey("debug")) // the chart as PNG: kept white, free grey, glass blue, outside dark; loops red
                using (var bmp = new Bitmap(W, H))
                {
                    for (int x = 0; x < W; x++)
                        for (int y = 0; y < H; y++)
                            bmp.SetPixel(x, H - 1 - y, cls[x, y] == -2 ? System.Drawing.Color.FromArgb(30, 30, 40) : keep[x, y] ? System.Drawing.Color.White : cls[x, y] == -1 ? System.Drawing.Color.SteelBlue : System.Drawing.Color.Gray);
                    foreach (List<Vector2> lp in loops)
                        foreach (Vector2 q in lp)
                        {
                            int x = (int)((q.x - x0) / cell), y = (int)((q.y - y0) / cell);
                            if (x >= 0 && y >= 0 && x < W && y < H) bmp.SetPixel(x, H - 1 - y, System.Drawing.Color.Red);
                        }
                    bmp.Save(Path.ChangeExtension(args[4], null) + "-" + f[0] + ".png");
                }
        }
        File.WriteAllText(args[4], sb.ToString());
        return 0;
    }

    // Marching squares over cell centres (threshold between kept and free): closed loops in cell coordinates.
    static List<List<Vector2>> Contours(bool[,] keep, int W, int H)
    {
        bool K(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && keep[x, y];
        var next = new Dictionary<Vector2, Vector2>();
        for (int x = -1; x < W; x++)
            for (int y = -1; y < H; y++)
            {
                // corners: 0 (x,y) 1 (x+1,y) 2 (x+1,y+1) 3 (x,y+1); edges midpoints, kept on the left of each segment
                int code = (K(x, y) ? 1 : 0) | (K(x + 1, y) ? 2 : 0) | (K(x + 1, y + 1) ? 4 : 0) | (K(x, y + 1) ? 8 : 0);
                Vector2 eB = new Vector2(x + 0.5f, y), eR = new Vector2(x + 1, y + 0.5f), eT = new Vector2(x + 0.5f, y + 1), eL = new Vector2(x, y + 0.5f);
                void Seg(Vector2 a, Vector2 b) => next[a] = b;
                switch (code)
                {
                    case 1: Seg(eB, eL); break;
                    case 2: Seg(eR, eB); break;
                    case 3: Seg(eR, eL); break;
                    case 4: Seg(eT, eR); break;
                    case 5: Seg(eB, eR); Seg(eT, eL); break;   // saddle: kept corners joined
                    case 6: Seg(eT, eB); break;
                    case 7: Seg(eT, eL); break;
                    case 8: Seg(eL, eT); break;
                    case 9: Seg(eB, eT); break;
                    case 10: Seg(eL, eB); Seg(eR, eT); break;  // saddle
                    case 11: Seg(eR, eT); break;
                    case 12: Seg(eL, eR); break;
                    case 13: Seg(eB, eR); break;
                    case 14: Seg(eL, eB); break;
                }
            }
        var loops = new List<List<Vector2>>();
        var used = new HashSet<Vector2>();
        foreach (Vector2 start in next.Keys.ToList())
        {
            if (used.Contains(start)) continue;
            var lp = new List<Vector2>();
            Vector2 p = start;
            while (!used.Contains(p) && next.TryGetValue(p, out Vector2 q)) { used.Add(p); lp.Add(p); p = q; }
            if (lp.Count >= 3) loops.Add(lp);
        }
        return loops;
    }

    static List<Vector2> Chaikin(List<Vector2> p)
    {
        var r = new List<Vector2>(2 * p.Count);
        for (int i = 0; i < p.Count; i++)
        {
            Vector2 a = p[i], b = p[(i + 1) % p.Count];
            r.Add(Vector2.Lerp(a, b, 0.25f));
            r.Add(Vector2.Lerp(a, b, 0.75f));
        }
        return r;
    }

    // Douglas-Peucker on a closed loop (split at the point farthest from the first).
    static List<Vector2> Simplify(List<Vector2> p, float tol)
    {
        if (p.Count < 8) return p;
        int far = 0;
        for (int i = 1; i < p.Count; i++) if ((p[i] - p[0]).sqrMagnitude > (p[far] - p[0]).sqrMagnitude) far = i;
        var keepIdx = new bool[p.Count];
        keepIdx[0] = keepIdx[far] = true;
        void Dp(int a, int b) // indices along the loop, b may wrap
        {
            int n = (b - a + p.Count) % p.Count;
            if (n < 2) return;
            Vector2 pa = p[a], pb = p[b % p.Count], ab = pb - pa;
            int best = -1;
            float bd = tol;
            for (int k = 1; k < n; k++)
            {
                Vector2 q = p[(a + k) % p.Count];
                float d = ab.sqrMagnitude > 1e-12f ? Mathf.Abs(ab.x * (q.y - pa.y) - ab.y * (q.x - pa.x)) / ab.magnitude : (q - pa).magnitude;
                if (d > bd) { bd = d; best = (a + k) % p.Count; }
            }
            if (best < 0) return;
            keepIdx[best] = true;
            Dp(a, best);
            Dp(best, b);
        }
        Dp(0, far);
        Dp(far, 0);
        return p.Where((q, i) => keepIdx[i]).ToList();
    }

    static float Area(List<Vector2> p)
    {
        float a = 0;
        for (int i = 0; i < p.Count; i++) a += p[i].x * p[(i + 1) % p.Count].y - p[(i + 1) % p.Count].x * p[i].y;
        return a / 2;
    }

    // Tile surfaces (tops, bevels, walls, joint sheets) of a result as a ray target, in the space given by map.
    static SurfaceIndex TileIndex(TileResult r, Func<Vector3, Vector3> map)
    {
        var tp = new List<Vector3>();
        var tt = new List<int>();
        foreach (PartTiles pt in r.Parts)
        {
            int o = tp.Count;
            tp.AddRange(pt.Vertices.Select(map));
            foreach (int[] b in pt.BinTriangles) tt.AddRange(b.Select(i => i + o));
            tt.AddRange(pt.JointTriangles.Select(i => i + o));
        }
        if (tt.Count == 0) return null;
        float rad = tp.Max(q => q.magnitude) + 1;
        return new SurfaceIndex(tp.ToArray(), tt.ToArray(), Vector3.zero, rad, 0.05f, 0.02f);
    }

    // DESIGN-v11 G1: area of the object's outer skin (its first tileable/3 triangles; faces whose normal ray hits anything within
    // the object's size are hidden, as in the builder) and the part of it with a tile above (within thickness + 5 cm).
    static (float covered, float total) OverlayCoverage(Vector3[] p, int[] t, int[] w, bool[] inward, int tileable, TileResult r, float thick)
    {
        SurfaceIndex tiles = TileIndex(r, q => q);
        Vector3 lo = Vector3.one * 1e9f, hi = -lo;
        for (int i = 0; i < tileable; i++) { lo = Vector3.Min(lo, p[t[i]]); hi = Vector3.Max(hi, p[t[i]]); }
        float reach = Mathf.Max(0.5f, 1.1f * (hi - lo).magnitude);
        var hull = new SurfaceIndex(p, t, Vector3.zero, p.Max(q => q.magnitude) + 1, 0.05f, 0.02f, w);
        int obstacleId = w.Max();
        float cov = 0, tot = 0;
        const int N = 6;
        for (int i = 0; i + 2 < tileable; i += 3)
        {
            Vector3 a = p[t[i]], b = p[t[i + 1]], c = p[t[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            float area = 0.5f * n.magnitude;
            if (area < 1e-9f) continue;
            n.Normalize();
            float dA = area / (N * (N + 1) / 2);
            for (int ia = 0; ia < N; ia++)
                for (int ib = 0; ia + ib < N; ib++)
                {
                    Vector3 q = a + (b - a) * ((ia + 1f / 3) / N) + (c - a) * ((ib + 1f / 3) / N);
                    if (inward[w[i / 3]] ? Vector3.Dot(n, q) < 0 || hull.Raycast(q + n * 0.01f, n, 0.5f, -1.01f, out _, false) : Hidden(hull, q, n, reach, obstacleId)) continue;
                    tot += dA;
                    if (tiles != null && tiles.Raycast(q + n * (thick + 0.05f), -n, thick + 0.06f, -1.01f, out _, false)) cov += dA;
                }
        }
        return (cov, tot);
    }

    // as TileBuilder.Enclosed in an overlay: no ray of the fan (n, tilted 50 deg four ways) gets out within reach
    static bool Hidden(SurfaceIndex hull, Vector3 q, Vector3 n, float reach, int obstacleId)
    {
        // as the builder: looking at its own inside (the normal ray meets the object) or at anything within 0.5 m
        if (hull.Raycast(q + n * 0.01f, n, reach, -1.01f, out SurfaceHit hn, false) && (hn.Owner != obstacleId || (hn.P - q).sqrMagnitude < 0.25f)) return true;
        Vector3 a = Mathf.Abs(n.x) < 0.9f ? Vector3.Cross(n, Vector3.right).normalized : Vector3.Cross(n, Vector3.up).normalized, b = Vector3.Cross(n, a);
        foreach (Vector3 d in new[] { n, n * 0.6428f + a * 0.766f, n * 0.6428f - a * 0.766f, n * 0.6428f + b * 0.766f, n * 0.6428f - b * 0.766f })
            if (!hull.Raycast(q + n * 0.01f, d, reach, -1.01f, out _, false)) return false;
        return true;
    }

    // DESIGN-v11 G1: the main cover at the object's foot: hull points of the main bodies within 10 cm of the object that are
    // neither under the object nor under a tile but have main tiles within 1.5 tiles; the largest distance of such a point to the
    // object (the bare strip between cover and foot). -1 if there is none.
    static float maskCov = -1;
    static Func<Vector3, Vector3> toPart;
    static float MaskCoverage(List<Vector3> p, List<int> t, List<int> w, List<bool> flap, TileSettings s, TileResult r, Func<Vector3, float> outOfMask, bool[] tankFaces = null)
    {
        SurfaceIndex tiles = TileIndex(r, q => q);
        var hull = new SurfaceIndex(p.ToArray(), t.ToArray(), Vector3.zero, p.Max(q => q.magnitude) + 1, 0.05f, 0.02f);
        float cov = 0, tot = 0;
        const int N = 6;
        for (int i = 0; i + 2 < t.Count; i += 3)
        {
            if (w[i / 3] < flap.Count && flap[w[i / 3]]) continue;
            if (tankFaces != null && (!tankFaces[i / 3] || s.Obstacle != null && s.Obstacle[w[i / 3]])) continue;
            Vector3 a = p[t[i]], b = p[t[i + 1]], c = p[t[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            float area = 0.5f * n.magnitude;
            if (area < 1e-9f) continue;
            n.Normalize();
            if (tankFaces != null && Mathf.Abs(Vector3.Dot(n, s.TankAxis)) > 0.99f) continue;
            if (s.CutPlanes != null && s.CutPlanes.Any(cp => cp.x * n.x + cp.y * n.y + cp.z * n.z > 0.5f)) continue; // faces the stack: hidden by the next part
            float dA = area / (N * (N + 1) / 2);
            for (int ia = 0; ia < N; ia++)
                for (int ib = 0; ia + ib < N; ib++)
                {
                    Vector3 q = a + (b - a) * ((ia + 1f / 3) / N) + (c - a) * ((ib + 1f / 3) / N);
                    if (outOfMask(q) > -0.02f || (s.CutPlanes != null && s.CutPlanes.Any(cp => cp.x * q.x + cp.y * q.y + cp.z * q.z + cp.w > -0.02f))) continue;
                    bool kept = false;
                    for (int k = 0; s.KeepOut != null && k + 3 < s.KeepOut.Length && !kept; k += 4)
                    {
                        Vector3 d = q - s.KeepOut[k];
                        kept = Enumerable.Range(1, 3).All(e => Mathf.Abs(Vector3.Dot(d, s.KeepOut[k + e].normalized)) < s.KeepOut[k + e].magnitude + 0.02f);
                    }
                    if (kept || hull.Raycast(q + n * 0.01f, n, 0.3f, -1.01f, out _, false)) continue; // keep-out or hidden (recess, seam)
                    tot += dA;
                    if (tiles != null && tiles.Raycast(q + n * (s.Thickness + 0.05f), -n, s.Thickness + 0.06f, -1.01f, out _, false)) cov += dA;
                    else if (overrides.ContainsKey("maskHoles") && rnd.Next(int.Parse(overrides["maskHoles"])) == 0) { Vector3 pq = toPart(q), pn = (toPart(q + n * 0.1f) - pq) * 10; Console.WriteLine($"   hole at part ({pq.x:F3},{pq.y:F3},{pq.z:F3}) n=({pn.x:F2},{pn.y:F2},{pn.z:F2})"); }
                }
        }
        return tot > 0 ? cov / tot : float.NaN;
    }
    static readonly System.Random rnd = new System.Random(1);

    static int band, bareBand; // FootGap: hull points in the 10 cm band round the foot, of them bare beyond gap + 3 mm
    static float FootGap(Vector3[] p, int[] t, int[] w, int tileable, TileResult main, Func<Vector3, Vector3> toObj, Func<Vector3, Vector3> toDecal, TileSettings s, Vector3 decalN)
    {
        SurfaceIndex tiles = TileIndex(main, toObj);
        if (tiles == null) return -1;
        float rad = p.Max(q => q.magnitude) + 1;
        var obj = new SurfaceIndex(p, t.Take(tileable).ToArray(), Vector3.zero, rad, 0.05f, 0.02f);
        var all = new SurfaceIndex(p, t, Vector3.zero, rad, 0.05f, 0.02f);
        float worst = -1;
        band = bareBand = 0;
        for (int i = tileable; i + 2 < t.Length; i += 3)
        {
            Vector3 a = p[t[i]], b = p[t[i + 1]], c = p[t[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            float area = 0.5f * n.magnitude;
            if (area < 1e-9f) continue;
            n.Normalize();
            if (Vector3.Angle(n, decalN) > s.MaxWrapDeg - 10) continue; // the cover ends at the wrap line there anyway
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max((b - a).magnitude, (c - a).magnitude) / 0.01f), 1, 60);
            for (int ia = 0; ia < steps; ia++)
                for (int ib = 0; ia + ib < steps; ib++)
                {
                    Vector3 q = a + (b - a) * ((ia + 1f / 3) / steps) + (c - a) * ((ib + 1f / 3) / steps);
                    if (!obj.Closest(q, 0.1f, n, -1.01f, out SurfaceHit ho)) continue;
                    if (all.Raycast(q + n * 0.002f, n, 0.5f, -1.01f, out _, false)) continue;                // under the object or hidden
                    Vector3 qd = toDecal(q);
                    if (s.CutPlanes != null && s.CutPlanes.Any(cp => cp.x * qd.x + cp.y * qd.y + cp.z * qd.z + cp.w > -0.01f)) continue; // stack face
                    if (!tiles.Closest(q, 1.5f * s.TileSize, n, -1.01f, out _)) continue;                   // the cover ends far away anyway
                    band++;
                    if (tiles.Raycast(q + n * (s.Thickness + 0.05f), -n, s.Thickness + 0.06f, -1.01f, out _, false)) continue; // covered
                    float d = (ho.P - q).magnitude;
                    if (d > s.Gap + 0.003f) bareBand++;
                    if (d > worst && overrides.ContainsKey("footDebug")) Console.WriteLine($"   foot: bare ({q.x:F3},{q.y:F3},{q.z:F3}) n ({n.x:F2},{n.y:F2},{n.z:F2}) {d * 1000:F0} mm from the object");
                    worst = Mathf.Max(worst, d);
                }
        }
        return worst;
    }

    struct Tri
    {
        public Vector3 A, B, C;
        public Vector3 NA, NB, NC;   // vertex normals (tiles)
        public Vector2 UA, UB, UC;
        public int Kind;             // 0 hull (double sided), 1 tile, 2 joint, 3 back
        public int Bin;
        public bool Tint;            // tint=1: tiles on a separate body (flap, gear, door) drawn reddish
    }

    static void AddTris(List<Tri> scene, PartTiles p, int[] tris, Xform x, int kind, int bin)
    {
        for (int i = 0; i < tris.Length; i += 3)
            scene.Add(new Tri
            {
                A = x.Apply(p.Vertices[tris[i]]), B = x.Apply(p.Vertices[tris[i + 1]]), C = x.Apply(p.Vertices[tris[i + 2]]),
                NA = x.ApplyDir(p.Normals[tris[i]]), NB = x.ApplyDir(p.Normals[tris[i + 1]]), NC = x.ApplyDir(p.Normals[tris[i + 2]]),
                UA = p.Uvs[tris[i]], UB = p.Uvs[tris[i + 1]], UC = p.Uvs[tris[i + 2]],
                Kind = kind, Bin = bin
            });
    }

    // Builds craft text for parts stacked top to bottom on their stack nodes, with a decal at height y on the side the
    // horizontal direction side points to (default +X).
    static string StackCraft(string[] names, float decalY, Vector3 side)
    {
        side.y = 0;
        side.Normalize();
        var sb = new System.Text.StringBuilder();
        var placed = new List<CraftPart>();
        var ys = new List<float>();
        var ranges = new List<(float lo, float hi)>();
        float y = 0;
        for (int i = 0; i < names.Length; i++)
        {
            Node cfg = partConfigs[Norm(names[i])].part;
            float rescale = Flt(cfg.Get("rescaleFactor") ?? "1.25");
            float top = NodeY(cfg, "node_stack_top") * rescale, bottom = NodeY(cfg, "node_stack_bottom") * rescale;
            if (i > 0) y -= top;
            ys.Add(y);
            ranges.Add((y + bottom, y + top));
            y += bottom;
        }
        int host = Enumerable.Range(0, names.Length).FirstOrDefault(i => decalY >= ranges[i].lo && decalY <= ranges[i].hi);
        var links = Enumerable.Range(0, names.Length).Select(_ => new List<string>()).ToList();
        for (int i = 0; i < names.Length; i++)
        {
            if (i + 1 < names.Length) links[i].Add($"{names[i + 1]}_{i + 2}");
            if (i == host) links[i].Add("ablativeDecal_99");
            placed.Add(new CraftPart(Node.Parse($"PART\n{{\npart = {names[i]}_{i + 1}\npos = 0,{ys[i].ToString(CultureInfo.InvariantCulture)},0\nrot = 0,0,0,1\nattm = 0\n}}\n").Nodes[0]));
        }
        // surface point on +X at decalY: cast from outside towards the axis over all stack meshes
        var pos = new List<Vector3>();
        var tri = new List<int>();
        foreach (CraftPart p in placed)
            foreach (var (verts, tris, _) in HullMeshes(p))
            {
                int start = pos.Count;
                pos.AddRange(verts);
                tri.AddRange(tris.Select(t => t + start));
            }
        var index = new SurfaceIndex(pos.ToArray(), tri.ToArray(), Vector3.zero, 200, 0.25f, 0.05f);
        string F(float a) => a.ToString("R", CultureInfo.InvariantCulture);
        // glue=part@y@azimuth;... surface-attached parts (intakes, flaps) on the stack at height y, azimuth in degrees from +X
        // towards +Z; attach node into the hull, turned about the hull normal so an intake faces up the stack (+Y)
        var glued = new System.Text.StringBuilder();
        if (overrides.TryGetValue("glue", out string glue))
            foreach (var (g, gi) in glue.Split(';').Select((g, gi) => (g, gi)))
            {
                string[] f = g.Split('@');
                float gy = Flt(f[1]), az = Flt(f[2]) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                if (!index.Raycast(new Vector3(0, gy, 0) + dir * 30, -dir, 30, 0.1f, out SurfaceHit gh)) continue;
                Node gc = partConfigs[Norm(f[0])].part;
                string[] na = (gc.Get("node_attach") ?? "0,0,0,0,-1,0").Split(',');
                Vector3 nPos = new Vector3(Flt(na[0]), Flt(na[1]), Flt(na[2])) * Flt(gc.Get("rescaleFactor") ?? "1.25"), nDir = new Vector3(Flt(na[3]), Flt(na[4]), Flt(na[5])).normalized;
                Quaternion r1 = FromTo(nDir, -gh.N);
                var probe = new CraftPart(Node.Parse($"PART\n{{\npart = {f[0]}_0\npos = 0,0,0\nrot = 0,0,0,1\nattm = 1\n}}\n").Nodes[0]);
                Vector3 fw = IntakeFrame(probe, out _, out Vector3 pf) ? pf : Vector3.up;
                Vector3 f1 = r1 * fw, n1 = gh.N;
                Vector3 fp = (f1 - n1 * Vector3.Dot(f1, n1)).normalized, tp = (Vector3.up - n1 * Vector3.Dot(Vector3.up, n1)).normalized;
                float th = Mathf.Atan2(Vector3.Dot(Vector3.Cross(fp, tp), n1), Vector3.Dot(fp, tp));
                Quaternion r = new Quaternion(n1.x * Mathf.Sin(th / 2), n1.y * Mathf.Sin(th / 2), n1.z * Mathf.Sin(th / 2), Mathf.Cos(th / 2)) * r1;
                Vector3 gp = gh.P - r * nPos;
                int gHost = Enumerable.Range(0, names.Length).FirstOrDefault(i => gy >= ranges[i].lo && gy <= ranges[i].hi);
                links[gHost].Add($"{f[0]}_{100 + gi}");
                glued.Append($"PART\n{{\npart = {f[0]}_{100 + gi}\npos = {F(gp.x)},{F(gp.y)},{F(gp.z)}\nrot = {F(r.x)},{F(r.y)},{F(r.z)},{F(r.w)}\nattm = 1\n}}\n");
            }
        for (int i = 0; i < names.Length; i++)
            sb.Append($"PART\n{{\npart = {names[i]}_{i + 1}\npos = 0,{ys[i].ToString(CultureInfo.InvariantCulture)},0\nrot = 0,0,0,1\nattm = 0\n" +
                      string.Concat(links[i].Select(l => $"link = {l}\n")) + "}\n");
        sb.Append(glued);
        Vector3 at = new Vector3(0, decalY, 0);
        if (index.Raycast(at + side * 30, -side, 30, 0.1f, out SurfaceHit h)) at = h.P;
        // decal local -y (shieldNormal) onto +X (90° about z), then about the vessel axis onto side
        float yaw = Mathf.Atan2(-side.z, side.x) / 2;
        Quaternion rot = new Quaternion(0, Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * new Quaternion(0, 0, 0.707106781f, 0.707106781f);
        sb.Append($"PART\n{{\npart = ablativeDecal_99\npos = {F(at.x)},{F(at.y)},{F(at.z)}\n" +
                  $"rot = {F(rot.x)},{F(rot.y)},{F(rot.z)},{F(rot.w)}\nattm = 1\nMODULE\n{{\nname = ModuleAblativeDecal\n}}\n}}\n");
        return sb.ToString();
    }

    // Wing or control surface (lifting surface without a stack attach rule), as in the module: such parts surface
    // attached to a wing part are covered too.
    static readonly string[] LiftModules = { "ModuleLiftingSurface", "ModuleControlSurface", "ModuleAeroSurface" };
    static bool IsWing(CraftPart p) =>
        partConfigs.TryGetValue(Norm(p.Name), out var cfg) &&
        (cfg.part.Get("attachRules") ?? "1").Split(',')[0].Trim() == "0" &&
        cfg.part.Nodes.Any(m => m.Name == "MODULE" && LiftModules.Contains(m.Get("name")));

    // surface-attached control surfaces, fins/wings and air intakes are covered with the part they sit on (module: Glued), unless
    // they carry a decal of their own
    static bool Glued(CraftPart c) => !c.Stack && partConfigs.TryGetValue(Norm(c.Name), out var cfg) &&
        cfg.part.Nodes.Any(m => m.Name == "MODULE" && (m.Get("name") == "ModuleControlSurface" || m.Get("name") == "ModuleAeroSurface" || m.Get("name") == "ModuleResourceIntake")) &&
        !c.Children.Any(k => k.Name == "ablativeDecal");

    // 1.7: a shielded docking port (ModuleDockingNode with deployAnimationController) stacked on the covered part is an object like
    // a cargo bay (module: ShieldedPort): its shield doors tiled closed, the tiles ride with them, the docking face stays free
    static bool ShieldedPort(CraftPart c) => c.Stack && partConfigs.TryGetValue(Norm(c.Name), out var cfg) && cfg.part.Values.Count(kv => kv.Key.StartsWith("node_stack_")) == 1 &&
        cfg.part.Nodes.Any(m => m.Name == "MODULE" && m.Get("name") == "ModuleDockingNode" && int.TryParse(m.Get("deployAnimationController") ?? "-1", out int i) && i >= 0) &&
        !c.Children.Any(k => k.Name == "ablativeDecal");

    // Names of the moving transforms of the part's control surfaces.
    static string[] FlapNames(CraftPart p) =>
        partConfigs.TryGetValue(Norm(p.Name), out var cfg)
            ? cfg.part.Nodes.Where(m => m.Name == "MODULE" && (m.Get("name") == "ModuleControlSurface" || m.Get("name") == "ModuleAeroSurface"))
                .Select(m => m.Get("transformName")).Where(n => !string.IsNullOrEmpty(n)).ToArray()
            : new string[0];

    // Hull = not an engine, decoupler, docking port, fairing or decal (by the part's config modules, as in the game module).
    static bool IsHull(CraftPart p) =>
        partConfigs.TryGetValue(Norm(p.Name), out var cfg) &&
        !cfg.part.Nodes.Any(m => m.Name == "MODULE" && NotHull.Any(x => (m.Get("name") ?? "").StartsWith(x)));

    static float NodeY(Node cfg, string key)
    {
        string v = cfg.Get(key);
        return v == null ? 0 : Flt(v.Split(',')[1]);
    }

    // Parent plus node-stacked hull parts within reach (same rule as the game module).
    static List<CraftPart> Targets(CraftPart parent, bool span, Vector3 center, float reach, bool glued = false)
    {
        var list = new List<CraftPart> { parent };
        if (!span) { if (glued) list.AddRange(parent.Children.Where(c => Glued(c) && IsHull(c) || ShieldedPort(c))); return list; } // whole part: glued flaps/intakes too
        var queue = new Queue<CraftPart>(list);
        while (queue.Count > 0 && list.Count < 12)
        {
            CraftPart p = queue.Dequeue();
            bool wing = IsWing(p);
            var nbs = new List<CraftPart>();
            if (p.Parent != null && (p.Stack || wing && IsWing(p.Parent))) nbs.Add(p.Parent);
            nbs.AddRange(p.Children.Where(c => c.Stack || wing && IsWing(c) || Glued(c)));
            foreach (CraftPart nb in nbs)
            {
                if (list.Contains(nb) || !IsHull(nb) || nb.Name == "ablativeDecal") continue;
                if (!HullMeshes(nb).Any(m => m.verts.Any(q => (q - center).sqrMagnitude < reach * reach))) continue;
                list.Add(nb);
                queue.Enqueue(nb);
            }
        }
        return list;
    }

    // Render meshes of a craft part in vessel space (front-face winding kept, mirrored transforms flipped). flap: key of the
    // control surface transform the mesh hangs under (its path in the model), null for the fixed part. Models are the
    // prefab pose, i.e. the flap in neutral position.
    static IEnumerable<(Vector3[] verts, int[] tris, string flap)> HullMeshes(CraftPart p, bool skipTransparent = true, bool intakeFilter = true)
    {
        if (!partConfigs.TryGetValue(Norm(p.Name), out var cfg)) yield break;
        string[] flapNames = FlapNames(p);
        float rescale = Flt(cfg.part.Get("rescaleFactor") ?? "1.25");
        var root = new Xform { Pos = Vector3.zero, Rot = Quaternion.identity, Scale = Vector3.one * rescale };
        var modelNodes = cfg.part.Nodes.Where(m => m.Name == "MODEL").ToList();
        var entries = modelNodes.Count > 0
            ? modelNodes.Select(m => (m.Get("model"), new Xform
            {
                Pos = Vec(m.Get("position") ?? "0,0,0"),
                Rot = Xform.Euler(Vec(m.Get("rotation") ?? "0,0,0")),
                Scale = Vec(m.Get("scale") ?? "1,1,1")
            }))
            : new[] { (MeshUrl(cfg.dir, cfg.part.Get("mesh")), Xform.Identity) };
        // air intake (DESIGN-v8 B6, as the module): opening and duct left out of the surface; no intake transform, no surface
        Node intakeCfg = cfg.part.Nodes.FirstOrDefault(m => m.Name == "MODULE" && m.Get("name") == "ModuleResourceIntake");
        bool intake = skipTransparent && intakeFilter && intakeCfg != null && Str("on", "intake") != "off";
        Vector3 inPos = Vector3.zero, inFwd = Vector3.zero;
        if (intake && !IntakeFrame(p, out inPos, out inFwd)) yield break;
        float inFront = float.MinValue; // the intake's lip, as the module (v11): only faces recessed behind it are the mouth
        if (intake)
            foreach (var (model, local) in entries)
                if (Model(model) is MuModel mf)
                    foreach (MuMesh m in mf.Meshes)
                        if (m.HasRenderer && m.Vertices != null && !m.Skinned)
                            foreach (Vector3 q in m.Vertices) inFront = Mathf.Max(inFront, Vector3.Dot(p.Xform.Then(root).Then(local).Apply(q) - inPos, inFwd));
        // cargo bay doors (DESIGN-v10 G5, as the module): transforms the bay's animation moves are bodies of their own
        bool bay = (ShieldedPort(p) || cfg.part.Nodes.Any(m => m.Name == "MODULE" && m.Get("name") == "ModuleCargoBay")) && Str("on", "doors") != "off";
        int entry = 0;
        foreach (var (model, local) in entries)
        {
            entry++;
            MuModel mu = Model(model);
            if (mu == null) continue;
            Xform toVessel = p.Xform.Then(root).Then(local);
            foreach (MuMesh m in mu.Meshes)
            {
                if (!m.HasRenderer || m.Vertices == null || m.Skinned) continue; // the module reads MeshFilters only (skinned: gear)
                var verts = m.Vertices.Select(q => toVessel.Apply(q)).ToArray();
                // 1.9: a mesh mirrored inside the model (negative scale on its own transform chain, e.g. ReStock's service bay door
                // 'Door001 1') flips its winding too; only the part/MODEL mirror was honoured, so DoorOuterFace saw that door inside out
                bool mirrored = toVessel.Mirrored ^ mu.Transforms[m.Node].world.Mirrored;
                var tris = new List<int>();
                for (int s = 0; s < m.Submeshes.Count; s++)
                {
                    int mat = s < m.MaterialIds.Length ? m.MaterialIds[s] : -1;
                    string shader = mat >= 0 && mat < mu.Shaders.Count ? mu.Shaders[mat] : "";
                    if (skipTransparent && SkipShaders.Any(shader.Contains)) continue;
                    int[] t = m.Submeshes[s];
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        int i0 = t[i], i1 = mirrored ? t[i + 2] : t[i + 1], i2 = mirrored ? t[i + 1] : t[i + 2];
                        if (intake && TileBuilder.IntakeOpening(verts[i0], verts[i1], verts[i2], inPos, inFwd, inFront))
                        {
                            if (overrides.ContainsKey("intakeDebug"))
                            {
                                Vector3 fn = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]).normalized, ctr = (verts[i0] + verts[i1] + verts[i2]) / 3;
                                Console.WriteLine($"   intake drop {m.Name}: n.fwd {Vector3.Dot(fn, inFwd):F2} depth {inFront - Vector3.Dot(ctr - inPos, inFwd):F3} area {0.5f * Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]).magnitude:F4}");
                            }
                            continue;
                        }
                        tris.Add(i0);
                        tris.Add(i1);
                        tris.Add(i2);
                    }
                }
                string flap = null;
                if (m.Path != null)
                    for (int i = 0; i < m.Path.Length && flap == null; i++)
                        if (flapNames.Contains(m.Path[i]) || bay && mu.Animated.Contains(m.Path[i])) flap = entry + ":" + string.Join("/", m.Path.Take(i + 1));
                if (overrides != null && overrides.ContainsKey("meshes") && !skipTransparent) // debug: render meshes, vessel-space bounds, shaders
                {
                    Vector3 lo = Vector3.one * 1e9f, hi = -lo;
                    foreach (Vector3 q in verts) { lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q); }
                    Console.WriteLine($"  mesh {p.Name}:{string.Join("/", m.Path ?? new string[0])} v={verts.Length} lo=({lo.x:F3},{lo.y:F3},{lo.z:F3}) hi=({hi.x:F3},{hi.y:F3},{hi.z:F3}) shaders={string.Join(",", m.MaterialIds.Select(i => i >= 0 && i < mu.Shaders.Count ? mu.Shaders[i] : "?"))}");
                }
                if (tris.Count > 0) yield return (verts, tris.ToArray(), flap);
            }
        }
    }

    // Texels of a texture that look like window glass (bluish), as a [x, y] map like Dxt1 (y = 0 the top row as stored).
    static float[,] GlassMap(Texture t)
    {
        var m = new float[t.W, t.H];
        for (int y = 0; y < t.H; y++)
            for (int x = 0; x < t.W; x++)
            {
                int i = 3 * ((t.H - 1 - y) * t.W + x);
                m[x, y] = t.Rgb[i + 2] > t.Rgb[i] + 0.08f && t.Rgb[i + 2] > 0.25f ? 1 : 0;
            }
        return m;
    }

    // DXT1 .dds (debug): luminance per texel, [x, y] with y = 0 the top row as stored.
    static float[,] Dxt1(string path)
    {
        byte[] d = File.ReadAllBytes(path);
        int h = BitConverter.ToInt32(d, 12), w = BitConverter.ToInt32(d, 16), o = 128;
        var lum = new float[w, h];
        for (int by = 0; by < h / 4; by++)
            for (int bx = 0; bx < w / 4; bx++, o += 8)
            {
                int c0 = BitConverter.ToUInt16(d, o), c1 = BitConverter.ToUInt16(d, o + 2);
                uint bits = BitConverter.ToUInt32(d, o + 4);
                float L(int c) => (((c >> 11) & 31) / 31f + ((c >> 5) & 63) / 63f + (c & 31) / 31f) / 3;
                float l0 = L(c0), l1 = L(c1);
                float[] pal = c0 > c1 ? new[] { l0, l1, (2 * l0 + l1) / 3, (l0 + 2 * l1) / 3 } : new[] { l0, l1, (l0 + l1) / 2, 0 };
                for (int k = 0; k < 16; k++) lum[bx * 4 + k % 4, by * 4 + k / 4] = pal[(bits >> (2 * k)) & 3];
            }
        return lum;
    }

    // Models of a part with their model-root -> vessel transform.
    static IEnumerable<(MuModel mu, Xform toVessel)> PartModels(CraftPart p)
    {
        if (!partConfigs.TryGetValue(Norm(p.Name), out var cfg)) yield break;
        var root = new Xform { Pos = Vector3.zero, Rot = Quaternion.identity, Scale = Vector3.one * Flt(cfg.part.Get("rescaleFactor") ?? "1.25") };
        var list = cfg.part.Nodes.Where(m => m.Name == "MODEL").Select(m => (m.Get("model"), new Xform
            {
                Pos = Vec(m.Get("position") ?? "0,0,0"), Rot = Xform.Euler(Vec(m.Get("rotation") ?? "0,0,0")), Scale = Vec(m.Get("scale") ?? "1,1,1")
            })).ToList();
        if (list.Count == 0) list.Add((MeshUrl(cfg.dir, cfg.part.Get("mesh")), Xform.Identity));
        foreach (var (model, local) in list)
        {
            MuModel mu = Model(model);
            if (mu != null) yield return (mu, p.Xform.Then(root).Then(local));
        }
    }

    // ModuleAblativeDecal.IntakeOpening: front faces (n.fwd > 0.5) and walls facing the intake axis. intakeRule=v2: the
    // proposed variant (see report) with thresholds front= and wall= and the front rule only near the axis (frontR=).
    static bool IntakeOpening(Vector3 a, Vector3 b, Vector3 c, Vector3 axisPos, Vector3 fwd)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (n.sqrMagnitude < 1e-16f) return false;
        n.Normalize();
        Vector3 radial = (a + b + c) / 3 - axisPos;
        radial -= fwd * Vector3.Dot(radial, fwd);
        float front = Flt(Str("0.5", "front")), wall = Flt(Str("-0.3", "wall")), frontR = Flt(Str("1e9", "frontR"));
        if (Vector3.Dot(n, fwd) > front && radial.magnitude < frontR) return true;
        return radial.sqrMagnitude > 1e-4f && Vector3.Dot(n, radial.normalized) < wall;
    }

    // Intake transform of the part (vessel space): position and forward (= direction of travel, KSP's convention).
    static bool IntakeFrame(CraftPart p, out Vector3 pos, out Vector3 fwd)
    {
        pos = fwd = Vector3.zero;
        if (!partConfigs.TryGetValue(Norm(p.Name), out var cfg)) return false;
        Node ic = cfg.part.Nodes.FirstOrDefault(m => m.Name == "MODULE" && m.Get("name") == "ModuleResourceIntake");
        if (ic == null) return false;
        var root = new Xform { Pos = Vector3.zero, Rot = Quaternion.identity, Scale = Vector3.one * Flt(cfg.part.Get("rescaleFactor") ?? "1.25") };
        var models = cfg.part.Nodes.Where(m => m.Name == "MODEL").Select(m => (m.Get("model"), new Xform
            {
                Pos = Vec(m.Get("position") ?? "0,0,0"), Rot = Xform.Euler(Vec(m.Get("rotation") ?? "0,0,0")), Scale = Vec(m.Get("scale") ?? "1,1,1")
            })).ToList();
        if (models.Count == 0) models.Add((MeshUrl(cfg.dir, cfg.part.Get("mesh")), Xform.Identity));
        foreach (var (model, local) in models)
        {
            MuModel mu = Model(model);
            if (mu == null) continue;
            foreach (var (tn, tw) in mu.Transforms)
                if (tn == (ic.Get("intakeTransformName") ?? "Intake"))
                {
                    Xform w = p.Xform.Then(root).Then(local).Then(tw);
                    pos = w.Apply(Vector3.zero);
                    fwd = w.ApplyDir(Vector3.forward).normalized;
                    return true;
                }
        }
        return false;
    }

    // ---------- 1.9: window scanner ----------

    static readonly string[] WindowScanDirs = { "Squad", "SquadExpansion", "ReStock", "ReStockPlus", "NearFutureSpacecraft", "StationPartsExpansionRedux" };

    // Every crewed part (CrewCapacity > 0) of the mods above: triangles whose _Emissive texels are bright (max RGB > 0.25 at a corner,
    // edge midpoint or the centroid) are window glass; clustered (within 0.15 m) into part-space boxes, clusters under 0.08 m (indicator
    // LEDs) dropped. Parts from ModuleManager.ConfigCache if present, else from the mods' .cfg files with ReStock/ReStockPlus MODEL
    // replacements (Patches/**/*.cfg) applied. Writes a summary and ABLATORDECAL_WINDOWS blocks.
    static int ScanWindows(string gd, string outFile)
    {
        gameData = gd;
        overrides = new Dictionary<string, string>();
        partConfigs.Clear();
        string cache = Path.Combine(gameData, "ModuleManager.ConfigCache");
        bool fromCache = File.Exists(cache);
        if (fromCache) LoadConfigCache(cache);
        else
        {
            foreach (string mod in WindowScanDirs)
            {
                string dir = Path.Combine(gameData, mod);
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.GetFiles(dir, "*.cfg", SearchOption.AllDirectories))
                {
                    string rel = Path.GetDirectoryName(file.Substring(gameData.Length).TrimStart('\\', '/')).Replace('\\', '/');
                    if (rel.Split('/').Any(s => s.Equals("Patches", StringComparison.OrdinalIgnoreCase))) continue;
                    foreach (Node pn in SafeParse(file).Nodes.Where(n => n.Name == "PART" && n.Get("name") != null))
                        partConfigs[Norm(pn.Get("name"))] = (pn, rel);
                }
            }
            // ReStock / ReStockPlus art patches: their MODEL nodes replace the part's models (!MODEL,* / !mesh), as with ReStock installed
            int patched = 0;
            foreach (string mod in new[] { "ReStock", "ReStockPlus" })
            {
                string dir = Path.Combine(gameData, mod, "Patches");
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.GetFiles(dir, "*.cfg", SearchOption.AllDirectories))
                    foreach (Node patch in SafeParse(file).Nodes.Where(n => n.Name.StartsWith("@PART[")))
                    {
                        var models = patch.Nodes.Where(n => n.Name == "MODEL").ToList();
                        if (models.Count == 0 || patch.Name.Contains(":NEEDS[") && !NeedsMet(patch.Name)) continue;
                        string names = patch.Name.Substring(6, patch.Name.IndexOf(']') - 6);
                        foreach (string pnm in names.Split('|', ','))
                        {
                            if (!partConfigs.TryGetValue(Norm(pnm), out var pc) || pnm.Contains("*")) continue;
                            pc.part.Nodes.RemoveAll(n => n.Name == "MODEL");
                            pc.part.Values.RemoveAll(v => v.Key == "mesh");
                            pc.part.Nodes.AddRange(models);
                            patched++;
                        }
                    }
            }
            Console.WriteLine($"  {partConfigs.Count} parts from .cfg files, {patched} ReStock model replacements");
        }
        var textures = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
        Texture LoadTex(string path)
        {
            if (path == null) return null;
            if (textures.TryGetValue(path, out Texture tx)) return tx;
            try { tx = path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) ? Texture.LoadDds(path) : Texture.Load(path); }
            catch (Exception e) { Console.WriteLine($"  cannot read {path}: {e.Message}"); tx = null; }
            return textures[path] = tx;
        }
        var report = new System.Text.StringBuilder();
        var blocks = new System.Text.StringBuilder();
        int crewed = 0, windowed = 0, boxes = 0;
        foreach (var kv in partConfigs.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            Node cfg = kv.Value.part;
            string dir = kv.Value.dir;
            if (!WindowScanDirs.Any(m => dir.Equals(m, StringComparison.OrdinalIgnoreCase) || dir.StartsWith(m + "/", StringComparison.OrdinalIgnoreCase))) continue;
            if (!int.TryParse(cfg.Get("CrewCapacity") ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out int crew) || crew <= 0) continue;
            crewed++;
            var root = new Xform { Pos = Vector3.zero, Rot = Quaternion.identity, Scale = Vector3.one * Flt(cfg.Get("rescaleFactor") ?? "1.25") };
            var modelNodes = cfg.Nodes.Where(m => m.Name == "MODEL").ToList();
            var entries = modelNodes.Count > 0
                ? modelNodes.Select(m => (url: m.Get("model"), local: new Xform { Pos = Vec(m.Get("position") ?? "0,0,0"), Rot = Xform.Euler(Vec(m.Get("rotation") ?? "0,0,0")), Scale = Vec(m.Get("scale") ?? "1,1,1") },
                    texMap: m.All("texture").Select(s => s.Split(',')).Where(s => s.Length == 2).GroupBy(s => s[0].Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First()[1].Trim(), StringComparer.OrdinalIgnoreCase))).ToList()
                : new[] { (url: MeshUrl(dir, cfg.Get("mesh")), local: Xform.Identity, texMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)) }.ToList();
            var glass = new List<(Vector3 lo, Vector3 hi)>();
            var texUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (url, local, texMap) in entries)
            {
                if (string.IsNullOrEmpty(url)) continue;
                MuModel mu = Model(url);
                if (mu == null) continue;
                string muDir = Path.GetDirectoryName(Path.Combine(gameData, url.Replace('/', Path.DirectorySeparatorChar)));
                string Resolve(string texName)
                {
                    string stem = Path.GetFileNameWithoutExtension(texName);
                    if (texMap.TryGetValue(stem, out string repl)) { string rp = Path.Combine(gameData, repl.Replace('/', Path.DirectorySeparatorChar)); foreach (string ext in new[] { ".dds", ".png" }) if (File.Exists(rp + ext)) return rp + ext; }
                    foreach (string ext in new[] { ".dds", ".png" }) if (File.Exists(Path.Combine(muDir, stem + ext))) return Path.Combine(muDir, stem + ext);
                    return null;
                }
                string fallback = null; // emissive shader without a recorded map: *-e / *emit* / *lum* texture next to the model
                if (Directory.Exists(muDir))
                    fallback = Directory.GetFiles(muDir, "*.dds").FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).EndsWith("-e", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(p).IndexOf("emit", StringComparison.OrdinalIgnoreCase) >= 0 || Path.GetFileName(p).IndexOf("lum", StringComparison.OrdinalIgnoreCase) >= 0);
                Xform toPart = root.Then(local);
                foreach (MuMesh m in mu.Meshes)
                {
                    if (!m.HasRenderer || m.Skinned || m.Vertices == null || m.Uv == null || m.Uv.Length != m.Vertices.Length) continue;
                    for (int slot = 0; slot < m.Submeshes.Count; slot++)
                    {
                        int mat = slot < m.MaterialIds.Length ? m.MaterialIds[slot] : -1;
                        if (mat < 0 || mat >= mu.Shaders.Count || SkipShaders.Any(mu.Shaders[mat].Contains)) continue;
                        int ei = mat < mu.EmissiveTex.Count ? mu.EmissiveTex[mat] : -1;
                        string texPath = ei >= 0 && ei < mu.Textures.Count ? Resolve(mu.Textures[ei]) : null;
                        if (texPath == null && mu.Shaders[mat].Contains("Emissive")) texPath = fallback;
                        Texture tex = LoadTex(texPath);
                        if (tex == null) continue;
                        texUsed.Add(Path.GetFileName(texPath));
                        int[] sm = m.Submeshes[slot];
                        for (int i = 0; i + 2 < sm.Length; i += 3)
                        {
                            Vector2 a = m.Uv[sm[i]], b = m.Uv[sm[i + 1]], c = m.Uv[sm[i + 2]];
                            float bright = 0;
                            foreach (Vector2 q in new[] { a, b, c, (a + b + c) / 3, (a + b) / 2, (b + c) / 2, (c + a) / 2 })
                            {
                                Color col = tex.Sample(q);
                                bright = Mathf.Max(bright, Mathf.Max(col.r, Mathf.Max(col.g, col.b)));
                            }
                            if (bright <= 0.25f) continue;
                            Vector3 pa = toPart.Apply(m.Vertices[sm[i]]), pb = toPart.Apply(m.Vertices[sm[i + 1]]), pc = toPart.Apply(m.Vertices[sm[i + 2]]);
                            glass.Add((Vector3.Min(pa, Vector3.Min(pb, pc)), Vector3.Max(pa, Vector3.Max(pb, pc))));
                        }
                    }
                }
            }
            // ponytail: O(n^2) union-find over glass triangles, a few thousand per part at most
            var parent = Enumerable.Range(0, glass.Count).ToArray();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            const float near = 0.02f; // 1.9: 2 cm, separate panes stay separate boxes (was 15 cm: mk1-3pod got one 80 cm box)
            for (int i = 0; i < glass.Count; i++)
                for (int j = i + 1; j < glass.Count; j++)
                {
                    Vector3 gap = Vector3.Max(glass[i].lo - glass[j].hi, glass[j].lo - glass[i].hi);
                    if (Mathf.Max(gap.x, Mathf.Max(gap.y, gap.z)) <= near) parent[Find(i)] = Find(j);
                }
            var clusters = glass.Select((g, i) => (g, root: Find(i))).GroupBy(x => x.root)
                .Select(g => (lo: g.Select(x => x.g.lo).Aggregate(Vector3.Min), hi: g.Select(x => x.g.hi).Aggregate(Vector3.Max), tris: g.Count()))
                .Where(c => Mathf.Max(c.hi.x - c.lo.x, Mathf.Max(c.hi.y - c.lo.y, c.hi.z - c.lo.z)) >= 0.08f)
                .OrderBy(c => -c.hi.y).ThenBy(c => c.lo.x).ToList();
            string name = cfg.Get("name");
            report.AppendLine($"{name} ({dir}, crew {crew}): {clusters.Count} windows from {glass.Count} glass tris; emissive {(texUsed.Count > 0 ? string.Join(", ", texUsed) : "none found")}");
            if (clusters.Count == 0) continue;
            windowed++;
            boxes += clusters.Count;
            string F3(float x) => x.ToString("F3", CultureInfo.InvariantCulture);
            blocks.AppendLine("ABLATORDECAL_WINDOWS");
            blocks.AppendLine("{");
            blocks.AppendLine($"\tpart = {name}");
            foreach (var c in clusters)
            {
                Vector3 ctr = (c.lo + c.hi) / 2, size = c.hi - c.lo;
                report.AppendLine($"    window center ({F3(ctr.x)}, {F3(ctr.y)}, {F3(ctr.z)}) size ({F3(size.x)}, {F3(size.y)}, {F3(size.z)}) tris {c.tris}");
                blocks.AppendLine("\tWINDOW");
                blocks.AppendLine("\t{");
                blocks.AppendLine($"\t\tcenter = {F3(ctr.x)}, {F3(ctr.y)}, {F3(ctr.z)}");
                blocks.AppendLine($"\t\tsize = {F3(size.x)}, {F3(size.y)}, {F3(size.z)}");
                blocks.AppendLine("\t}");
            }
            blocks.AppendLine("}");
        }
        string summary = $"// AblatorDecals window scan of {gameData} ({(fromCache ? "ModuleManager.ConfigCache" : ".cfg files + ReStock patches")})\r\n" +
                         $"// crewed parts: {crewed}, with windows: {windowed}, window boxes: {boxes}\r\n";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile)));
        File.WriteAllText(outFile, summary + "\r\n/*\r\n" + report.ToString().Replace("*/", "* /") + "*/\r\n\r\n" + blocks, new System.Text.UTF8Encoding(false));
        Console.WriteLine($"  crewed parts {crewed}, with windows {windowed}, window boxes {boxes} -> {outFile}");
        return 0;
    }

    static Node SafeParse(string file)
    {
        try { return Node.Parse(File.ReadAllText(file)); }
        catch (Exception e) { Console.WriteLine($"  cannot parse {file}: {e.Message}"); return new Node { Name = "root" }; }
    }

    // :NEEDS[A,B&C,!D] against the mod folders in GameData (rough: folder names only)
    static bool NeedsMet(string header)
    {
        int s = header.IndexOf(":NEEDS[", StringComparison.Ordinal) + 7, e = header.IndexOf(']', s);
        if (e < 0) return true;
        return header.Substring(s, e - s).Split(',', '&').All(term =>
        {
            string mod = term.Trim();
            bool not = mod.StartsWith("!");
            if (not) mod = mod.Substring(1);
            return Directory.Exists(Path.Combine(gameData, mod)) != not;
        });
    }

    // mesh = file in the part's folder; like KSP, any .mu there when that file does not exist (old Squad configs)
    static string MeshUrl(string dir, string mesh)
    {
        string url = dir + "/" + Path.GetFileNameWithoutExtension(mesh ?? "model.mu");
        if (File.Exists(Path.Combine(gameData, url + ".mu"))) return url;
        string any = Directory.Exists(Path.Combine(gameData, dir)) ? Directory.GetFiles(Path.Combine(gameData, dir), "*.mu").FirstOrDefault() : null;
        return any != null ? dir + "/" + Path.GetFileNameWithoutExtension(any) : url;
    }

    static Quaternion FromTo(Vector3 a, Vector3 b)
    {
        float d = Vector3.Dot(a, b);
        Vector3 ax = d < -0.9999f ? (Mathf.Abs(a.x) < 0.9f ? Vector3.Cross(a, Vector3.right) : Vector3.Cross(a, Vector3.up)).normalized : Vector3.Cross(a, b);
        var q = d < -0.9999f ? new Quaternion(ax.x, ax.y, ax.z, 0) : new Quaternion(ax.x, ax.y, ax.z, 1 + d);
        float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
    }

    static string Str(string def, string key) => overrides != null && overrides.TryGetValue(key, out string v) ? v : def;

    static MuModel Model(string url)
    {
        if (models.TryGetValue(url, out MuModel m)) return m;
        string path = Path.Combine(gameData, url.Replace('/', Path.DirectorySeparatorChar) + ".mu");
        try { m = File.Exists(path) ? MuModel.Load(path) : null; }
        catch (Exception e) { Console.WriteLine($"  cannot read {url}: {e.Message}"); m = null; }
        if (m == null) Console.WriteLine($"  model missing: {url}");
        return models[url] = m;
    }

    static void LoadConfigCache(string path)
    {
        foreach (Node url in Node.Parse(File.ReadAllText(path)).Nodes.Where(n => n.Name == "UrlConfig"))
        {
            Node part = url.Nodes.FirstOrDefault(n => n.Name == "PART");
            if (part == null || part.Get("name") == null) continue;
            string dir = Path.GetDirectoryName(url.Get("parentUrl") ?? "").Replace('\\', '/');
            // stock=1: Squad parts from their own unpatched file (models as without ReStock; ReStock only patches the cache)
            if (overrides.ContainsKey("stock") && dir.StartsWith("Squad") && File.Exists(Path.Combine(gameData, url.Get("parentUrl"))))
                part = Node.Parse(File.ReadAllText(Path.Combine(gameData, url.Get("parentUrl")))).Nodes
                    .FirstOrDefault(n => n.Name == "PART" && n.Get("name") == part.Get("name")) ?? part;
            partConfigs[Norm(part.Get("name"))] = (part, dir);
        }
    }

    static string Norm(string partName) => partName.Replace('_', '.').Trim();
    static float Flt(string s) => float.Parse(s.Trim(), CultureInfo.InvariantCulture);
    static Vector3 Vec(string s)
    {
        float[] a = s.Split(',').Select(Flt).ToArray();
        return new Vector3(a[0], a[1], a[2]);
    }

    // ---------- tiny software renderer ----------

    class Texture
    {
        public int W, H;
        public float[] Rgb; // row 0 = top of the image

        public static Texture Load(string path)
        {
            using (var bmp = new Bitmap(path))
            {
                var t = new Texture { W = bmp.Width, H = bmp.Height, Rgb = new float[bmp.Width * bmp.Height * 3] };
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, t.W, t.H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var raw = new byte[t.W * t.H * 4];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                bmp.UnlockBits(data);
                for (int i = 0; i < t.W * t.H; i++)
                {
                    t.Rgb[3 * i] = raw[4 * i + 2] / 255f;
                    t.Rgb[3 * i + 1] = raw[4 * i + 1] / 255f;
                    t.Rgb[3 * i + 2] = raw[4 * i] / 255f;
                }
                return t;
            }
        }

        // DXT1/DXT5 .dds, first mip level (debug: part textures). DDS rows are stored top first like Rgb; KSP flips DDS
        // textures, so Unity's v = 0 is the stored top row: flipped here so Sample stays as for PNGs.
        public static Texture LoadDds(string path)
        {
            byte[] d = File.ReadAllBytes(path);
            int h = BitConverter.ToInt32(d, 12), w = BitConverter.ToInt32(d, 16), o = 128;
            bool dxt5 = System.Text.Encoding.ASCII.GetString(d, 84, 4) == "DXT5";
            var t = new Texture { W = w, H = h, Rgb = new float[w * h * 3] };
            for (int by = 0; by < h / 4; by++)
                for (int bx = 0; bx < w / 4; bx++)
                {
                    if (dxt5) o += 8;
                    int c0 = BitConverter.ToUInt16(d, o), c1 = BitConverter.ToUInt16(d, o + 2);
                    uint bits = BitConverter.ToUInt32(d, o + 4);
                    o += 8;
                    Vector3 C(int c) => new Vector3(((c >> 11) & 31) / 31f, ((c >> 5) & 63) / 63f, (c & 31) / 31f);
                    Vector3 a = C(c0), b = C(c1);
                    Vector3[] pal = c0 > c1 || dxt5 ? new[] { a, b, (2 * a + b) / 3, (a + 2 * b) / 3 } : new[] { a, b, (a + b) / 2, Vector3.zero };
                    for (int k = 0; k < 16; k++)
                    {
                        Vector3 p = pal[(bits >> (2 * k)) & 3];
                        int x = bx * 4 + k % 4, y = h - 1 - (by * 4 + k / 4), i = 3 * (y * w + x);
                        t.Rgb[i] = p.x; t.Rgb[i + 1] = p.y; t.Rgb[i + 2] = p.z;
                    }
                }
            return t;
        }

        // Bilinear, repeating. Unity UVs: v = 0 is the bottom row of the image.
        public Color Sample(Vector2 uv)
        {
            float x = (uv.x - Mathf.Floor(uv.x)) * W - 0.5f, y = (1 - (uv.y - Mathf.Floor(uv.y))) * H - 0.5f;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            Color c00 = At(x0, y0), c10 = At(x0 + 1, y0), c01 = At(x0, y0 + 1), c11 = At(x0 + 1, y0 + 1);
            return Color.Lerp(Color.Lerp(c00, c10, fx), Color.Lerp(c01, c11, fx), fy);
        }

        Color At(int x, int y)
        {
            x = ((x % W) + W) % W;
            y = ((y % H) + H) % H;
            int i = 3 * (y * W + x);
            return new Color(Rgb[i], Rgb[i + 1], Rgb[i + 2]);
        }
    }

    // Renders to PNG. Returns the number of "leak" pixels: hull or white underside seen through a slit (tile or joint
    // pixels within LeakReach on two opposite sides) inside the tiled area (tiles within 40 px on all four sides), i.e.
    // hull showing through between the tiles (mark: paint them magenta).
    const int LeakReach = 6; static Func<Vector3, Vector3> Dl; static Texture hullTex, glowTex; static float glowK, glowSkin; static Vector3 glowFlow;

    // the module's default glowRamp (GlowColor): premultiplied tint at tile temperature k
    static Color GlowRamp(float k)
    {
        float[][] ramp = { new[] { 550f, 1, 0.5f, 0.14f, 0 }, new[] { 800f, 1, 0.5f, 0.14f, 0.55f }, new[] { 1300f, 1, 1, 1, 1.2f }, new[] { 1700f, 1, 1, 1, 1.65f }, new[] { 2000f, 1, 1, 1, 2.8f } };
        float[] ga = ramp[0], gb = ramp[ramp.Length - 1];
        if (k <= ramp[0][0]) return Color.black;
        for (int i = 1; i < ramp.Length; i++) if (k <= ramp[i][0]) { ga = ramp[i - 1]; gb = ramp[i]; break; }
        float kk = gb[0] > ga[0] ? Mathf.Clamp01((k - ga[0]) / (gb[0] - ga[0])) : 1, st = Mathf.Lerp(ga[4], gb[4], kk);
        return new Color(Mathf.Lerp(ga[1], gb[1], kk) * st, Mathf.Lerp(ga[2], gb[2], kk) * st, Mathf.Lerp(ga[3], gb[3], kk) * st);
    }
    static Color LocalGlow(Vector3 n, float fMax) => GlowRamp(glowFlow == Vector3.zero ? glowK :
        (float)(glowSkin + (glowK - glowSkin) * TileGlow.LocalWeight(Mathf.Max(0, Vector3.Dot(n, glowFlow)), fMax)));
    static int Render(List<Tri> scene, Texture tex, Vector3 eye, Vector3 target, Vector3 upHint, string file, string title, bool mark)
    {
        const int W = 1100, H = 800;
        Vector3 fwd = (target - eye).normalized, right = Vector3.Cross(upHint, fwd).normalized, up = Vector3.Cross(fwd, right);
        float focal = H / (2 * Mathf.Tan(20 * Mathf.Deg2Rad));
        var z = new float[W * H];
        var px = new byte[W * H * 4];
        var kind = new byte[W * H]; // 0 background, 1 hull, 2 tile, 3 joint, 4 underside
        for (int i = 0; i < W * H; i++) { z[i] = float.MaxValue; px[4 * i] = 60; px[4 * i + 1] = 52; px[4 * i + 2] = 48; px[4 * i + 3] = 255; }
        Vector3 light = (-fwd + up * 0.6f - right * 0.4f).normalized;
        var hullCol = new Color(0.82f, 0.82f, 0.8f);
        var jointCol = new Color(0.04f, 0.04f, 0.045f);
        var backCol = new Color(0.04f, 0.04f, 0.045f); // 1.7.2: undersides dark as in the game (1.5 M4); white ones read as white edges
        float fMaxGlow = 0; // the most exposed tile face (TileGlow.LocalWeight), as the module's FMax
        if (glowTex != null && glowFlow != Vector3.zero)
            foreach (Tri t in scene)
                if (t.Kind == 1) { Vector3 fn = Vector3.Cross(t.B - t.A, t.C - t.A); if (fn.sqrMagnitude > 1e-16f) fMaxGlow = Mathf.Max(fMaxGlow, Vector3.Dot(fn.normalized, glowFlow)); }

        foreach (Tri t in scene)
        {
            Vector3 ca = Cam(t.A), cb = Cam(t.B), cc = Cam(t.C);
            if (ca.z < 0.05f || cb.z < 0.05f || cc.z < 0.05f) continue;
            Vector3 face = Vector3.Cross(t.B - t.A, t.C - t.A);
            if (face.sqrMagnitude < 1e-16f) continue;
            face.Normalize();
            bool flip = Vector3.Dot(face, eye - t.A) < 0;
            if (flip && t.Kind != 0 && t.Kind != 5) continue; // tiles: back faces culled as in the game
            Vector2 pa = Proj(ca), pb = Proj(cb), pc = Proj(cc);
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(pa.x, Math.Min(pb.x, pc.x)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(pa.x, Math.Max(pb.x, pc.x))));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(pa.y, Math.Min(pb.y, pc.y)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(pa.y, Math.Max(pb.y, pc.y))));
            float area = Edge(pa, pb, pc);
            if (Math.Abs(area) < 1e-8f) continue;
            float iza = 1 / ca.z, izb = 1 / cb.z, izc = 1 / cc.z;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Edge(pb, pc, p) / area, w1 = Edge(pc, pa, p) / area, w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    float iz = w0 * iza + w1 * izb + w2 * izc, depth = 1 / iz;
                    int idx = y * W + x;
                    if (depth >= z[idx] - 1e-5f * depth) continue;
                    z[idx] = depth;
                    kind[idx] = (byte)(t.Kind == 0 || t.Kind == 6 ? 1 : t.Kind == 1 ? 2 : t.Kind == 2 ? 3 : 4);
                    // perspective-correct barycentrics
                    float b0 = w0 * iza * depth, b1 = w1 * izb * depth, b2 = w2 * izc * depth;
                    Color col, emit = Color.clear;
                    Vector3 nrm;
                    switch (t.Kind)
                    {
                        case 1:
                            nrm = (t.NA * b0 + t.NB * b1 + t.NC * b2).normalized;
                            if (tex != null) col = tex.Sample(t.UA * b0 + t.UB * b1 + t.UC * b2);
                            else { float g = 0.22f + 0.04f * (t.Bin % 4); col = new Color(g, g, g * 1.03f); }
                            if (glowTex != null) emit = glowTex.Sample(t.UA * b0 + t.UB * b1 + t.UC * b2) * LocalGlow(face, fMaxGlow);
                            break;
                        case 2: nrm = face; col = jointCol; if (glowTex != null) emit = LocalGlow(face, fMaxGlow) * TileGlow.JointGlow; break;
                        case 3: nrm = face; col = backCol; break;
                        case 5: nrm = flip ? -face : face; col = new Color(0.9f, 0.1f, 0.8f); break;
                        case 6: nrm = flip ? -face : face; col = hullTex.Sample(t.UA * b0 + t.UB * b1 + t.UC * b2); break;
                        default: nrm = flip ? -face : face; col = hullCol; break;
                    }
                    if (t.Tint) col = new Color(col.r * 0.6f + 0.35f, col.g * 0.6f + 0.05f, col.b * 0.6f + 0.05f);
                    float shade = 0.35f + 0.65f * Mathf.Max(0, Vector3.Dot(nrm, light));
                    px[4 * idx] = (byte)(255 * Mathf.Clamp01(col.b * shade + emit.b));
                    px[4 * idx + 1] = (byte)(255 * Mathf.Clamp01(col.g * shade + emit.g));
                    px[4 * idx + 2] = (byte)(255 * Mathf.Clamp01(col.r * shade + emit.r));
                }
        }

        int leaks = 0;
        if (overrides.ContainsKey("pick")) { var pp = overrides["pick"].Split(':'); int qx = int.Parse(pp[0]), qy = int.Parse(pp[1]); float zz = z[qy * W + qx]; Vector3 wp = eye + right * ((qx + 0.5f - W / 2f) * zz / focal) + up * (-(qy + 0.5f - H / 2f) * zz / focal) + fwd * zz, lp = Dl(wp); Console.WriteLine($"   pick local ({lp.x:F3},{lp.y:F3},{lp.z:F3}) vessel ({wp.x:F3},{wp.y:F3},{wp.z:F3}) kind {kind[qy * W + qx]}"); }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int k = kind[y * W + x];
                if (k != 1 && k != 4) continue;
                // a slit: tiles close on two opposite sides, and the tiled area all around (not a notch of the border)
                bool slit = Tiled(x, y, 1, 0, LeakReach) && Tiled(x, y, -1, 0, LeakReach) || Tiled(x, y, 0, 1, LeakReach) && Tiled(x, y, 0, -1, LeakReach) ||
                            Tiled(x, y, 1, 1, LeakReach) && Tiled(x, y, -1, -1, LeakReach) || Tiled(x, y, 1, -1, LeakReach) && Tiled(x, y, -1, 1, LeakReach);
                if (!slit || !Tiled(x, y, 1, 0, 40) || !Tiled(x, y, -1, 0, 40) || !Tiled(x, y, 0, 1, 40) || !Tiled(x, y, 0, -1, 40)) continue;
                leaks++;
                if (mark) { int i = 4 * (y * W + x); px[i] = 255; px[i + 1] = 0; px[i + 2] = 255; }
                if (leaks % 40 == 1 && overrides.ContainsKey("leaks")) { float zz = z[y * W + x]; Vector3 lp = Dl(eye + right * ((x + 0.5f - W / 2f) * zz / focal) + up * (-(y + 0.5f - H / 2f) * zz / focal) + fwd * zz); Console.WriteLine($"   leak ({x},{y}) local ({lp.x:F3},{lp.y:F3},{lp.z:F3})"); }
            }

        using (var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);
            using (var gfx = System.Drawing.Graphics.FromImage(bmp))
            using (var font = new Font("Segoe UI", 14))
                gfx.DrawString(title, font, Brushes.White, 10, 8);
            bmp.Save(file, ImageFormat.Png);
        }
        return leaks;

        bool Tiled(int x, int y, int dx, int dy, int reach)
        {
            for (int i = 1; i <= reach; i++)
            {
                int xx = x + dx * i, yy = y + dy * i;
                if (xx < 0 || yy < 0 || xx >= W || yy >= H) return false;
                int k = kind[yy * W + xx];
                if (k == 2 || k == 3) return true;
                if (k == 0) return false;
            }
            return false;
        }

        Vector3 Cam(Vector3 w)
        {
            Vector3 d = w - eye;
            return new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, up), Vector3.Dot(d, fwd));
        }
        Vector2 Proj(Vector3 c) => new Vector2(W / 2f + focal * c.x / c.z, H / 2f - focal * c.y / c.z);
    }

    static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
}

class CraftPart
{
    public Node Node;
    public string Id, Name;
    public Xform Xform;
    public CraftPart Parent;
    public List<CraftPart> Children = new List<CraftPart>();
    public bool Stack => Node.Get("attm") == "0";
    public bool IsHull => !Node.Nodes.Any(m => m.Name == "MODULE" && new[] { "ModuleEngines", "ModuleDecouple", "ModuleAnchoredDecoupler", "ModuleDockingNode", "ModuleProceduralFairing", "ModuleAblativeDecal" }.Any(x => (m.Get("name") ?? "").StartsWith(x)));

    public CraftPart(Node n)
    {
        Node = n;
        Id = n.Get("part");
        Name = Id.Substring(0, Id.LastIndexOf('_'));
        float[] p = n.Get("pos").Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        float[] r = n.Get("rot").Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        Xform = new Xform { Pos = new Vector3(p[0], p[1], p[2]), Rot = new Quaternion(r[0], r[1], r[2], r[3]), Scale = Vector3.one };
    }
}

// Minimal KSP ConfigNode text parser.
class Node
{
    public string Name;
    public List<KeyValuePair<string, string>> Values = new List<KeyValuePair<string, string>>();
    public List<Node> Nodes = new List<Node>();

    public string Get(string key) => Values.Where(v => v.Key == key).Select(v => v.Value).FirstOrDefault();
    public IEnumerable<string> All(string key) => Values.Where(v => v.Key == key).Select(v => v.Value);

    public static Node Parse(string text)
    {
        var root = new Node { Name = "root" };
        var stack = new Stack<Node>();
        stack.Push(root);
        string pending = null;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw;
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) line = line.Substring(0, comment);
            line = line.Trim();
            if (line.Length == 0) continue;
            if (line.EndsWith("{") && line.Length > 1) { pending = line.TrimEnd('{').Trim(); line = "{"; }
            if (line == "{")
            {
                var node = new Node { Name = pending ?? "" };
                stack.Peek().Nodes.Add(node);
                stack.Push(node);
                pending = null;
            }
            else if (line == "}") { if (stack.Count > 1) stack.Pop(); }
            else if (line.Contains("="))
            {
                int eq = line.IndexOf('=');
                stack.Peek().Values.Add(new KeyValuePair<string, string>(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim()));
            }
            else pending = line;
        }
        return root;
    }
}
