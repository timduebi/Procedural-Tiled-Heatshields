using System.Linq;
using KSP.Localization;
using KSP.UI.Screens;
using UnityEngine;

namespace AblatorDecals
{
    // Editor toolbar button: one small window that sets pattern, finish, tile size and thickness on every heat shield decal of the craft.
    // Single decals stay adjustable in their own menu.
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class DecalWindow : MonoBehaviour
    {
        private const string L = "#LOC_AblatorDecals_";
        private const string Lock = "AblatorDecalsWindow";
        private static readonly string[] Patterns = { "Hex", "Square" }, Finishes = { "Black", "Ceramic", "White", "Steel" };
        // 1.8 presets: localization key suffix, pattern, finish (all 0.10 m tiles, 0.025 m thick)
        private static readonly string[,] Presets = { { "starship", "Hex", "Black" }, { "shuttle", "Square", "Black" }, { "shuttle_white", "Square", "White" }, { "delta", "Hex", "Steel" } };

        private ApplicationLauncherButton button;
        private bool show;
        private Rect rect = new Rect(320, 140, 280, 0);
        private int pattern, finish;
        private float tileSize = 0.1f;
        private float thickness = 0.025f;
        private string[] patternNames, finishNames, presetNames;
        private string titleLabel, patternLabel, finishLabel, tileSizeLabel, thicknessLabel, applyAllLabel, presetsLabel, highlightAllLabel, tileBellyLabel, bellyDirLabel;
        // 1.9 "tile whole belly": Auto (SPH -Y, VAB the root's +Z), or an explicit world direction
        private static readonly string[] BellyDirs = { "Auto", "-Y", "+Z", "-Z", "+X", "-X" };
        private int bellyDir;
        private bool inputLocked;

        private void Start()
        {
            patternNames = Patterns.Select(p => Localizer.Format(L + "pattern_" + p.ToLowerInvariant())).ToArray();
            finishNames = Finishes.Select(f => Localizer.Format(L + "finish_" + f.ToLowerInvariant())).ToArray();
            titleLabel = Localizer.Format(L + "app_title");
            patternLabel = Localizer.Format(L + "pattern");
            finishLabel = Localizer.Format(L + "finish");
            tileSizeLabel = Localizer.Format(L + "tileSize");
            thicknessLabel = Localizer.Format(L + "thickness");
            applyAllLabel = Localizer.Format(L + "app_applyAll");
            presetsLabel = Localizer.Format(L + "app_presets");
            highlightAllLabel = Localizer.Format(L + "app_highlightAll");
            tileBellyLabel = Localizer.Format(L + "app_tileBelly");
            bellyDirLabel = Localizer.Format(L + "app_bellyDir");
            presetNames = Enumerable.Range(0, Presets.GetLength(0)).Select(i => Localizer.Format(L + "preset_" + Presets[i, 0])).ToArray();
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            if (ApplicationLauncher.Ready) AddButton();
        }

        private void AddButton()
        {
            if (button != null || ApplicationLauncher.Instance == null) return;
            Texture2D icon = GameDatabase.Instance.GetTexture("AblatorDecals/Icons/AppIcon", false);
            button = ApplicationLauncher.Instance.AddModApplication(Open, Close, null, null, null, null,
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH, icon != null ? icon : Texture2D.whiteTexture);
        }

        private void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            ModuleAblativeDecal.HighlightAll = false;
            if (inputLocked)
            {
                InputLockManager.RemoveControlLock(Lock);
                inputLocked = false;
            }
            if (button != null && ApplicationLauncher.Instance != null) ApplicationLauncher.Instance.RemoveModApplication(button);
        }

        // start from the first decal on the craft
        private void Open()
        {
            show = true;
            ModuleAblativeDecal first = Decals().FirstOrDefault();
            if (first == null) return;
            pattern = System.Math.Max(0, System.Array.IndexOf(Patterns, first.PatternValue));
            finish = System.Math.Max(0, System.Array.IndexOf(Finishes, first.FinishValue));
            tileSize = first.TileSizeValue;
            thickness = first.ThicknessValue;
        }

        private void Close()
        {
            show = false;
            if (inputLocked)
            {
                InputLockManager.RemoveControlLock(Lock);
                inputLocked = false;
            }
        }

        private static System.Collections.Generic.IEnumerable<ModuleAblativeDecal> Decals() =>
            EditorLogic.fetch != null && EditorLogic.fetch.ship != null
                ? EditorLogic.fetch.ship.parts.Where(p => p != null).Select(p => p.FindModuleImplementing<ModuleAblativeDecal>()).Where(m => m != null)
                : Enumerable.Empty<ModuleAblativeDecal>();

        private void OnGUI()
        {
            if (!show) return;
            rect = GUILayout.Window(GetInstanceID(), rect, Draw, titleLabel);
            // clicks on the window must not pick parts behind it
            bool wantLock = rect.Contains(Event.current.mousePosition);
            if (wantLock != inputLocked)
            {
                if (wantLock) InputLockManager.SetControlLock(ControlTypes.EDITOR_LOCK, Lock);
                else InputLockManager.RemoveControlLock(Lock);
                inputLocked = wantLock;
            }
        }

        private void Draw(int id)
        {
            GUILayout.Label(presetsLabel);
            int preset = GUILayout.SelectionGrid(-1, presetNames, 2);
            if (preset >= 0)
            {
                pattern = System.Array.IndexOf(Patterns, Presets[preset, 1]);
                finish = System.Array.IndexOf(Finishes, Presets[preset, 2]);
                tileSize = 0.1f;
                thickness = 0.025f;
            }
            GUILayout.Label(patternLabel);
            pattern = GUILayout.Toolbar(pattern, patternNames);
            GUILayout.Label(finishLabel);
            finish = GUILayout.SelectionGrid(finish, finishNames, 2);
            GUILayout.Label($"{tileSizeLabel}: {tileSize:F3} m");
            tileSize = Mathf.Round(GUILayout.HorizontalSlider(tileSize, 0.03f, 0.5f) * 200f) / 200f; // 5 mm steps
            GUILayout.Label($"{thicknessLabel}: {thickness:F3} m");
            thickness = Mathf.Round(GUILayout.HorizontalSlider(thickness, 0.005f, 0.3f) * 1000f) / 1000f; // 1 mm steps
            GUILayout.Space(6);
            ModuleAblativeDecal.HighlightAll = GUILayout.Toggle(ModuleAblativeDecal.HighlightAll, highlightAllLabel);
            if (GUILayout.Button(applyAllLabel)) ApplyAll();
            GUILayout.Space(6);
            GUILayout.Label(bellyDirLabel);
            bellyDir = GUILayout.SelectionGrid(bellyDir, BellyDirs, 6);
            if (GUILayout.Button(tileBellyLabel)) TileWholeBelly(BellyDirection());
            GUI.DragWindow();
        }

        private void ApplyAll()
        {
            bool any = false;
            foreach (ModuleAblativeDecal m in Decals().ToList())
            {
                m.ApplyGlobal(Patterns[pattern], Finishes[finish], tileSize, thickness);
                any = true;
            }
            if (any) EditorLogic.fetch.SetBackup(); // one undo step; also fires onEditorShipModified
        }

        private Vector3 BellyDirection()
        {
            switch (bellyDir)
            {
                case 1: return Vector3.down;
                case 2: return Vector3.forward;
                case 3: return Vector3.back;
                case 4: return Vector3.right;
                case 5: return Vector3.left;
            }
            Part root = EditorLogic.RootPart;
            if (EditorDriver.editorFacility == EditorFacility.SPH || root == null) return Vector3.down;
            return root.partTransform.forward.normalized; // VAB: +Z is the belly of Mk1/Mk2/Mk3 shuttles stood up
        }

        private static readonly string[] FuelResources = { "LiquidFuel", "Oxidizer", "LqdHydrogen", "LqdMethane" };

        // a hull part a decal belongs on: surface-attachable, no engine/wheel/panel/chute/clamp/strut/light, no small radial RCS block
        // or antenna, not already tiled by a decal on its parent
        private static bool BellyHull(Part p)
        {
            if (p == null || p.partInfo == null || p.partInfo.name == "ablativeDecal" || !p.attachRules.allowSrfAttach || p is CompoundPart) return false;
            if (p.children.Any(c => c != null && c.FindModuleImplementing<ModuleAblativeDecal>() != null)) return false;
            if (p.FindModuleImplementing<ModuleEngines>() != null || p.FindModuleImplementing<ModuleWheelBase>() != null ||
                p.FindModuleImplementing<ModuleDeployableSolarPanel>() != null || p.FindModuleImplementing<ModuleDeployableRadiator>() != null ||
                p.FindModuleImplementing<ModuleParachute>() != null || p.FindModuleImplementing<LaunchClamp>() != null || p.FindModuleImplementing<ModuleLight>() != null) return false;
            if (p.CrewCapacity == 0 && (p.FindModuleImplementing<ModuleRCS>() != null || p.FindModuleImplementing<ModuleDataTransmitter>() != null) &&
                !p.Resources.Cast<PartResource>().Any(r => FuelResources.Contains(r.resourceName)) &&
                p.FindModuleImplementing<ModuleCommand>() == null && p.FindModuleImplementing<ModuleCargoBay>() == null) return false;
            return !ModuleAblativeDecal.IsAutoCoveredChild(p);
        }

        // 1.9: one decal on every hull part whose belly side (bellyDir, world) is exposed, set to the window's look; one undo step
        private void TileWholeBelly(Vector3 bellyDir)
        {
            ShipConstruct ship = EditorLogic.fetch != null ? EditorLogic.fetch.ship : null;
            AvailablePart ap = PartLoader.getPartInfoByName("ablativeDecal");
            if (ship == null || ap == null || ap.partPrefab == null || bellyDir.sqrMagnitude < 0.5f) return;
            bellyDir.Normalize();
            var shipParts = new System.Collections.Generic.HashSet<Part>(ship.parts.Where(p => p != null));
            // decal colliders off, so the rays hit the hulls under the tiles
            var off = shipParts.Where(p => p.FindModuleImplementing<ModuleAblativeDecal>() != null)
                .SelectMany(p => p.GetComponentsInChildren<Collider>(true)).Where(c => c != null && c.enabled).ToList();
            foreach (Collider c in off) c.enabled = false;
            var added = new System.Collections.Generic.List<ModuleAblativeDecal>();
            try
            {
                foreach (Part target in shipParts.ToList())
                {
                    if (!BellyHull(target)) continue;
                    Bounds b = default;
                    bool any = false;
                    foreach (Collider c in target.GetComponentsInChildren<Collider>())
                    {
                        if (c == null || !c.enabled || c.isTrigger || c.GetComponentInParent<Part>() != target) continue;
                        if (any) b.Encapsulate(c.bounds); else { b = c.bounds; any = true; }
                    }
                    Vector3 centre = any ? b.center : target.partTransform.position;
                    RaycastHit hit = default;
                    bool exposed = false;
                    foreach (RaycastHit h in Physics.RaycastAll(centre + bellyDir * 15f, -bellyDir, 30f).OrderBy(h => h.distance))
                    {
                        if (h.collider == null || h.collider.isTrigger) continue;
                        Part hp = h.collider.GetComponentInParent<Part>();
                        if (hp == null || !shipParts.Contains(hp) || hp.partInfo != null && hp.partInfo.name == "ablativeDecal") continue;
                        exposed = hp == target; // the first ship part seen from the belly side: target is not hidden under another part
                        hit = h;
                        break;
                    }
                    if (!exposed) continue;
                    ModuleAblativeDecal m = AttachDecal(ap, target, hit.point, bellyDir, ship);
                    if (m != null) added.Add(m);
                }
            }
            finally
            {
                foreach (Collider c in off) if (c != null) c.enabled = true;
            }
            if (added.Count == 0) return;
            StartCoroutine(ConfigureBelly(added));
        }

        // as EditorLogic.SpawnPart + attachPart: instantiated, surface attached to target at point, shield (part -Y) along bellyDir
        private static ModuleAblativeDecal AttachDecal(AvailablePart ap, Part target, Vector3 point, Vector3 bellyDir, ShipConstruct ship)
        {
            Part p = Instantiate(ap.partPrefab);
            p.gameObject.SetActive(true);
            p.name = ap.name;
            p.partInfo = ap;
            p.persistentId = FlightGlobals.GetUniquepersistentId();
            p.InitializeModules();
            p.partTransform = p.transform;
            p.transform.position = point;
            p.transform.rotation = Quaternion.FromToRotation(Vector3.down, bellyDir);
            p.attachMode = AttachModes.SRF_ATTACH;
            p.isAttached = true;
            p.setParent(target); // also target.addChild(p)
            p.transform.parent = target.transform;
            if (p.srfAttachNode != null) p.srfAttachNode.attachedPart = target;
            p.attPos = Vector3.zero;
            p.attPos0 = p.transform.localPosition;
            p.attRotation = Quaternion.identity;
            p.attRotation0 = p.transform.localRotation;
            p.orgPos = p.transform.position;
            p.orgRot = p.transform.rotation;
            ship.Add(p);
            if (FlightGlobals.fetch != null && !FlightGlobals.PersistentLoadedPartIds.ContainsKey(p.persistentId)) FlightGlobals.PersistentLoadedPartIds.Add(p.persistentId, p);
            p.onAttach(target);
            foreach (PartModule pm in p.Modules) pm.enabled = true;
            return p.FindModuleImplementing<ModuleAblativeDecal>();
        }

        // the window's look once the new decals have started (B9PartSwitch switchers need their own start first), then one undo step
        private System.Collections.IEnumerator ConfigureBelly(System.Collections.Generic.List<ModuleAblativeDecal> added)
        {
            yield return null;
            yield return null;
            int count = 0;
            foreach (ModuleAblativeDecal m in added)
            {
                if (m == null || m.part == null) continue;
                m.ApplyGlobal(Patterns[pattern], Finishes[finish], tileSize, thickness);
                count++;
            }
            if (EditorLogic.fetch != null && count > 0) EditorLogic.fetch.SetBackup();
            ScreenMessages.PostScreenMessage(Localizer.Format(L + "app_tileBelly_msg", count), 4f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}
