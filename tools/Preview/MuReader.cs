using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Reads KSP .mu models (format as in KSP's PartReader) into meshes in model-root space.
// Only geometry is kept; everything else is parsed just to skip it.
public class MuMesh
{
    public Vector3[] Vertices;  // model-root space
    public Vector2[] Uv;        // first UV set (debug tools)
    public List<int[]> Submeshes = new List<int[]>();
    public int[] MaterialIds = new int[0];
    public bool HasRenderer;
    public string Name;
    public string[] Path;       // transform names from the root's child down to this mesh's transform
    public int Node;            // index into MuModel.Nodes
    public Vector3[] Local;     // mesh space
    public bool Skinned;        // SkinnedMeshRenderer: Vertices skinned in the default pose
    public string[] Bones;
    public Matrix4x4[] BindPoses;
    public int[] BoneIndex;     // 4 per vertex
    public float[] BoneWeight;  // 4 per vertex
}

public class MuNode
{
    public string Name;
    public int Parent;          // -1 for the root
    public Xform Local;
}

public class MuClip
{
    public string Name;
    public int Node;            // transform with the Animation component; curve paths start there
    public float Length;
    public List<(string path, string prop, float[] t, float[] v)> Curves = new List<(string, string, float[], float[])>();
}

public class MuModel
{
    public List<MuMesh> Meshes = new List<MuMesh>();
    public List<string> Shaders = new List<string>(); // by material id
    public List<int> EmissiveTex = new List<int>();    // by material id: index into Textures of its _Emissive map, -1 none
    public List<string> Textures = new List<string>(); // texture table (file names as stored)
    public List<(Xform world, Vector3 lo, Vector3 hi)> Airlocks = new List<(Xform, Vector3, Vector3)>(); // colliders tagged "Airlock", local bounds
    public HashSet<string> Animated = new HashSet<string>(); // names of transforms an animation clip moves (last path segment)
    public List<(string name, Xform world)> Transforms = new List<(string, Xform)>(); // every transform, model-root space
    public List<MuNode> Nodes = new List<MuNode>();       // same order as Transforms
    public List<MuClip> Clips = new List<MuClip>();

    // World (model-root) transforms of all nodes with clip sampled at normalized time (linear between keys; the clip ends,
    // used for retracted gear and closed bays, are exact). Unknown clip: the default pose.
    public Xform[] Pose(string clip, float normTime)
    {
        var local = new Xform[Nodes.Count];
        for (int i = 0; i < Nodes.Count; i++) local[i] = Nodes[i].Local;
        MuClip c = Clips.Find(x => x.Name == clip);
        if (c != null)
        {
            float time = Mathf.Clamp01(normTime) * c.Length;
            var euler = new Dictionary<int, Vector3>();
            foreach (var (path, prop, t, v) in c.Curves)
            {
                int node = c.Node;
                foreach (string seg in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int next = Nodes.FindIndex(x => x.Parent == node && x.Name == seg);
                    if (next < 0) { node = -1; break; }
                    node = next;
                }
                if (node < 0 || t.Length == 0) continue;
                int k = 0;
                while (k + 1 < t.Length && t[k + 1] <= time) k++;
                float val = k + 1 < t.Length && t[k + 1] > t[k] ? Mathf.Lerp(v[k], v[k + 1], Mathf.Clamp01((time - t[k]) / (t[k + 1] - t[k]))) : v[k];
                Xform l = local[node];
                int axis = prop.EndsWith(".x") ? 0 : prop.EndsWith(".y") ? 1 : prop.EndsWith(".z") ? 2 : 3;
                if (prop.StartsWith("m_LocalPosition")) l.Pos[axis] = val;
                else if (prop.StartsWith("m_LocalScale")) l.Scale[axis] = val;
                else if (prop.StartsWith("m_LocalRotation")) l.Rot[axis] = val;
                else if (prop.StartsWith("localEulerAngles"))
                {
                    if (!euler.TryGetValue(node, out Vector3 e)) e = Vector3.zero;
                    e[axis] = val;
                    euler[node] = e;
                }
                local[node] = l;
            }
            foreach (var kv in euler) local[kv.Key].Rot = Xform.Euler(kv.Value);
        }
        var world = new Xform[Nodes.Count];
        for (int i = 0; i < Nodes.Count; i++) // parents come first
        {
            Quaternion q = local[i].Rot;
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (m > 1e-6f) local[i].Rot = new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
            world[i] = Nodes[i].Parent < 0 ? Xform.Identity : world[Nodes[i].Parent].Then(local[i]);
        }
        return world;
    }

    // Vertices of a mesh in model-root space for a pose (skinned: sum of weight * bone * bindpose, as the GPU does).
    public Vector3[] Posed(MuMesh m, Xform[] world)
    {
        if (m.Local == null) return m.Vertices;
        var r = new Vector3[m.Local.Length];
        if (!m.Skinned || m.Bones == null || m.BindPoses == null || m.BoneIndex == null)
        {
            for (int i = 0; i < r.Length; i++) r[i] = world[m.Node].Apply(m.Local[i]);
            return r;
        }
        var bone = new Matrix4x4[m.Bones.Length];
        for (int b = 0; b < bone.Length; b++)
        {
            int n = Nodes.FindIndex(x => x.Name == m.Bones[b]);
            bone[b] = (n >= 0 ? Matrix(world[n]) : Matrix(world[m.Node])) * (b < m.BindPoses.Length ? m.BindPoses[b] : Matrix4x4.identity);
        }
        for (int i = 0; i < r.Length; i++)
        {
            Vector3 sum = Vector3.zero;
            for (int k = 0; k < 4; k++)
            {
                float w = m.BoneWeight[4 * i + k];
                if (w != 0) sum += w * bone[Mathf.Clamp(m.BoneIndex[4 * i + k], 0, bone.Length - 1)].MultiplyPoint3x4(m.Local[i]);
            }
            r[i] = sum;
        }
        return r;
    }

    static Matrix4x4 Matrix(Xform x)
    {
        Vector3 ex = x.Rot * Vector3.right * x.Scale.x, ey = x.Rot * Vector3.up * x.Scale.y, ez = x.Rot * Vector3.forward * x.Scale.z;
        var m = Matrix4x4.identity;
        m.m00 = ex.x; m.m10 = ex.y; m.m20 = ex.z;
        m.m01 = ey.x; m.m11 = ey.y; m.m21 = ey.z;
        m.m02 = ez.x; m.m12 = ez.y; m.m22 = ez.z;
        m.m03 = x.Pos.x; m.m13 = x.Pos.y; m.m23 = x.Pos.z;
        return m;
    }

    static readonly string[] OldShaders =
    {
        "KSP/Diffuse", "KSP/Diffuse", "KSP/Specular", "KSP/Bumped", "KSP/Bumped Specular", "KSP/Emissive/Diffuse",
        "KSP/Emissive/Specular", "KSP/Emissive/Bumped Specular", "KSP/Alpha/Cutoff", "KSP/Alpha/Cutoff Bumped",
        "KSP/Alpha/Translucent", "KSP/Alpha/Translucent Specular", "KSP/Alpha/Unlit Transparent", "KSP/Unlit",
        "KSP/Particles/Alpha Blended", "KSP/Particles/Additive", "KSP/Bumped Specular (Mapped)"
    };

    int version;

    public static MuModel Load(string path)
    {
        var m = new MuModel();
        using (var br = new BinaryReader(File.OpenRead(path)))
        {
            if (br.ReadInt32() != 76543) throw new InvalidDataException("not a .mu file: " + path);
            m.version = br.ReadInt32();
            br.ReadString();
            m.ReadChild(br, Xform.Identity, true, new string[0], -1);
        }
        // skinned meshes: default pose from the bones (known once every transform is read)
        Xform[] rest = null;
        foreach (MuMesh mesh in m.Meshes)
            if (mesh.Skinned) mesh.Vertices = m.Posed(mesh, rest = rest ?? m.Pose(null, 0));
        return m;
    }

    // root transform is replaced by the MODEL node's position/rotation/scale, like KSP does
    void ReadChild(BinaryReader br, Xform parent, bool isRoot, string[] parentPath, int parentNode)
    {
        string name = br.ReadString();
        string[] path = new string[parentPath.Length + 1];
        parentPath.CopyTo(path, 0);
        path[parentPath.Length] = name;
        var local = new Xform
        {
            Pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
            Rot = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
            Scale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle())
        };
        Xform world = isRoot ? Xform.Identity : parent.Then(local);
        Transforms.Add((name, world));
        int node = Nodes.Count;
        Nodes.Add(new MuNode { Name = name, Parent = parentNode, Local = isRoot ? Xform.Identity : local });
        MuMesh mesh = null;
        string tag = null;
        Vector3 blo = Vector3.zero, bhi = Vector3.zero;
        bool bounds = false;
        void Box(Vector3 size, Vector3 c) { blo = c - size / 2; bhi = c + size / 2; bounds = true; }
        void MeshBox() { var tmp = new MuMesh(); ReadMesh(br, tmp, Xform.Identity); blo = bhi = tmp.Vertices[0]; foreach (Vector3 q in tmp.Vertices) { blo = Vector3.Min(blo, q); bhi = Vector3.Max(bhi, q); } bounds = true; }
        while (br.BaseStream.Position != br.BaseStream.Length)
        {
            int entry = br.ReadInt32();
            switch (entry)
            {
                case 0: ReadChild(br, world, false, path, node); break;
                case 1: if (tag == "Airlock" && bounds) Airlocks.Add((world, blo, bhi)); return;
                case 2: SkipAnimation(br, node); break;
                case 3: br.ReadBoolean(); MeshBox(); break;                       // MeshCollider
                case 4: F(br, 4); break;                                          // SphereCollider
                case 5: F(br, 1); br.ReadInt32(); F(br, 3); break;                // CapsuleCollider
                case 6: Box(V3(br), V3(br)); break;                               // BoxCollider (size, center)
                case 7:                                                           // MeshFilter
                    mesh = mesh ?? new MuMesh { Name = name, Path = path, Node = node };
                    ReadMesh(br, mesh, world);
                    Meshes.Add(mesh);
                    break;
                case 8:                                                           // MeshRenderer
                    mesh = mesh ?? new MuMesh { Name = name, Path = path, Node = node };
                    if (version >= 1) { br.ReadBoolean(); br.ReadBoolean(); }
                    mesh.MaterialIds = Ints(br, br.ReadInt32());
                    mesh.HasRenderer = true;
                    break;
                case 9:                                                           // SkinnedMeshRenderer (vertices skinned after loading)
                {
                    var sk = new MuMesh { Name = name, Path = path, Node = node, Skinned = true, HasRenderer = true };
                    sk.MaterialIds = Ints(br, br.ReadInt32()); F(br, 6); br.ReadInt32(); br.ReadBoolean();
                    sk.Bones = new string[br.ReadInt32()];
                    for (int i = 0; i < sk.Bones.Length; i++) sk.Bones[i] = br.ReadString();
                    ReadMesh(br, sk, world);
                    Meshes.Add(sk);
                    break;
                }
                case 10:                                                          // Materials
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        int emissive = -1;
                        Shaders.Add(version >= 4 ? ReadMaterial4(br, ref emissive) : ReadMaterial(br, ref emissive));
                        EmissiveTex.Add(emissive);
                    }
                    break;
                case 12:                                                          // Textures
                    int tex = br.ReadInt32();
                    for (int i = 0; i < tex; i++) { Textures.Add(br.ReadString()); br.ReadInt32(); }
                    break;
                case 23: br.ReadInt32(); F(br, 2); F(br, 4); br.ReadInt32(); if (version > 1) F(br, 1); break; // Light
                case 24: tag = br.ReadString(); br.ReadInt32(); break;            // TagAndLayer
                case 25: br.ReadBoolean(); br.ReadBoolean(); MeshBox(); break;    // MeshCollider2
                case 26: br.ReadBoolean(); F(br, 4); break;                       // SphereCollider2
                case 27: br.ReadBoolean(); F(br, 2); br.ReadInt32(); F(br, 3); break; // CapsuleCollider2
                case 28: br.ReadBoolean(); Box(V3(br), V3(br)); break;            // BoxCollider2
                case 29: F(br, 19); break;                                        // WheelCollider
                case 30: br.ReadInt32(); br.ReadInt32(); br.ReadBoolean(); F(br, 4); break; // Camera
                case 31: SkipParticles(br); break;
                default: throw new InvalidDataException($"unknown .mu entry {entry} in {name}");
            }
        }
    }

    static Vector3 V3(BinaryReader br) => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
    static void F(BinaryReader br, int n) { for (int i = 0; i < n; i++) br.ReadSingle(); }
    static int[] Ints(BinaryReader br, int n) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = br.ReadInt32(); return a; }

    static void ReadMesh(BinaryReader br, MuMesh into = null, Xform world = default)
    {
        if (br.ReadInt32() != 13) throw new InvalidDataException("mesh start missing");
        int n = br.ReadInt32();
        br.ReadInt32();
        int entry;
        while ((entry = br.ReadInt32()) != 22)
        {
            switch (entry)
            {
                case 14:
                    var v = new Vector3[n];
                    var lv = new Vector3[n];
                    for (int i = 0; i < n; i++) v[i] = world.Apply(lv[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                    if (into != null) { into.Vertices = v; into.Local = lv; }
                    break;
                case 15:
                    var uv = new Vector2[n];
                    for (int i = 0; i < n; i++) uv[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                    if (into != null) into.Uv = uv;
                    break;
                case 16: F(br, 2 * n); break;
                case 17: F(br, 3 * n); break;
                case 18: F(br, 4 * n); break;
                case 19:
                    int[] t = Ints(br, br.ReadInt32());
                    if (into != null) into.Submeshes.Add(t);
                    break;
                case 20:
                {
                    var bi = new int[4 * n];
                    var bw = new float[4 * n];
                    for (int i = 0; i < 4 * n; i++) { bi[i] = br.ReadInt32(); bw[i] = br.ReadSingle(); }
                    if (into != null) { into.BoneIndex = bi; into.BoneWeight = bw; }
                    break;
                }
                case 21:
                {
                    var bp = new Matrix4x4[br.ReadInt32()];
                    for (int i = 0; i < bp.Length; i++) // row by row, as KSP's PartReader
                        for (int k = 0; k < 16; k++) bp[i][k / 4, k % 4] = br.ReadSingle();
                    if (into != null) into.BindPoses = bp;
                    break;
                }
                case 32: br.ReadBytes(4 * n); break;
                default: throw new InvalidDataException("unknown mesh entry " + entry);
            }
        }
    }

    static int Tex(BinaryReader br) { int i = br.ReadInt32(); F(br, 4); return i; }

    static string ReadMaterial4(BinaryReader br, ref int emissive)
    {
        br.ReadString();
        string shader = br.ReadString();
        int props = br.ReadInt32();
        for (int i = 0; i < props; i++)
        {
            string prop = br.ReadString();
            switch (br.ReadInt32())
            {
                case 0: case 1: F(br, 4); break;
                case 2: case 3: F(br, 1); break;
                case 4: int tx = Tex(br); if (prop == "_Emissive") emissive = tx; break;
            }
        }
        return shader;
    }

    // old material types 5-7 (KSP/Emissive/*) carry the emissive map as their second/last texture
    static string ReadMaterial(BinaryReader br, ref int emissive)
    {
        br.ReadString();
        int type = br.ReadInt32();
        switch (type)
        {
            case 2: Tex(br); F(br, 5); break;
            case 3: Tex(br); Tex(br); break;
            case 4: Tex(br); Tex(br); F(br, 5); break;
            case 5: Tex(br); emissive = Tex(br); F(br, 4); break;
            case 6: Tex(br); F(br, 5); emissive = Tex(br); F(br, 4); break;
            case 7: Tex(br); Tex(br); F(br, 5); emissive = Tex(br); F(br, 4); break;
            case 8: Tex(br); F(br, 1); break;
            case 9: Tex(br); Tex(br); F(br, 1); break;
            case 10: Tex(br); break;
            case 11: Tex(br); F(br, 1); F(br, 4); F(br, 1); break;
            case 12: case 13: Tex(br); F(br, 4); break;
            case 14: case 15: Tex(br); F(br, 5); break;
            case 16: Tex(br); Tex(br); Tex(br); F(br, 2); break;
            default: Tex(br); break;
        }
        return type >= 0 && type < OldShaders.Length ? OldShaders[type] : "KSP/Diffuse";
    }

    void SkipAnimation(BinaryReader br, int node)
    {
        int clips = br.ReadInt32();
        for (int c = 0; c < clips; c++)
        {
            var clip = new MuClip { Name = br.ReadString(), Node = node };
            F(br, 6); br.ReadInt32();
            int curves = br.ReadInt32();
            for (int k = 0; k < curves; k++)
            {
                string curvePath = br.ReadString(), property = br.ReadString(); br.ReadInt32(); br.ReadInt32(); br.ReadInt32();
                if (property.StartsWith("m_LocalPosition") || property.StartsWith("m_LocalRotation") || property.StartsWith("localEulerAngles"))
                    Animated.Add(curvePath.Substring(curvePath.LastIndexOf('/') + 1));
                int keys = br.ReadInt32();
                var t = new float[keys];
                var v = new float[keys];
                for (int i = 0; i < keys; i++) { t[i] = br.ReadSingle(); v[i] = br.ReadSingle(); F(br, 2); br.ReadInt32(); }
                clip.Curves.Add((curvePath, property, t, v));
                if (keys > 0) clip.Length = Math.Max(clip.Length, t[keys - 1]);
            }
            Clips.Add(clip);
        }
        br.ReadString();
        br.ReadBoolean();
    }

    static void SkipParticles(BinaryReader br)
    {
        br.ReadBoolean(); br.ReadInt32(); F(br, 6); br.ReadBoolean();
        F(br, 4); br.ReadInt32(); br.ReadInt32(); F(br, 9); F(br, 3);
        br.ReadBoolean(); br.ReadBoolean(); F(br, 20);
        F(br, 6); F(br, 1); F(br, 6); F(br, 1); br.ReadBoolean(); br.ReadBoolean(); F(br, 3);
        br.ReadInt32(); br.ReadInt32(); br.ReadInt32(); br.ReadInt32(); br.ReadInt32();
    }
}

// position / rotation / scale; composing ignores shear like KSP's typical hierarchies
public struct Xform
{
    public Vector3 Pos;
    public Quaternion Rot;
    public Vector3 Scale;

    public static Xform Identity => new Xform { Pos = Vector3.zero, Rot = Quaternion.identity, Scale = Vector3.one };

    public Vector3 Apply(Vector3 p) => Pos + Rot * Vector3.Scale(Scale, p);
    public Vector3 ApplyDir(Vector3 d) => Rot * d;

    public Xform Then(Xform child) => new Xform
    {
        Pos = Apply(child.Pos),
        Rot = Rot * child.Rot,
        Scale = Vector3.Scale(Scale, child.Scale)
    };

    // conjugate = inverse for unit quaternions (Quaternion.Inverse is native)
    static Quaternion Conj(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);

    public Vector3 InverseApply(Vector3 p)
    {
        Vector3 q = Conj(Rot) * (p - Pos);
        return new Vector3(q.x / Scale.x, q.y / Scale.y, q.z / Scale.z);
    }

    public Vector3 InverseApplyDir(Vector3 d) => Conj(Rot) * d;

    public bool Mirrored => Scale.x * Scale.y * Scale.z < 0;

    // Unity's ZXY Euler order, done in managed code (Quaternion.Euler is native)
    public static Quaternion Euler(Vector3 deg)
    {
        Vector3 r = deg * Mathf.Deg2Rad * 0.5f;
        float cx = Mathf.Cos(r.x), sx = Mathf.Sin(r.x), cy = Mathf.Cos(r.y), sy = Mathf.Sin(r.y), cz = Mathf.Cos(r.z), sz = Mathf.Sin(r.z);
        var qx = new Quaternion(sx, 0, 0, cx);
        var qy = new Quaternion(0, sy, 0, cy);
        var qz = new Quaternion(0, 0, sz, cz);
        return qy * qx * qz;
    }
}
