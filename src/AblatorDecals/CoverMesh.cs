using System;
using System.IO;
using UnityEngine;

namespace AblatorDecals
{
    // DESIGN-v11 (1.5): pre-made cockpit covers. A TileResult in the cockpit's PART space as a small binary file
    // (GameData/AblatorDecals/Covers/<part>_<Hex|Square>.bin, made by tools/Preview --bake-cover). The module loads it instead of
    // building tiles for that cockpit; Transform puts it into decal space. Pure managed code (SelfCheck reads it too).
    public static class CoverMesh
    {
        private const int Magic = 0x56434441; // "ADCV"
        private const int Version = 1;

        public static void Write(Stream s, TileResult r)
        {
            var w = new BinaryWriter(s);
            w.Write(Magic); w.Write(Version);
            w.Write(r.TileCount); w.Write(r.PartialCount); w.Write(r.CoveredArea);
            w.Write(r.Parts.Length);
            foreach (PartTiles p in r.Parts)
            {
                w.Write(p.Vertices.Length);
                for (int i = 0; i < p.Vertices.Length; i++) { V3(w, p.Vertices[i]); V3(w, p.Normals[i]); w.Write(p.Uvs[i].x); w.Write(p.Uvs[i].y); }
                w.Write(p.BinTriangles.Length);
                for (int b = 0; b < p.BinTriangles.Length; b++) { Ints(w, p.BinTriangles[b]); V3(w, p.BinNormals[b]); w.Write(p.BinAreas[b]); }
                Ints(w, p.JointTriangles);
                Ints(w, p.BackTriangles);
                w.Write(p.TileAreaVectors.Length);
                foreach (Vector3 a in p.TileAreaVectors) V3(w, a);
                w.Write(p.CoveredArea);
            }
            w.Write(r.Colliders.Length);
            foreach (TileBuilder.Box b in r.Colliders) { V3(w, b.Center); V3(w, b.Forward); V3(w, b.Up); V3(w, b.Size); }
            w.Flush();
        }

        public static TileResult Read(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var rd = new BinaryReader(ms))
            {
                if (rd.ReadInt32() != Magic || rd.ReadInt32() != Version) throw new InvalidDataException("not an AblatorDecals cover file");
                var r = new TileResult { OnSurface = true, TileCount = rd.ReadInt32(), PartialCount = rd.ReadInt32(), CoveredArea = rd.ReadSingle() };
                r.Parts = new PartTiles[rd.ReadInt32()];
                for (int k = 0; k < r.Parts.Length; k++)
                {
                    var p = new PartTiles { Owner = 0 };
                    int n = rd.ReadInt32();
                    p.Vertices = new Vector3[n]; p.Normals = new Vector3[n]; p.Uvs = new Vector2[n];
                    for (int i = 0; i < n; i++) { p.Vertices[i] = V3(rd); p.Normals[i] = V3(rd); p.Uvs[i] = new Vector2(rd.ReadSingle(), rd.ReadSingle()); }
                    int bins = rd.ReadInt32();
                    p.BinTriangles = new int[bins][]; p.BinNormals = new Vector3[bins]; p.BinAreas = new float[bins];
                    for (int b = 0; b < bins; b++) { p.BinTriangles[b] = Ints(rd); p.BinNormals[b] = V3(rd); p.BinAreas[b] = rd.ReadSingle(); }
                    p.JointTriangles = Ints(rd);
                    p.BackTriangles = Ints(rd);
                    p.TileAreaVectors = new Vector3[rd.ReadInt32()];
                    for (int i = 0; i < p.TileAreaVectors.Length; i++) p.TileAreaVectors[i] = V3(rd);
                    p.CoveredArea = rd.ReadSingle();
                    r.Parts[k] = p;
                }
                r.Colliders = new TileBuilder.Box[rd.ReadInt32()];
                for (int i = 0; i < r.Colliders.Length; i++) r.Colliders[i] = new TileBuilder.Box { Center = V3(rd), Forward = V3(rd), Up = V3(rd), Size = V3(rd) };
                return r;
            }
        }

        // a copy of a cover (part space) in another space (m: part -> decal, rigid); all tiles on owner 0 (the cockpit).
        // Uvs, BinTriangles, BinAreas, JointTriangles, and BackTriangles are shared read-only with the cached TileResult.
        public static TileResult Transform(TileResult c, Matrix4x4 m)
        {
            var r = new TileResult { OnSurface = true, TileCount = c.TileCount, PartialCount = c.PartialCount, CoveredArea = c.CoveredArea, Parts = new PartTiles[c.Parts.Length] };
            for (int k = 0; k < c.Parts.Length; k++)
            {
                PartTiles p = c.Parts[k];
                r.Parts[k] = new PartTiles
                {
                    Owner = 0, Uvs = p.Uvs, BinTriangles = p.BinTriangles, BinAreas = p.BinAreas, JointTriangles = p.JointTriangles,
                    BackTriangles = p.BackTriangles, CoveredArea = p.CoveredArea,
                    Vertices = Array.ConvertAll(p.Vertices, v => m.MultiplyPoint3x4(v)),
                    Normals = Array.ConvertAll(p.Normals, v => m.MultiplyVector(v).normalized),
                    BinNormals = Array.ConvertAll(p.BinNormals, v => m.MultiplyVector(v).normalized),
                    TileAreaVectors = Array.ConvertAll(p.TileAreaVectors, v => m.MultiplyVector(v))
                };
            }
            r.Colliders = Array.ConvertAll(c.Colliders, b => new TileBuilder.Box
            {
                Center = m.MultiplyPoint3x4(b.Center), Forward = m.MultiplyVector(b.Forward).normalized, Up = m.MultiplyVector(b.Up).normalized, Size = b.Size
            });
            return r;
        }

        private static void V3(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static Vector3 V3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        private static void Ints(BinaryWriter w, int[] a) { w.Write(a.Length); foreach (int i in a) w.Write(i); }
        private static int[] Ints(BinaryReader r) { var a = new int[r.ReadInt32()]; for (int i = 0; i < a.Length; i++) a[i] = r.ReadInt32(); return a; }
    }
}
