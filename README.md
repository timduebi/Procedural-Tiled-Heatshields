Language: **English** | [Deutsch](README.de.md)

<p align="center">
  <img src="media/banner.jpg" alt="Procedural Tiled Heatshield" width="100%" />
</p>

# Procedural Tiled Heatshield

**Procedural Tiled Heatshield** (`AblatorDecals`, **v1.0**) is a mod for **Kerbal Space Program 1.12.x**. It adds a single surface-attachable part (**Ablative Heat Tiles**, internal name `ablativeDecal`) that procedurally wraps thin 3D thermal protection tiles directly onto your craft's hull — inspired by Starship hexagons and Space Shuttle squares.

Thermally, the decal works as a real ablative heat shield: it absorbs windward convection heat from covered parts, consumes Ablator proportional to tile area and thickness, glows dynamically during reentry, and develops seamless wind-aligned reentry weathering streaks afterward — all while staying a single part.

> **A quick note from the author:** This is my very first KSP mod — I started it privately for my own playthrough after getting inspired by Matt Lowne's video on building custom heat shields with decals, created it with AI assistance, and decided to share it with the community. It is still **very much a work in progress**, so bugs and quirks can definitely happen — if you run into anything, please report it via [GitHub Issues](https://github.com/timduebi/Procedural-Tiled-Heatshields/issues)! It was built and tested primarily for **spaceplane parts** (Mk1/Mk2/Mk3, wings, flaps, bays, intakes, landing gear), where it works well. It works on other parts too (tanks, adapters, capsules, etc.), though non-spaceplane or unusual modded shapes may occasionally show visual artifacts.

---

## In-Game Screenshots

<p align="center">
  <img src="media/ingame_09_vab_editor_ui.jpg" alt="VAB Editor — Tile Window & Part Menu" width="100%" />
</p>

| | |
|---|---|
| ![Every Tank Size](media/ingame_06_tank_sizes_vab.jpg) | ![Delta Wing Belly](media/ingame_07_delta_wing_belly.jpg) |
| ![Tiled Belly on the Runway](media/ingame_08_belly_runway.jpg) | ![Post-Reentry Weathering in Orbit](media/ingame_10_weathered_orbit.jpg) |
| ![Shuttle Reentry](media/ingame_11_shuttle_reentry.jpg) | ![Full Reentry Glow](media/ingame_12_reentry_glow_top.jpg) |
| ![Weathered After Reentry](media/ingame_13_parachute_weathered.jpg) | ![Mk2 Spaceplane Reentry](media/ingame_03_reentry_plasma_horizon.jpg) |
| ![Reentry Glow Close-Up](media/ingame_04_reentry_glow_closeup.jpg) | ![Mk3 Shuttle in Orbit](media/ingame_05_shuttle_orbit_3quarter.jpg) |
| ![Mk3 Shuttle Tiled Belly](media/ingame_01_shuttle_belly_orbit.jpg) | ![Mk2 Spaceplane Over Kerbin](media/ingame_02_mk2_spaceplane_clouds.jpg) |

---|---|
| ![Mk2 Spaceplane Reentry](media/ingame_03_reentry_plasma_horizon.jpg) | ![Reentry Glow Close-Up](media/ingame_04_reentry_glow_closeup.jpg) |
| ![Mk3 Shuttle in Orbit](media/ingame_05_shuttle_orbit_3quarter.jpg) | ![Mk3 Shuttle Tiled Belly](media/ingame_01_shuttle_belly_orbit.jpg) |

<p align="center">
  <img src="media/ingame_02_mk2_spaceplane_clouds.jpg" alt="Mk2 Spaceplane Over Kerbin" width="85%" />
</p>

---

## Key Features & Showcase

### 6 PBR Tile Variants & Procedural Coverage
Choose between **Hexagonal** (Starship-style) and **Square** (Space Shuttle-style) grids in **Black**, **Plain Ceramic**, and **Brushed Stainless Steel** — with full PBR metallic, roughness, and normal maps (`TU/Metallic`, `Resurfaced/Standard`, or Stock shaders). Adjust wrap angle, covered height, tile size, and thickness interactively; tiles trim flush at part seams and keep hatches and windows clear.

| Tile Variants | Procedural Wrap & Height |
|---|---|
| ![All 6 Heatshield Variants](media/02_tile_variants.png) | ![Procedural Wrap Animation](media/gif_04_procedural_wrap.gif) |

### Animated Parts, Bays, Intakes & Landing Gear
Tiles automatically separate onto moving sub-bodies so they animate cleanly with **cargo bay & service bay doors**, **elevons & winglets**, **retractable landing gear**, and **shielded docking ports**, and conform around **surface-attached air intakes** without blocking intake openings.

| Cargo & Service Bays | Control Surfaces |
|---|---|
| ![Animated Cargo & Service Bays](media/gif_01_bays_opening.gif) | ![Animated Flaps & Winglets](media/gif_02_flaps_and_winglet.gif) |

| Landing Gear & Docking Ports | Air Intakes on Wings & Mk2 |
|---|---|
| ![Landing Gear & Shielded Docking Port](media/gif_03_gear_and_docking.gif) | ![Air Intakes](media/09_wing_and_mk2_intakes.png) |

### Dynamic Reentry Glow & Weathering Streaks
During reentry, windward tiles glow smoothly across part seams from deep crimson through molten orange to incandescent yellow-white. As peak plasma cools down, tiles reveal vessel-wide matte scorch and seamless wind-aligned reentry streaks — running nose-to-tail across flat delta wings and fanning around curved tank and fuselage flanks like Starship cross-flow (also previewable live via the VAB **Weathering** slider), plus optional reentry tile shedding repairable by Engineers on EVA.

| Reentry Thermal Glow | Weathering & Reentry Streaks (0% → 100%) |
|---|---|
| ![Reentry Thermal Glow](media/08_reentry_glow.png) | ![Tile Weathering 0% to 100%](media/gif_05_weathering.gif) |

### Cockpit Covers, Fuselages & Tanks
Pre-baked precision covers keep windows (and Mk2 RCS nozzles) clear on Mk1, Mk2, and Mk3 cockpits, while smooth-cylinder wrapping handles Mk2/Mk3 stacks and cylindrical tanks from `0.625 m` to `5.0 m`.

| Cockpits | Detail Cutouts & Seams |
|---|---|
| ![Cockpit Covers](media/01_cockpits.png) | ![Detail Highlights](media/07_detail_features.png) |

<details>
<summary><strong>More part showcases (Mk3 Fuselage, Mk2 Fuselage & Cylindrical Tank Sizes)</strong></summary>

![Mk3 Spaceplane Parts](media/03_mk3_fuselage.png)
![Mk2 Spaceplane Parts](media/04_mk2_fuselage.png)
![Cylindrical Tank Diameters](media/05_tank_sizes.png)

</details>

---

## Installation

1. Download the latest release ZIP from [SpaceDock](https://spacedock.info/mod/4884/Procedural%20Tiled%20Heatshield) or [GitHub Releases](https://github.com/timduebi/Procedural-Tiled-Heatshields/releases).
2. Copy the `GameData/AblatorDecals` folder into your KSP `GameData` directory.
3. **No dependencies required** (KSP 1.12.x standalone).

> **Optional integrations included:** [B9PartSwitch](https://github.com/blowfishpro/B9PartSwitch) (color-swatch switchers), **Community Tech Tree** (`heatManagementSystems`), **Deadly Reentry**, **FAR**, **VAB Organizer**, and **TexturesUnlimited / Resurfaced** (PBR shaders). Tested with Stock, Making History, ReStock/ReStockPlus, SSPX, and Near Future parts.

---

## Quick Usage

- **Where to find it:** Editor → **Thermal** category (**Ablative Heat Tiles**, unlocked with `supersonicFlight` in Science/Career).
- **Attach:** Surface-attach onto any fuselage, wing, cockpit, bay, or tank — one decal covers its host part plus directly attached control surfaces, air intakes, and retracted landing gear.
- **Customize (PAW):** Adjust **Pattern**, **Tile type**, **Tile size**, **Thickness (Ablator)**, **Max wrap angle**, **Covered height**, and **Reentry tile loss** (plus **Weathering**, **Missing tiles**, **Keep hatches/windows clear**, and **Shuffle tiles** under *Fine tuning*).
- **Craft-wide controls:** Click the hex-tile toolbar button in the VAB/SPH to **highlight all heat shields**, apply presets, or copy settings across the entire vessel in one undoable step.
- **Attaching parts onto tiles:** Radial parts (decouplers, gear, solar panels, RCS) automatically attach to the hull underneath and shift outward by tile thickness so they sit flush on top of the tiles without colliding.

---

## Documentation, Source & License

- **[SpaceDock Page](https://spacedock.info/mod/4884/Procedural%20Tiled%20Heatshield)** — downloads & CKAN metadata.
- **[KSP Forum Thread](https://forum.kerbalspaceprogram.com/topic/232194-112x-procedural-tiled-heatshield-v10-procedural-3d-thermal-protection-tiles-for-spaceplanes-more/)** — discussion, feedback & screenshots.
- **[Technical & Config Reference](docs/REFERENCE.md)** — custom textures, `ModuleAblativeDecal` config fields, thermal model, and build instructions.
- **[Changelog](CHANGELOG.md)** ([Deutsch](CHANGELOG.de.md)) — full version history.
- **[Architecture & Design Notes](docs/ARCHITECTURE.md)** — internal design documentation.
- **License:** [MIT License](LICENSE). Built independently using stock/PBR KSP shaders (no code or shaders taken from Conformal Decals).
