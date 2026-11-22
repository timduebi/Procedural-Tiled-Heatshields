using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using KSP.Localization;
using UnityEngine;
using Debug = UnityEngine.Debug;

[assembly: KSPAssembly("AblatorDecals", 1, 11)]

namespace AblatorDecals
{
    // One part = 3D heat tiles laid onto the parent (and parts stacked onto it), a few box colliders and one thermal node.
    public class ModuleAblativeDecal : PartModule, IPartMassModifier, IPartCostModifier
    {
        // PAW: status lines (ungrouped, on top), then "Tiles", "Area" and the collapsed "Fine tuning" group.
        // B9PartSwitch's optional switchers join the tiles group (uiGroupName = ablatorTiles in Patches/B9PartSwitch.cfg).
        private const string Grp = "ablatorTiles", GrpName = "#LOC_AblatorDecals_group_tiles";
        private const string GrpArea = "ablatorArea", GrpAreaName = "#LOC_AblatorDecals_group_area";
        private const string GrpFine = "ablatorFine", GrpFineName = "#LOC_AblatorDecals_group_fine";
        private const string L = "#LOC_AblatorDecals_";

        // ---------- readouts ----------

        // tile area actually lying on the covered parts; drives ablator amount, coverage, mass and cost
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = L + "coveredArea", guiFormat = "F2", guiUnits = " m²")]
        public double coveredArea;

        [KSPField(guiActive = true, guiName = L + "heatBlocked")]
        public string heatBlocked = "";

        [KSPField(guiActive = true, guiName = L + "facing", guiFormat = "P0")]
        public double facing;

        [KSPField(guiActive = true, guiName = L + "absorbing", guiUnits = " kW", guiFormat = "F0")]
        public double absorbed;

        [KSPField(guiActive = true, guiName = L + "energyAbsorbed", guiUnits = " MJ", guiFormat = "F1")]
        public double energyAbsorbed;

        [KSPField(guiActive = true, guiName = L + "hottestSkin")]
        public string hottestSkin = "";

        [KSPField(guiActive = true, guiName = L + "tileTemp")]
        public string tileTemp = "";

        [KSPField(guiActiveEditor = true, guiName = L + "tileCount", groupName = GrpFine, groupDisplayName = GrpFineName, groupStartCollapsed = true)]
        public int tileCount;

        [KSPField(guiActiveEditor = false, guiActive = true, guiName = L + "partsCovered")]
        public int partsCovered;

        // 1.11.2: hidden in VAB (KSP already displays the stock Ablator resource bar in the PAW)
        [KSPField(guiActiveEditor = false, guiName = L + "ablatorUnits", guiFormat = "F1", guiUnits = " u")]
        public double ablatorUnits;

        [KSPField(guiActiveEditor = true, guiName = L + "tileMass")]
        public string tileMass = "";

        // only shown while another decal already covers this area (DESIGN-v9 M2)
        [KSPField(guiActiveEditor = false, guiActive = false, guiName = L + "status")]
        public string status = "";

        // ---------- editor settings (UI_FloatEdit ignores guiFormat/guiUnits; it uses sigFigs and unit) ----------
        // affectSymCounterparts = None: CopyToCounterparts() hands the values over itself (mirrored angle).
        // Copies (alt-click, symmetry) get all field values through Unity's Instantiate; OnWasCopied only mirrors the angle.

        // Brick (staggered squares) is no longer offered; saved Brick decals keep it (SetupPatternOptions)
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "pattern", groupName = Grp, groupDisplayName = GrpName)]
        [UI_ChooseOption(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, options = new[] { "Hex", "Square" })]
        public string pattern = "Hex";

        // UI only: the finish itself lives in ModuleAblativeDecalFinish (see there why)
        [KSPField(guiActiveEditor = true, guiName = L + "finish", groupName = Grp, groupDisplayName = GrpName)]
        // 1.8: White is back, Steel is new; unknown finishes load as Black (SyncSettings keeps the chooser on one of these)
        [UI_ChooseOption(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, options = new[] { "Black", "Ceramic", "White", "Steel" })]
        public string finishUi = "Black";

        // lattice pitch: hex flat-to-flat, square edge
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "tileSize", groupName = Grp, groupDisplayName = GrpName)]
        [UI_FloatEdit(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, minValue = 0.03f, maxValue = 0.5f, incrementLarge = 0.05f, incrementSmall = 0.01f, incrementSlide = 0.005f, sigFigs = 3, unit = " m")]
        public float tileSize = 0.1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "patternAngle", groupName = GrpFine, groupDisplayName = GrpFineName, groupStartCollapsed = true)]
        [UI_FloatEdit(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, minValue = -90f, maxValue = 90f, incrementLarge = 15f, incrementSmall = 5f, incrementSlide = 5f, sigFigs = 0, unit = "°")]
        public float patternAngle;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "tileGap", groupName = GrpFine, groupDisplayName = GrpFineName, groupStartCollapsed = true)]
        [UI_FloatEdit(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, minValue = 0f, maxValue = 0.02f, incrementLarge = 0.005f, incrementSmall = 0.001f, incrementSlide = 0.0005f, sigFigs = 4, unit = " m")]
        public float tileGap = 0f; // 1.4: no joint by default; saved decals keep theirs

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "thickness", groupName = Grp, groupDisplayName = GrpName)]
        [UI_FloatEdit(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, minValue = 0.005f, maxValue = 0.3f, incrementLarge = 0.05f, incrementSmall = 0.005f, incrementSlide = 0.001f, sigFigs = 3, unit = " m")]
        public float thickness = 0.025f;

        // "Whole part" (default): the decal tiles its part as far as max wrap allows, no width/height. "Limit area": width/height.
        // Saves from before 1.3 load as limited (OnLoad).
        [KSPField(isPersistant = true)] public bool areaLimited;

        // Width/Height: measured along the hull (unrolled) or seen from straight ahead (projected).
        [KSPField(isPersistant = true)] public float width = 2f;

        [KSPField(isPersistant = true)] public float height = 2f;

        // max angle between the hull and the projection direction
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "maxWrap", groupName = GrpArea, groupDisplayName = GrpAreaName)]
        [UI_FloatEdit(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, minValue = 10f, maxValue = 180f, incrementLarge = 30f, incrementSmall = 5f, incrementSlide = 1f, sigFigs = 0, unit = "°")]
        public float maxWrap = 100f;

        // 1.5 (M2): share of the parent's extent along the decal normal that gets tiles, from the decal's side (100 = whole part);
        // a straight cut plane, on top of max wrap (the stricter one wins). 0 % would be no tiles at all, so the slider starts at 1.
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "coverHeight", groupName = GrpArea, groupDisplayName = GrpAreaName)]
        [UI_FloatRange(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, minValue = 1f, maxValue = 100f, stepIncrement = 1f)]
        public float coverHeight = 100f;

        [KSPField(isPersistant = true)] public bool unrolled = true;

        // 1.4: "Across parts" is gone; kept only so old saves load, ignored (TargetParts)
        [KSPField(isPersistant = true)] public bool spanParts;

        [KSPField(isPersistant = true)] public bool straightBorder;

        // only shown on a cockpit listed in CockpitCover.cfg: cover cut along its planes, whole part, max wrap 180
        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = L + "cockpitShape", groupName = GrpArea, groupDisplayName = GrpAreaName)]
        [UI_Toggle(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, enabledText = L + "cockpitShape_on", disabledText = L + "cockpitShape_off")]
        public bool cockpitShape = true;

        // 1.8: tiles on a smooth revolution hull over ribs, rings and pipes (round tanks, station modules; pipes get a visual skin).
        // Auto-set once on round parents (MakeJob), the player's toggle persists from then on; hidden under a cockpit cover.
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "smoothHull", groupName = GrpFine, groupDisplayName = GrpFineName, groupStartCollapsed = true)]
        [UI_Toggle(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, enabledText = L + "smoothHull_on", disabledText = L + "smoothHull_off")]
        public bool smoothHull = false;

        [KSPField(isPersistant = true)] public bool smoothHullInit = false;

        // 1.11.2: texture mapping/scale removed from PAW (fields stay persistent so legacy saves load unchanged)
        [KSPField(isPersistant = true)] public bool textureAcross;

        [KSPField(isPersistant = true)] public float textureScale = 2f;

        // share of tiles left out, in percent
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "tileLoss", groupName = GrpFine, groupDisplayName = GrpFineName, groupStartCollapsed = true)]
        [UI_FloatEdit(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, minValue = 0f, maxValue = 20f, incrementLarge = 5f, incrementSmall = 1f, incrementSlide = 1f, sigFigs = 0, unit = " %")]
        public float tileLoss;

        // 1.8: base soot/char tint in percent, visible in the editor and at launch; reentry adds a directional gradient on top
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "weathering", groupName = GrpFine, groupDisplayName = GrpFineName, groupStartCollapsed = true)]
        [UI_FloatRange(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, minValue = 0f, maxValue = 100f, stepIncrement = 5f)]
        public float weathering = 0f;

        // 1.8/1.11: tiles shed on hard reentries (low ablator, extreme skin heat); off by default in all game modes, placed directly
        // in the main Tiles group so players can toggle it easily when editing
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = L + "tileShedding", groupName = Grp, groupDisplayName = GrpName)]
        [UI_Toggle(affectSymCounterparts = UI_Scene.None, enabledText = L + "tileShedding_on", disabledText = L + "tileShedding_off")]
        public bool tileShedding = false;

        [KSPField(isPersistant = true)] public bool tileSheddingInit = false;

        // 1.8: hatches (colliders tagged "Airlock") stay free; set once when attached (off on the Mk3 crew cabin, whose bottom
        // hatch and windows would leave holes in the belly), the player's toggle persists from then on
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "keepHatches", groupName = GrpFine, groupDisplayName = GrpFineName, groupStartCollapsed = true)]
        [UI_Toggle(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, enabledText = L + "keepHatches_on", disabledText = L + "keepHatches_off")]
        public bool keepHatches = true;

        [KSPField(isPersistant = true)] public bool keepHatchesInit = false;

        // 1.9: windows (ABLATORDECAL_WINDOWS boxes) stay free; set like keepHatches (off on the Mk3 crew cabin)
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = L + "keepWindows", groupName = GrpFine, groupDisplayName = GrpFineName, groupStartCollapsed = true)]
        [UI_Toggle(scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.None, enabledText = L + "keepWindows_on", disabledText = L + "keepWindows_off")]
        public bool keepWindows = true;

        [KSPField(isPersistant = true)] public bool keepWindowsInit = false;

        // 1.9: parent part name the per-parent defaults (hatches, windows, smooth hull) were set for; an alt-copy onto another
        // part type sets them afresh
        [KSPField(isPersistant = true)] public string initParent = "";

        // share of tiles shed in flight (on top of tileLoss), reset by an Engineer's EVA repair
        [KSPField(isPersistant = true)] public float battleLoss = 0f;

        // 1.10: heat-weighted reentry airflow in vessel root space (length = weight), so streaks keep their direction after a
        // reload; all decals of a vessel add up to one flow (VesselChar)
        [KSPField(isPersistant = true)] public Vector3 charFlowDir = Vector3.zero;

        // 1.8: flight readout: nominal / marginal / low / depleted
        [KSPField(guiActive = true, guiName = L + "thermalStatus")]
        public string thermalStatus = "";

        // 1.8 (DecalWindow): orange anchor ring on every decal of the craft
        internal static bool HighlightAll;

        [KSPField(isPersistant = true)] public int seed = -1;

        // v1.0 picked the texture directly; read once to set the finish, then cleared (OnStart)
        [KSPField(isPersistant = true)] public string textureName = "";

        // new random atlas cells and missing tiles, here and on the symmetry counterparts
        [KSPEvent(guiActiveEditor = true, guiName = L + "shuffle", groupName = GrpFine, groupDisplayName = GrpFineName, groupStartCollapsed = true)]
        public void Shuffle()
        {
            foreach (ModuleAblativeDecal m in part.symmetryCounterparts.Select(p => p != null ? p.FindModuleImplementing<ModuleAblativeDecal>() : null).Append(this))
            {
                if (m == null) continue;
                m.seed = UnityEngine.Random.Range(0, 1000000);
                m.rebuildPending = true;
            }
        }

        // 1.8: this decal's look to every other decal of the craft (a pre-made cockpit cover keeps its fixed size/thickness)
        [KSPEvent(guiActiveEditor = true, guiName = L + "copyAll", groupName = Grp, groupDisplayName = GrpName)]
        public void CopySettingsToAll()
        {
            if (EditorLogic.fetch == null || EditorLogic.fetch.ship == null) return;
            int count = 0;
            foreach (Part p in EditorLogic.fetch.ship.parts)
            {
                ModuleAblativeDecal m = p != null ? p.FindModuleImplementing<ModuleAblativeDecal>() : null;
                if (m == null || m == this) continue;
                m.ApplyGlobal(pattern, Finish, tileSize, thickness); // keeps premade sizes, handles B9
                m.maxWrap = maxWrap;
                m.coverHeight = coverHeight;
                m.tileGap = tileGap;
                m.weathering = weathering;
                m.tileShedding = tileShedding;
                count++;
            }
            if (count > 0) EditorLogic.fetch.SetBackup();
            ScreenMessages.PostScreenMessage(Localizer.Format(L + "copyAll_msg", count), 4f, ScreenMessageStyle.UPPER_CENTER);
        }

        // 1.8: an Engineer on EVA restores shed tiles and the ablator (Career: uses one stock EVA repair kit)
        [KSPEvent(guiActive = false, guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 4f, guiName = L + "eva_repair")]
        public void RepairTiles()
        {
            Vessel eva = FlightGlobals.ActiveVessel;
            ProtoCrewMember crew = eva != null && eva.isEVA && eva.GetCrewCount() > 0 ? eva.GetVesselCrew()[0] : null;
            if (crew == null || !(crew.HasEffect<Experience.Effects.RepairSkill>() || string.Equals(crew.trait, "Engineer", StringComparison.OrdinalIgnoreCase)))
            {
                ScreenMessages.PostScreenMessage(Localizer.Format(L + "eva_needEngineer"), 4f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                ModuleInventoryPart inv = eva.rootPart != null ? eva.rootPart.FindModuleImplementing<ModuleInventoryPart>() : null;
                if (inv == null || !inv.ContainsPart("evaRepairKit"))
                {
                    ScreenMessages.PostScreenMessage(Localizer.Format(L + "eva_needKit"), 4f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }
                inv.RemoveNPartsFromInventory("evaRepairKit", 1, true);
            }
            battleLoss = 0f;
            if (ablator != null) ablator.amount = ablator.maxAmount;
            binExposure.Clear();
            maxExposure = 0f;
            charFlowDir = Vector3.zero;
            lowWarned = depletedWarned = false;
            BuildNow(); // fresh hosts; Apply re-applies the base weathering char
            ScreenMessages.PostScreenMessage(Localizer.Format(L + "eva_repaired"), 4f, ScreenMessageStyle.UPPER_CENTER);
        }

        // ---------- config ----------

        [KSPField] public string ablativeResource = "Ablator";
        [KSPField] public double unitsPerCubicMeter = 1000;
        [KSPField] public double pyrolysisLossFactor = 6000;
        [KSPField] public double ablationTempThresh = 500;
        [KSPField] public double efficiency = 1;
        // outward normal in part space; node_attach points the opposite way (into the parent)
        [KSPField] public Vector3 shieldNormal = Vector3.up;
        [KSPField] public float logInterval = 0f;
        [KSPField] public string textureFolder = "AblatorDecals/Textures/";
        [KSPField] public string shader = "PBR"; // 1.11: PBR auto-selects TU/Metallic -> Resurfaced/Standard -> Unity Standard -> KSP/Emissive/Specular
        [KSPField] public Vector3 specColor = new Vector3(0.25f, 0.25f, 0.25f);
        [KSPField] public float shininess = 0.35f;
        [KSPField] public Vector3 charColor = new Vector3(0.42f, 0.37f, 0.34f); // tile colour when the ablator is used up
        [KSPField] public bool glowEnabled = true;
        [KSPField] public float glowEmissivity = 0.95f; // radiative emissivity of the tile surface (higher = cooler tiles, less glow)
        [KSPField] public float glowHeatCapacity = 10f; // kJ/m2K; 1.9: 10 (was 30), the glow follows and fades within ~10 s
        [KSPField] public float glowConduction = 0.08f; // kW/m2K (1.9: 0.08, faster cool-down after reentry)
        [KSPField] public float glowFadeTime = 1.5f;    // s, time constant with which the shown glow colour follows its target (M6)
        // glow tint over tile temperature: "K r g b a; ..." (a = strength, continuous interpolation between stops), multiplied with the pattern's
        // emission texture (per tile: warm white core, dark orange rim). 1.5 (M1): three states, normal (none) -> medium (orange x 0.55,
        // dark rims) -> peak (white x 1.2: white-yellow core, orange rims). Temperature = the tiles' own (TileGlow, "Tile temperature"
        // in the PAW), which on a normal reentry peaks around 700-1300 K. 1.9: dark cherry from 720 K, red 850 K, orange 1020 K,
        // yellow 1280 K, white-hot from 1600 K (smoothstep between stops, so the onset is soft)
        [KSPField] public string glowRamp = "720 0.70 0.04 0.00 0.00; 850 0.90 0.12 0.01 0.22; 1020 1.00 0.34 0.04 0.65; 1280 1.00 0.72 0.26 1.20; 1600 1.00 0.96 0.82 1.85; 2000 1.00 1.00 1.00 2.60";
        // glow bins: tiles whose normals lie within this angle share one temperature and colour; fine bins = no visible steps
        [KSPField] public float glowBinAngle = 18f;
        [KSPField] public int glowMaxBins = 12;
        [KSPField] public Vector3 glowFlatColor = new Vector3(1f, 0.75f, 0.45f); // emission texture of patterns without one (plain)
        [KSPField] public float massPerArea = 0.0015f;  // t/m2
        [KSPField] public float costPerArea = 20f;
        [KSPField] public float surfaceOffset = 0.003f;
        [KSPField] public float cornerBendAngle = 35f;
        [KSPField] public float faceBendAngle = 60f;
        [KSPField] public float curvatureFactor = 1f;
        [KSPField] public int maxTiles = 60000;
        [KSPField] public float colliderPatchSize = 1f;
        [KSPField] public float gearMargin = 0.3f;      // m around landing gear without tile colliders (wheel casts ignore IgnoreCollision)
        [KSPField] public float rcsKeepOut = 0.12f;    // m, half size of the tile-free cube around each RCS nozzle of a covered part (M4)

        // ---------- state ----------

        private class TexEntry
        {
            public string Url, Title;
            public string Shape = "Any"; // Hex | Square | Any
            public string Finish = "";   // Black | White | Ceramic | Steel
            public int Cols, Rows;       // 0 = plain texture
            public bool RandomRotation;
            public string Emissive;      // emission texture url (same atlas layout), null: flat glowFlatColor
            public string Metallic;      // 1.11: PBR metallic-smoothness map (R = metallic, A = smoothness)
            public string Bump;          // 1.11: PBR tangent-space normal map (*_NRM)
            public bool Enabled = true;  // 1.11: False hides finish from editor pickers while keeping legacy saves loadable
            public bool PlainOptions;    // shared ceramic diffuse supports scaled windows and across-hull mapping
            public bool Atlas => Cols > 0 && Rows > 0;
        }

        // Everything a worker needs; plain data only (no Unity objects).
        private class BuildJob
        {
            public volatile bool Cancelled;
            public TileSettings Settings;
            public Vector3 N, U, V;
            public SurfaceInput Surface;   // null: flat preview
            public float Reach, Cell, Smooth;
            public float Width, Height;
            public TileResult Result;
            public bool OnSurface, Attached, Flight, Blocked;
            public bool SmoothHull;        // 1.8: tile a smooth revolution hull of the parent (SurfaceIndex.BuildSmoothHull)
            public Body[] Bodies;          // owner id -> body; only read on the main thread
            public TileSettings[] OverlaySettings = new TileSettings[0]; // per SurfaceInput.Overlays (v11)
            public TileResult Premade;     // v11: pre-made cockpit cover (part space of the cockpit), used instead of a build
            public Matrix4x4 PremadePose;  // cockpit part space -> decal space
            public Exception Error;
            public long Ms;
        }

        // A rigid piece of a covered part: the part itself, or a control surface flap (ModuleControlSurface transform).
        // Owner ids of triangles and PartTiles index the body list.
        private class Body
        {
            public Part Part;
            public Transform T;           // part transform, or the flap transform
            public bool Flap;             // moves on its own: tiles never cross its border (TileSettings.SeparateOwner)
            public bool Gear;             // A10: a moving piece of retracted landing gear; host under T also in the editor, no collider
            public Matrix4x4 ToDecal;     // T in neutral position -> decal part space
            public Matrix4x4 ToWorld;     // T in neutral position -> world (Collect: decal-free positions for the overlays)
            public int Group = -1;       // DESIGN-v11: overlay object it belongs to (index into SurfaceInput.Overlays), -1 the main cover,
                                          // -2 an object without overlay (outside a limited area); >= -2 apart from -1: an obstacle there
            public bool Inward;           // cargo bay door: its inner side (towards the bay's middle) is no skin (TileSettings.Inward)
        }

        // Hull triangles of the target parts in decal space, cached while parts and relative pose stay put.
        private class SurfaceInput
        {
            public Vector3[] Pos;
            public int[] Tri, Owner;
            public Body[] Bodies;
            public Part[] Parts;
            public Matrix4x4[] Poses;
            public Part[] Gear;           // landing gear in the footprint (A10), part of the cache key
            public OverlayInput[] Overlays = new OverlayInput[0];
            public bool FrameSkin;        // 1.9: the parent is a radial service bay: its frame gets a closed outer skin like its doors
            public bool[] InArea = new bool[0]; // per part: overlay for it ("limit area": attach point in the footprint), cache key
            public int MeshSig;           // 1.8: ComputeMeshSig of the parts, cache key
        }

        // DESIGN-v11: one attached object (glued flap/intake/fin part, the parent's flaps and bay doors, a landing gear) in its own
        // space (its part transform): its bodies' triangles and the main bodies' triangles within 1 m of it (obstacles). Built from
        // world positions, so nothing in here depends on where the decal sits.
        private class OverlayInput
        {
            public string Name;
            public Vector3[] Pos;
            public int[] Tri, Owner;      // owner: local id, bodies first, then one id for all obstacles
            public bool[] Obstacle, Inward;
            public bool Skin;             // 1.6: landing gear and intakes get a closed skin (TileSettings.Skin), the rest tiles
            public bool IsParentMoving;   // 1.8: the parent's own flaps/doors (a decal glued onto a door still tiles them)
            public bool DoorSkin;         // 1.9: doors of a radial service bay: closed outer skin that follows the decal's wrap and cuts
            public int[] Body;            // local id -> index into SurfaceInput.Bodies
            public Matrix4x4 ToDecal;     // object space -> decal part space
        }

        private PartResource ablator;
        private double kJPerUnit;
        private float nextLog;

        private readonly List<TexEntry> textures = new List<TexEntry>();
        private TexEntry texture;
        private Texture2D currentTexture;
        private Texture currentGlow;                  // _Emissive / _EmissionMap of the tile materials (M1)
        private Texture currentMetal, currentBump;    // 1.11: PBR metallic-smoothness and normal maps
        private Texture2D flatGlow, defaultMetal, defaultBump; // 1x1 fallbacks when an entry omits glow/metallic/bump
        private Material tileTemplate, jointTemplate, backTemplate;

        private GameObject decalRoot;                 // part space, under the model; parent of colliders (and editor hosts)
        private readonly List<GameObject> colliderObjects = new List<GameObject>();
        private readonly List<TileHost> hosts = new List<TileHost>();
        private float appliedChar = -1;

        // covered parts that got tiles (index-aligned with frontal)
        private Part[] targets = new Part[0];
        private Part[] lastTargets = new Part[0]; // what the last build looked at
        private double[] frontal = new double[0];
        private double hottestTile;

        private SurfaceInput surfaceCache;
        private int lastMeshSig;                  // 1.8: ComputeMeshSig at the last Collect (TweakScale/B9 watcher)
        private float nextMeshCheck;
        private readonly object gate = new object();
        private BuildJob finished, activeJob;
        private double[] qBuf = new double[0], covBuf = new double[0];
        private bool running, dirty, destroyed;
        private bool detached;                    // editor: set on PartDetached, parent may not be null yet
        private Matrix4x4[] lastPoses = new Matrix4x4[0];
        private bool rebuildPending, rebuildNow;
        private float nextRebuild, nextGuard, nextEditorLog, nextDisplay, nextKeepOutLog, nextKspGlowCheck;
        private bool guardWarned;
        private float diagnoseAt;
        private bool flightStarted;
        private Part dyingPart;
        private List<ModuleAblativeDecal> vesselDecals;
        private Vessel cachedVessel;
        private int vesselDecalsPartCount = -1;
        private ModuleAblativeDecalFinish finishModule;
        private string seenPattern, seenFinish;   // editor: B9PartSwitch changes the fields behind our back
        private string warnedTexture;
        private TileResult lastResult;
        private BuildJob lastJob;                 // with lastResult: editor re-makes the colliders when landing gear comes or goes            // last applied tiles (decal space): occupied points for younger decals, "on tiles" tests
        private List<Part> onTiles = new List<Part>();          // flight: non-target parts glued onto the covered hull inside the tiles
        private readonly HashSet<Part> noCollide = new HashSet<Part>(); // flight: parts that never collide with the tiles (sticky)
        private int ignoreFrames;                 // flight: re-apply the collision ignores after KSP's CollisionManager ran
        private bool othersHeld;                  // editor: another part hangs on the mouse, tile colliders are off
        private float[][] ramp;
        private bool blocked;                     // another decal already covers this area: no tiles (M2)
        private bool premadeShown;                // v11: the PAW shows the pre-made cockpit cover state (sliders hidden)
        private int shownHighlight = -1;          // 0 default, 1 PAW open, 2 blocked (red)
        private GameObject anchor;                // editor: hex ring at the attach point, grab handle (M5)
        private MeshRenderer anchorRenderer;

        // reentry summary
        private bool heating;
        private double quietTime, peakSkin, peakBlocked, ablatorAtStart, energyAtStart, peakGlowTemp, peakGlowShown;
        private double glowShownMax;              // strongest glow colour channel shown in the last UpdateGlow

        // 1.8: per host and bin, heat taken while facing the flow (directional soot); not saved, a reload chars evenly
        private readonly Dictionary<TileHost, float[]> binExposure = new Dictionary<TileHost, float[]>();
        private float maxExposure;
        private bool lowWarned, depletedWarned;
        private float nextEvaCheck;

        private static readonly string[] SkipShaders = { "DepthMask", "Particles", "Transparent", "Translucent", "FX" };

        private string PartName => part != null && part.partInfo != null ? part.partInfo.name : "?";
        private static bool Active => HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight;

        // ---------- lifecycle ----------

        public override void OnStart(StartState state)
        {
            if (!Active || part.partInfo == null || part.partTransform == null) return; // icon / loading

            ablator = part.Resources.Get(ablativeResource);
            PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(ablativeResource);
            if (ablator == null || def == null)
                Debug.LogError($"[AblatorDecals] {PartName}: resource '{ablativeResource}' missing on part or unknown");
            else
                kJPerUnit = pyrolysisLossFactor * def.specificHeatCapacity * def.density;
            if (seed < 0) seed = UnityEngine.Random.Range(0, 1000000);
            if (!tileSheddingInit)
            {
                // 1.11: off by default in all game modes; the player toggles it in the PAW or DecalWindow
                tileSheddingInit = true;
                tileShedding = false;
            }

            InitParentDefaults();
            LoadTextures();
            ramp = ParseRamp(glowRamp);
            finishModule = part.FindModuleImplementing<ModuleAblativeDecalFinish>();
            if (!string.IsNullOrEmpty(textureName))
            {
                // v1.0 save: the chosen texture tells the finish
                TexEntry old = textures.FirstOrDefault(t => t.Url == textureName);
                if (old != null && finishModule != null && !string.IsNullOrEmpty(old.Finish)) finishModule.finish = old.Finish;
                textureName = "";
            }
            SetupVisuals();

            if (HighLogic.LoadedSceneIsEditor)
            {
                foreach (string f in new[] { nameof(tileSize), nameof(patternAngle), nameof(tileGap), nameof(thickness),
                             nameof(maxWrap), nameof(coverHeight), nameof(cockpitShape), nameof(tileLoss),
                             nameof(keepHatches), nameof(keepWindows), nameof(smoothHull) })
                    Fields[f].uiControlEditor.onFieldChanged = OnTweak;
                Fields[nameof(pattern)].uiControlEditor.onFieldChanged = OnPatternChanged;
                Fields[nameof(finishUi)].uiControlEditor.onFieldChanged = OnFinishChanged;
                Fields[nameof(weathering)].uiControlEditor.onFieldChanged = OnWeatheringChanged; // tint only, no rebuild
                Fields[nameof(tileShedding)].uiControlEditor.onFieldChanged = (f, o) => CopyToCounterparts();
                var finishChoose = (UI_ChooseOption)Fields[nameof(finishUi)].uiControlEditor;
                finishChoose.options = EnabledFinishes();
                finishChoose.display = finishChoose.options.Select(o => Localizer.Format(L + "finish_" + o.ToLowerInvariant())).ToArray();
                // with B9PartSwitch its switchers take over pattern and finish (matched by name, no assembly reference)
                bool b9 = part.Modules.Cast<PartModule>().Any(m => m.moduleName == "ModuleB9PartSwitch");
                Fields[nameof(pattern)].guiActiveEditor = !b9;
                Fields[nameof(finishUi)].guiActiveEditor = !b9 && finishModule != null;
                SyncSettings();
                GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
                GameEvents.onVariantApplied.Add(OnVariantApplied);
                GameEvents.onEditorUndo.Add(OnEditorShip);
                GameEvents.onEditorRedo.Add(OnEditorShip);
                GameEvents.onEditorShipModified.Add(OnEditorShipModified);
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onPartWillDie.Add(OnPartWillDie);
                GameEvents.onVesselWasModified.Add(OnVesselWasModified);
                GameEvents.OnCollisionIgnoreUpdate.Add(OnCollisionIgnoreUpdate);
            }
        }

        public override void OnStartFinished(StartState state)
        {
            if (decalRoot == null) return;
            if (HighLogic.LoadedSceneIsFlight) BuildNow(); // once, synchronous
            else RequestBuild();
        }

        // Part.OnWasCopied (Part.cs) calls the original with the copy's module, so `this` is the source and the argument the new part.
        // The copy is created by instantiating this part (all fields, also unrolled and the finish module, come along):
        // a mirrored counterpart gets the mirrored pattern angle.
        public override void OnWasCopied(PartModule copyPartModule, bool asSymCounterpart)
        {
            if (asSymCounterpart && copyPartModule is ModuleAblativeDecal m && part.symMethod == SymmetryMethod.Mirror)
                m.patternAngle = -patternAngle;
        }

        // B9PartSwitch applies its subtypes through PartModule.Load, also after OnStart: pick the change up in the editor.
        public override void OnLoad(ConfigNode node)
        {
            // saved before 1.3 (width but no mode): keep the old behaviour, a limited area
            if (node.HasValue("width") && !node.HasValue(nameof(areaLimited))) areaLimited = true;
            if (decalRoot != null && HighLogic.LoadedSceneIsEditor) SettingsLoaded();
        }

        // pattern or finish changed outside our own PAW controls (B9PartSwitch, ModuleAblativeDecalFinish.OnLoad)
        internal void SettingsLoaded()
        {
            if (decalRoot == null || !HighLogic.LoadedSceneIsEditor) return;
            if (SyncSettings()) MonoUtilities.RefreshPartContextWindow(part);
            rebuildNow = true;
        }

        // Pattern options, finish chooser and texture for the current pattern/finish; true if PAW rows came or went.
        private bool SyncSettings()
        {
            SetupPatternOptions();
            if (finishModule != null)
            {
                // 1.11: keep the chooser on enabled finishes (Textures.cfg); a legacy craft saved with a disabled finish
                // (e.g. White) keeps its finish until the player switches away, just like legacy Brick
                var choose = (UI_ChooseOption)Fields[nameof(finishUi)].uiControlEditor;
                string[] enabled = EnabledFinishes();
                bool legacyDisabled = !string.IsNullOrEmpty(finishModule.finish) &&
                    !enabled.Any(o => Same(o, finishModule.finish)) &&
                    textures.Any(t => Same(t.Finish, finishModule.finish));
                choose.options = legacyDisabled ? enabled.Append(finishModule.finish).ToArray() : enabled;
                choose.display = choose.options.Select(o => Localizer.Format(L + "finish_" + o.ToLowerInvariant())).ToArray();
                finishModule.finish = choose.options.FirstOrDefault(o => Same(o, finishModule.finish)) ?? (enabled.Length > 0 ? enabled[0] : "Black");
            }
            finishUi = Finish;
            seenPattern = pattern;
            seenFinish = Finish;
            texture = ResolveTexture();
            bool changed = UpdateTextureUi();
            // 1.9: width/height/limit area stay hidden (fields kept so old saves load)
            return changed;
        }

        private void OnDestroy()
        {
            destroyed = true;
            if (activeJob != null) activeJob.Cancelled = true;
            if (HighLogic.LoadedSceneIsEditor && targets.Length > 0) NotifyYounger(new HashSet<Part>(targets));
            GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
            GameEvents.onVariantApplied.Remove(OnVariantApplied);
            GameEvents.onEditorUndo.Remove(OnEditorShip);
            GameEvents.onEditorRedo.Remove(OnEditorShip);
            GameEvents.onEditorShipModified.Remove(OnEditorShipModified);
            GameEvents.onPartWillDie.Remove(OnPartWillDie);
            GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            GameEvents.OnCollisionIgnoreUpdate.Remove(OnCollisionIgnoreUpdate);
            ClearHosts();
            ClearColliders();
            if (anchor != null)
            {
                Destroy(anchor.GetComponent<MeshFilter>().sharedMesh);
                Destroy(anchorRenderer.sharedMaterial.mainTexture);
                Destroy(anchorRenderer.sharedMaterial);
            }
            if (decalRoot != null) Destroy(decalRoot);
            foreach (Material m in new[] { tileTemplate, jointTemplate, backTemplate })
            {
                if (m == null) continue;
                if (m.mainTexture != null && m != tileTemplate) Destroy(m.mainTexture);
                Destroy(m);
            }
            if (flatGlow != null) Destroy(flatGlow);
            if (defaultMetal != null) Destroy(defaultMetal);
            if (defaultBump != null) Destroy(defaultBump);
        }

        public void Update()
        {
            if (!Active || decalRoot == null) return;

            // M2: polled every frame, so every way a part gets onto the mouse (part list, pick, alt-copy, subassembly, inventory)
            // turns all tile and anchor colliders off, and every way it ends turns them back on
            if (HighLogic.LoadedSceneIsEditor && NonDecalHeld() != othersHeld) SetColliders(othersHeld);

            BuildJob job;
            lock (gate) { job = finished; finished = null; }
            if (job != null) Receive(job);

            // fallback for switchers that set the fields without PartModule.Load: two string compares per frame
            if (HighLogic.LoadedSceneIsEditor && (pattern != seenPattern || Finish != seenFinish)) SettingsLoaded();

            if (HighLogic.LoadedSceneIsEditor)
            {
                UpdateFocus();
                if (Time.realtimeSinceStartup >= nextMeshCheck && part.parent != null && !detached)
                {
                    nextMeshCheck = Time.realtimeSinceStartup + 0.5f;
                    CheckMeshSig();
                }
            }

            // while another part hangs on the mouse, decals it does not carry wait (no rebuild cascades while dragging)
            bool wait = HighLogic.LoadedSceneIsEditor && OtherPartHeld();
            if (!wait && (rebuildNow || (rebuildPending && Time.realtimeSinceStartup >= nextRebuild)))
            {
                rebuildNow = rebuildPending = false;
                if (HighLogic.LoadedSceneIsFlight) BuildNow(); else RequestBuild();
            }

            if (Time.time >= nextGuard)
            {
                nextGuard = Time.time + 1f;
                GuardMaterials();
            }

            if (!HighLogic.LoadedSceneIsFlight) return;

            // here, not in FixedUpdate: that returns early in vacuum, where EVA repairs happen
            if (Time.realtimeSinceStartup >= nextEvaCheck)
            {
                nextEvaCheck = Time.realtimeSinceStartup + 0.25f;
                // 1.9: Sandbox keeps charring and weathering, but EVA repair is a Science/Career thing
                Events[nameof(RepairTiles)].active = (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode != Game.Modes.SANDBOX) &&
                    (battleLoss > 0.001f || (ablator != null && ablator.maxAmount > 0 && ablator.amount < ablator.maxAmount * 0.999));
            }

            if (ablator != null && ablator.maxAmount > 0)
            {
                float used = 1 - (float)(ablator.amount / ablator.maxAmount);
                // 1.9: small steps, char builds up smoothly; 1.10: also when other shields of the vessel char (unified level) or the
                // flow turns (streaks re-projected by TileHost past 10 deg)
                VesselChar vc = CharField();
                float uni = vc != null ? 0.95f * vc.Used + 0.05f * used : used;
                float gMask = 1f - Mathf.SmoothStep(0.08f, 0.45f, (float)glowShownMax);
                if (Mathf.Abs(used - appliedChar) >= 0.003f || Mathf.Abs(uni - appliedUnified) >= 0.003f ||
                    Mathf.Abs(gMask - appliedGlowMask) >= 0.02f ||
                    (vc != null && Vector3.Angle(vc.Flow, appliedFlow) > 10f)) ApplyChar(used);
            }

            if (diagnoseAt > 0 && Time.time >= diagnoseAt)
            {
                diagnoseAt = 0;
                foreach (TileHost h in hosts)
                    if (h.Alive)
                        Debug.Log($"[AblatorDecals] flight check {PartName} on {(h.TargetPart != null && h.TargetPart.partInfo != null ? h.TargetPart.partInfo.name : "?")}: " +
                                  $"visible={h.Renderer.isVisible} enabled={h.Renderer.enabled} layer={h.Go.layer} vertices={h.Mesh.vertexCount} bins={h.BinCount} " +
                                  $"texture={(h.Materials.Length > 0 && h.Materials[0].mainTexture != null ? h.Materials[0].mainTexture.name : "NONE")} " +
                                  $"shader={(h.Materials.Length > 0 ? h.Materials[0].shader.name : "?")} glow={(h.HasGlow ? GlowName(h.Materials[0]) : "none")}");
            }
        }

        // ---------- visuals ----------

        private void SetupVisuals()
        {
            if (decalRoot != null) return;

            // The MODEL is only the part list icon (built from the prefab while loading, OnStart never runs there). On the live
            // part its mesh goes entirely: reentry FX (Firefly & co.) draw every MeshFilter, also behind a disabled renderer.
            // Immediate, so the cache reset below cannot catch a dying renderer. On a copied part this also removes the
            // renderers of the copied tile hosts (their marker deletes the rest).
            foreach (Renderer r in part.FindModelComponents<Renderer>()) DestroyImmediate(r);
            foreach (MeshFilter f in part.FindModelComponents<MeshFilter>()) DestroyImmediate(f);
            foreach (Collider c in part.FindModelComponents<Collider>()) c.enabled = false;

            // Under "model" (hover highlight, selection) but with the part's own space as local space.
            Transform model = part.partTransform.Find("model");
            if (model == null) model = part.partTransform;
            decalRoot = new GameObject("AblativeDecalRoot") { layer = model.gameObject.layer };
            decalRoot.transform.SetParent(part.partTransform, false);
            decalRoot.transform.SetParent(model, true);
            decalRoot.AddComponent<AblativeDecalTilesMarker>().SetOwner(this);

            Shader s = ResolveTileShader(shader);
            tileTemplate = new Material(s) { name = "AblativeDecalTileTemplate" };
            if (tileTemplate.HasProperty("_SpecColor")) tileTemplate.SetColor("_SpecColor", new Color(specColor.x, specColor.y, specColor.z));
            if (tileTemplate.HasProperty("_Shininess")) tileTemplate.SetFloat("_Shininess", shininess);
            if (tileTemplate.HasProperty("_Metal")) tileTemplate.SetFloat("_Metal", 1f);
            if (tileTemplate.HasProperty("_Metallic")) tileTemplate.SetFloat("_Metallic", 1f);
            if (tileTemplate.HasProperty("_Smoothness")) tileTemplate.SetFloat("_Smoothness", 1f);
            if (tileTemplate.HasProperty("_GlossMapScale")) tileTemplate.SetFloat("_GlossMapScale", 1f);
            if (tileTemplate.HasProperty("_BumpScale")) tileTemplate.SetFloat("_BumpScale", 1f);
            if (tileTemplate.HasProperty("_Opacity")) tileTemplate.SetFloat("_Opacity", 1f);
            if (tileTemplate.HasProperty(TileHost.EmissiveColorID)) tileTemplate.SetColor(TileHost.EmissiveColorID, Color.black);
            if (tileTemplate.HasProperty(TileHost.EmissionColorID)) tileTemplate.SetColor(TileHost.EmissionColorID, Color.black);
            if (tileTemplate.HasProperty(PhysicsGlobals.TemperaturePropertyID)) tileTemplate.SetColor(PhysicsGlobals.TemperaturePropertyID, Color.clear);
            if (string.Equals(s.name, "Standard", StringComparison.Ordinal))
            {
                tileTemplate.EnableKeyword("_METALLICGLOSSMAP");
                tileTemplate.EnableKeyword("_NORMALMAP");
                tileTemplate.EnableKeyword("_EMISSION");
            }

            // joints and undersides: plain diffuse, 1x1 texture
            Shader diffuse = Shader.Find("KSP/Diffuse");
            // 1.7.1: joints glow with the tiles (KSP/Emissive/Diffuse, _EmissiveColor set by UpdateGlow), else plain diffuse
            Shader emissiveDiffuse = Shader.Find("KSP/Emissive/Diffuse");
            jointTemplate = FlatMaterial(emissiveDiffuse != null ? emissiveDiffuse : diffuse, new Color(0.04f, 0.04f, 0.045f), "Joint");
            if (jointTemplate.HasProperty(TileHost.EmissiveID)) jointTemplate.SetTexture(TileHost.EmissiveID, Texture2D.whiteTexture);
            if (jointTemplate.HasProperty(TileHost.EmissiveColorID)) jointTemplate.SetColor(TileHost.EmissiveColorID, Color.black);
            // 1.5 (M4): undersides dark like the joints; white ones shone wherever an underside came into view (doors opened
            // in the editor left the closed-pose tiles behind, 23.png)
            backTemplate = FlatMaterial(diffuse, new Color(0.04f, 0.04f, 0.045f), "Back");

            if (HighLogic.LoadedSceneIsEditor)
            {
                // anchor ring at the attach point: shows where the decal sits, and its collider is a handle to grab it by
                anchor = new GameObject("AblativeDecalAnchor") { layer = decalRoot.layer };
                anchor.transform.SetParent(decalRoot.transform, false);
                anchor.AddComponent<MeshFilter>().sharedMesh = RingMesh();
                anchorRenderer = anchor.AddComponent<MeshRenderer>();
                anchorRenderer.sharedMaterial = FlatMaterial(diffuse, new Color(1f, 0.55f, 0.1f), "Anchor");
                anchorRenderer.enabled = false;
                anchor.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.3f);
                PlaceAnchor();
            }

            TileHost.ResetRendererCaches(part);
        }

        // just above the tiles, facing out, about three tiles wide
        private void PlaceAnchor()
        {
            if (anchor == null) return;
            Vector3 n = shieldNormal.normalized;
            anchor.transform.localPosition = n * (thickness + surfaceOffset + 0.01f);
            anchor.transform.localRotation = Quaternion.LookRotation(n);
            anchor.transform.localScale = Vector3.one * Mathf.Max(0.08f, 1.5f * tileSize);
        }

        private static Material FlatMaterial(Shader s, Color c, string name)
        {
            var m = new Material(s) { name = "AblativeDecal" + name + "Template" };
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "AblativeDecal" + name };
            tex.SetPixel(0, 0, c);
            tex.Apply();
            m.mainTexture = tex;
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            return m;
        }

        // white -> char colour with base weathering plus the used share of the ablator; 1.8: per bin, windward bins (most heat
        // taken while facing the flow) char darker than leeward ones. 1.9 (all game modes): finish-aware colour, a boosted curve
        // so a normal Kerbin reentry (6-15 % ablator) shows clear directional char, and the specular fades to matte with it.
        // 1.10: one char level for the whole vessel (80 % the vessel's area-weighted ablator use, 20 % this decal's) with a gentle
        // nose-to-tail gradient along the flow, so neighbouring shields match across their seams; black tiles only dust towards
        // warm ash (the streaks carry the look), white/steel/ceramic soot to charcoal; plus wind-aligned streaks (TileHost)
        private void ApplyChar(float used)
        {
            appliedChar = used;
            VesselChar vc = CharField();
            // 1.11.3: 95 % vessel-wide level so adjacent wing panels and fuselage modules share identical weathering across seams
            float uni = vc != null ? 0.95f * vc.Used + 0.05f * used : used;
            appliedUnified = uni;
            Transform root = RootTransform();
            Vector3 flow = vc != null ? vc.Flow : DefaultFlow;
            appliedFlow = flow;
            // 1.11.3: hide the un-emissive streak overlay while tiles glow hot so dark lines never sit over reentry flames;
            // streaks emerge smoothly as the tiles cool down after peak heating
            float glowMask = HighLogic.LoadedSceneIsFlight ? (1f - Mathf.SmoothStep(0.08f, 0.45f, (float)glowShownMax)) : 1f;
            appliedGlowMask = glowMask;
            Vector3 belly = vc != null ? vc.Belly : (root != null ? root.InverseTransformDirection(part.partTransform.TransformDirection(shieldNormal)) : Vector3.forward);
            float bellyZ = vc != null ? vc.BellyZ : 0f;
            bool black = string.Equals(Finish, "Black", StringComparison.OrdinalIgnoreCase);
            bool ceramic = string.Equals(Finish, "Ceramic", StringComparison.OrdinalIgnoreCase);
            bool steel = string.Equals(Finish, "Steel", StringComparison.OrdinalIgnoreCase);
            bool darkShield = black || ceramic;
            bool pbr = tileTemplate != null && tileTemplate.shader != null &&
                !tileTemplate.shader.name.StartsWith("KSP/Emissive", StringComparison.Ordinal) &&
                !tileTemplate.shader.name.StartsWith("KSP/Diffuse", StringComparison.Ordinal);
            // 1.11.1: KSP runs in Gamma space, so under PBR (TU/Metallic, Standard + Deferred) the dark tile textures need
            // a deeper base multiplier to render true jet-black/charcoal like renders/02_tile_variants.png instead of grey-brown
            Color baseCol = !pbr ? Color.white
                : black ? (Shape == TileShape.Square ? new Color(0.56f, 0.57f, 0.59f) : new Color(0.42f, 0.43f, 0.45f))
                : ceramic ? new Color(0.48f, 0.49f, 0.52f)
                : Color.white;
            // Black & Plain Ceramic lift gently toward warm ash + silver-ash wind streaks; Steel stays brushed metallic with
            // subtle warm matte oxidation tint while dark soot streaks carry the scorch look
            Color charCol = black ? (pbr ? (Shape == TileShape.Square ? new Color(1.02f, 0.94f, 0.87f) : new Color(0.82f, 0.75f, 0.69f)) : new Color(1.85f, 1.70f, 1.56f))
                : ceramic ? (pbr ? new Color(0.90f, 0.83f, 0.77f) : new Color(1.75f, 1.62f, 1.50f))
                : steel ? new Color(0.84f, 0.79f, 0.73f)
                : new Color(0.28f, 0.25f, 0.22f);
            float colScale = darkShield ? 0.64f : steel ? 0.54f : 0.80f;
            StreakStyle style = !darkShield ? StreakStyle.Soot : Shape == TileShape.Square ? StreakStyle.SquareBlack : StreakStyle.HexBlack;
            float strength = style == StreakStyle.SquareBlack ? 0.76f : 0.82f;
            Texture2D streakTex = null;
            Shader streakSh = null;
            try
            {
                streakTex = StreakTexture(style);
                streakSh = StreakShader();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AblatorDecals] streak setup failed: {e}");
            }
            float baseWeath = Mathf.Clamp01(weathering / 100f);
            foreach (TileHost h in hosts)
            {
                if (!h.Alive) continue;
                for (int b = 0; b < h.BinCount; b++)
                {
                    // 1.11.3: continuous vessel-space windward facing (identical on both sides of any wing/fuselage seam;
                    // replaces per-decal maxExposure normalization which caused abrupt opacity steps between adjacent parts)
                    float dirFactor = 1f;
                    if (HighLogic.LoadedSceneIsFlight && root != null)
                    {
                        Vector3 wn = h.WorldBinNormal(b);
                        if (wn != Vector3.zero)
                        {
                            float windward = Mathf.Clamp01(Vector3.Dot(root.InverseTransformDirection(wn).normalized, -flow));
                            dirFactor = 0.55f + 0.55f * windward;
                        }
                    }
                    // 1.11.3: smooth delayed onset — stays clean during the first seconds of reentry (u < 0.006),
                    // reaches ~0.50 after one Kerbin reentry (u ~ 0.06) and climbs to 1.0 across 2+ reentries (u >= 0.22)
                    float u = uni * dirFactor;
                    float onset = Mathf.SmoothStep(0.006f, 0.025f, u);
                    float reentryAmt = onset * Mathf.Pow(Mathf.Clamp01((u - 0.004f) * 4.2f), 0.54f);
                    float amt = Mathf.Clamp01(baseWeath + reentryAmt);
                    h.SetBinChar(b, Color.Lerp(baseCol, charCol, amt * colScale), amt);
                    h.SetStreakAlpha(b, Mathf.Clamp01(amt * strength * glowMask));
                }
                if (streakTex != null && streakSh != null) h.EnsureStreaks(streakTex, streakSh, root, flow, belly, bellyZ);
            }
        }

        // ---------- 1.10 vessel-wide char and streaks ----------

        private float appliedUnified = -1, appliedGlowMask = 1f;
        private Vector3 appliedFlow = DefaultFlow;
        // the craft's longitudinal aft direction in root space: air runs nose to tail (VAB and SPH, the root's up is the nose)
        private static readonly Vector3 DefaultFlow = Vector3.down;

        private sealed class VesselChar
        {
            public int Frame = -1;
            public float Used;                        // area-weighted ablator use of all decals on the vessel
            public Vector3 Flow = DefaultFlow;        // root space, unit
            public Vector3 Belly = Vector3.forward;   // 1.11.3: area-weighted outward belly normal in root space (perp to Flow)
            public float BellyZ;                      // 1.11.3: mean coordinate of shields along Belly (root space)
        }

        // ponytail: weak table keyed by the Vessel (gone with it, nothing to clear on unload), as GlowFields
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Vessel, VesselChar> CharFields =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Vessel, VesselChar>();

        // flight only (null in the editor: the decal's own char, default flow); refreshed once per frame for the whole vessel
        private VesselChar CharField()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return null;
            VesselChar f = CharFields.GetOrCreateValue(vessel);
            if (f.Frame == Time.frameCount) return f;
            f.Frame = Time.frameCount;
            double area = 0, sum = 0;
            Vector3 flow = Vector3.zero, bellySum = Vector3.zero;
            Transform root = RootTransform();
            foreach (ModuleAblativeDecal d in VesselDecals())
            {
                if (d == null || d.vessel != vessel) continue;
                flow += d.charFlowDir;
                if (root != null && d.coveredArea > 0)
                    bellySum += root.InverseTransformDirection(d.part.partTransform.TransformDirection(d.shieldNormal)) * (float)d.coveredArea;
                if (d.coveredArea <= 0 || d.ablator == null || d.ablator.maxAmount <= 0) continue;
                area += d.coveredArea;
                sum += d.coveredArea * (1 - d.ablator.amount / d.ablator.maxAmount);
            }
            f.Used = area > 0 ? (float)(sum / area) : 0f;
            f.Flow = flow.sqrMagnitude > 1e-12f ? flow.normalized : DefaultFlow;
            Vector3 b = bellySum - f.Flow * Vector3.Dot(bellySum, f.Flow);
            if (b.sqrMagnitude < 1e-6f) b = Vector3.Cross(f.Flow, Mathf.Abs(f.Flow.y) < 0.9f ? Vector3.up : Vector3.forward);
            f.Belly = b.normalized;
            double zSum = 0, zArea = 0;
            if (root != null)
                foreach (ModuleAblativeDecal d in VesselDecals())
                {
                    if (d == null || d.vessel != vessel || d.coveredArea <= 0) continue;
                    foreach (TileHost h in d.hosts)
                    {
                        if (!h.Alive) continue;
                        zSum += Vector3.Dot(root.InverseTransformPoint(h.Renderer.bounds.center), f.Belly) * d.coveredArea;
                        zArea += d.coveredArea;
                    }
                }
            f.BellyZ = zArea > 0 ? (float)(zSum / zArea) : 0f;
            return f;
        }

        private Transform RootTransform()
        {
            if (HighLogic.LoadedSceneIsFlight) return vessel != null && vessel.rootPart != null ? vessel.rootPart.partTransform : null;
            Part root = HighLogic.LoadedSceneIsEditor ? EditorLogic.RootPart : null;
            return root != null ? root.partTransform : part.partTransform;
        }

        private static readonly Texture2D[] StreakTextures = new Texture2D[3];
        private static Shader streakShader;

        // generated once per style (CharStreaks), 512 px, mip-mapped, repeating; then no longer readable (GPU copy only).
        // 1.10.1: upload via SetPixels32 (mip 0) so Apply(true, true) generates the mip chain; LoadRawTextureData on a mip-mapped
        // Texture2D in Unity 2019.4 requires all mip levels in the byte buffer and threw an overread exception.
        private static Texture2D StreakTexture(StreakStyle style)
        {
            int i = (int)style;
            if (StreakTextures[i] != null) return StreakTextures[i];
            const int n = 512;
            byte[] raw = CharStreaks.Generate(style, n, 1);
            var px = new Color32[n * n];
            for (int k = 0; k < px.Length; k++)
            {
                int o = k * 4;
                px[k] = new Color32(raw[o], raw[o + 1], raw[o + 2], raw[o + 3]);
            }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "AblativeDecalStreaks" + style, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            Shader sh = StreakShader();
            Debug.Log($"[AblatorDecals] streak texture {style} ready ({n}x{n}, shader={(sh != null ? sh.name : "NONE")})");
            return StreakTextures[i] = t;
        }

        // KSP/Alpha/Translucent; Unity's transparent diffuse when that is missing or has no tint colour
        private static Shader StreakShader()
        {
            if (streakShader != null) return streakShader;
            Shader s = Shader.Find("KSP/Alpha/Translucent");
            if (s != null)
            {
                var probe = new Material(s);
                if (!probe.HasProperty("_Color") || !probe.HasProperty("_MainTex")) s = null;
                Destroy(probe);
            }
            return streakShader = s != null ? s : Shader.Find("Legacy Shaders/Transparent/Diffuse");
        }

        private void OnWeatheringChanged(BaseField field, object oldValue)
        {
            ApplyChar(0);
            // tint only: no CopyToCounterparts (that would rebuild every counterpart on each slider drag)
            foreach (Part p in part.symmetryCounterparts)
            {
                ModuleAblativeDecal m = p != null ? p.FindModuleImplementing<ModuleAblativeDecal>() : null;
                if (m == null) continue;
                m.weathering = weathering;
                m.ApplyChar(0);
            }
        }

        // flight: heat absorbed this step, spread over the bins by how they face the flow (zero-GC once a host has its array)
        private void TrackExposure(Vector3 vWorld, double heat)
        {
            float add = (float)(heat * TimeWarp.fixedDeltaTime);
            // 1.10: the airflow (opposite the motion) in root space, weighted by the heat taken, for the streak direction
            Transform root = RootTransform();
            if (root != null && vWorld != Vector3.zero) charFlowDir += root.InverseTransformDirection(-vWorld) * add;
            foreach (TileHost h in hosts)
            {
                if (!h.Alive || h.BinCount == 0) continue;
                if (!binExposure.TryGetValue(h, out float[] exp) || exp.Length != h.BinCount)
                    binExposure[h] = exp = new float[h.BinCount];
                Vector3 vLocal = h.DecalDirection(vWorld);
                for (int b = 0; b < exp.Length; b++)
                {
                    exp[b] += add * Mathf.Max(0f, Vector3.Dot(h.BinNormals[b], vLocal));
                    if (exp[b] > maxExposure) maxExposure = exp[b];
                }
            }
        }

        // flight rebuild (shedding) makes new hosts: carry each new bin the exposure of the closest old bin on the same part
        private void CarryExposure(List<TileHost> old)
        {
            var carried = new Dictionary<TileHost, float[]>();
            foreach (TileHost h in hosts)
            {
                if (!h.Alive || h.BinCount == 0) continue;
                var exp = new float[h.BinCount];
                for (int b = 0; b < exp.Length; b++)
                {
                    float best = -2f;
                    foreach (TileHost o in old)
                    {
                        if (o.TargetPart != h.TargetPart || !binExposure.TryGetValue(o, out float[] oe)) continue;
                        for (int k = 0; k < oe.Length && k < o.BinNormals.Length; k++)
                        {
                            float d = Vector3.Dot(o.BinNormals[k], h.BinNormals[b]);
                            if (d > best) { best = d; exp[b] = oe[k]; }
                        }
                    }
                }
                carried[h] = exp;
            }
            binExposure.Clear();
            foreach (var kv in carried) binExposure[kv.Key] = kv.Value;
        }

        private static Shader resolvedTileShader;
        private static bool loggedTileShader;

        // 1.11: PBR shader lookup (zero hard mod dependencies via reflection on loaded assemblies):
        // "PBR" tries TU/Metallic (TexturesUnlimited) -> Resurfaced/Standard (Shabby) -> Unity Standard -> KSP/Emissive/Specular.
        private static Shader ResolveTileShader(string configured)
        {
            if (resolvedTileShader != null && (string.IsNullOrEmpty(configured) || Same(configured, "PBR"))) return resolvedTileShader;
            bool pbr = string.IsNullOrEmpty(configured) || Same(configured, "PBR");
            Shader s = null;
            if (!pbr) s = FindModShader(configured);
            if (s == null)
            {
                foreach (string name in new[] { "TU/Metallic", "Resurfaced/Standard", "Standard", "KSP/Emissive/Specular", "KSP/Diffuse" })
                {
                    s = FindModShader(name);
                    if (s != null) break;
                }
            }
            if (s == null) s = Shader.Find("KSP/Diffuse");
            if (pbr) resolvedTileShader = s;
            if (!loggedTileShader && s != null)
            {
                loggedTileShader = true;
                Debug.Log($"[AblatorDecals] tile shader: {s.name}");
            }
            return s;
        }

        private static Shader FindModShader(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Shader s = Shader.Find(name);
            if (s != null) return s;
            // TexturesUnlimited (KSPShaderTools.TexturesUnlimitedLoader.getShader) and Shabby (Shabby.Shabby.FindShader) load shaders from asset bundles
            foreach (var asm in AssemblyLoader.loadedAssemblies)
            {
                if (asm?.assembly == null) continue;
                try
                {
                    Type tu = asm.assembly.GetType("KSPShaderTools.TexturesUnlimitedLoader", false)
                           ?? asm.assembly.GetType("KSPShaderTools.KSPShaderLoader", false);
                    if (tu != null)
                    {
                        var m = tu.GetMethod("getShader", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(string) }, null);
                        if (m != null && m.Invoke(null, new object[] { name }) is Shader ts && ts != null) return ts;
                    }
                    Type sh = asm.assembly.GetType("Shabby.Shabby", false);
                    if (sh != null)
                    {
                        var m = sh.GetMethod("FindShader", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(string) }, null);
                        if (m != null && m.Invoke(null, new object[] { name }) is Shader ss && ss != null) return ss;
                    }
                }
                catch { }
            }
            return null;
        }

        // 1.11: finishes that have at least one enabled ABLATORDECAL_TEXTURE entry in Textures.cfg, in canonical order.
        // Flipping enabled = False/True in Textures.cfg disables/re-enables a finish in both PAW and DecalWindow without code edits.
        private static readonly string[] CanonicalFinishes = { "Black", "Ceramic", "White", "Steel" };
        internal static string[] EnabledFinishes()
        {
            ConfigNode[] meta = GameDatabase.Instance != null ? GameDatabase.Instance.GetConfigNodes("ABLATORDECAL_TEXTURE") : null;
            if (meta == null || meta.Length == 0) return new[] { "Black", "Ceramic", "Steel" };
            var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ConfigNode n in meta)
            {
                if (Same(n.GetValue("enabled"), "false")) continue;
                string fin = n.GetValue("finish");
                if (!string.IsNullOrEmpty(fin)) active.Add(fin.Trim());
            }
            var ordered = new List<string>();
            foreach (string c in CanonicalFinishes) if (active.Remove(c)) ordered.Add(c);
            foreach (string extra in active) ordered.Add(extra);
            return ordered.Count > 0 ? ordered.ToArray() : new[] { "Black" };
        }

        // Some render mods swap materials/textures on vessel load; put ours back.
        private void GuardMaterials()
        {
            foreach (TileHost h in hosts)
            {
                if (!h.Alive || h.Intact(currentTexture, currentGlow, currentMetal, currentBump)) continue;
                if (h.TargetPart != null && !guardWarned)
                {
                    // KSP itself copies them: EditorLogic.highlightSelected (renderer.material, slot 0) and MaterialColorUpdater
                    // (thermal overlay, renderer.materials) instance the materials of every renderer under a part's model
                    guardWarned = true;
                    Debug.LogWarning($"[AblatorDecals] {PartName}: tile materials replaced on {(h.TargetPart.partInfo != null ? h.TargetPart.partInfo.name : "?")} ({h.Difference(currentTexture)}), restoring");
                }
                h.Restore(currentTexture, currentGlow, currentMetal, currentBump);
            }
        }

        private void ClearHosts()
        {
            List<Part> holders = HostHolders();
            foreach (TileHost h in hosts) h.Destroy();
            hosts.Clear();
            ResetHolders(holders);
        }

        // parts whose model holds one of our hosts (the decal; landing gear carrying tiles, A10)
        private List<Part> HostHolders() =>
            hosts.Where(h => h.Alive).Select(h => h.Go.GetComponentInParent<Part>()).Where(x => x != null).Distinct().ToList();

        // renderer caches of every part that held or holds a host (Part.SetOpacity must never meet a dead renderer)
        private static void ResetHolders(IEnumerable<Part> holders)
        {
            foreach (Part x in holders.Distinct()) if (x != null) TileHost.ResetRendererCaches(x);
        }

        private void ClearColliders()
        {
            foreach (GameObject go in colliderObjects) if (go != null) Destroy(go);
            colliderObjects.Clear();
        }

        // ---------- textures ----------

        private void LoadTextures()
        {
            textures.Clear();
            ConfigNode[] meta = GameDatabase.Instance.GetConfigNodes("ABLATORDECAL_TEXTURE");
            // emission, metallic and normal maps lie in the same folder but are no diffuse tile textures
            var aux = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ConfigNode n in meta)
                foreach (string key in new[] { "emissive", "metallic", "bump" })
                {
                    string v = n.GetValue(key);
                    if (!string.IsNullOrEmpty(v)) aux.Add(v.Trim());
                }
            foreach (GameDatabase.TextureInfo info in GameDatabase.Instance.GetAllTexturesInFolder(textureFolder).Where(t => !t.isNormalMap && !aux.Contains(t.name)))
            {
                // Several shapes may share their diffuse texture and have different emission/PBR atlases.
                foreach (ConfigNode node in meta.Where(n => Same(n.GetValue("texture"), info.name)).DefaultIfEmpty())
                {
                    var e = new TexEntry { Url = info.name, Title = info.name.Substring(info.name.LastIndexOf('/') + 1) };
                    if (node != null)
                    {
                        string title = node.GetValue("title");
                        if (!string.IsNullOrEmpty(title)) e.Title = Localizer.Format(title);
                        string shape = node.GetValue("shape");
                        if (!string.IsNullOrEmpty(shape)) e.Shape = shape.Trim();
                        string finish = node.GetValue("finish");
                        if (!string.IsNullOrEmpty(finish)) e.Finish = finish.Trim();
                        string cells = node.GetValue("cells");
                        if (!string.IsNullOrEmpty(cells))
                        {
                            string[] c = cells.Split(new[] { ',', 'x', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (c.Length >= 2 && int.TryParse(c[0], out int cols) && int.TryParse(c[1], out int rows)) { e.Cols = cols; e.Rows = rows; }
                        }
                        bool.TryParse(node.GetValue("randomRotation"), out e.RandomRotation);
                        bool.TryParse(node.GetValue("plainOptions"), out e.PlainOptions);
                        if (Same(node.GetValue("enabled"), "false")) e.Enabled = false;
                        string glow = node.GetValue("emissive");
                        if (!string.IsNullOrEmpty(glow)) e.Emissive = glow.Trim();
                        string metal = node.GetValue("metallic");
                        if (!string.IsNullOrEmpty(metal)) e.Metallic = metal.Trim();
                        string bump = node.GetValue("bump");
                        if (!string.IsNullOrEmpty(bump)) e.Bump = bump.Trim();
                    }
                    textures.Add(e);
                }
            }
        }

        private TileShape Shape => Enum.TryParse(pattern, true, out TileShape s) ? s : TileShape.Hex;
        private string Finish => finishModule != null ? finishModule.finish : "Black";

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // Square also covers Brick
        private bool Fits(TexEntry t) => Same(t.Shape, "Any") || Same(t.Shape, Shape == TileShape.Brick ? "Square" : Shape.ToString());

        // Hex and Square (Triangle on hold); a saved Brick decal keeps Brick in its list until it is switched away
        private void SetupPatternOptions()
        {
            var choose = (UI_ChooseOption)Fields[nameof(pattern)].uiControlEditor;
            bool brick = Same(pattern, "Brick");
            choose.options = brick ? new[] { "Hex", "Square", "Brick" } : new[] { "Hex", "Square" };
            choose.display = choose.options.Select(o => Localizer.Format(L + "pattern_" + o.ToLowerInvariant())).ToArray();
            string match = choose.options.FirstOrDefault(o => Same(o, pattern));
            pattern = match ?? "Hex";
        }

        // Texture for (pattern, finish): exact shape before "Any"; else the first that fits the pattern.
        private TexEntry ResolveTexture() =>
            textures.Where(t => Fits(t) && Same(t.Finish, Finish)).OrderBy(t => Same(t.Shape, "Any") ? 1 : 0).FirstOrDefault()
            ?? textures.FirstOrDefault(Fits) ?? textures.FirstOrDefault();

        // 1.11.2: textureAcross/textureScale removed from PAW
        private bool UpdateTextureUi() => false;

        private bool UseTileAtlas => texture != null && texture.Atlas &&
            (!texture.PlainOptions || (!textureAcross && Mathf.Approximately(textureScale, 2f)));

        private Texture2D FindTexture()
        {
            texture = ResolveTexture();
            string key = pattern + "/" + Finish;
            if ((texture == null || !Same(texture.Finish, Finish)) && warnedTexture != key)
            {
                warnedTexture = key;
                Debug.LogWarning($"[AblatorDecals] no texture for pattern {pattern} and finish {Finish} in Textures.cfg, using '{texture?.Url ?? "none"}'");
            }
            return texture != null ? GameDatabase.Instance.GetTexture(texture.Url, false) : null;
        }

        // emission texture of the current entry (FindTexture ran before), else a 1x1 glowFlatColor (plain textures: no per-tile UVs)
        private Texture FindGlow()
        {
            Texture2D t = texture != null && texture.Emissive != null ? GameDatabase.Instance.GetTexture(texture.Emissive, false) : null;
            if (t != null)
            {
                t.wrapMode = texture.PlainOptions ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                return t;
            }
            if (flatGlow == null)
            {
                flatGlow = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "AblativeDecalGlowFlat" };
                flatGlow.SetPixel(0, 0, new Color(glowFlatColor.x, glowFlatColor.y, glowFlatColor.z));
                flatGlow.Apply();
            }
            return flatGlow;
        }

        // 1.11: PBR metallic-smoothness atlas (R = metallic, A = smoothness), else matte dielectric 1x1 fallback
        private Texture FindMetal()
        {
            Texture2D t = texture != null && texture.Metallic != null ? GameDatabase.Instance.GetTexture(texture.Metallic, false) : null;
            if (t != null)
            {
                t.wrapMode = UseTileAtlas && !texture.PlainOptions ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                return t;
            }
            if (defaultMetal == null)
            {
                defaultMetal = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "AblativeDecalMetalDefault" };
                defaultMetal.SetPixel(0, 0, new Color(0.03f, 0.03f, 0.03f, 0.25f));
                defaultMetal.Apply();
            }
            return defaultMetal;
        }

        // 1.11: PBR tangent-space normal map (*_NRM, DXT5nm), else flat tangent normal 1x1 fallback
        private Texture FindBump()
        {
            Texture2D t = texture != null && texture.Bump != null
                ? (GameDatabase.Instance.GetTexture(texture.Bump, true) ?? GameDatabase.Instance.GetTexture(texture.Bump, false))
                : null;
            if (t != null)
            {
                t.wrapMode = UseTileAtlas && !texture.PlainOptions ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                return t;
            }
            if (defaultBump == null)
            {
                defaultBump = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "AblativeDecalBumpDefault" };
                defaultBump.SetPixel(0, 0, new Color(0.5f, 0.5f, 1f, 0.5f)); // flat normal in both RGB and DXT5nm (G=0.5, A=0.5)
                defaultBump.Apply();
            }
            return defaultBump;
        }

        // ---------- build ----------

        private TileSettings MakeSettings()
        {
            var s = new TileSettings
            {
                Shape = Shape,
                TileSize = tileSize,
                AngleDeg = patternAngle,
                Gap = tileGap,
                Thickness = thickness,
                Width = width,
                Height = height,
                MaxWrapDeg = maxWrap,
                StraightBorder = straightBorder,
                Unrolled = unrolled,
                TileLoss = Mathf.Clamp(Mathf.Clamp(tileLoss, 0f, 20f) / 100f + battleLoss, 0f, 0.35f),
                SurfaceOffset = surfaceOffset,
                CornerBendDeg = cornerBendAngle,
                FaceBendDeg = faceBendAngle,
                CurvatureFactor = curvatureFactor,
                TextureScale = textureScale,
                TextureAspect = currentTexture != null ? (float)currentTexture.height / currentTexture.width : 1f,
                MaxTiles = maxTiles,
                ColliderPatchSize = colliderPatchSize,
                Seed = seed,
                BinAngleDeg = glowBinAngle,
                MaxBins = glowMaxBins,
            };
            if (UseTileAtlas)
            {
                s.UvMode = UvMode.Atlas;
                s.AtlasCols = texture.Cols;
                s.AtlasRows = texture.Rows;
                s.AtlasRandomRotation = texture.RandomRotation;
            }
            else s.UvMode = textureAcross ? UvMode.Across : UvMode.Window;
            return s;
        }

        // whole part: the footprint is the part itself, so the hull index for the gear (Collect) takes every hull triangle
        private float Reach => areaLimited ? ReachFor(width, height) : 1e4f;
        private float ReachFor(float w, float h) => 0.5f * Mathf.Sqrt(w * w + h * h) + 0.5f * Mathf.Max(w, h) + 2 * tileSize + 1f;
        private float CellSize => Mathf.Max(tileSize * 0.5f, 0.05f);
        private float SmoothRadius => Mathf.Clamp(tileSize * 0.4f, 0.02f, 0.3f);

        // Main thread: copy settings, collect hull triangles (cached).
        private BuildJob MakeJob()
        {
            currentTexture = FindTexture();
            if (currentTexture != null) currentTexture.wrapMode = UseTileAtlas && !texture.PlainOptions ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            tileTemplate.mainTexture = currentTexture;
            currentGlow = FindGlow();
            int emissiveProp = TileHost.EmissiveTexProperty(tileTemplate);
            if (emissiveProp != 0) tileTemplate.SetTexture(emissiveProp, currentGlow);
            currentMetal = FindMetal();
            int metalProp = TileHost.MetalTexProperty(tileTemplate);
            if (metalProp != 0) tileTemplate.SetTexture(metalProp, currentMetal);
            currentBump = FindBump();
            if (tileTemplate.HasProperty(TileHost.BumpMapID)) tileTemplate.SetTexture(TileHost.BumpMapID, currentBump);

            Vector3 n = shieldNormal.normalized;
            Vector3 upHint = Mathf.Abs(Vector3.Dot(n, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right;
            Vector3 v = Quaternion.LookRotation(n, upHint) * Vector3.up;
            Vector3 u = Vector3.Cross(n, v); // v = u x n: seen from outside u points right, v up

            bool attached = part.parent != null && !detached;
            Part[] parts = attached ? TargetParts() : new Part[0];
            Matrix4x4[] poses = Poses(parts);
            lastTargets = parts;
            lastPoses = poses;
            Part[] gear = attached ? GearInFootprint(parts, u, v) : new Part[0]; // 1.6: on cockpit covers too
            SurfaceInput surface = attached ? Collect(parts, poses, gear, u, v) : null;
            Body[] bodies = surface != null ? surface.Bodies : new Body[0];
            TileSettings settings = MakeSettings();
            if (bodies.Any(b => b.Flap)) settings.SeparateOwner = bodies.Select(b => b.Flap).ToArray();
            if (bodies.Any(b => b.Group != -1)) // v11: objects end the cover at their foot, retracted gear it runs on under
            {
                settings.Obstacle = bodies.Select(b => b.Group != -1).ToArray();
                settings.PassUnder = bodies.Select(b => b.Gear).ToArray();
            }
            settings.Occupied = attached ? Occupied(parts) : null;
            if (attached) InitParentDefaults();
            settings.KeepOut = attached ? KeepOut(parts) : null;
            bool isBlocked = attached && Blocker(parts) != null;
            Cover cover = CoverOfParent();
            settings.FillHull = cover != null || parts.Length > 0 && TileBuilder.Mk2Hull(parts[0].partInfo.name);
            settings.Skin = surface != null && surface.FrameSkin && cover == null; // 1.9: radial bay frame: closed skin, no white slits
            if (attached && parts.Length > 0)
            {
                // 1.8: always (round parts: smooth hull, island floods), not only for the Mk2 rim fill
                settings.LimitedTank = cover == null && areaLimited;
                settings.TankAxis = poses[0].MultiplyVector(Vector3.up).normalized;
                settings.TankCenter = poses[0].MultiplyPoint3x4(Vector3.zero);
            }
            // 1.8: smooth hull, decided once on a round parent without flaps/doors or inflatables; then the player's toggle
            if (attached && cover == null && surface != null && !smoothHullInit)
            {
                smoothHullInit = true;
                bool eligible = !bodies.Any(b => b.Part == part.parent && b.Flap) &&
                                !part.parent.Modules.Cast<PartModule>().Any(m => m.moduleName.Contains("Centrifuge") || m.moduleName.Contains("DeployableHabitat"));
                smoothHull = eligible && SurfaceIndex.BuildSmoothHull(surface.Pos, surface.Tri, surface.Owner, 0, settings.TankAxis, settings.TankCenter, false) != null;
            }
            if (cover == null && !areaLimited && surface != null)
            {
                // whole part: no footprint (pi x the farthest hull point: half the way round a short wide 5 m / 7.5 m tank is
                // longer than 2 x), a straight cut at every part seam, only max wrap limits
                float size = 0;
                foreach (Vector3 vert in surface.Pos) size = Mathf.Max(size, vert.magnitude);
                settings.Width = settings.Height = Mathf.PI * size + 2 * tileSize;
                settings.SeparateOwner = bodies.Select(b => true).ToArray();
                // trim at every stack node with a 0.5 mm tolerance (1.9: free ones too, flat stack and docking faces are
                // never tiled); planes are infinite, so only nodes leading away from the targets
                var cuts = new List<Vector4>();
                for (int i = 0; i < parts.Length; i++)
                    foreach (AttachNode node in parts[i].attachNodes)
                    {
                        if (node.nodeType != AttachNode.NodeType.Stack || node.attachedPart != null && parts.Contains(node.attachedPart)) continue;
                        Vector3 dir = node.orientation.normalized;
                        if (dir.sqrMagnitude < 0.5f) continue;
                        // 1.9.1: a free node only cuts when axial (top/bottom/docking); FreeIva adds free radial hatch nodes
                        // (mk3CrewCabin front/back, Large_Crewed_Lab airlockL/R) that would slice whole sides off
                        if (node.attachedPart == null && !TileBuilder.FreeNodeCuts(node.id, dir)) continue;
                        // G5: Mk2/Mk3 cargo bays & service bays have inward payload nodes (top2/bottom2) recessed 20-100 mm
                        // inside the outer bulkhead. Flip inward nodes outward AND push the cut plane to the outermost stack
                        // node in that direction so stacked cargo bays never get a 40 mm seam gap.
                        Vector3 pos = node.position;
                        if (Vector3.Dot(dir, pos) < 0)
                        {
                            dir = -dir;
                            float outer = Vector3.Dot(dir, pos);
                            foreach (AttachNode other in parts[i].attachNodes)
                            {
                                if (other.nodeType != AttachNode.NodeType.Stack) continue;
                                Vector3 od = other.orientation.normalized;
                                if (Vector3.Dot(od, dir) > 0.9f)
                                {
                                    float proj = Vector3.Dot(dir, other.position);
                                    if (proj > outer) { outer = proj; pos = other.position; }
                                }
                            }
                        }
                        cuts.Add(ToDecal(new Vector4(dir.x, dir.y, dir.z, -Vector3.Dot(dir, pos + 0.0005f * dir)), poses[i]));
                    }
                if (cuts.Count > 0) settings.CutPlanes = cuts.ToArray();
            }
            if (cover != null)
            {
                // whole cockpit (TargetParts gave only the parent), cut along its planes (part space -> decal space)
                settings.ClipPlanes = cover.Clip.Select(c => ToDecal(c, poses[0])).ToArray();
                settings.CutPlanes = cover.Cut.Select(c => ToDecal(c, poses[0])).ToArray();
                if (cover.Regions.Length > 0) settings.Regions = cover.Regions.Select(rg => ToDecal(rg, poses[0])).ToArray(); // v11 mask
                settings.CeramicSideDeg = cover.CeramicSideDeg;
                settings.CeramicAxis = cover.CeramicAxis;
                // 2 x the farthest hull point from the cockpit's origin: unrolled, the part size alone ends short of the windows
                Vector3 origin = poses[0].MultiplyPoint3x4(Vector3.zero);
                float size = 0;
                if (surface != null)
                    for (int k = 0; k < surface.Tri.Length; k++)
                        if (surface.Bodies[surface.Owner[k / 3]].Group == -1) size = Mathf.Max(size, (surface.Pos[surface.Tri[k]] - origin).magnitude);
                settings.Width = settings.Height = 2 * size;
                settings.MaxWrapDeg = 180;
            }
            Vector4? heightCut = HeightCut(surface, n);
            if (heightCut is Vector4 hc) settings.CutPlanes = (settings.CutPlanes ?? new Vector4[0]).Append(hc).ToArray();
            // v11: bay doors and ramps follow the decal's Max wrap and height cut, in the object's space; both depend on the decal's
            // turn and settings only, not on where it sits. 1.6: so do flaps, fins and winglets (their top stays free like the
            // hull's); landing gear and intakes get a closed skin all round instead (user: "lieber alles zu")
            var overlaySettings = (surface != null ? surface.Overlays : new OverlayInput[0]).Select(oi =>
            {
                TileSettings os = TileBuilder.OverlaySettings(settings);
                if (oi.Skin && oi.DoorSkin)
                {
                    // 1.9: radial service bay doors: closed outer skin, but within the decal's Max wrap and stack/height cuts
                    Matrix4x4 toDoor = oi.ToDecal.inverse;
                    os.Skin = true;
                    os.WrapOwner = oi.Obstacle.Select(x => !x).ToArray();
                    os.WrapN = toDoor.MultiplyVector(n).normalized;
                    os.MaxWrapDeg = settings.MaxWrapDeg;
                    if (cover == null && settings.CutPlanes != null) os.CutPlanes = settings.CutPlanes.Select(c => ToDecal(c, toDoor)).ToArray();
                    return os;
                }
                if (oi.Skin) { os.WrapN = oi.ToDecal.inverse.MultiplyVector(n).normalized; os.Skin = true; os.GearSkin = oi.Body.Any(b => bodies[b].Gear); if (os.GearSkin) { os.WrapOwner = oi.Obstacle.Select(x => !x).ToArray(); os.MaxWrapDeg = 95f; } return os; } // 1.6.2: gear top (faces turned away from the decal) stays free
                Matrix4x4 toObj = oi.ToDecal.inverse;
                os.Skin = !oi.Inward.Any(x => x);
                os.WrapOwner = oi.Obstacle.Select(x => !x).ToArray();
                os.WrapN = toObj.MultiplyVector(n).normalized;
                // 1.7.1: fins, winglets and flaps (closed skin) up to 150 deg, so their edges and root rails are covered too; only doors
                // take the stack faces, a winglet over the seam to the next part is tiled on both sides of it (height cut only)
                bool doorObj = oi.Inward.Any(x => x);
                // 1.7.2: the shield of a capped docking port gets the rim fill, so its tip where the doors meet is closed
                os.FillHull = oi.Body.Any(b => bodies[b].Inward && bodies[b].Part != parts[0] && ShieldedPort(bodies[b].Part));
                os.MaxWrapDeg = doorObj ? settings.MaxWrapDeg : 150f;
                if (cover == null && settings.CutPlanes != null) os.CutPlanes = (doorObj ? settings.CutPlanes : heightCut is Vector4 hc0 ? new[] { hc0 } : new Vector4[0]).Select(c => ToDecal(c, toObj)).ToArray();
                return os;
            }).ToArray();
            settings.SmoothAxisLock = smoothHull && cover == null; // 1.9.1: no frame drift along long smooth tanks
            // 1.8: shed tiles must disappear on cockpits too, so a damaged cover is built instead of the pre-made one
            TileResult premade = cover != null && surface != null && battleLoss <= 0f ? PremadeCover(part.parent, Shape) : null;
            return new BuildJob
            {
                Settings = settings,
                OverlaySettings = overlaySettings,
                Premade = premade,
                PremadePose = premade != null ? poses[0] : Matrix4x4.identity,
                N = n, U = u, V = v,
                Attached = attached,
                Flight = HighLogic.LoadedSceneIsFlight,
                Bodies = bodies,
                Surface = surface,
                Blocked = isBlocked,
                SmoothHull = smoothHull && cover == null,
                Reach = ReachFor(settings.Width, settings.Height), Cell = CellSize, Smooth = SmoothRadius,
                Width = settings.Width, Height = settings.Height,
            };
        }

        // M2: cut plane (decal space) keeping the outer coverHeight % of the parent's extent along the decal normal n, measured
        // from its outermost point on the decal's side; null at 100 % or without hull. Only the parent's own body counts (not its
        // flaps, doors, gear or glued parts). Kept: dot(n, x) >= c, i.e. dot(-n, x) + c <= 0. c stays 1 cm below the decal's own
        // spot (decal space origin), else TileBuilder would turn the plane round (start point beyond it) and keep the far side.
        private Vector4? HeightCut(SurfaceInput surface, Vector3 n)
        {
            if (coverHeight >= 99.95f || surface == null || part.parent == null) return null;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int t = 0; t < surface.Owner.Length; t++)
            {
                int o = surface.Owner[t];
                if (o < 0 || o >= surface.Bodies.Length || surface.Bodies[o].Part != part.parent || surface.Bodies[o].T != part.parent.partTransform) continue;
                for (int k = 0; k < 3; k++)
                {
                    float d = Vector3.Dot(n, surface.Pos[surface.Tri[3 * t + k]]);
                    lo = Mathf.Min(lo, d);
                    hi = Mathf.Max(hi, d);
                }
            }
            if (lo >= hi) return null;
            float c = Mathf.Min(hi - Mathf.Clamp01(coverHeight / 100f) * (hi - lo), -0.01f);
            return new Vector4(-n.x, -n.y, -n.z, c);
        }

        // target part poses relative to this part
        private Matrix4x4[] Poses(Part[] parts)
        {
            Matrix4x4 toPart = part.partTransform.worldToLocalMatrix;
            return parts.Select(p => toPart * p.partTransform.localToWorldMatrix).ToArray();
        }

        // Cockpit cover planes, part space, as (normal, w): keep where dot(normal, x) + w <= 0. Clip: union (kept where at least
        // one plane keeps), Cut: intersection (PLANE with cut = true).
        private class Cover
        {
            public Vector4[] Clip, Cut;
            public bool KeepRcs;          // M3 (1.5): keepRcs = true, the RCS nozzles of this cockpit stay free
            public float CeramicSideDeg;  // 1.7.4: smooth untiled ceramic on nose tip & sides/chines (|dot(n, decalN)| <= sin(deg))
            public bool CeramicAxis;      // 1.9: ceramicAxis = true, the ceramic follows the part axis (shoulders, rims) also with a mask
            public CoverRegion[] Regions = new CoverRegion[0]; // DESIGN-v11 G2/G3: REGION nodes (mask regions), part space
        }

        // the parent's cover, or null when it does not apply
        private Cover CoverOfParent() => part.parent != null && !detached && cockpitShape ? CoverPlanes(part.parent) : null;

        // plane (part space of the pose's part) -> decal space
        private static Vector4 ToDecal(Vector4 plane, Matrix4x4 pose)
        {
            Vector3 n = new Vector3(plane.x, plane.y, plane.z);
            Vector3 dn = pose.MultiplyVector(n).normalized, dp = pose.MultiplyPoint3x4(-plane.w * n);
            return new Vector4(dn.x, dn.y, dn.z, -Vector3.Dot(dn, dp));
        }

        // ABLATORDECAL_COVER nodes of CockpitCover.cfg, by part name as KSP spells it (mk2Cockpit_Standard -> mk2Cockpit.Standard).
        // A later node for the same part replaces an earlier one (ModuleManager keeps file order).
        private static Dictionary<string, Cover> covers;

        private static Cover CoverPlanes(Part p)
        {
            if (covers == null)
            {
                covers = new Dictionary<string, Cover>();
                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("ABLATORDECAL_COVER"))
                {
                    string name = node.GetValue("part");
                    if (string.IsNullOrEmpty(name)) continue;
                    var clip = new List<Vector4>();
                    var cut = new List<Vector4>();
                    foreach (ConfigNode pl in node.GetNodes("PLANE"))
                    {
                        if (!pl.HasValue("normal") || !pl.HasValue("point")) continue;
                        Vector3 n = ConfigNode.ParseVector3(pl.GetValue("normal")).normalized, pt = ConfigNode.ParseVector3(pl.GetValue("point"));
                        bool.TryParse(pl.GetValue("cut"), out bool isCut);
                        (isCut ? cut : clip).Add(new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, pt)));
                    }
                    bool.TryParse(node.GetValue("keepRcs"), out bool keepRcs);
                    float.TryParse(node.GetValue("ceramicSideDeg"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ceramicSideDeg);
                    if (bool.TryParse(node.GetValue("ceramicAll"), out bool ceramicAll) && ceramicAll) ceramicSideDeg = 90f; // 1.9: the whole cover smooth ceramic
                    bool.TryParse(node.GetValue("ceramicAxis"), out bool ceramicAxis);
                    covers[name.Trim().Replace('_', '.')] = new Cover { Clip = clip.ToArray(), Cut = cut.ToArray(), KeepRcs = keepRcs, CeramicSideDeg = ceramicSideDeg, CeramicAxis = ceramicAxis, Regions = node.GetNodes("REGION").Select(ParseRegion).Where(r => r != null).ToArray() };
                }
            }
            return p.partInfo != null && covers.TryGetValue(p.partInfo.name, out Cover c) && (c.Clip.Length + c.Cut.Length + c.Regions.Length > 0 || c.CeramicSideDeg >= 89.5f) ? c : null;
        }

        // DESIGN-v11 G2/G3: REGION { origin u v domain = nx, ny, nz, px, py, pz (any number) loop = x y, x y, ... (any number) }, part
        // space; null if it has no usable loop
        private static CoverRegion ParseRegion(ConfigNode rn)
        {
            var loops = new List<Vector2[]>();
            foreach (string l in rn.GetValues("loop"))
            {
                var pts = new List<Vector2>();
                foreach (string xy in l.Split(','))
                {
                    string[] f = xy.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (f.Length == 2 && float.TryParse(f[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)) pts.Add(new Vector2(x, y));
                }
                if (pts.Count >= 3) loops.Add(pts.ToArray());
            }
            if (loops.Count == 0 || !rn.HasValue("u") || !rn.HasValue("v")) return null;
            var domain = new List<Vector4>();
            foreach (string d in rn.GetValues("domain"))
            {
                float[] q = d.Split(',').Select(s => float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : float.NaN).ToArray();
                if (q.Length != 6 || q.Any(float.IsNaN)) continue;
                Vector3 n = new Vector3(q[0], q[1], q[2]).normalized;
                domain.Add(new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, new Vector3(q[3], q[4], q[5]))));
            }
            return new CoverRegion
            {
                O = rn.HasValue("origin") ? ConfigNode.ParseVector3(rn.GetValue("origin")) : Vector3.zero,
                A = ConfigNode.ParseVector3(rn.GetValue("u")).normalized,
                B = ConfigNode.ParseVector3(rn.GetValue("v")).normalized,
                Loops = loops.ToArray(),
                Domain = domain.ToArray()
            };
        }

        // a cover region, part space of the pose's part -> decal space
        private static CoverRegion ToDecal(CoverRegion r, Matrix4x4 pose) => new CoverRegion
        {
            O = pose.MultiplyPoint3x4(r.O),
            A = pose.MultiplyVector(r.A).normalized,
            B = pose.MultiplyVector(r.B).normalized,
            Loops = r.Loops,
            Domain = r.Domain.Select(d => ToDecal(d, pose)).ToArray()
        };

        // DESIGN-v11: pre-made cockpit covers, GameData/AblatorDecals/Covers/<part name, '.' as '_'>_<Hex|Square>.bin (cockpit part
        // space, made by tools/Preview --bake-cover from the cockpit's CockpitCover.cfg node). Null if there is none (Brick, other
        // cockpits): then the cover is built from the node as before. Read once per file.
        private static readonly Dictionary<string, TileResult> premadeCovers = new Dictionary<string, TileResult>();

        private static TileResult PremadeCover(Part p, TileShape shape)
        {
            // Hex/Square: loaded if baked, else null and the cover is built at runtime
            if (p?.partInfo == null || (shape != TileShape.Hex && shape != TileShape.Square)) return null;
            string file = System.IO.Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "AblatorDecals", "Covers",
                p.partInfo.name.Replace('.', '_') + "_" + shape + ".bin");
            if (premadeCovers.TryGetValue(file, out TileResult c)) return c;
            try { c = System.IO.File.Exists(file) ? CoverMesh.Read(System.IO.File.ReadAllBytes(file)) : null; }
            catch (Exception e) { Debug.LogWarning($"[AblatorDecals] cover file {file} unreadable, building instead: {e.Message}"); c = null; }
            if (c != null) Debug.Log($"[AblatorDecals] pre-made cover {System.IO.Path.GetFileName(file)}: {c.TileCount} tiles, {c.CoveredArea:F2} m2");
            premadeCovers[file] = c;
            return c;
        }

        // 1.9: window boxes (part space: centre, half size) by part name as KSP spells it, from ABLATORDECAL_WINDOWS nodes
        // (part = <name>, WINDOW { center = x, y, z  size = sx, sy, sz } any number) and WINDOW nodes inside ABLATORDECAL_COVER
        private static Dictionary<string, List<(Vector3 center, Vector3 half)>> windows;

        private static List<(Vector3 center, Vector3 half)> Windows(Part p)
        {
            if (windows == null)
            {
                windows = new Dictionary<string, List<(Vector3, Vector3)>>();
                foreach (string type in new[] { "ABLATORDECAL_WINDOWS", "ABLATORDECAL_COVER" })
                    foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(type))
                    {
                        string name = node.GetValue("part");
                        if (string.IsNullOrEmpty(name)) continue;
                        name = name.Trim().Replace('_', '.');
                        foreach (ConfigNode w in node.GetNodes("WINDOW"))
                        {
                            if (!w.HasValue("center") || !w.HasValue("size")) continue;
                            if (!windows.TryGetValue(name, out var list)) windows[name] = list = new List<(Vector3, Vector3)>();
                            list.Add((ConfigNode.ParseVector3(w.GetValue("center")), ConfigNode.ParseVector3(w.GetValue("size")) / 2));
                        }
                    }
            }
            return p.partInfo != null && windows.TryGetValue(p.partInfo.name, out var l) ? l : null;
        }

        // M3: RCS nozzles stay free only on a cockpit whose CockpitCover.cfg entry says keepRcs = true (planes or not)
        private static bool KeepRcs(Part p)
        {
            CoverPlanes(p); // loads the table
            return p.partInfo != null && covers.TryGetValue(p.partInfo.name, out Cover c) && c.KeepRcs;
        }

        // Decals on the same craft, this one included.
        private IEnumerable<ModuleAblativeDecal> ShipDecals()
        {
            List<Part> parts = HighLogic.LoadedSceneIsFlight ? vessel?.parts : EditorLogic.fetch?.ship?.parts;
            return parts == null ? Enumerable.Empty<ModuleAblativeDecal>()
                : parts.Where(p => p != null).Select(p => p.FindModuleImplementing<ModuleAblativeDecal>()).Where(m => m != null);
        }

        // No decal on decal: points of the tiles of OLDER decals (lower persistentId) on the same parts, in this decal's space.
        // Younger decals are ignored here; they make room for this one instead (Apply asks them to rebuild).
        private Vector3[] Occupied(Part[] parts)
        {
            if (!areaLimited) return null; // a whole-part decal is first or blocked, it never fills around others
            var seen = new HashSet<Vector3Int>();
            var list = new List<Vector3>();
            foreach (ModuleAblativeDecal d in ShipDecals())
            {
                if (!d.areaLimited || d.part.persistentId >= part.persistentId || d.lastResult == null || !d.targets.Any(parts.Contains)) continue;
                Matrix4x4 m = part.partTransform.worldToLocalMatrix * d.part.partTransform.localToWorldMatrix;
                foreach (PartTiles pt in d.lastResult.Parts)
                    foreach (Vector3 vert in pt.Vertices)
                    {
                        Vector3 q = m.MultiplyPoint3x4(vert);
                        if (seen.Add(Vector3Int.RoundToInt(q * 200))) list.Add(q); // ponytail: 5 mm dedupe, tops/bevels/walls share spots
                    }
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        // M4: no tiles over RCS nozzles and hatches of the target parts. Boxes in decal space for TileSettings.KeepOut (groups of 4:
        // centre, three orthogonal half-axis vectors): a cube of half size rcsKeepOut around every thruster transform of
        // ModuleRCS/ModuleRCSFX (1.5, M3: only on cockpits with keepRcs = true in CockpitCover.cfg, elsewhere the tiles cover
        // them), and every collider tagged "Airlock" grown by 5 cm (a BoxCollider oriented, others by their world bounds).
        private Vector3[] KeepOut(Part[] parts)
        {
            Matrix4x4 toPart = part.partTransform.worldToLocalMatrix;
            var list = new List<Vector3>();
            void Add(Vector3 centre, Vector3 ax, Vector3 ay, Vector3 az) // world
            {
                list.Add(toPart.MultiplyPoint3x4(centre));
                list.Add(toPart.MultiplyVector(ax));
                list.Add(toPart.MultiplyVector(ay));
                list.Add(toPart.MultiplyVector(az));
            }
            int rcs = 0, hatches = 0, wins = 0;
            foreach (Part p in parts)
            {
                foreach (ModuleRCS m in KeepRcs(p) ? p.FindModulesImplementing<ModuleRCS>() : new List<ModuleRCS>())
                {
                    if (string.IsNullOrEmpty(m.thrusterTransformName)) continue;
                    foreach (Transform t in p.FindModelTransforms(m.thrusterTransformName))
                    {
                        Add(t.position, t.right * rcsKeepOut, t.up * rcsKeepOut, t.forward * rcsKeepOut);
                        rcs++;
                    }
                }
                foreach (Collider c in keepHatches ? p.FindModelComponents<Collider>() : new List<Collider>())
                {
                    if (c == null || !c.CompareTag("Airlock")) continue;
                    if (c is BoxCollider bc)
                    {
                        Transform t = bc.transform;
                        Vector3 ax = t.TransformVector(bc.size.x / 2, 0, 0), ay = t.TransformVector(0, bc.size.y / 2, 0), az = t.TransformVector(0, 0, bc.size.z / 2);
                        Vector3 h = TileBuilder.HatchKeepHalf(new Vector3(ax.magnitude, ay.magnitude, az.magnitude)); // 1.9: deep enough for recessed doors
                        Add(t.TransformPoint(bc.center), ax.normalized * h.x, ay.normalized * h.y, az.normalized * h.z);
                    }
                    else
                    {
                        Bounds b = c.bounds;
                        Vector3 e = TileBuilder.HatchKeepHalf(b.extents);
                        Add(b.center, new Vector3(e.x, 0, 0), new Vector3(0, e.y, 0), new Vector3(0, 0, e.z));
                    }
                    hatches++;
                }
                // 1.9: registered windows: 5 mm frame in plane, >= 14 cm deep along the pane normal (recessed glass)
                foreach (var (center, half0) in keepWindows ? Windows(p) ?? new List<(Vector3, Vector3)>() : new List<(Vector3, Vector3)>())
                {
                    Vector3 half = TileBuilder.WindowKeepHalf(half0);
                    Transform t = p.partTransform;
                    Add(t.TransformPoint(center), t.TransformVector(half.x, 0, 0), t.TransformVector(0, half.y, 0), t.TransformVector(0, 0, half.z));
                    wins++;
                }
            }
            if (list.Count > 0 && Time.realtimeSinceStartup >= nextKeepOutLog)
            {
                nextKeepOutLog = Time.realtimeSinceStartup + 5f;
                Debug.Log($"[AblatorDecals] {PartName} keep-out: rcs={rcs} hatches={hatches} windows={wins} on [{string.Join(", ", parts.Select(q => q.partInfo?.name))}]");
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        // M2: an older decal on the same part(s) when one of the two covers its whole part; the younger one then stays empty.
        // Two "limit area" decals share a part (the younger fills up to the older's tiles via Occupied).
        private ModuleAblativeDecal Blocker(Part[] parts) =>
            ShipDecals().FirstOrDefault(d => d != this && d.part.persistentId < part.persistentId && (!d.areaLimited || !areaLimited) &&
                                             !d.blocked && d.targets.Any(parts.Contains));

        // younger decals on these parts rebuild (they fit around this one, or get the area back)
        private void NotifyYounger(HashSet<Part> touched)
        {
            foreach (ModuleAblativeDecal d in ShipDecals())
                if (d.part.persistentId > part.persistentId && d.lastTargets.Any(touched.Contains))
                {
                    if (HighLogic.LoadedSceneIsFlight) d.rebuildNow = true; else d.rebuildPending = true;
                }
        }

        // world point within one tile of this decal's tiles
        private bool OnTiles(Vector3 world)
        {
            if (lastResult == null) return false;
            Vector3 p = part.partTransform.InverseTransformPoint(world);
            float r2 = tileSize * tileSize;
            foreach (PartTiles pt in lastResult.Parts)
                foreach (Vector3 vert in pt.Vertices)
                    if ((vert - p).sqrMagnitude <= r2) return true; // ponytail: linear scan, only on attach and build events
            return false;
        }

        private static Vector3 AttachPoint(Part p) => p.srfAttachNode != null ? p.partTransform.TransformPoint(p.srfAttachNode.position) : p.partTransform.position;

        // Parts glued onto a covered part inside the tiles that are no targets (gear, radial decouplers ...): they never collide
        // with the tiles and get the same heat protection as the part they sit on.
        private List<Part> PartsOnTiles() =>
            targets.Where(t => t != null).SelectMany(t => t.children)
                .Where(c => c != null && c != part && c.attachMode == AttachModes.SRF_ATTACH && !targets.Contains(c) &&
                            c.FindModuleImplementing<ModuleAblativeDecal>() == null && OnTiles(AttachPoint(c)))
                .ToList();

        // Worker (or main thread in flight): no Unity objects, no logging.
        private static void Work(BuildJob job)
        {
            try
            {
                if (job.Cancelled) return;
                Stopwatch watch = Stopwatch.StartNew();
                TileResult r = null;
                if (job.Blocked)
                {
                    job.Result = new TileResult { Parts = new PartTiles[0], Colliders = new TileBuilder.Box[0] };
                    return;
                }
                if (job.Premade != null) // v11: pre-made cockpit cover, no build (1.6: its overlays still are, below)
                {
                    r = CoverMesh.Transform(job.Premade, job.PremadePose);
                    job.OnSurface = true;
                }
                bool overlaysDone = false;
                if (job.Premade == null && job.Surface != null)
                {
                    Vector3[] sPos = job.Surface.Pos;
                    int[] sTri = job.Surface.Tri, sOwner = job.Surface.Owner;
                    // 1.8: smooth revolution hull of the parent (the player's toggle: forced fit, exact mesh if it fails)
                    SurfaceIndex.SmoothHullResult smooth = job.SmoothHull
                        ? SurfaceIndex.BuildSmoothHull(sPos, sTri, sOwner, 0, job.Settings.TankAxis, job.Settings.TankCenter, force: true)
                        : null;
                    if (smooth != null) { sPos = smooth.Pos; sTri = smooth.Tri; sOwner = smooth.Owner; job.Settings.FillHull = true; } // 1.9: dark bed under the tiles, no white through seams
                    var index = new SurfaceIndex(sPos, sTri, Vector3.zero, job.Reach, job.Cell, job.Smooth, sOwner);
                    if (job.Cancelled) return;
                    r = TileBuilder.Build(index, job.Settings, Vector3.zero, job.N, job.U, job.V);
                    job.OnSurface = r.OnSurface && r.TileCount > 0;
                    if (smooth != null && job.OnSurface && smooth.ProtrusionTri.Length > 0)
                    {
                        // pipes and ladders: visual skin; cuts turned like TileBuilder.Run does (decal spot on the kept side)
                        Vector4[] cuts = job.Settings.CutPlanes?.Select(c => c.w > 0 ? -c : c).ToArray();
                        PartTiles skin = TileBuilder.BuildVisualSkin(smooth.ProtrusionPos, smooth.ProtrusionTri, 0, job.Settings, job.N, cuts, Vector3.zero, job.U, job.V);
                        if (skin != null) r.Parts = r.Parts.Append(skin).ToArray();
                    }
                    // 1.8: decal glued onto the parent's own door (service-tank-375): no hull under it in the closed pose. Tile the
                    // static frame from the nearest face turned the decal's way, then the doors themselves.
                    if (!job.OnSurface && job.Surface.Overlays.Any(o => o.IsParentMoving))
                    {
                        if (!job.Settings.LimitedTank)
                        {
                            float best = 9f;
                            Vector3 altOrigin = Vector3.zero;
                            bool found = false;
                            for (int t = 0; t < sOwner.Length; t++)
                            {
                                int o = sOwner[t];
                                if (job.Settings.Obstacle != null && o >= 0 && o < job.Settings.Obstacle.Length && job.Settings.Obstacle[o]) continue;
                                Vector3 a = sPos[sTri[3 * t]], b = sPos[sTri[3 * t + 1]], c = sPos[sTri[3 * t + 2]];
                                Vector3 fn = Vector3.Cross(b - a, c - a);
                                if (fn.sqrMagnitude < 1e-16f || Vector3.Dot(fn.normalized, job.N) <= 0.35f) continue;
                                Vector3 m = (a + b + c) / 3;
                                if (m.sqrMagnitude >= best) continue;
                                best = m.sqrMagnitude; altOrigin = m; found = true;
                            }
                            if (found)
                            {
                                if (job.Cancelled) return;
                                r = TileBuilder.Build(index, job.Settings, altOrigin, job.N, job.U, job.V);
                            }
                        }
                        r = r ?? new TileResult { Parts = new PartTiles[0], Colliders = new TileBuilder.Box[0] };
                        if (r.Parts == null) r.Parts = new PartTiles[0];
                        if (r.Colliders == null) r.Colliders = new TileBuilder.Box[0];
                        TileResult main = new TileResult { Parts = r.Parts };
                        for (int i = 0; i < job.Surface.Overlays.Length && i < job.OverlaySettings.Length; i++)
                        {
                            if (job.Cancelled) return;
                            OverlayInput oi = job.Surface.Overlays[i];
                            if (!oi.IsParentMoving && !TileBuilder.CoverReaches(main, OverlayFoot(oi), 2 * job.Settings.TileSize + 0.05f)) continue;
                            AddOverlay(r, TileBuilder.BuildOverlay(oi.Pos, oi.Tri, oi.Owner, oi.Obstacle, oi.Inward, job.OverlaySettings[i]), oi);
                        }
                        overlaysDone = true;
                        if (r.TileCount > 0) { job.OnSurface = true; r.OnSurface = true; }
                    }
                }
                if (job.Surface != null && !overlaysDone)
                {
                    // DESIGN-v11: every attached object its own overlay, in its own space, then into decal space onto the result
                    // 1.6: only objects the main cover reaches (gear beside an area-limited cover stood out black with its doors open)
                    if (job.OnSurface)
                    {
                        TileResult main = new TileResult { Parts = r.Parts };
                        for (int i = 0; i < job.Surface.Overlays.Length && i < job.OverlaySettings.Length; i++)
                        {
                            if (job.Cancelled) return;
                            OverlayInput oi = job.Surface.Overlays[i];
                            if (job.Premade == null && !TileBuilder.CoverReaches(main, OverlayFoot(oi), 2 * job.Settings.TileSize + 0.05f)) continue;
                            AddOverlay(r, TileBuilder.BuildOverlay(oi.Pos, oi.Tri, oi.Owner, oi.Obstacle, oi.Inward, job.OverlaySettings[i]), oi);
                        }
                    }
                }
                if (!job.OnSurface && !job.Flight)
                {
                    // flat stand-in: at most 4 x 4 m (a 20 x 20 m preview cost 2 s and 222 MB)
                    job.Settings.Width = job.Width = Mathf.Min(job.Width, 4f);
                    job.Settings.Height = job.Height = Mathf.Min(job.Height, 4f);
                }
                if (!job.OnSurface)
                {
                    if (job.Cancelled) return;
                    r = job.Flight
                        ? new TileResult { Parts = new PartTiles[0], Colliders = new TileBuilder.Box[0] } // flight: no stand-in
                        : TileBuilder.Build(Flat(job), job.Settings, Vector3.zero, job.N, job.U, job.V);
                }
                job.Result = r;
                job.Ms = watch.ElapsedMilliseconds;
            }
            catch (Exception e)
            {
                job.Error = e;
            }
        }

        // an overlay object's own (non-obstacle) corners in decal space
        private static List<Vector3> OverlayFoot(OverlayInput oi)
        {
            var foot = new List<Vector3>();
            for (int k = 0; k < oi.Owner.Length; k++)
                if (!oi.Obstacle[oi.Owner[k]]) for (int j = 0; j < 3; j++) foot.Add(oi.ToDecal.MultiplyPoint3x4(oi.Pos[oi.Tri[3 * k + j]]));
            return foot;
        }

        // Flat stand-in surface for the preview (no parent, or nothing usable under the decal).
        private static SurfaceIndex Flat(BuildJob job)
        {
            Vector3 hu = job.U * (job.Width / 2 + job.Settings.TileSize), hv = job.V * (job.Height / 2 + job.Settings.TileSize);
            var verts = new[] { -hu - hv, hu - hv, hu + hv, -hu + hv };
            int[] tris = Vector3.Dot(Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]), job.N) >= 0
                ? new[] { 0, 1, 2, 0, 2, 3 }
                : new[] { 0, 2, 1, 0, 3, 2 };
            return new SurfaceIndex(verts, tris, Vector3.zero, hu.magnitude + hv.magnitude + 1, job.Cell, job.Smooth);
        }

        // Editor: asynchronous, at most one running build; a change meanwhile discards that result and restarts once.
        private void RequestBuild()
        {
            if (destroyed || decalRoot == null) return;
            if (running) { dirty = true; if (activeJob != null) activeJob.Cancelled = true; return; }
            running = true;
            dirty = false;
            BuildJob job = MakeJob();
            activeJob = job;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Work(job);
                lock (gate) finished = job;
            });
        }

        // Flight: synchronous.
        private void BuildNow()
        {
            if (destroyed || decalRoot == null || part.parent == null) return;
            BuildJob job = MakeJob();
            Work(job);
            Apply(job);
        }

        private void Receive(BuildJob job)
        {
            running = false;
            if (destroyed) return;
            if (!job.Cancelled) Apply(job); // even if stale: the user sees progress while dragging
            if (dirty) RequestBuild();      // then build the newest state once (including right after a cancel)
        }

        // Main thread: meshes, colliders, readouts.
        private void Apply(BuildJob job)
        {
            if (job.Error != null)
            {
                Debug.LogError($"[AblatorDecals] {PartName}: build failed: {job.Error}");
                return;
            }
            Stopwatch watch = Stopwatch.StartNew();
            TileResult r = job.Result;
            bool flight = HighLogic.LoadedSceneIsFlight;
            bool onSurface = job.Attached && job.OnSurface;
            bool wasBlocked = blocked;
            blocked = job.Blocked;
            if (blocked != wasBlocked && !flight)
            {
                status = blocked ? Localizer.Format(L + "occupied") : "";
                Fields[nameof(status)].guiActiveEditor = blocked;
                MonoUtilities.RefreshPartContextWindow(part);
                if (blocked) ScreenMessages.PostScreenMessage(Localizer.Format(L + "occupied_msg"), 4f, ScreenMessageStyle.UPPER_CENTER);
            }
            // v11: a pre-made cockpit cover ignores tile size, angle, joint and thickness: those sliders go, the status says so
            bool pre = job.Premade != null && !blocked;
            if (pre != premadeShown && !flight)
            {
                premadeShown = pre;
                foreach (string f in new[] { nameof(tileSize), nameof(patternAngle), nameof(tileGap), nameof(thickness) })
                    if (Fields[f] != null) Fields[f].guiActiveEditor = !pre;
                if (!blocked)
                {
                    status = pre ? Localizer.Format(L + "premade") : "";
                    Fields[nameof(status)].guiActiveEditor = pre;
                }
                MonoUtilities.RefreshPartContextWindow(part);
            }
            if (job.Attached && !job.OnSurface && !job.Blocked && part.parent != null)
                Debug.LogWarning($"[AblatorDecals] {PartName}: no usable surface of {part.parent.partInfo.name} under the decal{(flight ? ", no tiles" : ", showing flat preview")}");
            if (r.HitTileLimit)
                Debug.LogWarning($"[AblatorDecals] {PartName}: tile limit {maxTiles} reached, increase tile size");

            var kept = new List<Part>();
            bool hostsChanged = false;
            List<Part> holders = flight ? new List<Part>() : HostHolders();
            var touched = new HashSet<Part>(targets); // parts that had tiles: younger decals there may get room
            List<TileHost> oldHosts = flight && binExposure.Count > 0 ? new List<TileHost>(hosts) : null;
            if (flight)
            {
                // hosts hang on the target parts and are rebuilt each time
                hostsChanged = hosts.Count > 0;
                foreach (TileHost h in hosts) if (h.TargetPart != null) h.TargetPart.RefreshHighlighter();
                ClearHosts();
            }
            if (!flight && NonDecalHeld() != othersHeld) SetColliders(othersHeld); // M2: fresh hosts below follow the held state
            Matrix4x4 partToWorld = part.partTransform.localToWorldMatrix;
            int used = 0;
            foreach (PartTiles pt in r.Parts)
            {
                Body body = onSurface && pt.Owner >= 0 && pt.Owner < job.Bodies.Length ? job.Bodies[pt.Owner] : null;
                Part target = body?.Part;
                Transform parent = decalRoot.transform;
                int layer = decalRoot.layer;
                Matrix4x4 decalToHost = parent.worldToLocalMatrix * partToWorld;
                // 1.5 (M4): flaps and cargo bay doors/ramp hang on their own transform in the editor too (before only gear did), so
                // opening the doors or the Mk3 ramp in the editor takes their tiles along instead of leaving them in the air
                if (flight || body != null && (body.Gear || body.Flap))
                {
                    // on the body's transform: the target part, or the flap/gear piece, which then carries its tiles along
                    if (target == null || target.partTransform == null || body.T == null) continue;
                    parent = body.T;
                    Transform m = target.partTransform.Find("model");
                    layer = body.Flap ? body.T.gameObject.layer : (m != null ? m.gameObject : target.gameObject).layer;
                    decalToHost = body.ToDecal.inverse; // tiles were laid on the flap in neutral position
                }
                TileHost host;
                if (!flight && used < hosts.Count && hosts[used].Alive)
                {
                    host = hosts[used]; // editor: pooled, KSP caches the renderers
                    if (host.Go.transform.parent != parent)
                    {
                        host.Go.transform.SetParent(parent, false);
                        host.Go.layer = layer;
                        hostsChanged = true;
                    }
                }
                else
                {
                    host = new TileHost(this, target, parent, layer, "AblativeDecalTiles");
                    if (!flight && used < hosts.Count) { hosts[used].Destroy(); hosts[used] = host; }
                    else hosts.Add(host);
                    hostsChanged = true;
                }
                used++;
                host.Apply(pt, decalToHost, tileTemplate, jointTemplate, backTemplate);
                // M3: exact clicks; none on gear (wheel casts) and none on flaps/doors (a collider under another part's model would
                // pick that part on click and surface attach, not the decal)
                if (!flight) host.SetMeshCollider(body == null || !(body.Gear || body.Flap), !othersHeld);
                if (target != null && !kept.Contains(target)) kept.Add(target); // a flap and the rest of its part: one thermal target
            }
            if (!flight)
                while (hosts.Count > used)
                {
                    hosts[hosts.Count - 1].Destroy();
                    hosts.RemoveAt(hosts.Count - 1);
                    hostsChanged = true;
                }
            if (!flight && hostsChanged)
            {
                holders.Add(part);
                holders.AddRange(HostHolders());
                ResetHolders(holders);
            }
            if (flight) foreach (Part t in kept) t.RefreshHighlighter(); // its Highlighter may list our new renderer
            targets = kept.ToArray();
            frontal = new double[targets.Length];
            partsCovered = targets.Length;
            lastResult = r;
            if (flight) onTiles = PartsOnTiles();

            // tell younger decals on the same parts to fit around the new tiles (they ignore none, we ignore them: no loop)
            touched.UnionWith(kept);
            NotifyYounger(touched);
            PlaceAnchor();

            if (!flight)
            {
                bool showCover = job.Attached && part.parent != null && CoverPlanes(part.parent) != null;
                bool showSmooth = !(showCover && cockpitShape);
                if (Fields[nameof(cockpitShape)].guiActiveEditor != showCover || Fields[nameof(smoothHull)].guiActiveEditor != showSmooth)
                {
                    Fields[nameof(cockpitShape)].guiActiveEditor = showCover;
                    Fields[nameof(smoothHull)].guiActiveEditor = showSmooth;
                    MonoUtilities.RefreshPartContextWindow(part);
                }
            }

            // Detached (editor drag): show what the decal would hold. Attached but nothing below: protects nothing.
            coveredArea = onSurface || !job.Attached ? r.CoveredArea : 0;
            tileCount = r.TileCount;
            if (oldHosts != null) CarryExposure(oldHosts);
            // 1.8: base weathering shows in the editor too
            ApplyChar(flight && ablator != null && ablator.maxAmount > 0 ? 1 - (float)(ablator.amount / ablator.maxAmount) : 0f);

            lastJob = job;
            RebuildColliders(r, job, flight && onSurface);
            tileMass = $"{coveredArea * massPerArea * 1000:F1} kg";
            if (HighLogic.LoadedSceneIsEditor) UpdateAblatorAmount();
            nextRebuild = Time.realtimeSinceStartup + 0.1f;

            watch.Stop();
            if (flight || Time.realtimeSinceStartup >= nextEditorLog)
            {
                nextEditorLog = Time.realtimeSinceStartup + 1f;
                int bins = hosts.Sum(h => h.BinCount);
                Debug.Log($"[AblatorDecals] build {HighLogic.LoadedScene} {PartName} parts=[{string.Join(", ", targets.Select(p => p.partInfo.name))}] " +
                          $"onSurface={onSurface} tiles={r.TileCount} partial={r.PartialCount} bins={bins} colliders={r.Colliders.Length} covered={coveredArea:F2}m2 " +
                          $"time={job.Ms + watch.ElapsedMilliseconds}ms (core {job.Ms}ms) texture={(currentTexture != null ? currentTexture.name : "NONE")} " +
                          $"shader={tileTemplate.shader.name} glow={GlowName(tileTemplate)} emissive={(currentGlow != null ? currentGlow.name : "none")}");
            }
        }

        // Thin boxes under the decal (editor: hit by surface attach and clicks, so layer and parent follow the part, no trigger).
        // Flight: each flap gets one box under its transform, so it turns with the flap; builder boxes centred on a flap go.
        private void RebuildColliders(TileResult r, BuildJob job, bool flaps)
        {
            ClearColliders();
            if (!HighLogic.LoadedSceneIsFlight) return; // editor: exact mesh colliders on the tile hosts instead (M3)
            List<Bounds> gear = GearVolumes();
            var flapBoxes = new List<KeyValuePair<Body, TileBuilder.Box>>();
            if (flaps)
                foreach (PartTiles pt in r.Parts)
                    if (pt.Owner >= 0 && pt.Owner < job.Bodies.Length && job.Bodies[pt.Owner].Flap && !job.Bodies[pt.Owner].Gear && job.Bodies[pt.Owner].T != null)
                        flapBoxes.Add(new KeyValuePair<Body, TileBuilder.Box>(job.Bodies[pt.Owner], FitBox(pt)));
            foreach (TileBuilder.Box box in r.Colliders)
                if (!flapBoxes.Any(f => Contains(f.Value, box.Center)) && !OnGear(box, gear))
                    AddCollider(box, decalRoot.transform, Matrix4x4.identity, decalRoot.layer);
            foreach (KeyValuePair<Body, TileBuilder.Box> f in flapBoxes)
                if (!OnGear(f.Value, gear))
                    AddCollider(f.Value, f.Key.T, f.Key.ToDecal.inverse, f.Key.T.gameObject.layer);
            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.OnCollisionIgnoreUpdate.Fire(); // also re-applies ours once KSP's CollisionManager has run
                IgnoreTileCollisions();
            }
        }

        // Landing gear on the covered parts, as generous world boxes: part renderers and colliders plus the wheel's travel
        // (wheel centre +- (radius + suspension travel) along the suspension axis), inflated by gearMargin to cover the
        // retracted pose too. Wheel suspension is a scene query (ray/sphere cast) that skips only colliders of the gear's own
        // rigidbody; Physics.IgnoreCollision does not reach it, so no tile box may sit there (the tile mesh stays).
        private List<Bounds> GearVolumes()
        {
            var list = new List<Bounds>();
            foreach (Part g in targets.Where(t => t != null).SelectMany(t => t.children))
            {
                ModuleWheelBase wheel = g != null ? g.FindModuleImplementing<ModuleWheelBase>() : null;
                if (wheel == null) continue;
                var b = new Bounds(g.partTransform.position, Vector3.zero);
                foreach (Renderer rr in g.GetComponentsInChildren<Renderer>())
                    if (rr.enabled) b.Encapsulate(rr.bounds);       // disabled/inactive ones report a box at the origin
                foreach (Collider c in g.GetComponentsInChildren<Collider>())
                    if (c.enabled) b.Encapsulate(c.bounds);
                Transform host = wheel.wheelColliderHost;
                if (host != null)
                {
                    var susp = g.FindModuleImplementing<ModuleWheels.ModuleWheelSuspension>();
                    Vector3 centre = host.TransformPoint(wheel.center);
                    Vector3 axis = host.up * (wheel.radius + (susp != null ? susp.suspensionDistance : 0)) * host.lossyScale.y;
                    b.Encapsulate(centre + axis);
                    b.Encapsulate(centre - axis);
                }
                b.Expand(2 * gearMargin);
                list.Add(b);
            }
            return list;
        }

        // world AABB of a tile box (decal space, neutral pose) touches a gear volume
        private bool OnGear(TileBuilder.Box box, List<Bounds> gear)
        {
            if (gear.Count == 0) return false;
            Matrix4x4 m = part.partTransform.localToWorldMatrix;
            Vector3 right = Vector3.Cross(box.Up, box.Forward);
            var w = new Bounds(m.MultiplyPoint3x4(box.Center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                w.Encapsulate(m.MultiplyPoint3x4(box.Center + 0.5f * (right * box.Size.x * ((i & 1) == 0 ? -1 : 1) +
                    box.Up * box.Size.y * ((i & 2) == 0 ? -1 : 1) + box.Forward * box.Size.z * ((i & 4) == 0 ? -1 : 1))));
            return gear.Any(g => g.Intersects(w));
        }

        // editor: off while another (non-decal) part hangs on the mouse, so it attaches to the hull under the tiles
        private void SetColliders(bool on)
        {
            othersHeld = !on;
            foreach (GameObject go in colliderObjects)
                if (go != null) go.GetComponent<Collider>().enabled = on;
            foreach (TileHost h in hosts) if (h.Alive) h.EnableCollider(on);
            if (anchor != null) anchor.GetComponent<Collider>().enabled = on;
        }

        private static int heldCheckFrame = -1;
        private static bool heldNonDecalCached;

        // editor: a part without a decal module hangs on the mouse (a held decal must still hit other decals' tiles: blocked, red)
        private static bool NonDecalHeld()
        {
            int frame = Time.frameCount;
            if (frame != heldCheckFrame)
            {
                heldCheckFrame = frame;
                Part sel = EditorLogic.SelectedPart;
                heldNonDecalCached = sel != null && sel.FindModuleImplementing<ModuleAblativeDecal>() == null;
            }
            return heldNonDecalCached;
        }

        // a part hangs on the mouse that is neither this decal nor carries it
        private bool OtherPartHeld()
        {
            Part sel = EditorLogic.SelectedPart;
            if (sel == null) return false;
            for (Part q = part; q != null; q = q.parent)
                if (q == sel) return false;
            return true;
        }

        // editor: which decal is where (M5) and "area taken" (M2): anchor ring on hover or open PAW; open PAW keeps the part
        // highlighted, a blocked decal glows red
        private void UpdateFocus()
        {
            bool paw = UIPartActionController.Instance != null && UIPartActionController.Instance.ItemListContains(part, false);
            if (anchorRenderer != null) anchorRenderer.enabled = paw || HighlightAll || Mouse.HoveredPart == part;
            int want = blocked && part.parent != null && !detached ? 2 : paw ? 1 : 0;
            if (want == shownHighlight) return;
            shownHighlight = want;
            if (want == 0)
            {
                part.SetHighlightDefault();
                return;
            }
            part.SetHighlightType(Part.HighlightType.AlwaysOn);
            part.SetHighlightColor(want == 2 ? Color.red : Part.defaultHighlightPart);
            part.SetHighlight(true, false);
        }

        // flat hex ring (radius 1, normal +Z) for the anchor
        private static Mesh RingMesh()
        {
            var v = new Vector3[12];
            var t = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI / 3 * i;
                v[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                v[i + 6] = v[i] * 0.65f;
                int j = (i + 1) % 6;
                t.AddRange(new[] { i, i + 6, j, j, i + 6, j + 6 });   // one face
                t.AddRange(new[] { i, j, i + 6, j, j + 6, i + 6 });   // and the other, so it shows from any side
            }
            var m = new Mesh { name = "AblativeDecalAnchor", vertices = v, triangles = t.ToArray() };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // box in decal space -> collider under parent (decalToParent: decal space -> parent space)
        private void AddCollider(TileBuilder.Box box, Transform parent, Matrix4x4 decalToParent, int layer)
        {
            var go = new GameObject("AblativeDecalCollider") { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = decalToParent.MultiplyPoint3x4(box.Center);
            go.transform.localRotation = Quaternion.LookRotation(decalToParent.MultiplyVector(box.Forward), decalToParent.MultiplyVector(box.Up));
            if (parent != decalRoot.transform)
            {
                // a scaled model (rescaleFactor) would scale the box; keep it at decal size
                Vector3 a = part.partTransform.lossyScale, b = parent.lossyScale;
                go.transform.localScale = new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
            }
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = Vector3.Max(box.Size, Vector3.one * 0.01f);
            col.enabled = !othersHeld;
            colliderObjects.Add(go);
        }

        // One box around the tiles of a flap, aligned with their mean normal (decal space).
        private static TileBuilder.Box FitBox(PartTiles pt)
        {
            Vector3 n = Vector3.zero;
            foreach (Vector3 a in pt.TileAreaVectors) n += a;
            n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            Vector3 up = Vector3.Cross(n, Mathf.Abs(n.x) < 0.9f ? Vector3.right : Vector3.forward).normalized;
            Vector3 right = Vector3.Cross(up, n); // LookRotation(n, up) axes
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            foreach (Vector3 p in pt.Vertices)
            {
                var l = new Vector3(Vector3.Dot(p, right), Vector3.Dot(p, up), Vector3.Dot(p, n));
                lo = Vector3.Min(lo, l);
                hi = Vector3.Max(hi, l);
            }
            if (pt.Vertices.Length == 0) lo = hi = Vector3.zero;
            Vector3 mid = (lo + hi) * 0.5f;
            return new TileBuilder.Box { Center = right * mid.x + up * mid.y + n * mid.z, Forward = n, Up = up, Size = hi - lo };
        }

        private static bool Contains(TileBuilder.Box b, Vector3 p)
        {
            Vector3 d = p - b.Center, right = Vector3.Cross(b.Up, b.Forward);
            return Mathf.Abs(Vector3.Dot(d, right)) <= b.Size.x / 2 + 0.01f &&
                   Mathf.Abs(Vector3.Dot(d, b.Up)) <= b.Size.y / 2 + 0.01f &&
                   Mathf.Abs(Vector3.Dot(d, b.Forward)) <= b.Size.z / 2 + 0.01f;
        }

        // Flight: parts hanging on the decal (old saves) and parts glued onto the covered hull inside the tiles (gear with its
        // wheel colliders, radial decouplers), with their subtrees, never collide with the tiles, also once separated: KSP's
        // CollisionManager turns collisions between vessels back on, so this runs again after it (OnCollisionIgnoreUpdate).
        // The tiles still touch the ground and everything else.
        private void IgnoreTileCollisions()
        {
            Collider[] own = colliderObjects.Where(g => g != null).Select(g => g.GetComponent<Collider>()).Where(c => c != null).ToArray();
            noCollide.RemoveWhere(p => p == null);
            noCollide.UnionWith(part.children);
            noCollide.UnionWith(onTiles.Where(p => p != null));
            noCollide.UnionWith(targets.Where(t => t != null && t.FindModuleImplementing<ModuleWheelBase>() != null)); // tiled gear (A10)
            if (own.Length == 0) return;
            var stack = new Stack<Part>(noCollide);
            var done = new HashSet<Part>();
            while (stack.Count > 0)
            {
                Part c = stack.Pop();
                if (c == null || !done.Add(c)) continue;
                foreach (Part cc in c.children) stack.Push(cc);
                foreach (Collider other in c.GetComponentsInChildren<Collider>(true))
                    if (other.GetComponentInParent<ModuleAblativeDecal>() != this) // ponytail: skip own boxes if nested in flight
                        foreach (Collider o in own)
                            Physics.IgnoreCollision(o, other);
            }
        }

        private void OnCollisionIgnoreUpdate() => ignoreFrames = 2; // CollisionManager updates in its next FixedUpdate

        // ---------- target parts and hull triangles ----------

        // M1 (1.4): the parent plus the control surfaces (ModuleControlSurface, ModuleAeroSurface derives from it) and air
        // intakes surface attached to it; those are tiled with it (flaps as bodies of their own, intakes with the opening left
        // free). No stacked parts or wings any more ("Across parts" is gone). Parts on the tiles (radial decouplers, gear, ...)
        // are no targets, so attaching them does not change the parent's tiles.
        private Part[] TargetParts()
        {
            var list = new List<Part> { part.parent };
            // 1.6: with a cockpit cover too: its planes/regions/pre-made tiles are for the cockpit alone, glued parts are overlays
            list.AddRange(part.parent.children.Where(c => c != null && c != part && c != dyingPart && (Glued(c) || ShieldedPort(c))));
            return list.ToArray();
        }

        // 1.7: a shielded docking port stacked on the covered part (Clamp-O-Tron Shielded on the Dynawing's nose) is an object like a
        // cargo bay: its shield doors are tiled closed and the tiles ride with them, the docking face stays free; its base gets the
        // closed skin. Not with a decal of its own.
        // 1.7.1: a cap only (one stack node); an inline port (mk2DockingPort) is a hull part with a decal of its own
        private static bool ShieldedPort(Part c) => c.attachMode == AttachModes.STACK && c.attachNodes.Count(nd => nd.nodeType == AttachNode.NodeType.Stack) == 1 &&
            c.FindModuleImplementing<ModuleDockingNode>() is ModuleDockingNode d &&
            d.deployAnimationController >= 0 && !c.children.Any(k => k != null && k.FindModuleImplementing<ModuleAblativeDecal>() != null);

        // DESIGN-v11: control surfaces (also fins such as tailfin, R8winglet; ModuleAeroSurface derives from it) and intakes, each an
        // overlay object; plain wings and strakes (ModuleLiftingSurface only) need a decal of their own. A part with a decal of its
        // own is that decal's (not tiled twice, no "area taken" between the two)
        private static bool Glued(Part c) => c.attachMode == AttachModes.SRF_ATTACH &&
            (c.FindModuleImplementing<ModuleControlSurface>() != null || c.FindModuleImplementing<ModuleResourceIntake>() != null) &&
            !c.children.Any(k => k != null && k.FindModuleImplementing<ModuleAblativeDecal>() != null);

        // Render meshes of the covered parts in this part's local space; triangle owner = index into the body list.
        // Meshes under a control surface flap count as their own body, taken in neutral position. Cargo bay doors (DESIGN-v10 G5:
        // the transforms the ModuleCargoBay's deploy animation moves) are bodies of their own too, laid out closed.
        // DESIGN-v11 overlay: every glued part (with its flaps), the parent's own flaps and bay doors, and each landing gear is an
        // object of its own (Body.Group, SurfaceInput.Overlays): an obstacle for the main cover, tiled by its own overlay in its own
        // space. With "limit area" only objects whose attach point lies in the footprint get an overlay (the rest: Group -2).
        private SurfaceInput Collect(Part[] parts, Matrix4x4[] poses, Part[] gear, Vector3 u, Vector3 v)
        {
            Matrix4x4 toPart = part.partTransform.worldToLocalMatrix;
            SurfaceInput c = surfaceCache;
            bool[] inArea = parts.Select(p => InFootprint(p, u, v)).ToArray();
            int meshSig = ComputeMeshSig(parts);
            lastMeshSig = meshSig;
            if (c != null && c.Parts.SequenceEqual(parts) && c.Gear.SequenceEqual(gear) && SamePoses(c.Poses, poses) && c.InArea.SequenceEqual(inArea) && c.MeshSig == meshSig) return c;

            var pos = new List<Vector3>();
            var wpos = new List<Vector3>();                   // the same points in world space (overlays)
            var tri = new List<int>();
            var owner = new List<int>();
            var bodies = new List<Body>();
            var frames = new List<(Transform frame, string name)>(); // overlay objects
            int parentMoving = -1;
            var doorSkinGroups = new HashSet<int>(); // 1.9: overlay groups holding radial service bay doors
            bool frameSkin = false;
            for (int pi = 0; pi < parts.Length; pi++)
            {
                int main = bodies.Count;
                int group = pi == 0 ? -1 : inArea[pi] ? frames.Count : -2;
                if (group >= 0) frames.Add((parts[pi].partTransform, parts[pi].partInfo?.name));
                bodies.Add(new Body { Part = parts[pi], T = parts[pi].partTransform, ToDecal = poses[pi], ToWorld = parts[pi].partTransform.localToWorldMatrix, Group = group, Flap = group >= 0 });
                List<Body> flaps = Flaps(parts[pi], toPart);
                int firstFlap = bodies.Count;
                bodies.AddRange(flaps);
                if (pi == 0 && flaps.Count > 0) { parentMoving = frames.Count; frames.Add((parts[0].partTransform, parts[0].partInfo?.name + " flaps")); }
                foreach (Body fb in flaps) fb.Group = pi == 0 ? parentMoving : group;
                // air intake: tiled outside, opening left free. KSP takes the intake transform's forward as the direction of
                // travel (ModuleResourceIntake: Dot(srf_vel_direction, intakeTransform.forward)), so the air comes in along -forward.
                bool intake = false;
                int intakeDropped = 0, triBefore = tri.Count;
                Vector3 inPos = Vector3.zero, inFwd = Vector3.zero;
                float inFront = 0;
                ModuleResourceIntake mri = parts[pi].FindModuleImplementing<ModuleResourceIntake>();
                if (mri != null)
                {
                    Transform it = parts[pi].FindModelTransform(mri.intakeTransformName);
                    if (it == null)
                    {
                        Debug.Log($"[AblatorDecals] intake {parts[pi].partInfo?.name}: transform={mri.intakeTransformName} not found, left bare");
                        continue; // no idea where the opening is
                    }
                    intake = true;
                    inPos = it.position;            // world space: which faces count as opening does not depend on the decal
                    inFwd = it.forward.normalized;
                    // the intake's front (its lip), so only faces recessed behind it count as the mouth (v11)
                    inFront = float.MinValue;
                    foreach (MeshFilter mf in parts[pi].FindModelComponents<MeshFilter>())
                    {
                        Renderer rr = mf.GetComponent<Renderer>();
                        if (mf.sharedMesh == null || rr == null || !rr.enabled || !mf.sharedMesh.isReadable || mf.GetComponent<AblativeDecalTilesMarker>() != null) continue;
                        Matrix4x4 lw = mf.transform.localToWorldMatrix;
                        foreach (Vector3 vert in mf.sharedMesh.vertices) inFront = Mathf.Max(inFront, Vector3.Dot(lw.MultiplyPoint3x4(vert) - inPos, inFwd));
                    }
                }
                // cargo bay doors: sample the deploy animation at both ends, the topmost transforms that move (with meshes under
                // them) are the doors; build in the closed pose, then put every transform and the state back exactly (as AddGear)
                var doors = new List<Body>();
                ModuleCargoBay bay = parts[pi].FindModuleImplementing<ModuleCargoBay>();
                ModuleDockingNode dock = bay == null && pi > 0 && ShieldedPort(parts[pi]) ? parts[pi].FindModuleImplementing<ModuleDockingNode>() : null; // 1.7: shield doors
                int doorModule = bay != null ? bay.DeployModuleIndex : dock != null ? dock.deployAnimationController : -1;
                float closedAt = bay != null ? bay.closedPosition : dock != null ? 1 - dock.deployAnimationTarget : 0;
                ModuleAnimateGeneric doorAnim = doorModule >= 0 && doorModule < parts[pi].Modules.Count ? parts[pi].Modules[doorModule] as ModuleAnimateGeneric : null;
                Animation anim = doorAnim != null && !string.IsNullOrEmpty(doorAnim.animationName) ? parts[pi].FindModelAnimators(doorAnim.animationName).FirstOrDefault() : null;
                AnimationState st = anim != null ? anim[doorAnim.animationName] : null;
                Transform[] all = null;
                Vector3[] lp = null, ls = null;
                Quaternion[] lr = null;
                bool stEnabled = false;
                float stTime = 0, stWeight = 0;
                if (st != null)
                {
                    all = parts[pi].partTransform.GetComponentsInChildren<Transform>(true);
                    lp = all.Select(x => x.localPosition).ToArray();
                    lr = all.Select(x => x.localRotation).ToArray();
                    ls = all.Select(x => x.localScale).ToArray();
                    stEnabled = st.enabled;
                    stTime = st.normalizedTime;
                    stWeight = st.weight;
                    st.enabled = true;
                    st.weight = 1;
                    st.normalizedTime = 1 - closedAt;
                    anim.Sample();
                    Vector3[] openP = all.Select(x => x.localPosition).ToArray();
                    Quaternion[] openR = all.Select(x => x.localRotation).ToArray();
                    st.normalizedTime = closedAt;
                    anim.Sample();
                    Vector3[] closedP = all.Select(x => x.localPosition).ToArray();
                    Quaternion[] closedR = all.Select(x => x.localRotation).ToArray();
                    Part bayPart = parts[pi];
                    // doors in the current pose against the other one: the topmost moving transforms with meshes under them, and
                    // (1.8, articulated accordion doors of service-tank-375: DoorA -> DoorB -> ...) moving children with their own mesh
                    void RecordDoors(Vector3[] otherP, Quaternion[] otherR)
                    {
                        doors.Clear();
                        for (int i = 0; i < all.Length; i++) // parents come before their children
                            if (((all[i].localPosition - otherP[i]).sqrMagnitude > 1e-6f || Quaternion.Angle(all[i].localRotation, otherR[i]) > 0.5f) &&
                                (!doors.Any(d => all[i].IsChildOf(d.T)) || all[i].GetComponent<MeshFilter>() != null) &&
                                all[i].GetComponentsInChildren<MeshFilter>(true).Length > 0)
                                doors.Add(new Body { Part = bayPart, T = all[i], Flap = true, ToDecal = toPart * all[i].localToWorldMatrix, ToWorld = all[i].localToWorldMatrix, Inward = true });
                    }
                    // mean squared distance from the part axis of the door meshes (part space x^2 + z^2) in the current pose
                    float DoorR2()
                    {
                        Matrix4x4 w2p = bayPart.partTransform.worldToLocalMatrix;
                        double sum = 0;
                        int n = 0;
                        foreach (MeshFilter dm in doors.SelectMany(d => d.T.GetComponentsInChildren<MeshFilter>(true)).Distinct())
                        {
                            if (dm.sharedMesh == null || !dm.sharedMesh.isReadable) continue;
                            Matrix4x4 lw = w2p * dm.transform.localToWorldMatrix;
                            foreach (Vector3 vert in dm.sharedMesh.vertices) { Vector3 q = lw.MultiplyPoint3x4(vert); sum += q.x * q.x + q.z * q.z; n++; }
                        }
                        return n > 0 ? (float)(sum / n) : 0f;
                    }
                    RecordDoors(openP, openR);
                    if (doors.Count > 0)
                    {
                        // 1.8: some bays (service-tank-375) report the open end as closed: doors farther out there -> swap
                        float r2Closed = DoorR2();
                        st.normalizedTime = 1 - closedAt;
                        anim.Sample();
                        float r2Open = DoorR2();
                        if (r2Closed > r2Open * 1.05f)
                        {
                            closedAt = 1 - closedAt; // the pose sampled now
                            RecordDoors(closedP, closedR);
                        }
                        else
                        {
                            st.normalizedTime = closedAt;
                            anim.Sample();
                        }
                    }
                    Debug.Log($"[AblatorDecals] {(dock != null ? "shielded port" : "bay")} {parts[pi].partInfo?.name}: anim={doorAnim.animationName} closed={closedAt} doors={doors.Count}");
                }
                int firstDoor = bodies.Count;
                bodies.AddRange(doors);
                if (pi == 0 && doors.Count > 0 && parentMoving < 0) { parentMoving = frames.Count; frames.Add((parts[0].partTransform, parts[0].partInfo?.name + " doors")); }
                if (dock != null && group >= 0 && doors.Count > 0) { group = frames.Count; frames.Add((parts[pi].partTransform, parts[pi].partInfo?.name + " shield")); } // doors: an object of their own (real tiles)
                foreach (Body db in doors) db.Group = pi == 0 ? parentMoving : group;
                // 1.9: radial service bay (ServiceBay_125_v2, service-tank-*, ServiceModule*, nflv-service-bay-*; not Mk2/Mk3 bays):
                // only the doors' outer shell, no hinge cylinders, ribs or inner thickness faces
                string bayName = parts[pi].partInfo != null ? parts[pi].partInfo.name : "";
                bool radialBay = bay != null && doors.Count >= 2 && !bayName.StartsWith("mk2", StringComparison.OrdinalIgnoreCase) && !bayName.StartsWith("mk3", StringComparison.OrdinalIgnoreCase);
                Matrix4x4 w2bay = parts[pi].partTransform.worldToLocalMatrix;
                float maxDoorR = 0;
                if (radialBay)
                {
                    foreach (MeshFilter dm in doors.SelectMany(d => d.T.GetComponentsInChildren<MeshFilter>(true)).Distinct()) // closed pose
                    {
                        if (dm.sharedMesh == null || !dm.sharedMesh.isReadable) continue;
                        Matrix4x4 lw = w2bay * dm.transform.localToWorldMatrix;
                        foreach (Vector3 vert in dm.sharedMesh.vertices) { Vector3 q = lw.MultiplyPoint3x4(vert); maxDoorR = Mathf.Max(maxDoorR, Mathf.Sqrt(q.x * q.x + q.z * q.z)); }
                    }
                    if (doors.Count > 0 && doors[0].Group >= 0) doorSkinGroups.Add(doors[0].Group);
                    if (pi == 0) frameSkin = true;
                }
                int doorDropped = 0;
                try
                {
                foreach (MeshFilter mf in parts[pi].FindModelComponents<MeshFilter>())
                {
                    Renderer r = mf.GetComponent<Renderer>();
                    Mesh src = mf.sharedMesh;
                    if (src == null || r == null || !r.enabled || !mf.gameObject.activeInHierarchy) continue;
                    if (mf.GetComponent<AblativeDecalTilesMarker>() != null) continue; // tiles of a decal (flap/gear hosts hang under the model)
                    Material sm = r.sharedMaterial;
                    string shaderName = sm != null && sm.shader != null ? sm.shader.name : "";
                    if (SkipShaders.Any(shaderName.Contains)) continue;
                    if (!src.isReadable)
                    {
                        Debug.LogWarning($"[AblatorDecals] mesh '{src.name}' on {parts[pi].partInfo.name} is not readable, skipped");
                        continue;
                    }

                    int bi = main;
                    Matrix4x4 mw = mf.transform.localToWorldMatrix;
                    for (int k = 0; k < flaps.Count; k++)
                        if (mf.transform.IsChildOf(flaps[k].T))
                        {
                            bi = firstFlap + k;
                            mw = flaps[k].ToWorld * flaps[k].T.worldToLocalMatrix * mf.transform.localToWorldMatrix; // neutral
                            break;
                        }
                    for (int k = doors.Count - 1; k >= 0 && bi == main; k--) // deepest door first (articulated segments)
                        if (mf.transform.IsChildOf(doors[k].T)) bi = firstDoor + k; // closed pose: as it is
                    Matrix4x4 m = toPart * mw;
                    bool mirrored = m.determinant < 0;
                    int start = pos.Count;
                    foreach (Vector3 vert in src.vertices)
                    {
                        pos.Add(m.MultiplyPoint3x4(vert));
                        wpos.Add(mw.MultiplyPoint3x4(vert));
                    }
                    int[] t = src.triangles;
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        int i0 = start + t[i], i1 = start + (mirrored ? t[i + 2] : t[i + 1]), i2 = start + (mirrored ? t[i + 1] : t[i + 2]);
                        if (intake && TileBuilder.IntakeOpening(wpos[i0], wpos[i1], wpos[i2], inPos, inFwd, inFront)) { intakeDropped++; continue; }
                        if (radialBay && (bi >= firstDoor || bi == main) && !TileBuilder.DoorOuterFace(w2bay.MultiplyPoint3x4(wpos[i0]), w2bay.MultiplyPoint3x4(wpos[i1]), w2bay.MultiplyPoint3x4(wpos[i2]), maxDoorR)) { doorDropped++; continue; }
                        tri.Add(i0);
                        tri.Add(i1);
                        tri.Add(i2);
                        owner.Add(bi);
                    }
                }
                }
                finally
                {
                    if (st != null)
                    {
                        for (int i = 0; i < all.Length; i++)
                            if (all[i] != null) { all[i].localPosition = lp[i]; all[i].localRotation = lr[i]; all[i].localScale = ls[i]; }
                        st.normalizedTime = stTime;
                        st.weight = stWeight;
                        st.enabled = stEnabled;
                    }
                }
                if (intake)
                    Debug.Log($"[AblatorDecals] intake {parts[pi].partInfo?.name}: transform={mri.intakeTransformName} faces kept={(tri.Count - triBefore) / 3} dropped={intakeDropped}");
                if (radialBay)
                    Debug.Log($"[AblatorDecals] radial bay {bayName}: door radius={maxDoorR:F3} inner door faces dropped={doorDropped}");
            }
            if (gear.Length > 0 && tri.Count > 0)
            {
                // hull of the covered parts only (built before any gear triangle goes in), main thread, thrown away after
                // world space (v11: which gear faces are kept does not depend on where the decal sits)
                var hull = new SurfaceIndex(wpos.ToArray(), tri.ToArray(), part.partTransform.position, Reach, 0.25f, SmoothRadius); // coarse cells: 0.5 m queries
                foreach (Part g in gear)
                {
                    int first = bodies.Count;
                    AddGear(g, toPart, hull, pos, wpos, tri, owner, bodies);
                    if (bodies.Count == first) continue;
                    for (int b = first; b < bodies.Count; b++) bodies[b].Group = frames.Count;
                    frames.Add((g.partTransform, g.partInfo?.name));
                }
            }
            var overlays = new List<OverlayInput>();
            for (int g = 0; g < frames.Count && tri.Count > 0; g++)
            {
                OverlayInput oi = Overlay(frames[g].frame, g, wpos, tri, owner, bodies, toPart);
                if (oi == null) continue;
                oi.Name = frames[g].name;
                oi.IsParentMoving = g == parentMoving;
                oi.DoorSkin = doorSkinGroups.Contains(g);
                oi.Skin = oi.Body.Any(b => bodies[b].Gear || bodies[b].Part != parts[0] && bodies[b].Part.FindModuleImplementing<ModuleResourceIntake>() != null) ||
                          oi.Body.All(b => bodies[b].Part != parts[0] && !bodies[b].Inward && ShieldedPort(bodies[b].Part)) || // 1.7: the port's base
                          oi.DoorSkin;
                overlays.Add(oi);
            }
            surfaceCache = tri.Count == 0 ? null : new SurfaceInput
            {
                Pos = pos.ToArray(), Tri = tri.ToArray(), Owner = owner.ToArray(), Bodies = bodies.ToArray(), Parts = parts, Poses = poses, Gear = gear,
                Overlays = overlays.ToArray(), InArea = inArea, MeshSig = meshSig, FrameSkin = frameSkin
            };
            return surfaceCache;
        }

        // 1.8: TweakScale / B9PartSwitch swap or rescale the parent's meshes without a pose change: hash of every mesh, its
        // visibility and its scale (0.1 mm) on the target parts, our own tiles left out
        private static int ComputeMeshSig(Part[] parts)
        {
            unchecked
            {
                int h = 17;
                foreach (Part p in parts)
                {
                    if (p == null) continue;
                    foreach (MeshFilter mf in p.FindModelComponents<MeshFilter>())
                    {
                        if (mf == null || mf.GetComponent<AblativeDecalTilesMarker>() != null) continue;
                        Renderer r = mf.GetComponent<Renderer>();
                        Vector3 s = mf.transform.lossyScale;
                        h = h * 31 + (mf.sharedMesh != null ? mf.sharedMesh.GetInstanceID() : 0);
                        h = h * 31 + (mf.gameObject.activeInHierarchy ? 1 : 0);
                        h = h * 31 + (r != null && r.enabled ? 1 : 0);
                        h = h * 31 + Mathf.RoundToInt(s.x * 10000);
                        h = h * 31 + Mathf.RoundToInt(s.y * 10000);
                        h = h * 31 + Mathf.RoundToInt(s.z * 10000);
                    }
                }
                return h;
            }
        }

        // One overlay object (DESIGN-v11) in the space of frame: the triangles of its bodies (Group == group) and, as obstacles, those
        // of the main cover's bodies (Group -1) within 1 m of it; from world positions only.
        private static OverlayInput Overlay(Transform frame, int group, List<Vector3> wpos, List<int> tri, List<int> owner, List<Body> bodies, Matrix4x4 toPart)
        {
            Matrix4x4 w2o = frame.worldToLocalMatrix;
            var local = new Dictionary<int, int>();
            var ids = new List<int>();
            var p = new List<Vector3>();
            var t = new List<int>();
            var o = new List<int>();
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int k = 0; k < owner.Count; k++)
            {
                if (bodies[owner[k]].Group != group) continue;
                if (!local.TryGetValue(owner[k], out int id)) { local[owner[k]] = id = ids.Count; ids.Add(owner[k]); }
                for (int j = 0; j < 3; j++)
                {
                    Vector3 q = w2o.MultiplyPoint3x4(wpos[tri[3 * k + j]]);
                    lo = Vector3.Min(lo, q);
                    hi = Vector3.Max(hi, q);
                    t.Add(p.Count);
                    p.Add(q);
                }
                o.Add(id);
            }
            if (ids.Count == 0) return null;
            lo -= Vector3.one;
            hi += Vector3.one;
            Part self = bodies[ids[0]].Part;
            bool port = ShieldedPort(self);
            for (int k = 0; k < owner.Count; k++)
            {
                // 1.7.1: a shielded port's base and its shield doors see each other as obstacles: nothing under the closed hood
                if (bodies[owner[k]].Group != -1 && !(port && bodies[owner[k]].Group != group && bodies[owner[k]].Part == self)) continue;
                Vector3 a = w2o.MultiplyPoint3x4(wpos[tri[3 * k]]), b = w2o.MultiplyPoint3x4(wpos[tri[3 * k + 1]]), c = w2o.MultiplyPoint3x4(wpos[tri[3 * k + 2]]);
                Vector3 tl = Vector3.Min(a, Vector3.Min(b, c)), th = Vector3.Max(a, Vector3.Max(b, c));
                if (tl.x > hi.x || tl.y > hi.y || tl.z > hi.z || th.x < lo.x || th.y < lo.y || th.z < lo.z) continue;
                t.Add(p.Count); p.Add(a);
                t.Add(p.Count); p.Add(b);
                t.Add(p.Count); p.Add(c);
                o.Add(ids.Count);
            }
            return new OverlayInput
            {
                Pos = p.ToArray(), Tri = t.ToArray(), Owner = o.ToArray(), Body = ids.ToArray(),
                Obstacle = Enumerable.Range(0, ids.Count + 1).Select(i => i == ids.Count).ToArray(),
                Inward = Enumerable.Range(0, ids.Count + 1).Select(i => i < ids.Count && bodies[ids[i]].Inward).ToArray(),
                ToDecal = toPart * frame.localToWorldMatrix
            };
        }

        // Worker: an object's overlay tiles (object space) into decal space, onto the main result, owners as body indices.
        private static void AddOverlay(TileResult r, TileResult o, OverlayInput oi)
        {
            Matrix4x4 m = oi.ToDecal;
            bool flip = m.determinant < 0;
            foreach (PartTiles pt in o.Parts)
            {
                for (int i = 0; i < pt.Vertices.Length; i++) pt.Vertices[i] = m.MultiplyPoint3x4(pt.Vertices[i]);
                for (int i = 0; i < pt.Normals.Length; i++) pt.Normals[i] = m.MultiplyVector(pt.Normals[i]).normalized;
                for (int i = 0; i < pt.BinNormals.Length; i++) pt.BinNormals[i] = m.MultiplyVector(pt.BinNormals[i]).normalized;
                for (int i = 0; i < pt.TileAreaVectors.Length; i++) pt.TileAreaVectors[i] = m.MultiplyVector(pt.TileAreaVectors[i]);
                if (flip)
                    foreach (int[] tris in pt.BinTriangles.Append(pt.JointTriangles).Append(pt.BackTriangles))
                        for (int i = 0; i + 2 < tris.Length; i += 3) { int x = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = x; }
                pt.Owner = oi.Body[pt.Owner];
            }
            r.Parts = r.Parts.Concat(o.Parts).ToArray();
            r.TileCount += o.TileCount;
            r.PartialCount += o.PartialCount;
            r.CoveredArea += o.CoveredArea;
            r.HitTileLimit |= o.HitTileLimit;
        }

        // attach point within the (projected, generous) footprint of a "limit area" decal; always with "whole part"
        private bool InFootprint(Part g, Vector3 u, Vector3 v)
        {
            if (!areaLimited || CoverOfParent() != null) return true; // a cockpit cover takes the whole cockpit
            Vector3 q = part.partTransform.InverseTransformPoint(AttachPoint(g));
            return Mathf.Abs(Vector3.Dot(q, u)) <= width / 2 + tileSize && Mathf.Abs(Vector3.Dot(q, v)) <= height / 2 + tileSize;
        }

        // A10: landing gear glued onto a covered part with its attach point inside the (projected, generous) footprint
        private Part[] GearInFootprint(Part[] parts, Vector3 u, Vector3 v) =>
            parts.SelectMany(t => t.children).Where(g => g != null && g.FindModuleImplementing<ModuleWheelBase>() != null && InFootprint(g, u, v) && GearOnDecalSide(g)).Distinct().ToArray();

        // 1.6.2: gear sticking out on the far side of the part (under a wing whose top has a decal) is not this decal's: the top
        // decal tiled the whole gear as well, top included
        private bool GearOnDecalSide(Part g)
        {
            if (g.srfAttachNode == null) return true;
            Vector3 gOut = -g.partTransform.TransformDirection(g.srfAttachNode.orientation).normalized;
            return Vector3.Dot(gOut, part.partTransform.TransformDirection(shieldNormal).normalized) >= -0.3f;
        }

        // A10/M3 (1.4): the whole outer skin of RETRACTED landing gear as tile carriers, curved nacelles (GearMedium) and bay doors
        // (SmallGearBay) included. Each MeshFilter transform, and each SkinnedMeshRenderer (its root bone, else its own
        // transform), is a moving body of its own (SeparateOwner), laid out in the retracted pose and carried by that transform.
        // Dropped are only triangles facing the hull (dot(n, hull normal) < -0.2), lying more than 2 cm inside it, or hidden while
        // retracted (wheel, strut, bay interior: a ray along n hits the gear's own skin within 0.5 m). The retracted pose is
        // sampled briefly from the deploy animation; all transforms of the part are put back exactly afterwards (saved local
        // poses, animation state values).
        private void AddGear(Part g, Matrix4x4 toPart, SurfaceIndex hull, List<Vector3> pos, List<Vector3> wpos, List<int> tri, List<int> owner, List<Body> bodies)
        {
            var dep = g.FindModuleImplementing<ModuleWheels.ModuleWheelDeployment>();
            Animation anim = dep != null && !string.IsNullOrEmpty(dep.animationStateName) ? g.FindModelAnimator(dep.animationTrfName, dep.animationStateName) : null;
            AnimationState st = anim != null ? anim[dep.animationStateName] : null;
            Transform[] all = null;
            Vector3[] lp = null, ls = null;
            Quaternion[] lr = null;
            bool stEnabled = false;
            float stTime = 0, stWeight = 0;
            int gearFaces = 0, gearBodies = 0, skinned = 0;
            if (st != null)
            {
                all = g.partTransform.GetComponentsInChildren<Transform>(true);
                lp = all.Select(t => t.localPosition).ToArray();
                lr = all.Select(t => t.localRotation).ToArray();
                ls = all.Select(t => t.localScale).ToArray();
                stEnabled = st.enabled;
                stTime = st.normalizedTime;
                stWeight = st.weight;
                st.enabled = true;
                st.weight = 1;
                st.normalizedTime = 1 - dep.deployedPosition; // KSP: retractedPosition = 1 - deployedPosition
                anim.Sample();
            }
            try
            {
                // pass 1: every visible mesh of the gear in the retracted pose, world space (v11: so the tests below do not depend on
                // where the decal sits), front faces wound outward
                var pieces = new List<GearPiece>();
                foreach (MeshFilter mf in g.FindModelComponents<MeshFilter>())
                {
                    Renderer r = mf.GetComponent<Renderer>();
                    Mesh src = mf.sharedMesh;
                    if (src == null || r == null || !r.enabled || !mf.gameObject.activeInHierarchy || !src.isReadable) continue;
                    if (mf.GetComponent<AblativeDecalTilesMarker>() != null) continue; // our own (or another decal's) tiles
                    Matrix4x4 m = mf.transform.localToWorldMatrix;
                    Vector3[] vs = src.vertices;
                    for (int i = 0; i < vs.Length; i++) vs[i] = m.MultiplyPoint3x4(vs[i]);
                    pieces.Add(new GearPiece { T = mf.transform, ToDecal = toPart * m, V = vs, Tri = GearTriangles(src, r, m.determinant < 0) });
                }
                // ponytail: skinned meshes are skinned here by hand (vertex = sum of weight * bone.localToWorld * bindpose * v, what
                // the GPU does) instead of SkinnedMeshRenderer.BakeMesh: Unity 2019.4 only has BakeMesh(Mesh), whose output may or
                // may not carry the transform's scale; the sum is exact either way. Blend shapes are ignored (gear has none).
                foreach (SkinnedMeshRenderer smr in g.FindModelComponents<SkinnedMeshRenderer>())
                {
                    Mesh src = smr.sharedMesh;
                    if (src == null || !smr.enabled || !smr.gameObject.activeInHierarchy || !src.isReadable) continue;
                    if (smr.GetComponent<AblativeDecalTilesMarker>() != null) continue;
                    Transform[] bones = smr.bones;
                    Matrix4x4[] bind = src.bindposes;
                    BoneWeight[] bw = src.boneWeights;
                    Vector3[] vs = src.vertices;
                    Matrix4x4 rigid = smr.transform.localToWorldMatrix;
                    bool skin = bones != null && bones.Length > 0 && bind.Length == bones.Length && bw.Length == vs.Length;
                    Matrix4x4[] bm = skin ? bones.Select((b, i) => b != null ? b.localToWorldMatrix * bind[i] : rigid).ToArray() : null;
                    for (int i = 0; i < vs.Length; i++)
                    {
                        if (!skin) { vs[i] = rigid.MultiplyPoint3x4(vs[i]); continue; }
                        BoneWeight w = bw[i];
                        Vector3 v = vs[i];
                        vs[i] = w.weight0 * bm[w.boneIndex0].MultiplyPoint3x4(v) + w.weight1 * bm[w.boneIndex1].MultiplyPoint3x4(v) +
                                w.weight2 * bm[w.boneIndex2].MultiplyPoint3x4(v) + w.weight3 * bm[w.boneIndex3].MultiplyPoint3x4(v);
                    }
                    Transform body = smr.rootBone != null ? smr.rootBone : smr.transform;
                    int[] heavy = skin ? bw.Select(w => w.weight0 >= w.weight1 && w.weight0 >= w.weight2 && w.weight0 >= w.weight3 ? w.boneIndex0
                        : w.weight1 >= w.weight2 && w.weight1 >= w.weight3 ? w.boneIndex1 : w.weight2 >= w.weight3 ? w.boneIndex2 : w.boneIndex3).ToArray() : null;
                    pieces.Add(new GearPiece { T = body, ToDecal = toPart * body.localToWorldMatrix, V = vs,
                        Tri = GearTriangles(src, smr, (skin ? bm[0] : rigid).determinant < 0), Skinned = true, Bone = heavy, Bones = skin ? bones : null });
                }

                // all of the gear's own skin, for the hidden-while-retracted test
                var occPos = new List<Vector3>();
                var occTri = new List<int>();
                foreach (GearPiece p in pieces)
                {
                    int o = occPos.Count;
                    occPos.AddRange(p.V);
                    foreach (int k in p.Tri) occTri.Add(o + k);
                }
                if (occTri.Count == 0) return;
                var own = new SurfaceIndex(occPos.ToArray(), occTri.ToArray(), g.partTransform.position, 20f, 0.1f, 0.02f);

                Vector3 attach = AttachPoint(g);
                Vector3 outside = g.srfAttachNode != null ? -g.partTransform.TransformDirection(g.srfAttachNode.orientation).normalized : Vector3.zero;
                if (hull.Closest(attach, 0.6f, outside, 0.7f, out SurfaceHit baseFace)) { attach = baseFace.P; outside = baseFace.FaceN; } // 1.6.2: only a face along the attach direction (gear on a wing edge: an edge facet tilted 60 deg dropped one whole pod side)
                // pass 2: keep the outer skin
                foreach (GearPiece p in pieces)
                {
                    int before = tri.Count, bodiesBefore = bodies.Count;
                    var used = new Dictionary<int, int>();
                    var bodyOf = new Dictionary<Transform, int>();
                    for (int i = 0; i + 2 < p.Tri.Length; i += 3)
                    {
                        Vector3 a = p.V[p.Tri[i]], b = p.V[p.Tri[i + 1]], c = p.V[p.Tri[i + 2]];
                        Vector3 n = Vector3.Cross(b - a, c - a);
                        if (n.sqrMagnitude < 1e-16f) continue;
                        n.Normalize();
                        Vector3 ctr = (a + b + c) / 3;
                        // 1.6.2: kept if ANY corner stands out of the hull (not the centre: the long side faces of a gear pod alternated
                        // kept/dropped, a white saw tooth along its side)
                        if (Mathf.Max(Vector3.Dot(a - attach, outside), Mathf.Max(Vector3.Dot(b - attach, outside), Vector3.Dot(c - attach, outside))) < -0.012f || Vector3.Dot(n, outside) < -0.45f) continue;
                        if (own.Raycast(ctr + n * 0.002f, n, 0.5f, -1f, out _, false)) continue;          // hidden by the gear itself
                        for (int j = 0; j < 3; j++)
                        {
                            int k = p.Tri[i + j];
                            if (!used.TryGetValue(k, out int idx)) { idx = pos.Count; used[k] = idx; pos.Add(toPart.MultiplyPoint3x4(p.V[k])); wpos.Add(p.V[k]); }
                            tri.Add(idx);
                        }
                        Transform bt = p.Bone != null && p.Bones[p.Bone[p.Tri[i]]] != null ? p.Bones[p.Bone[p.Tri[i]]] : p.T;
                        if (!bodyOf.TryGetValue(bt, out int bi))
                        {
                            bodyOf[bt] = bi = bodies.Count;
                            bodies.Add(new Body { Part = g, T = bt, Flap = true, Gear = true, ToDecal = bt == p.T ? p.ToDecal : toPart * bt.localToWorldMatrix });
                        }
                        owner.Add(bi);
                    }
                    if (tri.Count == before) continue;
                    gearFaces += (tri.Count - before) / 3;
                    gearBodies += bodies.Count - bodiesBefore;
                    if (p.Skinned) skinned++;
                }
            }
            finally
            {
                if (st != null)
                {
                    for (int i = 0; i < all.Length; i++)
                        if (all[i] != null) { all[i].localPosition = lp[i]; all[i].localRotation = lr[i]; all[i].localScale = ls[i]; }
                    st.normalizedTime = stTime;
                    st.weight = stWeight;
                    st.enabled = stEnabled;
                }
                Debug.Log($"[AblatorDecals] gear {g.partInfo?.name}: deploy={(st != null ? dep.animationTrfName + "/" + dep.animationStateName : "none")} retracted faces={gearFaces} bodies={gearBodies} skinned={skinned}");
            }
        }

        private class GearPiece
        {
            public Transform T;          // body transform: the MeshFilter's, or the skinned mesh's root bone
            public Matrix4x4 ToDecal;    // T (retracted) -> decal space
            public Vector3[] V;          // world space, retracted
            public int[] Tri;            // front faces outward also under a mirrored transform
            public bool Skinned;
            public int[] Bone;           // skinned: per vertex its heaviest bone (index into Bones), the body it rides on (1.6)
            public Transform[] Bones;
        }

        private static int[] GearTriangles(Mesh mesh, Renderer renderer, bool mirrored)
        {
            // Landing lights use transparent flare cards that extend beyond the housing.
            if (Same(renderer.transform.name, "flare")) return new int[0];
            Material[] materials = renderer.sharedMaterials;
            var triangles = new List<int>();
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                string shaderName = i < materials.Length ? materials[i]?.shader?.name ?? "" : "";
                if (!SkipShaders.Any(shaderName.Contains)) triangles.AddRange(mesh.GetTriangles(i));
            }
            return Wound(triangles.ToArray(), mirrored);
        }

        private static int[] Wound(int[] t, bool mirrored)
        {
            if (!mirrored) return t;
            var w = (int[])t.Clone();
            for (int i = 0; i + 2 < w.Length; i += 3) { w[i + 1] = t[i + 2]; w[i + 2] = t[i + 1]; }
            return w;
        }

        // Moving transforms of the part's control surfaces (all matches of transformName). Neutral rotation from the prefab,
        // which never deflects, so editor (deploy toggle) and flight (deflected at start) build the same tiles.
        private static List<Body> Flaps(Part p, Matrix4x4 toPart)
        {
            var list = new List<Body>();
            foreach (ModuleControlSurface cs in p.Modules.Cast<PartModule>().OfType<ModuleControlSurface>())
            {
                if (string.IsNullOrEmpty(cs.transformName)) continue;
                Transform[] now = p.FindModelTransforms(cs.transformName);
                Transform[] prefab = p.partInfo != null && p.partInfo.partPrefab != null ? p.partInfo.partPrefab.FindModelTransforms(cs.transformName) : null;
                for (int i = 0; i < now.Length; i++)
                {
                    Transform t = now[i];
                    if (list.Any(b => b.T == t)) continue;
                    Quaternion neutral = prefab != null && i < prefab.Length ? prefab[i].localRotation : t.localRotation;
                    Matrix4x4 world = (t.parent != null ? t.parent.localToWorldMatrix : Matrix4x4.identity) * Matrix4x4.TRS(t.localPosition, neutral, t.localScale);
                    list.Add(new Body { Part = p, T = t, Flap = true, ToDecal = toPart * world, ToWorld = world });
                }
            }
            return list;
        }

        private static bool SamePoses(Matrix4x4[] a, Matrix4x4[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                for (int k = 0; k < 16; k++)
                    if (Mathf.Abs(a[i][k] - b[i][k]) > 1e-4f) return false;
            return true;
        }

        // ---------- editor ----------

        // Slider drags fire per step; Update() picks up the pending rebuild.
        private void OnTweak(BaseField field, object oldValue)
        {
            if (field != null && field.name == nameof(keepHatches)) keepHatchesInit = true;
            if (field != null && field.name == nameof(keepWindows)) keepWindowsInit = true;
            if (field != null && field.name == nameof(smoothHull)) smoothHullInit = true;
            rebuildPending = true;
            CopyToCounterparts();
        }

        // 1.9: per-parent defaults when attached: hatches and windows stay free, except on the Mk3 crew cabin and (1.10) the
        // Mobile Lab (hatches tiled over). On another parent type (alt-copy, moved decal) they and the smooth hull are set afresh; the
        // player's toggles persist while the parent type stays.
        private void InitParentDefaults()
        {
            if (part.parent == null || detached) return;
            string parentName = part.parent.partInfo != null ? part.parent.partInfo.name : "";
            if (string.IsNullOrEmpty(initParent) && keepHatchesInit) initParent = parentName; // pre-1.9 save: keep the player's choices
            // 1.10: the Mobile Lab's belly hatch is tiled over too (its side windows stay free)
            bool isMk3Cabin = Same(parentName, "mk3CrewCabin") || Same(parentName, "Large_Crewed_Lab") || Same(parentName, "Large.Crewed.Lab");
            if (!Same(initParent, parentName))
            {
                initParent = parentName;
                keepHatches = !isMk3Cabin; keepHatchesInit = true;
                keepWindows = true; keepWindowsInit = true;
                smoothHullInit = false; // MakeJob decides the smooth hull for the new parent
                return;
            }
            if (!keepHatchesInit) { keepHatches = !isMk3Cabin; keepHatchesInit = true; }
            if (!keepWindowsInit) { keepWindows = true; keepWindowsInit = true; }
        }

        private void OnPatternChanged(BaseField field, object oldValue)
        {
            SyncSettings(); // texture for the new shape; a saved Brick drops out of the list once left
            rebuildPending = true;
            CopyToCounterparts();
            MonoUtilities.RefreshPartContextWindow(part);
        }

        private void OnFinishChanged(BaseField field, object oldValue)
        {
            if (finishModule != null) finishModule.finish = finishUi;
            SyncSettings();
            rebuildPending = true;
            CopyToCounterparts();
            MonoUtilities.RefreshPartContextWindow(part);
        }

        // all settings to the symmetry counterparts; a mirrored one gets the mirrored angle
        private void CopyToCounterparts()
        {
            foreach (Part p in part.symmetryCounterparts)
            {
                ModuleAblativeDecal m = p != null ? p.FindModuleImplementing<ModuleAblativeDecal>() : null;
                if (m == null) continue;
                m.pattern = pattern;
                if (m.finishModule != null) m.finishModule.finish = Finish;
                m.tileSize = tileSize;
                m.patternAngle = part.symMethod == SymmetryMethod.Mirror ? -patternAngle : patternAngle;
                m.tileGap = tileGap;
                m.width = width;
                m.height = height;
                m.thickness = thickness;
                m.maxWrap = maxWrap;
                m.coverHeight = coverHeight;
                m.unrolled = unrolled;
                m.straightBorder = straightBorder;
                m.areaLimited = areaLimited;
                m.cockpitShape = cockpitShape;
                m.tileLoss = tileLoss;
                m.textureAcross = textureAcross;
                m.textureScale = textureScale;
                m.weathering = weathering;
                m.tileShedding = tileShedding;
                m.keepHatches = keepHatches;
                m.keepHatchesInit = true;
                m.keepWindows = keepWindows;
                m.keepWindowsInit = true;
                m.smoothHull = smoothHull;
                m.smoothHullInit = true;
                if (m.decalRoot != null) { m.SyncSettings(); m.rebuildPending = true; }
            }
        }

        private void OnEditorPartEvent(ConstructionEventType type, Part eventPart)
        {
            if (eventPart != null && eventPart.FindModuleImplementing<ModuleAblativeDecal>() == null)
            {
                // another part: it attaches to the hull under the tiles (colliders off while it is held), then sits on them
                switch (type)
                {
                    case ConstructionEventType.PartPicked:
                    case ConstructionEventType.PartCreated:
                    case ConstructionEventType.PartCopied: // alt-click copy (1.3 missed it)
                        SetColliders(false);
                        break;
                    case ConstructionEventType.PartAttached:
                        SetColliders(true);
                        LiftOntoTiles(eventPart);
                        foreach (Part c in eventPart.symmetryCounterparts) LiftOntoTiles(c);
                        GearChanged(eventPart);
                        break;
                    case ConstructionEventType.PartDetached:
                    case ConstructionEventType.PartOffset:
                    case ConstructionEventType.PartRotated:
                        GearChanged(eventPart);
                        break;
                    case ConstructionEventType.PartDropped:
                    case ConstructionEventType.PartDeleted:
                        SetColliders(true);
                        break;
                }
                return;
            }
            if (eventPart != part && !part.symmetryCounterparts.Contains(eventPart)) return;
            switch (type)
            {
                case ConstructionEventType.PartDetached:
                    detached = true; // part.parent may still be set at this point
                    rebuildNow = true;
                    break;
                case ConstructionEventType.PartAttached:
                    detached = false;
                    InitParentDefaults();
                    rebuildNow = true;
                    break;
                case ConstructionEventType.PartOffset:
                case ConstructionEventType.PartRotated:
                    rebuildNow = true;
                    break;
                case ConstructionEventType.PartOffsetting:
                case ConstructionEventType.PartRotating:
                    rebuildPending = true;
                    break;
            }
        }

        // editor: landing gear came, went or moved (also in the subtree): new surface (gear tiles, A10) and colliders
        private void GearChanged(Part p)
        {
            if (decalRoot == null) return;
            if (p.FindModulesImplementing<ModuleWheelBase>().Count == 0 && !p.FindChildParts<Part>(true).Any(c => c.FindModuleImplementing<ModuleWheelBase>() != null)) return;
            surfaceCache = null;
            rebuildNow = true;
        }

        // A part glued onto the covered hull inside the tiles is moved out by the tile height along the hull normal. Idempotent:
        // the position comes from the hull hit (the parent's colliders, where KSP attached it), not from the current offset.
        private void LiftOntoTiles(Part p)
        {
            if (p == null || p.parent == null || p.attachMode != AttachModes.SRF_ATTACH || !lastTargets.Contains(p.parent)) return;
            if (p.parent == part.parent && Glued(p)) return; // a flap or intake there is a target: tiled itself, stays put
            Vector3 at = AttachPoint(p);
            if (!OnTiles(at)) return;
            Vector3 dir = p.srfAttachNode != null ? p.partTransform.TransformDirection(p.srfAttachNode.orientation).normalized : -p.partTransform.up;
            for (int pass = 0; pass < 2; pass++, dir = -dir) // node direction convention differs between parts: try both ways
            {
                RaycastHit best = default;
                float bestDist = float.MaxValue;
                foreach (RaycastHit hit in Physics.RaycastAll(at - dir * 0.5f, dir, 1f))
                    if (hit.distance < bestDist && hit.collider.GetComponentInParent<Part>() == p.parent) { best = hit; bestDist = hit.distance; }
                if (bestDist == float.MaxValue) continue;
                p.partTransform.position += best.point + best.normal * (thickness + surfaceOffset) - at;
                return;
            }
        }

        // Parts stacked on/removed from the covered hull change what this decal lies on.
        private void OnEditorShipModified(ShipConstruct ship)
        {
            // only when the parts under this decal or their pose changed (every click fires this event)
            if (part.parent == null || detached) return;
            Part[] now = TargetParts();
            if (!now.SequenceEqual(lastTargets) || !SamePoses(lastPoses, Poses(now))) rebuildPending = true;
            CheckMeshSig();
        }

        // 1.8: parent meshes swapped or rescaled (TweakScale, B9PartSwitch): new surface
        private void CheckMeshSig()
        {
            if (lastTargets.Length == 0 || ComputeMeshSig(lastTargets) == lastMeshSig) return;
            surfaceCache = null;
            rebuildPending = true;
        }

        private void OnVariantApplied(Part variantPart, PartVariant variant)
        {
            if (variantPart == part.parent || lastTargets.Contains(variantPart)) { surfaceCache = null; rebuildNow = true; }
        }

        private void OnEditorShip(ShipConstruct ship)
        {
            SetColliders(true);
            surfaceCache = null;
            rebuildNow = true;
        }

        private void UpdateAblatorAmount()
        {
            if (ablator == null) return;
            double units = coveredArea * thickness * unitsPerCubicMeter;
            bool changed = Math.Abs(units - ablator.maxAmount) > 1e-6;
            ablatorUnits = ablator.maxAmount = ablator.amount = units;
            // mass/cost readouts; no PAW refresh here, that would break an active slider drag
            if (changed && EditorLogic.fetch != null && EditorLogic.fetch.ship != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        // ---------- mass and cost ----------

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => (float)coveredArea * massPerArea;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => (float)coveredArea * costPerArea;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        // ---------- flight ----------

        private void OnPartWillDie(Part dying)
        {
            if (dying == null || part == null) return;
            if (dying == part.parent) part.Die();
            else if (targets.Contains(dying))
            {
                dyingPart = dying; // tiles over a lost part go with it on the next build
                surfaceCache = null;
                rebuildNow = true;
            }
        }

        private void OnVesselWasModified(Vessel v)
        {
            if (v != vessel || part == null || part.parent == null || !flightStarted) return;
            if (!TargetParts().SequenceEqual(lastTargets)) { surfaceCache = null; rebuildNow = true; }
            else onTiles = PartsOnTiles(); // something may have been attached to the tiles (EVA construction), or docked
            IgnoreTileCollisions();
            ignoreFrames = 2;
        }

        private bool HasAblator => ablator != null && ablator.amount > 0;

        // This decal's frontal area on the given part (from its last physics tick).
        private double FrontalOn(Part p)
        {
            if (!HasAblator) return 0;
            int i = Array.IndexOf(targets, p);
            return i >= 0 && i < frontal.Length ? frontal[i] : 0;
        }

        private List<ModuleAblativeDecal> VesselDecals()
        {
            if (vessel != cachedVessel || vesselDecals == null || vesselDecalsPartCount != vessel.parts.Count)
            {
                cachedVessel = vessel;
                vesselDecals = vessel.FindPartModulesImplementing<ModuleAblativeDecal>();
                vesselDecalsPartCount = vessel.parts.Count;
            }
            return vesselDecals;
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (ignoreFrames > 0 && --ignoreFrames == 0) IgnoreTileCollisions();
            if (ablator == null || vessel == null || part.parent == null) return;
            if (vessel.packed) return;

            if (!flightStarted)
            {
                flightStarted = true;
                diagnoseAt = Time.time + 3f;
            }

            if (vessel.atmDensity <= 0 && !heating && glowShownMax < 0.002f && absorbed <= 0)
            {
                facing = 0;
                absorbed = 0;
                return;
            }

            // tile area seen by the oncoming flow, per covered part: sum of area * max(0, n.v)
            // (per host: a flap host turns with its flap; all hosts on one part, flap or not, add up for that part)
            Vector3d vel = vessel.srf_velocity;
            Vector3 vWorld = vel.magnitude >= 1 ? (Vector3)vel.normalized : Vector3.zero;
            double frontalTotal = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                double sum = 0;
                foreach (TileHost host in hosts)
                {
                    if (host.TargetPart != targets[i] || !host.Alive) continue;
                    Vector3 vLocal = host.DecalDirection(vWorld);
                    if (host.BinCount > 0)
                    {
                        for (int b = 0; b < host.BinCount; b++)
                        {
                            float d = Vector3.Dot(host.BinNormals[b], vLocal);
                            if (d > 0) sum += d * host.BinAreas[b];
                        }
                    }
                    else
                    {
                        foreach (Vector3 a in host.TileAreaVectors)
                        {
                            float d = Vector3.Dot(a, vLocal);
                            if (d > 0) sum += d;
                        }
                    }
                }
                frontal[i] = sum;
                frontalTotal += sum;
            }
            facing = coveredArea > 0 ? frontalTotal / coveredArea : 0;

            // per covered part: share of its convective heating this decal takes (all decals on a part capped at 100 %);
            // parts glued on inside the tiles (gear, decouplers) get the share of the part they sit on (q index targets.Length + k)
            int nt = targets.Length;
            int qLen = nt + onTiles.Count;
            if (qBuf.Length < qLen) qBuf = new double[qLen];
            if (covBuf.Length < nt) covBuf = new double[nt];
            double[] q = qBuf, covs = covBuf;
            double qWanted = 0, qOffered = 0, hottest = 0, hottestMax = 1;
            for (int i = 0; i < nt; i++)
            {
                Part p = targets[i];
                if (p == null || p.vessel != vessel) { q[i] = covs[i] = 0; continue; }
                double raw = HasAblator ? ShieldMath.RawCoverage(frontal[i], 1, p.exposedArea) : 0;
                double rawSum = 0;
                foreach (ModuleAblativeDecal d in VesselDecals()) rawSum += d == this ? raw : ShieldMath.RawCoverage(d.FrontalOn(p), 1, p.exposedArea);
                double cov = covs[i] = ShieldMath.Coverage(raw, rawSum);
                double qIn = p.skinTemperature > ablationTempThresh ? Math.Max(0, p.thermalConvectionFlux) : 0;
                q[i] = cov * efficiency * qIn;
                qWanted += q[i];
                qOffered += Math.Max(0, p.thermalConvectionFlux);
                if (p.skinTemperature / p.skinMaxTemp > hottest / hottestMax) { hottest = p.skinTemperature; hottestMax = p.skinMaxTemp; }
            }
            for (int k = 0; k < onTiles.Count; k++)
            {
                Part e = onTiles[k];
                int i = e != null && e.vessel == vessel ? Array.IndexOf(targets, e.parent) : -1;
                if (i < 0) { q[nt + k] = 0; continue; }
                double qIn = e.skinTemperature > ablationTempThresh ? Math.Max(0, e.thermalConvectionFlux) : 0;
                q[nt + k] = covs[i] * efficiency * qIn;
                qWanted += q[nt + k];
                qOffered += Math.Max(0, e.thermalConvectionFlux);
            }

            // one ablator pool for all covered parts; if it runs short every part gets the same fraction
            absorbed = ShieldMath.Absorb(1, qWanted, ablator.amount, kJPerUnit, TimeWarp.fixedDeltaTime, out double used);
            double scale = qWanted > 0 ? absorbed / qWanted : 0;
            if (absorbed > 0)
            {
                for (int i = 0; i < qLen; i++)
                    if (q[i] > 0) (i < nt ? targets[i] : onTiles[i - nt]).AddExposedThermalFlux(-q[i] * scale);
                ablator.amount -= used;
                energyAbsorbed += absorbed * TimeWarp.fixedDeltaTime / 1000;
            }
            double blocked = qOffered > 0 ? absorbed / qOffered : 0;
            if (absorbed > 0) TrackExposure(vWorld, absorbed);
            UpdateGlow(vWorld);

            // 1.8: warnings and shedding while heat comes in (an empty ablator absorbs nothing, but is still being heated)
            double ablatorFrac = ablator.maxAmount > 0 ? ablator.amount / ablator.maxAmount : 0;
            double heatRatio = hottest / hottestMax;
            if (absorbed > 0 || qWanted > 0)
            {
                if (!lowWarned && ablatorFrac < 0.10)
                {
                    lowWarned = true;
                    ScreenMessages.PostScreenMessage(Localizer.Format(L + "lowAblator_msg"), 5f, ScreenMessageStyle.UPPER_CENTER);
                }
                if (!depletedWarned && ablator.amount <= 0)
                {
                    depletedWarned = true;
                    ScreenMessages.PostScreenMessage(Localizer.Format(L + "depleted_msg"), 5f, ScreenMessageStyle.UPPER_CENTER);
                }
                if (tileShedding && (ablatorFrac < 0.30 || heatRatio > 0.85))
                {
                    // up to 25 % of the tiles as the ablator runs from 30 % to empty or the skin nears its limit, in 5 % steps
                    // (at most 5 rebuilds over a reentry)
                    float target = Mathf.Clamp(Mathf.Max((float)(0.30 - ablatorFrac) / 0.30f, (float)(heatRatio - 0.85) / 0.15f) * 0.25f, 0f, 0.25f);
                    float stepped = Mathf.Floor(target / 0.05f + 1e-4f) * 0.05f;
                    if (stepped > battleLoss + 1e-4f)
                    {
                        battleLoss = stepped;
                        rebuildNow = true;
                        ScreenMessages.PostScreenMessage(Localizer.Format(L + "shedding_msg"), 4f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
            }

            if (Time.realtimeSinceStartup >= nextDisplay) // formatted strings: 4 times a second is plenty
            {
                nextDisplay = Time.realtimeSinceStartup + 0.25f;
                heatBlocked = targets.Length > 1 ? Localizer.Format(L + "parts_fmt", blocked.ToString("P0"), targets.Length) : blocked.ToString("P0");
                hottestSkin = $"{hottest:F0} / {hottestMax:F0} K";
                tileTemp = $"{hottestTile:F0} K";
                thermalStatus = Localizer.Format(L + (ablator.amount <= 0 ? "status_depleted" : ablatorFrac < 0.10 ? "status_low"
                    : heatRatio > 0.70 || ablatorFrac < 0.30 ? "status_marginal" : "status_nominal"));
            }
            TrackReentry(blocked, hottest, hottestMax);

            if (logInterval > 0 && vessel.atmDensity > 0 && Time.fixedTime >= nextLog)
            {
                nextLog = Time.fixedTime + logInterval;
                string perPart = string.Join("; ", Enumerable.Range(0, targets.Length).Where(i => targets[i] != null).Select(i =>
                    $"{targets[i].partInfo.name} frontal={frontal[i]:F2}/{targets[i].exposedArea:F2}m2 Q={targets[i].thermalConvectionFlux:F0}kW " +
                    $"absorbed={q[i] * scale:F0}kW skin={targets[i].skinTemperature:F0}/{targets[i].skinMaxTemp:F0}K"));
                Debug.Log($"[AblatorDecals] {PartName} mach={vessel.mach:F2} facing={facing:F2} blocked={blocked:P1} absorbed={absorbed:F0}kW " +
                          $"ablator={ablator.amount:F1}/{ablator.maxAmount:F1} tile={hottestTile:F0}K | {perPart}");
            }
        }

        // One continuous emission level across adjoining shields. The incident convective flux is
        // weighted by actual tile area and facing. Ablator consumption still uses each part's flux.
        private sealed class VesselGlow
        {
            public double LastTick = double.NaN;
            public TileGlow Surface;
            public double Shown, Skin, FMax, ExtTemp; // 1.7.1 / 1.7.4: skin temp, max facing, and external post-shock air temp
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Vessel, VesselGlow> GlowFields =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Vessel, VesselGlow>();

        private void UpdateGlow(Vector3 vWorld)
        {
            var field = GlowFields.GetOrCreateValue(vessel);
            double tick = Planetarium.GetUniversalTime();
            if (tick != field.LastTick)
            {
                double area = 0, skinSum = 0, filmSum = 0, fluxSum = 0, extAirSum = 0, fMax = 0;
                double prevT = field.Surface != null ? field.Surface.T : 0;
                foreach (var decal in VesselDecals())
                {
                    if (decal == null || decal.vessel != vessel) continue;
                    foreach (var host in decal.hosts)
                    {
                        Part target = host.TargetPart;
                        if (!host.Alive || target == null) continue;
                        var data = target.ptd;
                        double coeff = data != null && data.convectionArea > 0 ? data.finalCoeff / data.convectionArea : 0;
                        double ext = data != null ? data.postShockExtTemp : target.skinTemperature;
                        Vector3 flow = host.DecalDirection(vWorld);
                        for (int b = 0; b < host.BinCount; b++)
                        {
                            double weight = host.BinAreas[b];
                            double fb = Math.Max(0, Vector3.Dot(host.BinNormals[b], flow));
                            if (weight > 0) fMax = Math.Max(fMax, fb);
                            // Heating is concentrated on windward faces (fb); convective cooling in cold air acts across the boundary layer.
                            double fbEff = field.Surface != null ? TileGlow.EffectiveFacing(fb, ext, prevT) : fb;
                            double film = weight * coeff * fbEff;
                            area += weight;
                            skinSum += weight * target.skinTemperature;
                            extAirSum += weight * ext;
                            filmSum += film;
                            fluxSum += film * ext;
                        }
                    }
                }
                if (area <= 0) return;
                field.LastTick = tick;
                double skin = skinSum / area;
                double extAir = extAirSum / area;
                double effExt = filmSum > 0 ? fluxSum / filmSum : extAir;
                field.Skin = skin;
                field.FMax = fMax;
                field.ExtTemp = effExt;
                if (field.Surface == null)
                {
                    field.Surface = new TileGlow(skin) { Emissivity = glowEmissivity, HeatCapacity = glowHeatCapacity, Conduction = glowConduction };
                    field.Shown = skin;
                }
                double dt = TimeWarp.fixedDeltaTime;
                // Radiative/conductive sink never holds tiles glowing in cold post-reentry air or vacuum.
                double sink = Math.Max(250.0, Math.Min(skin, extAir));
                double temperature = field.Surface.Step(dt, sink, effExt, filmSum / area, 1);
                double fade = glowFadeTime > 0 ? 1 - Math.Exp(-dt / glowFadeTime) : 1;
                field.Shown += (temperature - field.Shown) * fade;
            }
            if (field.Surface == null) return;
            hottestTile = field.Surface.T;
            glowShownMax = 0;
            bool checkKspGlow = Time.time >= nextKspGlowCheck;
            if (checkKspGlow) nextKspGlowCheck = Time.time + 1;
            foreach (TileHost h in hosts)
            {
                Part p = h.TargetPart;
                if (p == null || !h.Alive) continue;
                // KSP's own glow (part.mpb through Part.temperatureRenderer) would override our per-bin colours; its renderer list
                // is built at Part.Start before our hosts exist, but the thermal overlay (FlightOverlays -> Part.ResetMPB)
                // rebuilds it from the model and picks our renderer up: take it out again, and its property block with it
                if (checkKspGlow && KspGlowRenderers(p) is List<Renderer> kspList && kspList.Remove(h.Renderer)) h.Renderer.SetPropertyBlock(null);
                bool show = glowEnabled && h.HasGlow;
                if (!show) continue;
                // 1.7.1 / 1.7.4: each glow bin by how it faces the flow (only while external air is hot enough to heat tiles)
                Vector3 flow = h.DecalDirection(vWorld);
                Color jointSum = Color.black;
                double jointArea = 0;
                for (int b = 0; b < h.BinCount; b++)
                {
                    Color c = GlowColor((float)TileGlow.BinTemp(field.Skin, field.Shown, field.ExtTemp, Math.Max(0, Vector3.Dot(h.BinNormals[b], flow)), field.FMax));
                    glowShownMax = Math.Max(glowShownMax, Mathf.Max(c.r, c.g, c.b));
                    jointSum += c * (float)h.BinAreas[b];
                    jointArea += h.BinAreas[b];
                    Color last = h.GlowSet[b];
                    if (Mathf.Max(Mathf.Abs(c.r - last.r), Mathf.Abs(c.g - last.g), Mathf.Abs(c.b - last.b)) < 0.002f) continue;
                    h.GlowSet[b] = c;
                    h.SetBinGlow(b, c);
                }
                Color jc = jointArea > 0 ? jointSum * (float)(TileGlow.JointGlow / jointArea) : Color.black;
                if (Mathf.Max(Mathf.Abs(jc.r - h.JointSet.r), Mathf.Abs(jc.g - h.JointSet.g), Mathf.Abs(jc.b - h.JointSet.b)) >= 0.002f) h.SetJointGlow(jc);
            }
        }

        private static string GlowName(Material m)
        {
            int id = TileHost.GlowProperty(m);
            return id == TileHost.EmissiveColorID ? "_EmissiveColor"
                : id == TileHost.EmissionColorID ? "_EmissionColor"
                : id != 0 ? "_TemperatureColor" : "none";
        }

        // Part.temperatureRenderer (private MaterialColorUpdater) .renderers by reflection; null if KSP renames them
        private static readonly System.Reflection.FieldInfo TempRendererField = typeof(Part).GetField("temperatureRenderer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.FieldInfo RendererListField = TempRendererField?.FieldType.GetField("renderers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        private static List<Renderer> KspGlowRenderers(Part p)
        {
            object updater = TempRendererField?.GetValue(p);
            return updater != null ? RendererListField?.GetValue(updater) as List<Renderer> : null;
        }

        // "K r g b a; ..." -> stops sorted by temperature; a broken entry is skipped
        private static float[][] ParseRamp(string text) =>
            (text ?? "").Split(';')
                .Select(e => e.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(f => f.Length == 5)
                .Select(f => f.Select(x => float.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : float.NaN).ToArray())
                .Where(f => !f.Any(float.IsNaN))
                .OrderBy(f => f[0]).ToArray();

        // glow colour from the ramp (continuous interpolation between stops, clamped at both ends; a = strength). Returned premultiplied
        // (rgb * a, alpha 1): the same emission whether the shader scales _TemperatureColor.rgb by its alpha or not, so below
        // the first stop (a = 0) nothing glows either way.
        private Color GlowColor(float t)
        {
            if (ramp == null || ramp.Length == 0) return Color.black;
            float[] a = ramp[0], b = ramp[ramp.Length - 1];
            for (int i = 1; i < ramp.Length; i++)
                if (t <= ramp[i][0]) { a = ramp[i - 1]; b = ramp[i]; break; }
            float k = b[0] > a[0] ? Mathf.Clamp01((t - a[0]) / (b[0] - a[0])) : 1;
            k = k * k * (3f - 2f * k); // 1.9: smoothstep, no kinks at the stops (soft onset)
            float s = Mathf.Lerp(a[4], b[4], k);
            return new Color(Mathf.Lerp(a[1], b[1], k) * s, Mathf.Lerp(a[2], b[2], k) * s, Mathf.Lerp(a[3], b[3], k) * s, 1);
        }

        // ---------- global settings (DecalWindow) ----------

        // Pattern, finish, tile size and thickness from the editor window (thickness: ablator, mass and cost follow on the rebuild). With B9PartSwitch its switchers are switched (they would reset
        // our fields when the craft loads); the change arrives through OnLoad like a click on the B9 switcher.
        internal void ApplyGlobal(string newPattern, string newFinish, float newSize, float newThickness)
        {
            if (!SwitchB9("tilePattern", newPattern)) pattern = newPattern;
            ModuleAblativeDecalFinish fm = finishModule ?? part.FindModuleImplementing<ModuleAblativeDecalFinish>(); // 1.9: also before OnStart
            if (!SwitchB9("tileFinish", newFinish) && fm != null) fm.finish = newFinish;
            if (!premadeShown && (CoverOfParent() == null || PremadeCover(part.parent, Shape) == null))
            {
                tileSize = Mathf.Clamp(newSize, 0.03f, 0.5f);
                thickness = Mathf.Clamp(newThickness, 0.005f, 0.3f);
            }
            if (decalRoot != null) SyncSettings();
            rebuildPending = true;
        }

        // ModuleB9PartSwitch.SwitchSubtype(string) by reflection (no assembly reference); false without that switcher
        private bool SwitchB9(string moduleId, string subtype)
        {
            foreach (PartModule m in part.Modules)
            {
                if (m.moduleName != "ModuleB9PartSwitch" || m.GetType().GetField("moduleID")?.GetValue(m) as string != moduleId) continue;
                System.Reflection.MethodInfo sw = m.GetType().GetMethod("SwitchSubtype", new[] { typeof(string) });
                if (sw == null) return false;
                sw.Invoke(m, new object[] { subtype });
                return true;
            }
            return false;
        }

        internal string PatternValue => pattern;
        internal string FinishValue => Finish;
        internal float TileSizeValue => tileSize;
        internal float ThicknessValue => thickness;
        internal bool TileSheddingValue { get => tileShedding; set { tileShedding = value; tileSheddingInit = true; } }

        // One summary line per heating pass, written after 10 s without absorption.
        private void TrackReentry(double blocked, double skin, double skinMax)
        {
            if (absorbed > 0)
            {
                if (!heating)
                {
                    heating = true;
                    ablatorAtStart = ablator.amount;
                    peakSkin = peakBlocked = peakGlowTemp = peakGlowShown = 0;
                    energyAtStart = energyAbsorbed;
                }
                quietTime = 0;
                peakGlowTemp = Math.Max(peakGlowTemp, hottestTile);
                peakGlowShown = Math.Max(peakGlowShown, glowShownMax);
                peakSkin = Math.Max(peakSkin, skin);
                peakBlocked = Math.Max(peakBlocked, blocked);
                return;
            }
            if (!heating) return;
            peakGlowTemp = Math.Max(peakGlowTemp, hottestTile); // the tiles stay hot (and glowing) after the absorption ends
            peakGlowShown = Math.Max(peakGlowShown, glowShownMax);
            quietTime += TimeWarp.fixedDeltaTime;
            if (quietTime < 10) return;
            heating = false;
            Debug.Log($"[AblatorDecals] reentry summary {PartName} over {targets.Length} part(s): absorbed {energyAbsorbed - energyAtStart:F1} MJ, " +
                      $"hottest covered skin {peakSkin:F0}/{skinMax:F0} K, peak heat blocked {peakBlocked:P0}, " +
                      $"ablator used {ablatorAtStart - ablator.amount:F1} of {ablator.maxAmount:F1} u, {ablator.amount / ablator.maxAmount:P0} left, " +
                      $"peak glow temp={peakGlowTemp:F0} K, peak intensity={peakGlowShown:F2}");
        }
    }
}
