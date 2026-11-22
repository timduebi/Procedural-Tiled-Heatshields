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
        private string titleLabel, patternLabel, finishLabel, tileSizeLabel, thicknessLabel, applyAllLabel, presetsLabel, highlightAllLabel;
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
    }
}
