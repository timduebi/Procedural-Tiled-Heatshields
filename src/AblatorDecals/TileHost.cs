using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AblatorDecals
{
    // Destroys itself when the part that created it was cloned (the clone's owner field is not serialized, so it is null).
    public class AblativeDecalTilesMarker : MonoBehaviour
    {
        [NonSerialized] private PartModule owner;

        public void SetOwner(PartModule module) => owner = module;

        private void Start()
        {
            if (owner != null) return;
            Part p = GetComponentInParent<Part>();
            transform.SetParent(null, false); // out of the part's hierarchy now, so the cache reset below cannot pick it up
            Destroy(gameObject);
            if (p != null) TileHost.ResetRendererCaches(p);
        }
    }

    // The tile mesh lying on one target part: one GameObject, one mesh, one material per glow bin + joint + back.
    public class TileHost
    {
        private static readonly Color Dark = Color.clear; // no glow: premultiplied black (see ModuleAblativeDecal.GlowColor)

        public readonly Part TargetPart;
        public GameObject Go { get; private set; }

        // false once Unity destroyed the object (target part died) or after Destroy()
        public bool Alive => Go != null && Renderer != null && Mesh != null;
        public MeshRenderer Renderer { get; private set; }
        public Mesh Mesh { get; private set; }
        public Material[] Materials { get; private set; } = new Material[0]; // bins..., joint, back
        public int BinCount { get; private set; }

        // decal space data, for thermal and glow
        public Vector3[] BinNormals = new Vector3[0];
        public float[] BinAreas = new float[0];
        public Vector3[] TileAreaVectors = new Vector3[0];
        public Color[] GlowSet = new Color[0];   // colour last written to the bin material

        // host space -> decal space at build time (a flap host: decal space with the flap in neutral position)
        private Matrix4x4 hostToDecal = Matrix4x4.identity;
        private int glowPropId;
        private bool hasEmissiveTex;
        // 1.9: the template's specular colour; charred tiles fade towards matte scorch
        private Color baseSpecColor = Color.gray;
        private bool hasSpecColor;
        private static readonly Color MatteScorch = new Color(0.04f, 0.035f, 0.03f, 1f);

        // World direction in the space of the decal data above, following the host as it moves (flap deflection, flex).
        public Vector3 DecalDirection(Vector3 world)
        {
            if (Go == null) return Vector3.zero;
            Vector3 local = Go.transform.InverseTransformDirection(world);
            Vector3 d = hostToDecal.MultiplyVector(local);
            return d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.zero;
        }

        public TileHost(PartModule owner, Part targetPart, Transform parent, int layer, string name)
        {
            TargetPart = targetPart;
            Go = new GameObject(name) { layer = layer };
            Go.transform.SetParent(parent, false);
            Go.AddComponent<AblativeDecalTilesMarker>().SetOwner(owner);
            Go.AddComponent<MeshFilter>();
            Renderer = Go.AddComponent<MeshRenderer>();
            Renderer.shadowCastingMode = ShadowCastingMode.On;
            Renderer.receiveShadows = true;
        }

        // Fills the mesh (vertices go from decal space to this host's space) and makes fresh materials from the templates.
        public void Apply(PartTiles t, Matrix4x4 decalToHost, Material tile, Material joint, Material back)
        {
            if (Go == null) return;
            hasSpecColor = tile.HasProperty("_SpecColor");
            baseSpecColor = hasSpecColor ? tile.GetColor("_SpecColor") : Color.gray;
            int bins = t.BinTriangles.Length;
            BinCount = bins;
            BinNormals = t.BinNormals;
            BinAreas = t.BinAreas;
            TileAreaVectors = t.TileAreaVectors;
            hostToDecal = decalToHost.inverse;

            int n = t.Vertices.Length;
            var verts = new Vector3[n];
            var norms = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                verts[i] = decalToHost.MultiplyPoint3x4(t.Vertices[i]);
                norms[i] = decalToHost.MultiplyVector(t.Normals[i]).normalized;
            }

            if (Mesh == null)
            {
                Mesh = new Mesh { name = "AblativeDecalTiles" };
                Go.GetComponent<MeshFilter>().sharedMesh = Mesh;
            }
            Mesh.Clear();
            Mesh.indexFormat = n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            Mesh.vertices = verts;
            Mesh.normals = norms;
            Mesh.uv = t.Uvs;
            Mesh.subMeshCount = bins + 2;
            for (int i = 0; i < bins; i++) Mesh.SetTriangles(t.BinTriangles[i], i);
            Mesh.SetTriangles(t.JointTriangles, bins);
            Mesh.SetTriangles(t.BackTriangles, bins + 1);
            Mesh.RecalculateBounds();

            DestroyMaterials();
            Materials = new Material[bins + 2];
            for (int i = 0; i < bins; i++) Materials[i] = new Material(tile) { name = "AblativeDecalTile" + i };
            Materials[bins] = new Material(joint) { name = "AblativeDecalJoint" };
            Materials[bins + 1] = new Material(back) { name = "AblativeDecalBack" };
            Renderer.sharedMaterials = Materials;

            glowPropId = bins > 0 ? GlowProperty(Materials[0]) : 0;
            hasEmissiveTex = bins > 0 && Materials[0].HasProperty(EmissiveID);

            GlowSet = new Color[bins];
            for (int i = 0; i < bins; i++) SetBinGlow(i, Dark);
            JointSet = Color.black;
            streakDirty = true; // 1.10: the streak overlay follows the new tiles on its next EnsureStreaks
            streakAlpha = new float[bins];
        }

        // editor: exact (non-convex) collider on the tile mesh for clicks and surface attach; off (removed) on gear hosts
        public void SetMeshCollider(bool want, bool enabled)
        {
            if (Go == null) return;
            MeshCollider mc = Go.GetComponent<MeshCollider>();
            if (!want)
            {
                if (mc != null) Object.Destroy(mc);
                return;
            }
            if (mc == null) mc = Go.AddComponent<MeshCollider>();
            mc.sharedMesh = null; // re-cook after the mesh changed
            mc.sharedMesh = Mesh;
            mc.enabled = enabled;
        }

        public void EnableCollider(bool on)
        {
            MeshCollider mc = Go != null ? Go.GetComponent<MeshCollider>() : null;
            if (mc != null) mc.enabled = on;
        }

        // 1.5 (M1): the glow goes into _EmissiveColor (KSP/Emissive/*: _Emissive texture x _EmissiveColor, per tile a soft core
        // to rim gradient); a shader without it falls back to _TemperatureColor (flat per bin, as up to 1.4)
        public static readonly int EmissiveColorID = Shader.PropertyToID("_EmissiveColor"), EmissiveID = Shader.PropertyToID("_Emissive");
        public bool HasGlow => BinCount > 0 && glowPropId != 0;
        public static int GlowProperty(Material m) =>
            m.HasProperty(EmissiveColorID) ? EmissiveColorID : m.HasProperty(PhysicsGlobals.TemperaturePropertyID) ? PhysicsGlobals.TemperaturePropertyID : 0;

        // true while the renderer still holds our materials with our textures
        public bool Intact(Texture tex, Texture glow)
        {
            if (Renderer == null) return false;
            Material[] now = Renderer.sharedMaterials;
            if (now == null || now.Length != Materials.Length) return false;
            for (int i = 0; i < now.Length; i++)
                if (now[i] != Materials[i]) return false;
            for (int i = 0; i < BinCount; i++)
            {
                if (tex != null && Materials[i].mainTexture != tex) return false;
                if (glow != null && hasEmissiveTex && Materials[i].GetTexture(EmissiveID) != glow) return false;
            }
            return true;
        }

        // what differs (for the log): first slot whose material is not ours, or the texture
        public string Difference(Texture tex)
        {
            if (Renderer == null) return "renderer gone";
            Material[] now = Renderer.sharedMaterials;
            if (now.Length != Materials.Length) return $"{now.Length} materials instead of {Materials.Length}";
            for (int i = 0; i < now.Length; i++)
                if (now[i] != Materials[i])
                    return $"slot {i}: '{(now[i] != null ? now[i].name : "null")}' shader '{(now[i] != null && now[i].shader != null ? now[i].shader.name : "?")}'";
            return $"texture '{(BinCount > 0 && Materials[0].mainTexture != null ? Materials[0].mainTexture.name : "null")}' instead of '{(tex != null ? tex.name : "null")}'";
        }

        public void Restore(Texture tex, Texture glow)
        {
            if (Renderer == null) return;
            Material[] old = Renderer.sharedMaterials;
            Renderer.sharedMaterials = Materials;
            if (old != null)
            {
                for (int i = 0; i < old.Length; i++)
                {
                    Material m = old[i];
                    if (m != null && Array.IndexOf(Materials, m) < 0 && m.name != null && m.name.StartsWith("AblativeDecal") && m.name.Contains("(Instance)"))
                        Object.Destroy(m);
                }
            }
            for (int i = 0; i < BinCount; i++)
            {
                Materials[i].mainTexture = tex;
                if (glow != null && Materials[i].HasProperty(EmissiveID)) Materials[i].SetTexture(EmissiveID, glow);
            }
        }

        public void SetChar(Color c, float charAmt = 0f)
        {
            if (Go == null) return;
            for (int i = 0; i < BinCount; i++) SetBinChar(i, c, charAmt);
        }

        // 1.8: per-bin char (directional soot: windward bins darker); 1.9: the specular fades to matte scorch with charAmt
        public void SetBinChar(int bin, Color c, float charAmt = 0f)
        {
            if (Go == null || bin < 0 || bin >= BinCount || bin >= Materials.Length) return;
            Material m = Materials[bin];
            if (m == null) return;
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (hasSpecColor && m.HasProperty("_SpecColor")) m.SetColor("_SpecColor", Color.Lerp(baseSpecColor, MatteScorch, Mathf.Clamp01(charAmt * 0.9f)));
        }

        // 1.7.1: the joint material glows too (KSP/Emissive/Diffuse), one colour per host
        public Color JointSet = Color.black;
        public void SetJointGlow(Color c)
        {
            if (BinCount <= 0 || BinCount >= Materials.Length) return;
            Material m = Materials[BinCount];
            if (m != null && m.HasProperty(EmissiveColorID)) m.SetColor(EmissiveColorID, c);
            JointSet = c;
        }

        public void SetBinGlow(int bin, Color c)
        {
            if (bin < 0 || bin >= BinCount) return;
            if (glowPropId != 0) Materials[bin].SetColor(glowPropId, c);
        }

        private void DestroyMaterials()
        {
            foreach (Material m in Materials) if (m != null) Object.Destroy(m);
            Materials = new Material[0];
            glowPropId = 0;
            hasEmissiveTex = false;
        }

        // ---------- 1.10 reentry streaks ----------
        // A child object with the tile tops (lifted 1.2 mm along the normals) and the streak texture, UVs projected in vessel
        // root space along the reentry flow, so streaks run unbroken across module seams. One material per glow bin (alpha =
        // that bin's char). Made on the first non-zero char, kept (only refilled) on rebuilds so no part caches a dead renderer.
        private GameObject streakGo;
        private MeshRenderer streakRenderer;
        private Mesh streakMesh;
        private Material[] streakMats = new Material[0];
        private float[] streakAlpha = new float[0];
        private bool streakDirty = true;
        private Vector3[] streakRootPos = new Vector3[0], streakRootN = new Vector3[0]; // per streak vertex, vessel root space
        private Vector3 streakFlow;
        private Texture streakTex;
        private const float StreakLift = 0.0018f, StreakU = 1.8f, StreakV = 3.2f;

        public void SetStreakAlpha(int bin, float a)
        {
            if (bin >= 0 && bin < streakAlpha.Length) streakAlpha[bin] = a;
        }

        // root: vessel root transform; flow: reentry airflow in root space (unit). Call after SetStreakAlpha for all bins.
        public void EnsureStreaks(Texture tex, Shader shader, Transform root, Vector3 flow)
        {
            if (Go == null || Mesh == null || root == null || tex == null || shader == null || BinCount == 0) return;
            bool any = false;
            foreach (float a in streakAlpha) if (a > 0.001f) { any = true; break; }
            if (streakGo == null)
            {
                if (!any) return;
                streakGo = new GameObject("AblativeDecalStreaks") { layer = Go.layer };
                streakGo.transform.SetParent(Go.transform, false);
                streakGo.AddComponent<MeshFilter>().sharedMesh = streakMesh = new Mesh { name = "AblativeDecalStreaks" };
                streakRenderer = streakGo.AddComponent<MeshRenderer>();
                streakRenderer.shadowCastingMode = ShadowCastingMode.Off;
                streakRenderer.receiveShadows = true;
                streakDirty = true;
                Part holder = Go.GetComponentInParent<Part>();
                if (holder != null) ResetRendererCaches(holder);
            }
            streakGo.layer = Go.layer;
            if (streakDirty || streakMats.Length != BinCount || streakTex != tex || (streakMats.Length > 0 && streakMats[0].shader != shader))
                BuildStreaks(tex, shader, root, flow);
            else if (Vector3.Angle(streakFlow, flow) > 10f) ProjectStreaks(flow);
            for (int b = 0; b < streakMats.Length; b++)
            {
                float a = b < streakAlpha.Length ? streakAlpha[b] : 0;
                if (Mathf.Abs(streakMats[b].color.a - a) > 0.002f) streakMats[b].color = new Color(1, 1, 1, a);
            }
            Material[] now = streakRenderer.sharedMaterials;
            bool ours = now.Length == streakMats.Length;
            for (int i = 0; ours && i < now.Length; i++) ours = now[i] == streakMats[i];
            if (!ours) streakRenderer.sharedMaterials = streakMats; // KSP's highlighter may have instanced them
            streakRenderer.enabled = any;
        }

        private void BuildStreaks(Texture tex, Shader shader, Transform root, Vector3 flow)
        {
            streakDirty = false;
            streakTex = tex;
            Vector3[] hv = Mesh.vertices, hn = Mesh.normals;
            Matrix4x4 toRoot = root.worldToLocalMatrix * Go.transform.localToWorldMatrix;
            var pos = new List<Vector3>();
            var nrm = new List<Vector3>();
            var rootPos = new List<Vector3>();
            var rootN = new List<Vector3>();
            var subs = new int[BinCount][];
            for (int b = 0; b < BinCount; b++)
            {
                int[] tris = Mesh.GetTriangles(b);
                var outTris = new List<int>(tris.Length);
                Vector3 binN = BinNormals != null && b < BinNormals.Length ? BinNormals[b] : Vector3.zero;
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    Vector3 a = hv[tris[t]], c1 = hv[tris[t + 1]], c2 = hv[tris[t + 2]];
                    Vector3 fn = Vector3.Cross(c1 - a, c2 - a);
                    if (fn.sqrMagnitude < 1e-14f) continue;
                    // tile tops only: walls and bevels turn away from the bin's direction (decal space)
                    if (binN != Vector3.zero && Vector3.Dot(hostToDecal.MultiplyVector(fn).normalized, binN) < 0.6f) continue;
                    Vector3 rn = toRoot.MultiplyVector(fn).normalized;
                    for (int k = 0; k < 3; k++)
                    {
                        int vi = tris[t + k];
                        Vector3 p = hv[vi] + hn[vi] * StreakLift;
                        outTris.Add(pos.Count);
                        pos.Add(p);
                        nrm.Add(hn[vi]);
                        rootPos.Add(toRoot.MultiplyPoint3x4(p));
                        rootN.Add(rn); // per face: one projection axis per triangle
                    }
                }
                subs[b] = outTris.ToArray();
            }
            streakRootPos = rootPos.ToArray();
            streakRootN = rootN.ToArray();
            streakMesh.Clear();
            streakMesh.indexFormat = pos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            streakMesh.SetVertices(pos);
            streakMesh.SetNormals(nrm);
            streakMesh.subMeshCount = BinCount;
            for (int b = 0; b < BinCount; b++) streakMesh.SetTriangles(subs[b], b);
            ProjectStreaks(flow);
            streakMesh.RecalculateBounds();

            if (streakMats.Length != BinCount || (streakMats.Length > 0 && streakMats[0].shader != shader))
            {
                foreach (Material m in streakMats) if (m != null) Object.Destroy(m);
                streakMats = new Material[BinCount];
                for (int b = 0; b < BinCount; b++)
                {
                    var m = new Material(shader) { name = "AblativeDecalStreaks" + b, renderQueue = 3001, color = new Color(1, 1, 1, 0) };
                    if (m.HasProperty("_Opacity")) m.SetFloat("_Opacity", 1f);
                    streakMats[b] = m;
                }
            }
            foreach (Material m in streakMats) m.mainTexture = tex;
            streakRenderer.sharedMaterials = streakMats;
        }

        // u across the flow (on the axis perpendicular to the flow that lies most in the face), v along it, in metres / tile size
        private void ProjectStreaks(Vector3 flow)
        {
            streakFlow = flow;
            Vector3 right = Vector3.Cross(flow, Mathf.Abs(flow.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
            Vector3 third = Vector3.Cross(right, flow);
            var uv = new Vector2[streakRootPos.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                Vector3 p = streakRootPos[i], n = streakRootN[i];
                Vector3 across = Mathf.Abs(Vector3.Dot(n, third)) >= Mathf.Abs(Vector3.Dot(n, right)) ? right : third;
                uv[i] = new Vector2(Vector3.Dot(p, across) / StreakU, Vector3.Dot(p, flow) / StreakV);
            }
            streakMesh.uv = uv;
        }

        // KSP caches a part's renderers: the model caches, Part.HighlightRenderer (Part.SetOpacity calls SetPropertyBlock on
        // every entry without a null check) and the Highlighter. Call after renderers under the part's model came or went;
        // everything is rebuilt from the live hierarchy, so no destroyed renderer can stay in a list.
        public static void ResetRendererCaches(Part p)
        {
            p.ResetModelRenderersCache();
            p.ResetModelMeshRenderersCache();
            p.ResetModelSkinnedMeshRenderersCache();
            List<Renderer> list = p.FindModelRenderersCached(); // a fresh copy
            list.RemoveNonHighlightableRenderers();
            p.HighlightRenderer = list;
            p.RefreshHighlighter();
        }

        public void Destroy()
        {
            DestroyMaterials();
            foreach (Material m in streakMats) if (m != null) Object.Destroy(m);
            streakMats = new Material[0];
            if (streakMesh != null) Object.Destroy(streakMesh);
            streakMesh = null;
            streakRenderer = null; // its GameObject is a child of Go and goes with it
            if (Mesh != null) Object.Destroy(Mesh);
            if (Go != null)
            {
                // Destroy is deferred to the end of the frame, and Part.FindModelComponents walks transforms regardless of
                // the active state: leave the part's hierarchy now so a cache rebuilt this frame cannot hold the dying renderer.
                Go.SetActive(false);
                Go.transform.SetParent(null, false);
                Object.Destroy(Go);
            }
            Mesh = null;
            Go = null;
            Renderer = null;
        }
    }
}
