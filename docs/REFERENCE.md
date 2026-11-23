# Procedural Tiled Heatshield — Technical & Config Reference

This document holds the full configuration reference, custom texture specification, and developer notes for **Procedural Tiled Heatshield (`AblatorDecals`)**. For an overview and showcase, see [README.md](../README.md).

---

## Custom Textures

To replace or add tile finishes, place PNG textures in `GameData/AblatorDecals/Textures/` and define an `ABLATORDECAL_TEXTURE` node in `Textures.cfg` (or your own `.cfg` file):

```cfg
ABLATORDECAL_TEXTURE
{
	texture = AblatorDecals/Textures/HexBlack
	metallic = AblatorDecals/Textures/HexBlack_MET
	bump = AblatorDecals/Textures/HexBlack_NRM
	emissive = AblatorDecals/Textures/HexGlow
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
| `metallic` | Optional PBR Metallic-Smoothness atlas (`R` = Metallic, `A` = Smoothness). |
| `bump` | Optional tangent-space normal map (`*_NRM.png`). |
| `emissive` | Optional emission atlas in the same layout for per-tile thermal glow. |
| `title` | Display name or localization key. |
| `shape` | `Hex`, `Square`, or `Any`. Exact match wins over `Any`. |
| `finish` | Target finish (`Black`, `Ceramic`, `Steel`, or `White` if re-enabled). |
| `enabled` | Set `False` to hide a finish from editor pickers while keeping legacy saves compatible. |
| `cells` | `cols, rows` of the tile atlas (`4, 4` by default). |
| `randomRotation` | Randomly rotates tile UVs by multiples of 60° (Hex) or 90° (Square). |

> **Re-enabling NASA White tiles:** The legacy `HexWhite` and `SquareWhite` textures remain bundled but disabled by default. Set `enabled = True` in `GameData/AblatorDecals/Textures/Textures.cfg` (and uncomment the `White` `SUBTYPE` in `Patches/B9PartSwitch.cfg` if using B9PartSwitch) to re-enable them.

---

## Module Config Reference (`ModuleAblativeDecal`)

Fields configurable in `GameData/AblatorDecals/Parts/ablativeDecal.cfg` or via ModuleManager patches:

| Field | Default | Meaning |
|---|---|---|
| `ablativeResource` | `Ablator` | Resource consumed while absorbing convection heat. |
| `unitsPerCubicMeter` | `1000` | Ablator capacity per m³ of tile volume (`area × thickness`). |
| `pyrolysisLossFactor` | `6000` | Matches stock `ModuleAblator` energy calculation (~2400 kJ/unit). |
| `ablationTempThresh` | `500` | Part skin temperature (K) above which ablation begins. |
| `efficiency` | `1` | Fraction of covered convection flux absorbed. |
| `shieldNormal` | `0, -1, 0` (cfg) | Outward shield normal in part space. |
| `shader` | `PBR` | Auto-selects `TU/Metallic` (TexturesUnlimited), `Resurfaced/Standard`, Unity `Standard`, or falls back to `KSP/Emissive/Specular`. |
| `glowEnabled` | `True` | Enables/disables visual reentry glow. |
| `glowEmissivity` | `0.95` | Surface emissivity used in the glow thermal balance. |
| `glowHeatCapacity` | `10` | Thermal inertia in kJ/m²K (controls warm-up / cool-down lag). |
| `glowConduction` | `0.08` | Thermal conduction to hull in kW/m²K. |
| `glowBinAngle` | `18` | Angular bin size (deg) for grouping tile normals for glow. |
| `glowMaxBins` | `12` | Maximum glow bins per tile mesh. |
| `glowFadeTime` | `1.5` | Time constant (s) for smoothing glow transitions. |
| `rcsKeepOut` | `0.12` | Keep-out half-extent (m) around RCS nozzles on cockpits with `keepRcs = true`. |
| `massPerArea` | `0.0015` | Tile mass per m² in tonnes (`1.5 kg/m²`). |
| `costPerArea` | `20` | Tile cost per m² in funds. |
| `surfaceOffset` | `0.003` | Offset above hull surface in meters. |
| `cornerBendAngle` | `35` | Max smoothed bend angle (deg) tiles will cross. |
| `faceBendAngle` | `60` | Max individual face bend angle (deg). |
| `curvatureFactor` | `1` | Minimum bend radius (in tile sizes) tiles will follow. |
| `maxTiles` | `60000` | Safety cap on tiles per decal. |
| `colliderPatchSize` | `1` | Box collider patch size (m) for editor clicking. |
| `gearMargin` | `0.3` | Margin (m) around landing gear where tile colliders are omitted. |
| `logInterval` | `0` | Seconds between thermal debug log lines (`0` = off). |

---

## Thermal & Weathering Model

- **Convection shielding:** Each covered part receives protection proportional to the windward tile area (`sum(tileArea × max(0, dot(normal, airflow))) / part.exposedArea`, capped at 100%). Protection engages above `ablationTempThresh` (500 K) and consumes Ablator from the decal's shared pool.
- **Thermal glow:** Purely visual. Tiles are grouped by normal direction and share a vessel-averaged skin temperature so glow transitions seamlessly across part seams.
- **Vessel-wide weathering & cross-flow streaks (v1.12.0):** Reentry charring is shared seamlessly across all shields on the craft (`AblativeDecalStreaks` lifted `3.5 mm` without shadow acne). Straight nose-to-tail streaks project along the belly airflow plane on wings and flat undersides, while a second cross-flow layer (`AblativeDecalCrossStreaks`) activates on curved tank and fuselage flanks so feathery plasma streaks fan around the cylinder curvature like Starship reentry flow. Streaks stay tucked away during peak flames (`glowMask`) and emerge naturally as the tiles cool down (and preview live in the VAB with the **Weathering** slider): pale ash/plasma filaments on **Hex Black** & **Hex Plain Ceramic**, smoky Shuttle flow bands on **Square Black** & **Square Plain Ceramic**, and dark charcoal soot streaks on brushed **Stainless Steel**.

---

## Building from Source & Packaging

Target framework: `.NET Framework 4.8`.

```powershell
# Build against your local KSP installation
dotnet build src/AblatorDecals -c Release -p:KSPDir="C:\Path\to\KSP"

# Run offline geometry, thermal & glow self-tests
dotnet run --project tests/SelfCheck -c Release

# Package release ZIP into dist/AblatorDecals-<version>.zip
powershell -File tools/package-release.ps1 -Build -SelfCheck
```

Further design notes: [ARCHITECTURE.md](ARCHITECTURE.md), [DESIGN.md](DESIGN.md), [CHANGELOG.md](../CHANGELOG.md).
