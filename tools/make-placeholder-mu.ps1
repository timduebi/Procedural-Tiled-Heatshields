# Writes GameData/AblatorDecals/Assets/placeholder.mu: a small cluster of 7 hex tiles (facing -Y, 3 shades).
# KSP renders the part list icon from it; in the editor/flight ModuleAblativeDecal hides it and builds the real tiles.
# Format follows KSP's PartReader (.mu version 4). Own file, so mods like ReStock that remove stock models can't break the part.
param([string]$Out = "$PSScriptRoot\..\GameData\AblatorDecals\Assets\placeholder.mu")

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;

public static class PlaceholderMu
{
    // PartToolsLib.EntryType values
    const int ChildTransformEnd = 1, MeshFilter = 7, MeshRenderer = 8, Materials = 10;
    const int MeshStart = 13, MeshVerts = 14, MeshUV = 15, MeshNormals = 17, MeshTriangles = 19, MeshEnd = 22;

    static readonly List<float[]> verts = new List<float[]>(), norms = new List<float[]>();
    static readonly List<int>[] tris = { new List<int>(), new List<int>(), new List<int>() };

    static float[] Sub(float[] a, float[] b) { return new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] }; }
    static float[] Cross(float[] a, float[] b) { return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] }; }
    static float Dot(float[] a, float[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    static int V(float[] p, float[] n) { verts.Add(p); norms.Add(n); return verts.Count - 1; }

    // Unity front face: Cross(b - a, c - a) points along the face normal
    static void Tri(List<int> list, int a, int b, int c, float[] n)
    {
        float[] f = Cross(Sub(verts[b], verts[a]), Sub(verts[c], verts[a]));
        list.Add(a);
        if (Dot(f, n) >= 0) { list.Add(b); list.Add(c); } else { list.Add(c); list.Add(b); }
    }

    public static void Write(string path)
    {
        verts.Clear(); norms.Clear(); foreach (var t in tris) t.Clear();
        const float size = 0.25f, thick = 0.04f;
        float r = size / (float)Math.Sqrt(3) * 0.97f;
        float[][] shades = { new[] { 0.22f, 0.22f, 0.23f }, new[] { 0.16f, 0.16f, 0.165f }, new[] { 0.29f, 0.28f, 0.27f } };
        float[] down = { 0, -1, 0 };

        var centers = new List<float[]> { new float[] { 0, 0 } };
        for (int k = 0; k < 6; k++) centers.Add(new[] { size * (float)Math.Cos(Math.PI / 3 * k), size * (float)Math.Sin(Math.PI / 3 * k) });
        for (int t = 0; t < centers.Count; t++)
        {
            float cx = centers[t][0], cz = centers[t][1];
            List<int> list = tris[t % 3];
            var top = new float[6][];
            var bottom = new float[6][];
            for (int k = 0; k < 6; k++)
            {
                double a = (30 + 60 * k) * Math.PI / 180;
                top[k] = new[] { cx + r * (float)Math.Cos(a), -thick, cz + r * (float)Math.Sin(a) };
                bottom[k] = new[] { top[k][0], 0f, top[k][2] };
            }
            var ti = new int[6];
            for (int k = 0; k < 6; k++) ti[k] = V(top[k], down);
            for (int k = 1; k < 5; k++) Tri(list, ti[0], ti[k], ti[k + 1], down);
            for (int k = 0; k < 6; k++)
            {
                int k1 = (k + 1) % 6;
                float mx = (top[k][0] + top[k1][0]) / 2 - cx, mz = (top[k][2] + top[k1][2]) / 2 - cz;
                float len = (float)Math.Sqrt(mx * mx + mz * mz);
                float[] n = { mx / len, 0, mz / len };
                int i0 = V(top[k], n), i1 = V(top[k1], n), i2 = V(bottom[k1], n), i3 = V(bottom[k], n);
                Tri(list, i0, i1, i2, n);
                Tri(list, i0, i2, i3, n);
            }
        }

        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(76543); w.Write(4);            // FileType.ModelBinary, version
            w.Write("placeholder");
            w.Write("AblativeDecalPlaceholder");    // root transform
            foreach (float f in new float[] { 0, 0, 0, 0, 0, 0, 1, 1, 1, 1 }) w.Write(f); // position, rotation, scale

            w.Write(MeshFilter);
            w.Write(MeshStart); w.Write(verts.Count); w.Write(tris.Length);
            w.Write(MeshVerts); foreach (var p in verts) foreach (float f in p) w.Write(f);
            w.Write(MeshUV); foreach (var p in verts) { w.Write(p[0] + 0.5f); w.Write(p[2] + 0.5f); }
            w.Write(MeshNormals); foreach (var p in norms) foreach (float f in p) w.Write(f);
            foreach (var list in tris) { w.Write(MeshTriangles); w.Write(list.Count); foreach (int i in list) w.Write(i); } // one submesh each
            w.Write(MeshEnd);

            w.Write(MeshRenderer);
            w.Write(true); w.Write(true);           // cast / receive shadows
            w.Write(tris.Length); for (int i = 0; i < tris.Length; i++) w.Write(i);

            w.Write(Materials); w.Write(tris.Length);
            for (int i = 0; i < tris.Length; i++)
            {
                w.Write("placeholder" + i);
                w.Write("KSP/Diffuse");
                w.Write(1);                         // one shader property
                w.Write("_Color"); w.Write(0);      // ShaderPropertyType.Color
                foreach (float f in shades[i]) w.Write(f);
                w.Write(1f);
            }
            w.Write(ChildTransformEnd);
        }
    }

    public static int VertexCount { get { return verts.Count; } }
}
'@

New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
[PlaceholderMu]::Write((Resolve-Path (Split-Path $Out)).Path + "\" + (Split-Path $Out -Leaf))
"wrote $Out ($((Get-Item $Out).Length) bytes, $([PlaceholderMu]::VertexCount) vertices)"
