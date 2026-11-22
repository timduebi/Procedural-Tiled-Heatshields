Language: **English** | [Deutsch](CHANGELOG.de.md)

# Changelog

## 1.9.1 (2026-11-22)

- **FreeIva Compatibility (`mk3CrewCabin` & `Large_Crewed_Lab`):** Unattached stack nodes now only generate stack cut planes when they are true axial end nodes (`|dir.y| > 0.7`, excluding `*airlock*`/`*hatch*` nodes via `TileBuilder.FreeNodeCuts`). Fixes FreeIva's radial hatch nodes (`node_stack_front`/`back`, `node_stack_airlockL`/`R`) slicing off both faces and the door of `mk3CrewCabin` and breaking `Large_Crewed_Lab` into a flat preview. Also added a `Reseed()` fallback when a decal attaches directly on top of a window/hatch keep-out.
- **Long 5 m Tank Axis Lock (`Size4_Tank_04`):** Added `SmoothAxisLock` so tiles on long smooth-cylinder tanks align strictly to the part's axis without tangent-frame drift across 15 m weld ripples (eliminating diagonal collision artifacts at non-zero pattern angles).
- **PAW Cleanup:** Completely removed all UI attributes and handlers from `areaLimited`, `width`, `height`, `unrolled`, and `straightBorder` (kept as plain persistent fields for legacy saves), and updated `cockpitShape_off` from `"Width and height"` to `"Whole part"`.
- **Capsule & Cockpit Refinements:** Removed `keepRcs = true` from `mk1-3pod` (RCS nozzles now tiled cleanly with no holes). Removed the bespoke `mk2Cockpit_Inline` cover and deepened its `WindowKeepOuts.cfg` panes so all 4 canopy windows stay clear under normal tiling.

## 1.9.0 (2026-11-22)

- **One-Click "Tile whole belly" in Editor Window (`DecalWindow`):** Added belly direction selector (`Auto`, `-Y`, `+Z`, `-Z`, `+X`, `-X`) and **Tile whole belly** button that automatically attaches decals to every uncovered belly-facing fuselage, tank, bay, cockpit, and capsule on the vessel (applying the active pattern, finish, and tile size with single-step Ctrl+Z undo).
- **Docking Ports & HeatShield Transitions (`Patches/SurfaceAttach.cfg`):** Enabled surface-attaching decals onto `dockingPortLarge`, `dockingPort1`, `HeatShield0/1/1p5/2/3`, `restock-heatshield-1875-1`, `InflatableHeatShield`, `InflatableAirlock`, `restock-airlock-1`, and `docking-nose-0625-1`. Lowered smooth-cylinder minimum height from `0.25 m` to `0.10 m` so thin cylindrical docking rings and heatshield rims wrap cleanly.
- **Window Audit & Protection (`WindowKeepOuts.cfg`):** Audited all 70 crewed parts across Stock, Making History, ReStock, ReStockPlus, SSPX, and Near Future Spacecraft and added **60 parts / 313 cleaned window keep-outs** (`Keep windows clear` toggle in **Fine tuning**), with automatic recessed-window depth padding (`TileBuilder.WindowKeepHalf`) so windows recessed inside smooth cylinders (`crewCabin`, `Large_Crewed_Lab`, `sspx-aquaculture-375-1`, etc.) stay crisp and unobstructed.
- **Capsule & Lander Covers Shield Only the Windward Belly (`CockpitCover.cfg`):** 26 covers across Stock, Making History, ReStock, ReStockPlus, and Near Future Spacecraft, designed like the Mk1/Mk2/Mk3 cockpits instead of wrapping the whole capsule: windows, hatches, and the leeward upper hull stay clean and white. Conical reentry pods (`mk1pod_v2`, `mk1-3pod`, `Mk2Pod`, `restock-mk2-pod`, `command-25-1`, `command-mk3-9`) get 3D tiles on the whole lower skirt plus the `+Z` belly, with a smooth line sweeping up the sides behind the windows (`REGION`), and axis-referenced smooth ceramic rims (new cover key `ceramicAxis = true`). Inline cockpits (`Mark2Cockpit`, `mk2Cockpit_Inline`) shield only the belly half (`z >= 0.05`). Spheres, landers, and the cupola get a thin smooth ceramic coat (`ceramicAll = true`) only on the lower reentry bowl (`kv1Pod`–`kv3Pod`, `restock-pod-sphere-1..3`), the collar below the glass dome (`cupola`), or the side away from the windows (`landerCabinSmall`, `mk2LanderCabin_v2`, `MEMLander`, Near Future landers and utility pod).
- **Sleek Ceramic Coat (`ceramicAll`):** The all-ceramic shell now sits 5 mm above the hull instead of 25 mm, so handrails, trims, and corners no longer balloon into blobs. It never places tiles, and flat stack bottoms stay bare.
- **No More Tiled Stack Faces & Docking Faces:** In whole-part mode every stack node now gets a cut, even when nothing is attached, so flat end bulkheads, docking-port faces (`dockingPortLarge`, `dockingPort1/2/3`, `docking-nose-0625-1`), and heat shield ends stay clean. Recessed end-face pockets (Mk2 end profiles) stay bare instead of getting jagged tiles. The smooth cylinder drops flat caps and end flanges entirely, and smooth-hull parts get the dark joint bed under their tiles, so no white shows through seams.
- **Smooth Cylinder on Station Labs:** Smooth-hull auto-detection now accepts round parts whose recessed windows or raised frames move only a few sectors (`Large_Crewed_Lab`). Mk3-like flat bellies are still rejected. The window frames and handrails get the protrusion skin, and only the window panes stay cut out.
- **Tighter Window & Hatch Cutouts:** Window keep-outs get a 5 mm in-plane frame (was 15 mm). `--scan-windows` merges panes only within 2 cm (was 15 cm), so `mk1-3pod` gets its four real panes instead of one 80 cm box (`Mk2Pod` and `mk2LanderCabin_v2` refreshed too). Hatch keep-outs reach at least 14 cm deep (`TileBuilder.HatchKeepHalf`), so recessed hatch doors are cut free, not only their frames.
- **Radial Service Bays (`ServiceBay_125_v2`, `ServiceBay_250_v2`, `service-tank-*`):** Filtered out interior hinge cylinders and inner door thickness faces (`TileBuilder.DoorOuterFace`) and wrapped all four moving bay doors in a continuous outer skin (`DoorSkin`) that animates with the doors and follows the decal's wrap and stack cuts. The bay frame gets the same closed outer skin, and skin edges flare out at the foot to close hairline seams, so no white patches or slits show between doors and frame.
- **Reentry Glow & Charring Overhaul + Sandbox Support:**
  - **Faster post-reentry cooldown & smoother onset:** Lowered `glowHeatCapacity` (`30 -> 10`), raised `glowConduction` (`0.02 -> 0.08`), reduced `glowFadeTime` (`3 -> 1.5 s`), raised glow onset to `720 K` with smoothstep color transitions (deep cherry red -> molten orange -> gold -> yellow-white), and widened directional `LocalWeight` (`0.15..1.25`) so noses and leading edges glow first.
  - **Visible finish-aware charring in ALL modes (including Sandbox):** Boosted charring response (`Pow(used * 3.0 * dirFactor, 0.65)`) with matte `_SpecColor` fade — oxidized matte ash-brown scorch on **Black** tiles and dark smoky charcoal soot on **White**, **Steel**, and **Ceramic**. Charring now works in Sandbox, Science, and Career; **EVA Repair** is hidden in Sandbox (Science & Career only).
- **UI & Alt-Copy Fixes:** Hid Width, Height, Coverage (`areaLimited`), Unroll, and Edge controls in the PAW (fields remain persistent for legacy saves). Fixed Alt-copying a decal onto `mk3CrewCabin` retaining `keepHatches = true` from another part (`initParent` resets parent defaults when switching parent part type).

## 1.8.0 (2026-11-22)

- **New Tile Finishes (`NASA White` & `Steel`):** Added **White** (NASA LRSI off-white ceramic tiles, `HexWhite` / `SquareWhite`) and **Steel** (brushed stainless steel with subtle heat-tinted edges and specular sheen, `HexSteel` / `SquareSteel`) alongside **Black** and **Plain ceramic** in the PAW, Editor toolbar window, `Textures.cfg`, and `B9PartSwitch.cfg`.
- **Automatic Smooth Cylinder & Visual Pipe Overlay for Round Parts (ReStock, ReStockPBR, SSPX, NFLV):** Cylindrical and conical tanks/station modules with molded horizontal stiffening ribs or external vertical fuel pipes/ladders/handrails now automatically fit a smooth 64-segment surface-of-revolution envelope (`Hull surface: Smooth cylinder / Exact mesh` toggle in **Area**, auto-enabled on axisymmetric hulls and auto-disabled on flat-bellied/non-round hulls like Mk2, Mk3, wings, and hubs). Horizontal ribs and weld grooves are smoothly bridged (zero white stripes), while protruding external pipes and ladders receive a purely visual, untiled, finish-matched conformal skin overlay (like landing gear & intakes — glows and chars with the shield, zero colliders/mass/cost).
- **Round-Parts & Mod Compatibility:**
  - **Service Bays & Cargo Bays (`service-tank-375` / Near Future Spacecraft, etc.):** Fixed attaching a decal directly onto a bay door showing a floating flat preview rectangle (`no usable surface`). Added automatic closed-vs-open door pose verification and support for articulated multi-segment folding doors (`DoorA..DoorE`).
  - **TweakScale & B9PartSwitch Parent Mesh Watcher:** Automatically detects parent part scale changes (`TweakScale`) and mesh variant switches (`B9PartSwitch` on NFLV, SSPX, ReStock) and rebuilds the tile surface immediately.
  - **5 m & 7.5 m Tanks (`ReStock`, `ReStockPBR`, `NFLV`):** Expanded whole-part unrolled footprint to $\pi \cdot R_{\text{far}}$ so short wide 5 m / 7.5 m tanks wrap full 180° without clipping, raised default `maxTiles` from `40,000` to `60,000`, and enabled bounded whole-part island flooding (`FloodIslands`) in Exact-mesh mode.
- **Mk3 Passenger Module (`mk3CrewCabin`) & Hatch Toggle:** Added **Keep hatches clear** (`keepHatches`) toggle in **Fine tuning** — defaults to **Tile over** on `mk3CrewCabin` (so its bottom hatch/door and windows are tiled cleanly) and **Clear** on all other parts.
- **Editor Quality of Life:** Added one-click **Presets** (*Starship*, *Shuttle*, *Shuttle White*, *Delta Steel*) and **Highlight all heat shields** toggle to the Editor toolbar window (`DecalWindow`), plus **Copy settings to all shields** right in the PAW.
- **Weathering, Directional Soot Gradient, Thermal Status & Warnings:** Added **Weathering** slider (0–100%, live re-tint without mesh rebuild), per-bin directional reentry charring (windward tiles char up to ~2× darker than leeward ones), flight **Shield status** readout (*Nominal / Marginal / Ablator low! / Depleted!*), and one-shot screen warnings at <10% and 0% Ablator.
- **Optional Reentry Tile Shedding & Engineer EVA Repair:** Added **Reentry tile shedding** toggle (defaults **Disabled** in Sandbox, **Enabled** in Science & Career) that sheds up to 25% of tiles in 5% steps under extreme heating or low Ablator. Engineers on EVA can **Repair heat tiles (EVA)** to restore shed tiles and refill Ablator (free in Sandbox & Science; consumes 1 stock `evaRepairKit` in Career).
- **Mod Compatibility Patches & Release Tooling:** Added optional ModuleManager patches for **Community Tech Tree** (`heatManagementSystems`), **Deadly Reentry**, and **FAR** (`GeometryPartModule` exclusion). Added MIT `LICENSE`, moved internal design docs into `docs/`, and added `tools/package-release.ps1` for clean CKAN/Linux-compatible release ZIPs.

## 1.7.4 (2026-11-21)

- **Smooth black untiled ceramic on Mk1 & Mk2 cockpit nose tip and sides (`ceramicSideDeg`):** Mk2 (`58°`) and Mk1 (`50°`) cockpits now render a sleek, mask-clipped smooth matte black ceramic shell over the nose tip and both sides/chines (with per-normal reentry glow bins and flush stack-seam cuts), while keeping real 3D individual Hex/Square tiles on the belly and the upper nose ahead of the windshield. Mk3 remains 100% real 3D tiles. All Mk1 and Mk2 pre-baked cockpit covers rebaked.
- **Fixed false red glow when pitching up after reentry:** Convective cooling in cold air now acts across the boundary layer even in level flight (`TileGlow.EffectiveFacing`), radiative/conductive cooling no longer gets stuck at residual hull skin temperature, and directional airflow glow weighting (`TileGlow.BinTemp`) smoothly activates only while external air is hot enough to heat tiles (`> 550 K`), so pitching up at subsonic/transonic speeds after reentry never flashes the shield red.
- **~15× faster Hex ↔ Square switching on Mk2 parts:** Removed the legacy `Fill()` pass from non-overlay Mk2 hull parts (which took 1–2.1 s per part while `AddTankJointBed` already closes the hull in < 2 ms), cutting `mk2DockingPort` rebuilds from ~2,100 ms to ~128 ms and `mk2CrewCabin` from ~1,886 ms to ~139 ms. Also immediately cancels stale in-flight editor background jobs when a new switch is requested.

## 1.7.3 (2026-11-21)

- **Cockpits (Mk1 / Mk2 / Mk3) with clean 3D individual tiles:** Replaced the old cockpit border-fill pass (which dropped curved border tiles and produced flat black patches and holes) with exact contour clipping directly in tile placement plus a recessed dark joint underlay (`AddMaskJointBed`). Restricted nose caps (`CapShell`/`CurvedShell`) to true doubly-curved nose tips so 1D chines and belly faces on Mk2/Mk3 stay tiled with real 3D tiles. Fixed asymmetric nose-panel cutout on the Mk3 cockpit (`CockpitCover.cfg`). All six pre-baked cockpit covers (`Hex` and `Square`) rebaked (~3× smaller files, 100% mask coverage, 0 protruding tiles).
- **Part-seam gaps fixed:** Fixed inward cargo-bay / service-bay stack nodes (`top2`/`bottom2`, recessed 20–100 mm inside the outer bulkhead) cutting off exterior tiles and creating 40 mm gaps between stacked bays. Prevented shallow tapers from pulling cut planes behind the seam, kept smaller seam slivers at stack cuts, and disabled bevels on tiles right beside a stack seam so adjacent parts meet flush without V-grooves.
- **Performance & stability:** Reduced default `glowBinAngle` / `glowMaxBins` from `6°` / `48` to `18°` / `12` (and capped overlays at `24°` / `8`), cutting material and draw-call counts by ~4–10× per decal. Eliminated 50 Hz `FixedUpdate` GC allocations, added early returns when packed or cool in vacuum, and switched frontal-area summation to bin level. Upgraded `SurfaceIndex.CurvedShell` from $O(V^2)$ to an $O(V)$ spatial hash. Made editor background build jobs cancellable, fixed mirrored transform normals and leaked cloned materials in `TileHost`, cached localized labels and input lock state in `DecalWindow`, prevented `Apply to all` from overwriting fixed `tileSize`/`thickness` on pre-baked cockpit covers, and added KSP-AVC metadata (`AblatorDecals.version`).

## 1.7.2 (2026-11-21)

- Cockpit polish: grids now unwrap cleanly across sharp edges (Mk2 chine) instead of starting a second flood; border fill extends slightly underneath trimmed edge tiles (~1 cm strip, eliminating bright gaps), and untextured fill pieces are dark; all cockpits rebaked. Shielded docking port hood receives border fill and a closed tip. Preview renderer: undersides shaded dark like in-game.

## 1.7.1 (2026-11-21)

Following the 1.7.0 playtest. Verified offline (0 build warnings/errors, SelfCheck passing), renders in `preview/v15/`.

- **Smooth cockpit masks** (Mk1/Mk2/Mk3): tiles protruding more than 5 mm outside the mask are dropped; border fill closes flush along the mask contour (dark, without jagged steps), including across the Mk2 chine. Mk3: fixed the dark patch underneath the cockpit (caused by a large belly face misidentified as a nose cap). All six cockpits rebaked.
- **mk2DockingPort** is treated as a regular fuselage part again (shielded docking ports now only apply to the capped port with a single stack node).
- **Winglets/Flaps**: skin wraps up to 150° (no remaining bright edge), and a winglet spanning a seam to a neighbor part is tiled on both sides of the seam (stack cuts now apply only to doors).
- **Shielded docking port (capped)**: no tiles underneath the closed hood (base and doors treat each other as obstacles).
- **Glow**: reduced contrast between individual tiles (flatter emission textures), joints glow along with tiles (`KSP/Emissive/Diffuse`), and tiles glow hotter or cooler based on airflow incidence (nose, belly, leading edges hotter; smoothstep transition). → `preview/v15/glow.png`.

## 1.7.0 (2026-11-21)

Following the 1.6.2 playtest. Verified offline (0 build warnings/errors, SelfCheck passing), renders in `preview/v14/`.

- **Real individual tiles instead of projected skin** on fuselages, Mk2 parts, cargo bays, and cockpits; continuous skin remains only for landing gear, air intakes, winglets, and control surfaces. This also eliminates gaps at part seams (where projected skin lifted along rounded part ends and pulled back from seams).
- **Squares wrap as far as Hex** across the chine of Mk2 tanks and Mk2/Mk3 cargo bays: across sharp creases, tiles unwrap onto the adjacent face (Hex already crossed via diagonal steps).
- **Cockpits rebaked** (same masks): island floods no longer place tiles between tiles of the main flood (which caused Voronoi artifacts with Squares); they now maintain one cell spacing for uniform tiles.
- Gap filling restricted to borders (at most 1.5 tiles from a real tile).
- **Shielded Docking Port** (Clamp-O-Tron Shielded) on a tiled part: real tiles on the protective doors that animate with the port; docking face stays clear, base gets skin coverage.
- VAB Organizer: Heat Shields subcategory (`Patches/VABOrganizer.cfg`). Tech tree: `supersonicFlight` alongside the Mk2 cockpit, `entryCost = 4000`.
- Preview tool: `acc=1` (include tiles from previous decals to inspect seams), `--mudump <.mu>`.

## 1.6.2 (2026-11-20)

Following the 1.6.0/1.6.1 playtest. Verified offline (0 build warnings/errors, SelfCheck passing), renders in `preview/v13/`.

- **Closed skin on all Mk2 fuselage parts** (tanks, cabin, docking port, adapters; excluding cockpits, cargo bays, lander can): Squares reach as high as Hex, no ridge along the chine, no gaps at part seams. Interior surfaces (docking port tunnel) stay clear.
- **Improved skin mapping** (also cockpit covers, gear, intakes, fins): arc-length parameterization around the hull instead of angular (no stretched/squashed side tiles, no fan distortion at intake ends); flat parts (winglets, fins, flaps) projected straight through their thin axis (no zigzag edges). All six cockpits rebaked.
- **Flush part seams**: tiles cut at a plane or mask no longer have a bevel there; upper edges of adjacent parts meet without a V-groove (including cockpit-to-fuselage).
- **Landing gear**: side faces no longer dropped in sawtooth patterns (corner-based instead of center-based check); top faces (> 95° from decal normal) stay clear; gear on the opposite side of the part is excluded from the decal.
- **Glow**: banding in 1.6.0 came from per-bin glow (6° bins grouped greedily by normal) and per-part skin temperature; since 1.6.1 one shared value per craft drives all tiles, skins, and cockpit covers. Preview: `glow=<emissiveTexture> glowK=<K>`.
- Preview: `edgeB`/`edgeT` views (tile ends at stack faces), debug modes `gearWrap`, `gearDebug`.

## 1.6.1 (2026-11-20)

- Mk3 cockpit reworked: nose cap with angular chin plus dark side panel step behind the hatch; belly tiled, upper fuselage clear. All six cockpits rebaked (100% mask coverage).
- Mk1/Mk2: stack-face cut plane shifted slightly back (removes stray strip along front rim).
- Stronger peak glow: added stops at 1700 K (white × 1.65) and 2000 K (white × 2.8).

## 1.6.0 (2026-11-20)

- Pre-baked cockpit covers for Mk1 C, Mk2 B (side line raised 12 cm), and Mk3 B. All three support Hex and Square as well as attached landing gear.
- Continuous tiled shells for landing gear and air intakes; landing light reflector face stays clear. Flaps follow the covered side.
- Gap filling at cockpit masks and Mk2 tanks, plus dark joint bed closing tank sides up to the wrap limit for both patterns.
- Updated glow textures for Square/Black, Hex/Ceramic, and Square/Ceramic. Reentry summary logs peak tile temp and peak intensity.
- Measurements and reproduction steps in [docs/CLAUDE-HANDOFF.md](docs/CLAUDE-HANDOFF.md).

## 1.5.0 (2026-11-19)

Following the 1.4.0 playtest. Verified offline (clean build, SelfCheck passing, KSP APIs and shaders checked against game files).

### Module

- **Three-state glow**: cool (no glow below 550 K), medium (800 K: orange with darker edges), peak (1300 K: white-yellow core with orange edges), smoothstepped and time-damped. Uses `KSP/Emissive/Specular` (`_Emissive`, `_EmissiveColor`, plus `_TemperatureColor`) with per-pattern emission atlases (`Textures/HexGlow.png`, `SquareGlow.png`). Plain ceramic glows flat (`glowFlatColor`), falling back to `_TemperatureColor` without emissive shader support. Preview: `preview/v11/glow-states.png`.
- **Fixed glow banding**: narrowed bin angle from 20° to 6° (`glowMaxBins` = 48), averaged skin temperature and convection coefficient by tile area across the entire decal so seams never jump, and replaced the multi-plateau 5-stop ramp with smooth three-state transitions.
- **New slider "Covered height"** (Area, 1–100%, 1% steps, default 100%): clips along the decal normal as an additional straight cut plane combined with Max wrap angle.
- **RCS nozzles** now remain uncovered only on cockpits with `keepRcs = true` in `CockpitCover.cfg` (Mk2 yes, Mk3 no); elsewhere tiles cover them. Hatches (`Airlock`) remain uncovered everywhere.
- **Mk3 cargo bay / ramp**: in the editor, flap, bay door, and ramp tiles now attach directly to their animated transforms (without mesh colliders that would hijack clicks), moving properly when opened/closed. Dark tile backsides instead of white.
- **"tile materials ... restoring"**: identified as harmless KSP material cloning (`EditorLogic.highlightSelected` and `MaterialColorUpdater.CreateRendererList`); the material guard restores custom materials and emission textures automatically.
- Updated `build` and `flight check` log formats (`glow=...`, `emissive=...`).
- `[assembly: KSPAssembly("AblatorDecals", 1, 5)]`, Version 1.5.0; en/de localization updated.

### Geometry

- **Dedicated overlays for attached parts** (control surfaces, tailfins/winglets, air intakes, cargo bay doors, Mk3 cargo ramp, retracted landing gear): main decal tiles only the host part while each attached part gets its own deterministic exterior tile shell anchored to its own transform, independent of decal position/size/angle.
- **Main decal stops at attachment bases**: attached parts act as obstacles (tiles cut flush around their footprint), except retracted landing gear underneath which hull tiles continue.
- **Air intakes**: lip and rear exterior tiled; intake opening and front 35 cm duct walls left clear; inward-facing self-occluding faces skipped.
- Pure wings/strakes (`ModuleLiftingSurface` without control surface) do not get automatic overlays and use their own decals.
- **Cargo bay doors and Mk3 ramp** inherit Max wrap angle, Covered height, and stack cuts from the main decal in closed pose.
- **Cockpit masks (`REGION` in `CockpitCover.cfg`)**: closed contours on chart planes with smooth straight-cut edges and island flooding.
- **Pre-baked cockpit covers** (`Covers/<part>_<Hex|Square>.bin`, generated via `tools/Preview --bake-cover`), disabling fixed sliders ("Pre-made cover") while keeping finish/texture choices active.
- **Mk3 and Mk2 cockpits** redesigned to match Shuttle-style masks with rounded chin/window cutouts and round RCS cutouts (`keepRcs`).
- `SurfaceIndex.Raycast` splits long rays for full cargo-bay visibility checks.

## 1.4.0 (2026-11-16)

Following the 1.3.0 playtest. Verified offline.

### Fixed

- **Attaching parts onto tiles**: disabled tile and ring colliders whenever any non-decal part is held by the cursor (including Alt-copied parts and subassemblies) so parts attach cleanly to the hull underneath and shift outward by tile thickness.
- **Missing flaps and air intakes** in Whole part mode restored.
- **Landing gear coverage**: tiles cover the full retracted exterior shell of gear (including nacelles, doors, and skinned meshes like GearSmall), omitting only hull-facing/buried/internal wheel & strut faces.

### Added

- **Keep-out zones**: hatches (`Airlock` + 5 cm) and RCS nozzles (`rcsKeepOut` = 0.12 m) left clear (`TileSettings.KeepOut`).
- **Thickness in editor toolbar window**: "Apply to all heat shields" now sets thickness as well (single undo step; Ablator and mass update accordingly).
- **Smoothed glow fade**: `glowFadeTime` = 3 s time-damped transitions with smoothstep ramp interpolation.
- Cockpit shape support for `mk3Cockpit_Shuttle`.

### Changed

- **Removed "Span parts"**: decals cover their host part plus surface-attached flaps, intakes, and landing gear. Legacy saved value is loaded and ignored.
- **Much subtler glow ramp** and `glowEmissivity` 0.95; tile renderers excluded from `Part.temperatureRenderer` resets.
- **Default joint width 0 m** (existing saves keep their stored width).
- **Removed White tiles** (`HexWhite`, `SquareWhite`); saved White decals load as Black.
- `[assembly: KSPAssembly("AblatorDecals", 1, 4)]`, Version 1.4.0.

### Geometry

- Fixed Mk2 cargo bay tiling (stack planes on inward `top2`/`bottom2` nodes oriented outward from part center).
- Cargo bay doors (`ModuleCargoBay` animated transforms) tiled in closed pose and move with the doors in flight.
- Exact wrap-angle cut line on hull faces (snaps cleanly to sharp creases within ±20° such as Mk3 chines).
- Clean cuts around keep-out boxes, intake openings, and zero-gap seam fronts.

## 1.3.0 (2026-11-16)

Following the 1.2.0 playtest. Verified offline.

### Added

- **One heat shield per part**: new default **Whole part** coverage (Max wrap angle default 100°, clean cuts at part seams). Width/height shown only under **Limit area** (pre-1.3 saves load as Limit area).
- **Area already covered**: second decal on an occupied part highlights red with a status message; only two **Limit area** decals may share a part.
- **Decal handle ring**: hovering or opening the PAW shows an orange hex ring at the attach point; open PAW highlights the whole decal.
- Detailed gear and intake diagnostic logs.

### Changed

- Exact mesh colliders on tiles in the editor for accurate clicking (box colliders retained in flight), reliably re-enabled once cursor is empty.
- Suppresses cascade rebuilds while dragging parts.
- Slower, later yellow glow ramp and `glowHeatCapacity` = 30 kJ/m²K.
- `[assembly: KSPAssembly("AblatorDecals", 1, 3)]`, Version 1.3.0.

## 1.2.0 (2026-11-16)

Following the 1.1.0 playtest. Verified offline.

### Fixed

- **Fixed 6631 `NullReferenceException`s in `Part.SetOpacity`** in the editor by immediately unparenting destroyed hosts and rebuilding part/highlighter renderer caches.
- **Reentry artifacts**: destroyed the placeholder model on live parts instead of only hiding its renderer.

### Added

- **No decal on decal** (`attachRules = 0,1,0,0,1`); overlapping newer decals yield to older ones.
- **Parts attach on tiles cleanly** without triggering decal rebuilds and with flight collision ignored.
- **Landing gear** excluded from fuselage colliders (`gearMargin` = 0.3 m) while receiving its own retracted exterior tile shell that animates with doors/struts; parts sitting on tiles share thermal protection.
- **Air intakes and small control surfaces** supported across covered parts.
- **Cockpit shape** for Mk1 and Mk2 cockpits (`CockpitCover.cfg`).
- Editor toolbar button window for bulk editing Pattern, Tile type, and Tile size (`tools/make-app-icon.ps1`).

### Changed

- Custom yellower glow ramp (`glowRamp`); removed any ReStock dependency.
- `[assembly: KSPAssembly("AblatorDecals", 1, 2)]`, Version 1.2.0.

## 1.1.0 (2026-11-15)

Following the first 1.0 playtest. Verified offline.

### Added

- Parts attach on top of tiles without colliding in flight.
- **Control surfaces**: movable flaps split into their own body and deflect with `ModuleControlSurface` in flight.
- Wing/flap span support, **Tile type** (`Black`, `White`, `Plain ceramic`), **Unroll** ("Along the hull" vs "Seen from the front"), optional **B9PartSwitch** color swatches, and **Shuffle tiles**.

### Changed

- Reorganized PAW groups (**Tiles**, **Area**, **Fine tuning**), smaller default tile size (0.1 m), mitered crease edges (< 5 mm gap), straight cuts in unrolled mode, removed staggered squares from new choices, `[assembly: KSPAssembly("AblatorDecals", 1, 1)]`.

## 1.0.0 (2026-11-13)

Initial rewrite replacing prototype versions v1–v5. Verified offline.

### Added

- Rewritten planar rigid tile geometry (`TilePattern`, `TileBuilder`) with neighbor-clipped adaptive tiles, dark joint sheet, and beveled tops.
- Hexagons (Starship), Squares, and Staggered Squares (Shuttle) patterns with ±90° rotation, straight or whole-tile borders, and cosmetic missing tiles (0–20%).
- Position deduplication fixing runaway 180° wrap flooding.
- Multi-part stack coverage, cell atlases (`HexBlack`, `SquareBlack`, `SquareWhite`) and plain `Ceramic` texture, non-blocking worker-thread editor builds, visual thermal glow (`TileGlow`), charring, flight hosts attached to target parts, EN/DE localization, custom `placeholder.mu` icon, area-based mass/cost, and diagnostic logging.
