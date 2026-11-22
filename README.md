Language: **English** | [Deutsch](README.de.md)

# AblatorDecals (Ablative Heat Tiles)

AblatorDecals is a mod for Kerbal Space Program 1.12.5. The **Ablative Heat Tiles** part (`ablativeDecal`) attaches to the surface of a part like a sticker and places thin 3D thermal protection tiles directly onto its hull. Surface-attached landing gear, air intakes, and control surfaces on that part can receive their own tile shells, and tiles on moving parts animate with them. Patterns mimic Starship (hexagons) or Space Shuttle (squares). Thermally, the decal acts as an ablative heat shield: during reentry it absorbs convection heat from the covered parts and consumes Ablator, all while remaining a single part.

Version 1.8.0 has been verified offline with the full self-test suite and the preview renderer against Stock and ReStock part models. See [CHANGELOG.md](CHANGELOG.md) for version history and [docs/CLAUDE-HANDOFF.md](docs/CLAUDE-HANDOFF.md) for development handoff notes.

## Installation

1. Copy the `GameData/AblatorDecals` folder into your KSP installation's `GameData` directory.
2. ModuleManager is not required. Optional patches are included for [B9PartSwitch](https://github.com/blowfishpro/B9PartSwitch) (color-swatch switchers), **Community Tech Tree** (`heatManagementSystems`), **Deadly Reentry**, **FAR**, and **VAB Organizer**.

The part appears in the editor under the **Thermal** category (and in the Heat Shields subcategory if VAB Organizer is installed). In Science and Career modes, it unlocks alongside the Mk2 Cockpit (`supersonicFlight`, or `heatManagementSystems` with Community Tech Tree).

### Building from source

```powershell
dotnet build src/AblatorDecals -c Release
```

Building requires the KSP managed DLLs. The path to your KSP installation is configured via the `KSPDir` MSBuild property (defined in [AblatorDecals.csproj](src/AblatorDecals/AblatorDecals.csproj)). Override it on the command line:

```powershell
dotnet build src/AblatorDecals -c Release -p:KSPDir="C:\Path\to\KSP"
```

The build writes `AblatorDecals.dll` to `GameData/AblatorDecals/Plugins` and mirrors `GameData/AblatorDecals` into `<KSPDir>\GameData\AblatorDecals` if that `GameData` directory exists. Target framework is `.NET Framework 4.8`.

To build without copying into a KSP folder (only reading KSP's managed DLLs):

```powershell
dotnet build src/AblatorDecals -c Release -p:KSPDir="C:\nonexistent" -p:Managed="C:\Path\to\KSP\KSP_x64_Data\Managed"
```

## Editor Usage

Attach the decal to the surface of any part (surface-attach), including wings and control surfaces, and configure it via the Part Action Window (PAW). In-game labels come from [en-us.cfg](GameData/AblatorDecals/Localization/en-us.cfg) and [de-de.cfg](GameData/AblatorDecals/Localization/de-de.cfg).

PAW layout (top to bottom):

1. **Status lines** (ungrouped): in the editor, **Covered area** (m²), **Tiles**, **Parts covered**, **Ablator** (units), **Tile mass** (kg); in flight, airflow coverage and thermal protection metrics (see [In-Flight Readouts](#in-flight-readouts)).
2. **Tiles**: Pattern, Tile type, Tile size, Pattern angle, Joint width, Thickness (ablator).
3. **Area**: Coverage (Whole part / Limit area), Width and Height (only when set to Limit area), Max wrap angle, Covered height, Unroll, Edge.
4. **Fine tuning** (collapsed): Texture and Texture scale (only with Plain ceramic), Missing tiles, Shuffle tiles.

| Group | Setting | Options / Range | Default | Effect |
|---|---|---|---|---|
| Tiles | Pattern | Hexagons, Squares | Hexagons | Tile geometry and grid layout. |
| Tiles | Tile type | Black, Plain ceramic, NASA White, Steel | Black | Surface appearance. Texture is picked from Pattern + Tile type (Black = Starship / Shuttle HRSI, NASA White = Shuttle LRSI, Steel = Starship stainless, Plain ceramic = continuous ceramic). |
| Tiles | Tile size | 0.03 to 0.5 m | 0.1 m | Grid pitch including joint: flat-to-flat distance for hexagons, edge length for squares. |
| Tiles | Pattern angle | -90 to 90 deg | 0 | Rotates the pattern around the decal normal. |
| Tiles | Joint width | 0 to 0.02 m | 0 m | Width of joints between tiles. Existing saved decals retain their stored value. |
| Tiles | Thickness (ablator) | 0.005 to 0.3 m | 0.025 m | Tile thickness. Determines total Ablator capacity (`covered area × thickness × 1000` units/m³). |
| Tiles | Copy settings to all shields | Button | | Copies look, wrap, height, gap, weathering, and shedding settings to every other decal on the craft (preserving fixed sizes on pre-made cockpit covers). |
| Area | Coverage | Whole part / Limit area | Whole part | **Whole part**: tiles the entire host part within Max wrap angle of the decal normal, cleanly trimmed at part seams. **Limit area**: uses Width and Height. Saves from before 1.3 load as Limit area. |
| Area | Width | 0.2 to 60 m | 2 m | Footprint width (only in Limit area mode). |
| Area | Height | 0.2 to 60 m | 2 m | Footprint height (only in Limit area mode). |
| Area | Max wrap angle | 10 to 180 deg | 100 | How far tiles wrap around the hull (angle between surface normal and decal normal). |
| Area | Covered height | 1 to 100 % (1% steps) | 100 % | Fraction of the part's extent along the decal normal, measured from the decal side (100% = full part; 50% on the side of an Mk3 tank = up to half height). Straight cut, combined with Max wrap angle (stricter wins). |
| Area | Hull surface | Smooth cylinder / Exact mesh | Auto | Auto-enabled on round axisymmetric tanks/station modules (ReStock, ReStockPBR, SSPX, NFLV): bridges horizontal ribs smoothly and wraps protruding external pipes/ladders in a matching visual-only skin. Off on Mk2/Mk3/wings/hubs. |
| Area | Unroll | Along the hull / Seen from the front | Along the hull | How Width and Height are measured: wrapped along the hull like a sticker, or projected straight from the front (useful for nose cones). |
| Area | Edge | Whole tiles / Straight cut | Whole tiles | Behavior at the footprint boundary. |
| Fine tuning | Texture | Per tile / Across the tiles | Per tile | Only with Plain ceramic: per-tile UV crop or one texture mapped across all tiles. |
| Fine tuning | Texture scale | 0 to 20 m | 2 m | Only with Plain ceramic: width of one texture repeat. With "Across the tiles", 0 stretches the texture across the footprint. |
| Fine tuning | Missing tiles | 0 to 20 % | 0 | Percentage of tiles omitted purely for visual wear. |
| Fine tuning | Weathering | 0 to 100 % (5% steps) | 0 % | Base soot/char tint visible immediately in the editor and at launch (no mesh rebuild needed). |
| Fine tuning | Reentry tile shedding | Enabled / Disabled | Off (Sandbox), On (Science/Career) | Under heavy reentry heating or low Ablator, sheds up to 25% of tiles in 5% steps. Engineers can repair shed tiles and refill Ablator on EVA (consumes 1 stock EVA Repair Kit in Career). |
| Fine tuning | Keep hatches clear | Clear / Tile over | Clear (`Tile over` on `mk3CrewCabin`) | Controls whether `Airlock` hatches stay uncovered or are tiled over. |
| Fine tuning | Shuffle tiles | Button | | Picks a new random seed for atlas cells and missing tiles (also updates symmetry counterparts). |

### Pattern and Tile Type

- **Hexagons (Hex)**: Starship style. At 0° angle, a vertex points upward.
- **Squares (Square)**: Space Shuttle style, orthogonal grid.
- Staggered squares (Brick) are no longer offered for new decals. Existing saved decals that use Brick still load and display the option until changed.

| Tile type | with Hexagons | with Squares |
|---|---|---|
| Black | `HexBlack` (Starship) | `SquareBlack` (Shuttle HRSI) |
| Plain ceramic | `Ceramic` with `HexCeramicGlow` | `Ceramic` with `SquareCeramicGlow` |
| NASA White | `HexWhite` | `SquareWhite` (Shuttle LRSI) |
| Steel | `HexSteel` (Starship steel) | `SquareSteel` |

Changing the pattern preserves the current tile size.

### B9PartSwitch (optional)

When [B9PartSwitch](https://github.com/blowfishpro/B9PartSwitch) is installed (easily via CKAN; requires ModuleManager), [B9PartSwitch.cfg](GameData/AblatorDecals/Patches/B9PartSwitch.cfg) replaces the built-in Pattern and Tile type choosers with two B9 color-swatch switchers in the **Tiles** group. Without B9PartSwitch, the mod uses its own choosers and works standalone.

Note: B9PartSwitch stores its selected variant independently. Decals saved before installing B9PartSwitch will default to Hexagons / Black on first load with B9 and should be re-selected once.

### One Heat Shield per Part

The default mode is **Whole part**: the decal tiles its host part along with any control surfaces, air intakes, and retracted landing gear attached to it, up to **Max wrap angle** from the decal normal, trimmed cleanly at part seams. On a five-part fuselage, five decals sit seamlessly side by side.

A second decal on an already-covered part will not build tiles: it highlights red, shows **Area already covered** in the PAW, and posts a screen message. Two decals can only share a part if both are set to **Limit area**; the newer decal fills up to the older decal's tiles. A **Whole part** decal always takes precedence.

Finding decals on a vessel: hovering over a decal or keeping its PAW open displays an orange hexagonal ring at its attachment point, and keeping the PAW open highlights the whole decal. You can grab the decal by that ring. In the editor, tiles are clickable by their exact mesh shape.

### Max Wrap Angle

Tiles flood outward across the hull from the decal center until reaching the wrap angle limit, a sharp surface bend, or a part boundary. At 180°, tiles wrap completely around a closed tank (within Width and Height if limited) without ever overlapping or double-covering: candidate tiles are discarded if a tile already exists at the same location on the same side.

### Which Parts a Decal Covers

A decal covers the part it is attached to, plus any control surfaces (`ModuleControlSurface`, `ModuleAeroSurface`), air intakes (`ModuleResourceIntake`), and retracted landing gear attached directly to that part. Stacked neighbor parts and main wings are not covered automatically—place a separate decal on each part. (The legacy "Span parts" toggle was removed in 1.4; older saves still load and ignore the stored flag.)

**Keep-out zones:** Hatches (colliders tagged `Airlock`, plus 5 cm margin) remain uncovered on all parts. RCS nozzles (`ModuleRCS`/`ModuleRCSFX`, a box of half-extent `rcsKeepOut` = 0.12 m around each nozzle) remain uncovered only on cockpits whose entry in [CockpitCover.cfg](GameData/AblatorDecals/CockpitCover.cfg) sets `keepRcs = true` (e.g., Mk2 yes, Mk3 no); elsewhere tiles cover over them. Tiles are cut cleanly along keep-out boundaries.

### Control Surfaces

On parts with `ModuleControlSurface` (elevons, rudders, flaps), the movable flap is treated as its own body: no tile bridges the hinge line, and the flap's tiles attach to the moving flap transform in flight so they deflect with it. Meshes are always built in the neutral pose (even if deployed in the editor). Thermally, the flap and fixed base belong to the same part.

### Attaching Parts onto the Tiles

Nothing attaches directly to the decal part itself (`attachRules = 0,1,0,0,1`). Whenever you hold another part with the mouse (from the part list, picked off the vessel, Alt-copied, or as a subassembly)—such as a radial decoupler, landing gear, solar panel, or RCS thruster—all decals temporarily disable their tile and ring colliders so the new part attaches to the underlying hull just as if no decal were there. If the attachment point lies within the tiled area, the part (and its symmetry counterparts) is shifted outward by the tile thickness so it sits flush on top of the tiles. Attached parts do not receive tiles from the parent decal (except retracted landing gear, see below), never collide with the tiles in flight (even after staging or docking, including wheel colliders), and share the thermal protection of the part they sit on. The tiles themselves can contact the ground. Parts attached directly to decals in older saves continue to load.

You cannot attach a decal on top of another decal: hovering over existing tiles keeps the placement preview red. If two decals overlap after placement, the newer one yields and omits tiles where the older decal already placed them.

### Air Intakes and Landing Gear

Air intakes surface-attached to a tiled part are tiled on their exterior; the intake opening and front duct walls remain clear so airflow and intake function are unaffected (intakes without an intake transform receive no tiles). Landing gear is never treated as a fuselage obstruction: hull tiles run cleanly underneath the gear mount, and wheels pass through freely when extending. In addition, landing gear inside the tiled region receives its own tile shell over its retracted exterior (nacelles, doors, bay covers, including skinned meshes) that moves with the gear animation, giving a seamless tiled underside when retracted. Surfaces facing the hull, embedded more than 2 cm into the hull, or concealed when retracted (wheels, struts, bay interiors) remain uncovered.

### Cockpit Shape

On cockpits defined in [CockpitCover.cfg](GameData/AblatorDecals/CockpitCover.cfg) (`Mark1Cockpit`, `mk2Cockpit_Standard`, `mk3Cockpit_Shuttle`), the **Area** group shows the **Cockpit shape** toggle (enabled by default). When enabled, the decal covers the entire cockpit up to just below the windows using pre-baked cover meshes (`Covers/<part>_<Hex|Square>.bin`, with Max wrap angle = 180° and no neighbor parts covered). When disabled, it behaves like a normal decal.

### Apply to All Heat Shields

The hex-tile toolbar button in the editor opens a small window for **Pattern**, **Tile type**, **Tile size**, and **Thickness**. Clicking **Apply to all heat shields** applies those four settings to every decal on the vessel (including via B9PartSwitch switchers when installed) in a single undoable step. Individual decals can still be tweaked afterward.

### Edge

- **Whole tiles**: Only complete tiles are kept along the footprint boundary, producing a stepped/jagged edge.
- **Straight cut**: Border tiles are cleanly sliced straight along the footprint boundary.

Regardless of this setting, tiles always trim cleanly at part edges and seams instead of overhanging.

### Missing Tiles

Randomly omits a percentage of tiles for a weathered look. Neighboring tiles keep their side walls and dark joint sheets remain intact. Omitted tiles do not count toward covered area, mass, Ablator, or thermal protection.

### Additional Usage Notes

- Geometry updates in the editor run asynchronously on a background worker thread so dragging sliders stays smooth without frame hitches.
- Before attaching a decal to a part, a flat preview patch is shown so you can inspect the pattern and tile type.
- Symmetry: All settings (including Tile type and Unroll) propagate to symmetry counterparts. Under mirror symmetry, the counterpart receives the negated Pattern angle.
- Mass and cost scale with covered area (default 1.5 kg and 20 funds per m², plus the base part).

## In-Flight Readouts

In flight, the decal PAW displays:

| Readout | Meaning |
|---|---|
| Facing airflow | Fraction of tile area facing the airstream (sum of `tile area × cos(angle to airflow)` divided by total tile area). |
| Heat blocked | Fraction of the covered parts' convection heat currently absorbed by the decal (shows part count when covering multiple parts). |
| Absorbing | Instantaneous thermal power absorbed in kW. |
| Energy absorbed | Cumulative thermal energy absorbed in MJ. |
| Hottest covered skin | Skin temperature of the hottest covered part relative to its maximum temperature (`current / max K`). |
| Tile temperature | Computed surface temperature of the hottest tile group (visual only, see below). |
| Covered area, Parts covered | Same as in the editor. |

## Thermal & Glow Model (Summary)

- The decal protects only parts that have tiles on them, proportional to the tile area facing the airflow: for each covered part, `sum(tile area × max(0, dot(normal, airflow))) / part.exposedArea`, capped at 100% across all decals on that part.
- Protection engages once the part's skin temperature exceeds `ablationTempThresh` (default 500 K), removing that fraction of `thermalConvectionFlux` (scaled by `efficiency`). Solar and planetary radiation are untouched.
- Absorbed energy consumes Ablator from the decal's shared pool. With default values, 1 unit absorbs 2400 kJ (`pyrolysisLossFactor × specific heat × density`). Once depleted, protection stops; if partially depleted during a step, all covered parts receive the same fraction.
- Ablator capacity is computed in the editor from `coveredArea × thickness × unitsPerCubicMeter` and is never reset in flight.
- **Glow** is purely visual. Tiles are grouped by normal direction (`glowBinAngle` = 18° bins, `glowMaxBins` = 12) and share a vessel/decal-averaged skin temperature and convection coefficient so glow never steps across part seams. Temperature integrates over time (`glowHeatCapacity`, `glowEmissivity`, `glowConduction`) and fades smoothly (`glowFadeTime` = 3 s). Using `KSP/Emissive/Specular`, per-tile emission atlases (`HexGlow`, `SquareGlow`, `HexCeramicGlow`, `SquareCeramicGlow`, plus glowing joints via `KSP/Emissive/Diffuse`) are tinted by `_EmissiveColor` along `glowRamp` (no glow below 550 K, orange at 800 K, white-yellow core at 1300 K+, and local airflow weighting via `TileGlow.LocalWeight`). Without the emissive shader, it falls back to `_TemperatureColor`. KSP's own part glow (`MaterialPropertyBlock`) is kept off tile renderers.
- **Charring** shifts tile color toward `charColor` as Ablator is consumed.
- If the parent part is destroyed, the decal is destroyed with it. If another covered part (such as a flap or intake) separates or is destroyed, the tile mesh rebuilds without it.

## Custom Textures

The easiest way is to replace one of the included PNGs. To add a new texture:

1. Place a PNG file in `GameData/AblatorDecals/Textures/`.
2. Add an `ABLATORDECAL_TEXTURE` config node specifying `shape` and `finish` (see [Textures.cfg](GameData/AblatorDecals/Textures/Textures.cfg); can also live in your own `.cfg`):

```
ABLATORDECAL_TEXTURE
{
	texture = AblatorDecals/Textures/HexBlack
	title = #LOC_AblatorDecals_tex_hexblack
	shape = Hex
	finish = Black
	cells = 4, 4
	randomRotation = True
}
```

| Field | Meaning |
|---|---|
| `texture` | Texture URL without extension, relative to `GameData`. |
| `title` | Display name or localization key (descriptive). Falls back to filename. |
| `shape` | `Hex`, `Square`, or `Any` (`Square` also applies to staggered squares). Default: `Any`. |
| `finish` | Target tile type: `Black` or `Ceramic`. The first matching entry (exact `shape` before `Any`) is chosen. |
| `cells` | `cols, rows` of a tile atlas. Omit for a plain repeating texture (enables Texture / Texture scale options). |
| `emissive` | Optional emission atlas in the same layout for thermal glow (see `tools/make-glow-textures.ps1`). Entries referenced as `emissive` are excluded from diffuse selection. |
| `plainOptions` | Allows per-tile / across-tiles projection and Texture scale controls even when an atlas layout is used for emission. |
| `randomRotation` | Atlas only: randomly rotates cell UVs by multiples of 60° (Hex), 90° (Square), or 180° (Brick). |

**Atlas layout:** A texture with `cells = c, r` is split into `c × r` equal cells. Each tile picks a cell deterministically from its grid coordinates and decal seed. Hex cells fill a pointy-top hexagon inscribed in the cell; Square cells fill the entire cell. UVs are unmirrored when viewed from outside.

**Alpha = Gloss:** `KSP/Specular` / `KSP/Emissive/Specular` uses the alpha channel as the specular gloss mask (higher alpha = glossier), never as transparency.

The included atlases (`HexBlack.png`, `SquareBlack.png`, 1024×1024 px, 4×4 cells) are generated deterministically by [tools/make-tile-atlases.ps1](tools/make-tile-atlases.ps1); `Ceramic.png` by [tools/make-ceramic-texture.ps1](tools/make-ceramic-texture.ps1); glow atlases by [tools/make-glow-textures.ps1](tools/make-glow-textures.ps1); and `Assets/placeholder.mu` by [tools/make-placeholder-mu.ps1](tools/make-placeholder-mu.ps1).

## Config Reference

These fields belong to `MODULE { name = ModuleAblativeDecal }` (not shown in the PAW) and can be edited in [ablativeDecal.cfg](GameData/AblatorDecals/Parts/ablativeDecal.cfg) or patched with ModuleManager.

| Field | Default | Meaning |
|---|---|---|
| `ablativeResource` | `Ablator` | Resource consumed while absorbing heat. |
| `unitsPerCubicMeter` | `1000` | Ablator units per m³ of tile volume (`area × thickness`). |
| `pyrolysisLossFactor` | `6000` | Same as stock `ModuleAblator`. Multiplied by resource specific heat and density to get energy per unit. |
| `ablationTempThresh` | `500` | Target part skin temperature (K) above which ablation begins. |
| `efficiency` | `1` | Fraction of covered convection flux absorbed (tuning multiplier). |
| `shieldNormal` | `0, 1, 0` (in cfg: `0, -1, 0`) | Outward normal in part space (`node_attach` points into parent, so cfg uses `0, -1, 0`). |
| `logInterval` | `0` | Seconds between thermal debug log lines (`0` = off). |
| `textureFolder` | `AblatorDecals/Textures/` | Folder scanned for textures. |
| `shader` | `KSP/Emissive/Specular` | Tile shader (falls back to `KSP/Diffuse` if missing). |
| `specColor` | `0.25, 0.25, 0.25` | Material `_SpecColor` (RGB). |
| `shininess` | `0.35` | Material `_Shininess`. |
| `charColor` | `0.42, 0.37, 0.34` | Tile color when Ablator is depleted (RGB). |
| `glowEnabled` | `True` | Enables/disables visual reentry glow. |
| `glowEmissivity` | `0.95` | Surface emissivity used in the glow thermal balance (higher = cooler tiles). |
| `glowHeatCapacity` | `30` | Thermal inertia of tile surface in kJ/m²K (30 gives ~20–40 s lag on reentry). |
| `glowConduction` | `0.02` | Thermal conduction to hull in kW/m²K. |
| `glowRamp` | `550 1 0.5 0.14 0; 800 1 0.5 0.14 0.55; 1300 1 1 1 1.2; ...` | Temperature-to-glow stops `K r g b strength` with smoothstep interpolation; multiplied into `_EmissiveColor` × emissive texture. |
| `glowBinAngle` | `18` | Angular bin size (deg) for grouping tile normals for glow. |
| `glowMaxBins` | `12` | Maximum glow bins per tile mesh. |
| `glowFlatColor` | `1, 0.75, 0.45` | Fallback emissive tint for textures without an `emissive` atlas. |
| `glowFadeTime` | `3` | Time constant (s) for smoothing glow color changes (`0` = instant). |
| `rcsKeepOut` | `0.12` | Half-extent (m) of keep-out cube around RCS nozzles on cockpits with `keepRcs = true`. |
| `massPerArea` | `0.0015` | Tile mass per m² in tonnes. |
| `costPerArea` | `20` | Cost per m² of covered area. |
| `surfaceOffset` | `0.003` | Offset above hull surface in m (prevents z-fighting). |
| `cornerBendAngle` | `35` | Max smoothed surface bend angle (deg) tiles will cross. |
| `faceBendAngle` | `60` | Max individual face bend angle (deg). |
| `curvatureFactor` | `1` | Minimum bend radius (in tile sizes) tiles will follow. Higher is stricter. |
| `maxTiles` | `60000` | Maximum tiles per decal (logs a warning if reached). |
| `colliderPatchSize` | `1` | Size (m) of box collider patches for editor clicking. |
| `gearMargin` | `0.3` | Margin (m) around landing gear/wheels where tile colliders are omitted (tiles stay visible). |

## In-Game Test Checklist

This mod was verified offline via automated self-tests and the software preview renderer. When testing in-game, check `KSP.log` for `[AblatorDecals]` lines (`build ...`, `flight check ...`, `reentry summary ...`) plus any warnings or errors:

1. **Startup & Part List:** Open the VAB/SPH. "Ablative Heat Tiles" appears in **Thermal** with the 7-hex icon, localized labels, and no raw `#LOC_...` keys.
2. **Attachment:** Attach a decal to a tank. Tiles appear immediately, textured, dark backsides, default 0.1 m size. Check `[AblatorDecals] build Editor ...` (`shader=KSP/Emissive/Specular`, `glow=_EmissiveColor`, matching `emissive=...`).
3. **PAW Controls:** Check status readouts, **Tiles**, **Area**, and collapsed **Fine tuning** groups. Verify **Texture** and **Texture scale** only show with **Plain ceramic**.
4. **Patterns & Finishes:** Cycle Hexagons/Squares with Black/Plain ceramic (with and without B9PartSwitch if installed).
5. **Smooth Sliders:** Drag Tile size (down to 0.03 m), Width, Height, Joint width, Pattern angle, and Thickness without editor stutter or PAW flicker.
6. **Unroll & Wrap:** Test "Along the hull" vs "Seen from the front" on an Mk3 fuselage, and 180° wrap around a cylindrical tank (no overlapping tiles or hangs).
7. **Seams & Attachments:** Stack parts and place decals per part; attach control surfaces and air intakes directly to a tiled part and verify they are tiled cleanly.
8. **Attaching Parts on Tiles:** Place radial decouplers, landing gear, solar panels, and RCS thrusters onto tiled areas (from part list, picked up, Alt-copied, and with symmetry). Confirm they sit flush on top of the tiles and decouple/operate cleanly in flight.
9. **Control Surfaces in Flight:** Deflect elevons/rudders in flight; flap tiles deflect with the flap while wing tiles stay fixed.
10. **Cockpit Shape & Pre-made Covers:** Place decals on Mk1, Mk2, and Mk3 cockpits; verify smooth window/chin borders, Mk2 RCS cutouts, and clean seams to adjacent fuselage parts.
11. **Landing Gear & Shielded Docking Port:** Retract/extend gear (tiles animate with doors/nacelles; wheels unobstructed) and open/close a shielded docking port (no tiles under closed hood, tip closed).
12. **Cargo Bay & Ramp:** Open/close Mk2/Mk3 cargo bays and Mk3 cargo ramp in editor and flight; door tiles move with the doors.
13. **Reentry & Glow:** Reenter nose/belly-first; verify Ablator consumption, charring, smooth multi-stage glow transitions across part seams (`glow=_EmissiveColor`), and `reentry summary` log output.
14. **Staging & Editor Undo/Redo:** Stage covered parts in flight, detach/reattach/Alt-copy decals in the editor, and verify zero `NullReferenceException` / `SetOpacity` errors in `KSP.log`.

## Known Limitations

- **Verified offline only.** Geometry, thermal math, and glow integration are verified in `tests/SelfCheck`, visuals in `tools/Preview`, and KSP API usage against decompiled KSP assemblies. Live in-game behavior—especially lighting, render mods (e.g., Deferred, Scatterer, TexturesUnlimited), stage separation, and large-area performance—still needs in-game testing.
- **Parts placed on tiles** attach to the underlying hull and are offset outward once. Moving the decal afterward leaves those parts where they were until re-attached.
- **Cockpit shape** planes/regions in [CockpitCover.cfg](GameData/AblatorDecals/CockpitCover.cfg) target Stock (and ReStock, which uses the same Squad cockpit models). Other cockpit mods require their own `:NEEDS[...]` cover entries.
- **Control surfaces**: In flight, the moving flap receives its own rotating collider box, while editor click collider patches spanning the hinge remain in neutral pose.
- **Skinned or animated parent meshes** are not supported (except retracted landing gear). Meshes marked non-readable (`isReadable = false`) are skipped with a log warning.
- Tiles are flat and rigid: on tightly curved hulls they are individually trimmed, and small gaps may remain at extreme creases.
- Parts exceeding `maxTiles` (default 60,000) stop adding tiles and log a warning.
- In flight, tile meshes only rebuild on startup, vessel modification (staging/part loss), reentry tile shedding, or Engineer EVA repair.
- Thermal protection is an approximation tuned via `efficiency` and `unitsPerCubicMeter` rather than a duplicate of stock heat shield drag-cube occlusion.
- **License:** Licensed under the [MIT License](LICENSE). No code or shaders were taken from Conformal Decals; all tile geometry is generated independently using stock KSP shaders.

## Files & Development

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — technical architecture and KSP API notes (German)
- [docs/DESIGN.md](docs/DESIGN.md) — core specification (German)
- [CHANGELOG.md](CHANGELOG.md) — release notes ([Deutsch](CHANGELOG.de.md))
- Self-test suite: `dotnet run --project tests/SelfCheck` (ends with `SelfCheck OK`)
- Offline software renderer: `tools/Preview` (renders `.craft` files and KSP `.mu` models to PNG)
