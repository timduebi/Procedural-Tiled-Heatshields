# AblatorDecals – Architektur (Stand v6 / 1.0.0)

Ziel: KSP 1.12.5, Unity 2019.4.18f1, .NET Framework 4.8.
Referenz-Assembly: `KSP_x64_Data\Managed\Assembly-CSharp.dll` (Build 03190), dekompiliert mit ilspycmd 11.2. Alle KSP-APIs wurden gegen die dekompilierten Quellen geprüft, nicht geraten. Die Spezifikation steht in `DESIGN.md`; bei Abweichungen gilt der Code. Bedienung und Config-Felder stehen in `README.md`.

Die Abschnitte 1 bis 3 sind Hintergrund (Conformal Decals, KSP-APIs, Stock-Okklusion) und gelten unverändert. Ab Abschnitt 4 steht der aktuelle Stand.

---

## 1. Conformal Decals: wie es funktioniert

Quellen: das GitHub-Repo `drewcassidy/KSP-Conformal-Decals` existiert nicht mehr. Gelesen wurden der Fork `Mecripp/KSP-Conformal-Decals` (Stand 2020, mit Shader-Quellen) und die dekompilierte `ConformalDecals.dll` v0.2.14.

- **Projektion.** Das Decal-Part hat einen Projector-Transform, dessen lokale Skalierung `(Breite, Höhe, Tiefe)` eine Box aufspannt. Pro Parent-Mesh wird die Matrix `Ortho * (target.worldToLocal * projector.localToWorld)^-1` berechnet und per `MaterialPropertyBlock` an einen eigenen Shader gegeben.
- **Rendering.** CD erzeugt keine Geometrie. In `Camera.onPreCull` zeichnet es die Parent-Meshes per `Graphics.DrawMesh` mit dem Decal-Material noch einmal; der Vertex-Shader rechnet `uv = _ProjectionMatrix * vertex`.
- **Clipping** im Fragment-Shader: `clip(uv.xyz)`, `clip(1 - uv.xyz)` und `clip(dot(_DecalNormal, normal))`. Am Part-Rand endet das Decal von selbst, weil nur Parent-Geometrie gezeichnet wird.
- **Part-Anbindung.** Surface-Attach, Editor-Events (`onEditorPartEvent`, `onVariantApplied`, `onEditorUndo`), in Flight stirbt das Decal mit dem Parent, Symmetrie von Hand über `symmetryCounterparts`, Persistenz über `[KSPField(isPersistant = true)]`.
- **Konsequenz.** Die CD-Shader liegen vorkompiliert in einem Unity-AssetBundle. KSP kann zur Laufzeit keine Shader kompilieren; ein eigenes Bundle bräuchte den Unity-2019.4.18f1-Editor. AblatorDecals erzeugt deshalb echte Geometrie und nutzt Stock-Shader.
- **Lizenz.** Es wurde kein CD-Code und kein CD-Shader übernommen. Der Ansatz (eigene Kachel-Geometrie auf dem Rumpf) ist unabhängig. Würde je CD-Code oder -Shader übernommen, müsste AblatorDecals GPL v3 werden. Die Lizenzwahl liegt beim Autor.

---

## 2. Verifizierte KSP-APIs (Auszug, gegen Assembly-CSharp.dll)

| API | Befund |
|---|---|
| `Part.thermalConvectionFlux` (double, kW) | `= ptd.convectionFlux * thermalConvergenceFactor`; `skinTemperature` wird danach damit integriert. |
| `PartThermalData.convectionFlux`, `.finalCoeff`, `.convectionArea`, `.postShockExtTemp` | `convectionFlux = (postShockExtTemp - skinTemperature) * finalCoeff`; `finalCoeff` ist proportional zu `convectionArea`. Bei niedriger Mach-Zahl zählt die ganze Oberfläche, bei hoher nur die Frontfläche. |
| `Part.exposedArea` (double, m²) | Frontfläche in Strömungsrichtung nach Knoten-Okklusion (`DragCubes.ExposedArea`). |
| `Part.AddExposedThermalFlux(double kW)` | Wirkt im nächsten Thermikschritt auf die exponierte Haut. Genau diesen Aufruf nutzt das Stock-`ModuleAblator` (mit negativem Wert). |
| `Part.skinTemperature`, `skinMaxTemp`, `Part.ptd`, `Vessel.srf_velocity`, `Part.attachMode` (`AttachModes.STACK`), `Part.symMethod` (`SymmetryMethod.Mirror`) | public. |
| Physiklose Parts (`PhysicsSignificance = 1`) | Bleiben im Thermik-Graph (`thermalMass = max(..., 0.1)`). |
| Ablator-Ressource | `density = 0.001` t/Einheit, `hsp = 400`, `flowMode = NO_FLOW`. |
| Stock-`ModuleAblator` | Abgeführter Fluss `= pyrolysisLossFactor * hsp * Einheiten/s * density`; mit Stock-Werten 2400 kJ je Einheit. |
| Events | `onEditorPartEvent`, `onVariantApplied`, `onEditorUndo/Redo`, `onEditorShipModified`, `onPartWillDie`, `onVesselWasModified`, `OnCollisionIgnoreUpdate`. |
| Texturen | `GameDatabase.Instance.GetConfigNodes(string)`, `GetAllTexturesInFolder`, `GetTexture(url, false)`. |
| Glühen | `PhysicsGlobals.GetBlackBodyRadiation(float, Part)`, `PhysicsGlobals.TemperaturePropertyID` (`_TemperatureColor`). |
| Masse, Kosten | `IPartMassModifier`, `IPartCostModifier` mit `ModifierChangeWhen.FIXED`. |
| UI | `KSPField.groupName/groupDisplayName`, `UI_FloatEdit`, `UI_ChooseOption`, `UI_Toggle`, `MonoUtilities.RefreshPartContextWindow`, `KSP.Localization.Localizer.Format`. |
| `PartModule.OnWasCopied(PartModule, bool)` | Wird mit dem Modul der Kopie als Argument am Original aufgerufen. |

Nur im Spiel prüfbar bleibt, ob alle Shader-Namen (`KSP/Specular`, `KSP/Diffuse`) per `Shader.Find` zur Laufzeit gefunden werden (die `build`-Logzeile zeigt es) und die Reihenfolge von `PartModule.FixedUpdate` und `FlightIntegrator.FixedUpdate` (egal für die Funktion: das Modul liest den Fluss des letzten Schritts, mit einem Tick Verzug).

---

## 3. Stock-Okklusion

`FlightIntegrator.UpdateOcclusionConvection()` (nur bei Mach > 1) bildet je Part eine Box aus `DragCubes.WeightedCenter/WeightedSize`, projiziert sie auf die Ebene senkrecht zur Anströmung und sortiert die Parts nach `maximumDot`. Das vorderste Part erzeugt einen Stoßkegel; stumpfe Parts (`invFineness >= 1`) haben einen abgelösten Stoß mit `occludeConvectionAreaMult = 0`. Jedes weiter hinten liegende Part bekommt `convectionAreaMultiplier *= 1 - Überlappung + Überlappung * occludeMult`.

Ein Stock-Heatshield schützt also nicht, weil es "Heatshield" heißt, sondern weil es ein flaches, breites Part mit Drag Cube ganz vorne ist; es selbst bekommt die volle Konvektion und baut sie über `ModuleAblator` ab. AblatorDecals nutzt das nicht: Das Decal hat keinen Drag Cube (`DRAG_CUBE { none = True }`, `maximum_drag = 0`) und schirmt keine anderen Teile ab. Es nimmt nur Konvektionshitze der Teile auf, auf denen es liegt (Abschnitt 5.1).

---

## 4. Dateien und Verantwortung

```
src/AblatorDecals/
  AblatorDecals.csproj       net48, referenziert KSP-DLLs (Eigenschaft KSPDir); Build kopiert GameData in die KSP-Installation
  ModuleAblativeDecal.cs     PartModule: Editor-PAW, Ziel-Parts, Rumpf-Dreiecke einsammeln, Build-Steuerung, Thermik, Glühen, Verkohlung, Masse/Kosten, Logs
  TileHost.cs                GameObject mit Mesh und Materialien auf einem Ziel-Part; AblativeDecalTilesMarker
  SurfaceIndex.cs            räumlicher Index über Rumpf-Dreiecke (Closest, Raycast, geglättete Normalen)
  TilePattern.cs             Kachelgitter (Hex, Square, Brick): Basisvektoren, Nachbarschritte, Kanten
  TileBuilder.cs             Flutung, adaptive Zellen, Netz, UVs, Bins, Collider; öffentliche API (TileSettings, PartTiles, TileResult)
  ShieldMath.cs              Deckungsanteil und Ablator-Verbrauch (reine Mathematik)
  TileGlow.cs                Oberflächentemperatur je Bin (reine Mathematik)
GameData/AblatorDecals/
  Plugins/AblatorDecals.dll
  Parts/ablativeDecal.cfg    Part (Modul, Ablator-Ressource, Drag Cube none, Platzhalter-Modell)
  Assets/placeholder.mu      eigenes Icon-Modell (7 Hex-Kacheln), erzeugt von tools/make-placeholder-mu.ps1
  Textures/                  HexBlack, SquareBlack, SquareWhite (Atlanten), Ceramic (einfach), Textures.cfg
  Localization/              en-us.cfg, de-de.cfg
GameData/AblatorDecalsTest/  TestTweaks.cfg (Mk1-3-Reaktionsräder, nur zum Testen, gehört nicht zum Mod)
tests/SelfCheck/             Konsolen-Selbsttest der reinen Mathematik
tools/                       make-tile-atlases.ps1, make-ceramic-texture.ps1, make-placeholder-mu.ps1, Preview/
```

`SurfaceIndex`, `TilePattern`, `TileBuilder`, `ShieldMath` und `TileGlow` sind reine Mathematik. Sie verwenden nur verwaltete Unity-Typen (`Vector2/3`, `Mathf`, `Quaternion * Vector3`), damit sie außerhalb von Unity (Selbsttest, Vorschau) laufen. Nicht verwendet werden dort Typen mit nativem Anteil wie `Matrix4x4.inverse`, `Quaternion.Inverse/Euler/LookRotation/AngleAxis`, `Mesh`, `Debug.Log` oder `Time`.

Es gibt nur eine Modulklasse. Thermik und Optik teilen sich Parent, Ziel-Parts und Kachelflächen.

## 5. Datenfluss

```
Ziel-Parts  ->  Rumpf-Dreiecke  ->  SurfaceIndex  ->  TileBuilder  ->  TileHost je Part  ->  Thermik / Glühen
(Hauptthread)   (Hauptthread)       (Worker)           (Worker)         (Hauptthread)        (FixedUpdate)
```

1. **Ziel-Parts** (`TargetParts`): das Parent plus die per Surface-Attach daran klebenden Steuerflächen (`ModuleControlSurface`) und Einlässe (`ModuleResourceIntake`); mit Cockpit-Form nur das Cockpit. Fahrwerk kommt getrennt dazu (`AddGear`, eingefahrene Pose). Seit 1.4 kein `spanParts` mehr (Feld nur noch für alte Saves).
2. **Rumpf-Dreiecke** (`Collect`): alle aktiven, aktivierten `MeshFilter` der Ziel-Parts mit lesbarem Mesh, ohne Shader der Namen `DepthMask`, `Particles`, `Transparent`, `Translucent`, `FX`. Positionen im lokalen Raum des Decal-Parts, Wicklung bei gespiegelten Transformationen korrigiert; jedes Dreieck merkt sich den Ziel-Part als Besitzer. Das Ergebnis wird zwischengespeichert, solange Ziel-Parts und relative Lage gleich bleiben (Regler ändern nur die Einstellungen).
3. **`SurfaceIndex`** wird je Build neu aufgebaut (interner Zustand ist nicht threadsicher), mit Ursprung am Decal, Reichweite, Zellgröße und Glättungsradius aus dem Modul.
4. **`TileBuilder.Build`** (Verhalten wie `DESIGN.md` Abschnitt 2, hier nur grob):
   - *Flutung*: Breitensuche über das Kachelgitter ab dem Punkt unter dem Decal. Von Kachel zu Nachbarkachel wird über die Oberfläche gelaufen (kleine Schritte, auf die Fläche gesetzt). Es endet bei fehlender Fläche, zu starkem Knick, `MaxWrap`, seitlichem Wegrutschen. Position-Deduplizierung: Eine Kandidatenkachel wird verworfen, wenn auf derselben Seite schon eine Kachel näher als 0,6 Kachelgrößen liegt. Das verhindert das endlose Umlaufen geschlossener Rümpfe und gestapelte Kacheln. An Part-Kanten entstehen "virtuelle" Kacheln, die an der Kante beschnitten werden. Abbruch bei `MaxTiles`.
   - *Adaptive Zellen*: Jede Kachel bleibt eben und starr, ihr Umriss wird aus den tatsächlichen Nachbarpositionen geschnitten (konvexes Clipping an Halbebenen). Auf Ebene und Zylinder ergibt das regelmäßige Kacheln, auf doppelt gekrümmten Flächen leicht einzeln zugeschnittene mit konstanter Fuge. Es gibt keine Fugen-Spreizung (Spread) mehr. Wo zwei Flutungsfronten aufeinandertreffen (Naht-Kacheln ohne Gitternachbarn), darf eine Randkante bis zu 1,6 Gitterabständen in Richtung der fremden Kachel wachsen; die Mittelsenkrechte zwischen beiden schließt die Fuge dann auf `Gap`, die Kacheln reichen also bis zur Mittellinie.
   - *Aufsetzen*: Umrisspunkte werden auf den Rumpf gesetzt; gültig sind sie bei Treffer, innerhalb des Umgriffs und bei begrenzter Neigung. Sind nicht alle gültig, entsteht eine Teilkachel mit Schnitt an der Kante. Die Kachelebene liegt `SurfaceOffset + Thickness` über dem höchsten aufgesetzten Punkt, damit keine Rumpf-Facette durchstößt.
   - *Netz je Kachel*: Oberseite, Fase, Seitenwände an Kanten ohne volle gezeichnete Nachbarkachel (Rand, Fußabdruck, Teilkachel-Schnitt, ausgelassene oder Teil-Nachbarn) und an Kanten, die mehr als 2 mm über der Ebene der Nachbarkachel stehen (Stufe an Knicken; dort genügt ein Quad), ein dunkles Fugenblatt knapp unter der Fase (schließt Fugen lückenlos), weiße Rückseite. Submesh-Reihenfolge: Bins…, Joint, Back.
   - *UVs*: Atlas (Zelle per Hash aus Gitterposition und Seed, optional zufällig gedreht), Window (zufälliger Ausschnitt je Kachel) oder Across (ein Bild über alle Kacheln).
   - *Bins*: Kacheln werden je Besitzer gierig nach Normale gruppiert (Winkel `BinAngleDeg`, höchstens `MaxBins`). Ein Bin ist ein Submesh mit eigenem Material und eigener Glüh-Temperatur.
   - *Collider*: dünne Boxen über Kachel-Flächen (`ColliderPatchSize`, höchstens `MaxColliders`).
   - Ergebnis: `TileResult` mit `PartTiles` je Besitzer (Vertices, Normalen, UVs, Bin-Dreiecke, Fugen- und Rückseiten-Dreiecke, `TileAreaVectors` je Kachel, `CoveredArea`).
5. **`TileHost` je Ziel-Part** übernimmt das Netz: Vertices vom Decal-Raum in den Raum des Hosts umgerechnet, ein Material je Bin (Kopie des Kachel-Templates), je ein Material für Fuge und Rückseite.
6. **Thermik** (5.1) liest je Host die `TileAreaVectors`; **Glühen** (5.2) nutzt `BinNormals`.

Ist unter dem Decal keine Oberfläche zu finden: im Editor eine flache Ersatzfläche (`Flat`), im Flug keine Kacheln.

### 5.1 Thermik (FixedUpdate, nur Flug, nur mit Parent)

Pro Ziel-Part `p`: `frontal_p = Σ Kachelfläche · max(0, n · v̂)` über alle Kacheln dieses Hosts (`n` und `v̂` im Part-Raum des Decals). `raw_p = frontal_p / p.exposedArea`. Alle Decals desselben Vessels auf `p` werden summiert; `ShieldMath.Coverage` skaliert so, dass die Summe höchstens 100 % ist. `Q_p = coverage_p · efficiency · max(0, p.thermalConvectionFlux)`, sobald `p.skinTemperature > ablationTempThresh`. Der Ablator-Vorrat ist gemeinsam für alle Ziel-Parts (`ShieldMath.Absorb`); reicht er nicht, bekommen alle Teile denselben Bruchteil. Abgezogen wird mit `p.AddExposedThermalFlux(-Q_p)`. Die Ablator-Menge im Editor ist `coveredArea · thickness · unitsPerCubicMeter` und wird im Flug nie neu gesetzt. Eine `reentry summary`-Zeile entsteht nach 10 s ohne Aufnahme.

### 5.2 Glühen und Verkohlung (rein optisch)

`TileGlow` integriert je Bin `C·dT/dt = h·f·(T_ext − T) − ε·σ·(T⁴ − T_skin⁴) − k·(T − T_skin)` linearisiert-implizit (stabil für jedes `dt`), mit `f = max(0, n_bin · v̂)`, `h = ptd.finalCoeff / ptd.convectionArea` (0 ohne `ptd` oder bei Fläche ≤ 0) und `T_ext = ptd.postShockExtTemp`. Die Farbe kommt aus `PhysicsGlobals.GetBlackBodyRadiation` und wird als `_TemperatureColor` auf das Bin-Material gesetzt, nur wenn der Shader die Eigenschaft hat und `glowEnabled` gilt, und nur bei einer Änderung von mehr als 3 K. Die Verkohlung setzt `_Color` der Kachelmaterialien von Weiß nach `charColor` mit `pow(verbrauchter Anteil, 0.8)`, aktualisiert bei einer Änderung von mehr als 1 %.

### 5.3 Diagnose-Logzeilen (für den Test im Spiel)

- `[AblatorDecals] build <Szene> <Part> parts=[...] onSurface=... tiles=... partial=... bins=... colliders=... covered=...m2 time=...ms (core ...ms) texture=... shader=... hasTemperatureColor=...` bei jedem (Neu-)Aufbau, im Editor höchstens einmal je Sekunde.
- `[AblatorDecals] flight check <Part> on <Ziel-Part>: visible= enabled= layer= vertices= bins= texture= shader= hasTemperatureColor=` 3 s nach Physikstart, je Host.
- `[AblatorDecals] reentry summary ...` nach einem Aufheizvorgang.
- Mit `logInterval > 0`: sekündliche Thermikzeile.
- Warnungen: Shader nicht gefunden, Textur nicht gefunden, kein nutzbarer Untergrund, `MaxTiles` erreicht, Mesh nicht lesbar, Materialien von anderem Mod verändert.

## 6. Threading im Editor

Der Editor baut asynchron, damit Regler nicht ruckeln:

- Auf dem Hauptthread (`MakeJob`): Einstellungen in ein `TileSettings` kopieren, Ziel-Parts und Rumpf-Dreiecke einsammeln, Textur auflösen. Der `BuildJob` enthält danach nur einfache Daten (Arrays, `Vector3`, Settings), keine Unity-Objekte (`Part[]` wird nur auf dem Hauptthread gelesen).
- Auf einem Worker (`ThreadPool.QueueUserWorkItem`, `Work`): `SurfaceIndex` und `TileBuilder.Build`. Es werden keine Unity-API und kein Logging aufgerufen. Ausnahmen werden im Job gespeichert und auf dem Hauptthread geloggt.
- Das Ergebnis wird unter einem Lock (`gate`) abgelegt und im nächsten `Update` übernommen (`Receive`, `Apply`): Netze, Materialien, Collider, Anzeigen.
- Je Modul läuft höchstens ein Build. Kommt währenddessen eine Änderung, setzt sie `dirty`; nach dem Ergebnis (das trotzdem angezeigt wird, damit man Fortschritt sieht) startet einmal der neueste Stand. Regleränderungen werden mit mindestens 0,1 s Abstand zusammengefasst (`rebuildPending`, `nextRebuild`). Ereignisse mit sofortiger Wirkung (Anheften, Lösen, Verschieben, Variantenwechsel, Undo, Redo) setzen `rebuildNow`.
- `onEditorShipModified` feuert bei jedem Klick; das Modul baut nur neu, wenn sich Ziel-Parts oder deren Lage zum Decal geändert haben.
- Im Flug läuft der Build synchron auf dem Hauptthread, einmal in `OnStartFinished`, danach nur bei `onVesselWasModified` (nur wenn sich die Ziel-Parts geändert haben) oder wenn ein Ziel-Part stirbt.

## 7. Lebensdauer der GameObjects

- **Part-Modell.** `Assets/placeholder.mu` ist nur Icon-Modell und Drag-Cube-Ersatz. `SetupVisuals` deaktiviert dessen Renderer und Collider; sichtbar sind nur die eigenen Netze. Eigenes Modell, weil ReStock und ähnliche Mods Stock-Modelle entfernen.
- **`decalRoot`** (`AblativeDecalRoot`): Kind von `model` des Decal-Parts, mit dem Part-Raum als lokalem Raum. Es trägt die Collider-Boxen (Editor und Flug, damit das Part anklickbar ist). Nach einem Neuaufbau im Flug feuert das Modul `GameEvents.OnCollisionIgnoreUpdate`, damit KSP die neuen Collider in die Ignorierliste des Vessels aufnimmt.
- **Editor-Hosts**: Kinder von `decalRoot`, also unter `model` des Decal-Parts. Dadurch greift das Hover-Highlight. Die Hosts werden gepoolt und wiederverwendet (nur das Netz wird ersetzt); nur wenn Hosts dazukommen oder wegfallen, wird `RefreshHighlight` aufgerufen.
- **Flug-Hosts**: Kinder des Transforms des **Ziel-Parts** (nicht unter dessen `model`). So bleiben die Kacheln beim Verbiegen der Rakete am richtigen Part und KSPs `MaterialColorUpdater` fasst deren Materialien nicht an. Die Hosts werden bei jedem Neuaufbau vollständig abgerissen und neu angelegt.
- **Marker.** Jedes Host-GameObject und `decalRoot` tragen `AblativeDecalTilesMarker` mit einem nicht serialisierten Besitzer-Feld. Wird ein Part geklont, serialisiert Unity die Kind-Objekte mit, aber nicht das Feld: `Start()` der Kopie findet keinen Besitzer und zerstört sie. Das verhindert Geister-Kopien.
- **Aufräumen.** `OnDestroy` des Moduls meldet alle Events ab (jedes `Add` hat ein `Remove`), zerstört Hosts, Collider, `decalRoot` und die Material-Vorlagen samt Flach-Texturen. `TileHost.Destroy` zerstört Materialien und Mesh, setzt das GameObject sofort inaktiv, löst es sofort aus der Hierarchie (`Part.FindModelComponents` sammelt auch inaktive Objekte; sonst landet der sterbende Renderer in `Part.HighlightRenderer` und `Part.SetOpacity` wirft NREs, v8 A1) und zerstört es verzögert. Nach jedem Erzeugen/Zerstören von Renderern unter `model` setzt `TileHost.ResetRendererCaches` alle Renderer-Caches, die Highlight-Liste und den Highlighter neu. Das Platzhalter-Modell wird auf dem lebenden Part per `DestroyImmediate` entfernt (v8 A2).
- **Materialien.** Je Host und Bin eine eigene Instanz aus einer Vorlage (`KSP/Specular`, Rückfall `KSP/Diffuse`); Fuge (dunkel) und Rückseite (weiß) sind `KSP/Diffuse` mit 1×1-Textur. Ein Wächter prüft einmal pro Sekunde, ob `renderer.sharedMaterials` und die Textur noch die eigenen sind, und stellt sie sonst wieder her (mit Warnung im Log).

## 8. Gelernte Fallen

Aus `DESIGN.md` 4.1 plus Funde der Code-Prüfung:

1. `MaterialColorUpdater` (Part-Glühen) ruft `renderer.materials` für alle Renderer unter `model` auf und **klont** deren Materialien. Texturen und Farben deshalb immer auf den eigenen Materialinstanzen setzen und per Wächter prüfen. Flug-Hosts hängen aus diesem Grund nicht unter `model`.
2. `UI_FloatEdit` ignoriert `guiFormat/guiUnits`; `sigFigs` und `unit` gehören an das Attribut.
3. `KSPField` kann kein `Color` parsen; die Config-Farben sind `Vector3`.
4. `node_attach` zeigt in das Parent hinein. Die Außennormale steht deshalb in `shieldNormal = 0, -1, 0`.
5. `onEditorShipModified` feuert bei jedem Klick. Neu gebaut wird nur, wenn sich die Ziel-Parts geändert haben. Das Modul feuert das Event außerdem selbst, wenn sich die Ablator-Menge ändert (Masse und Kosten); darum darf es auf das eigene Event nicht neu bauen.
6. Das PAW nie während eines Slider-Zugs neu aufbauen; `MonoUtilities.RefreshPartContextWindow` nur nach Chooser-Wechsel.
7. Netze über 65 000 Vertices brauchen `IndexFormat.UInt32` (`TileHost` setzt es).
8. ReStock entfernt Stock-Modelle; daher das eigene `placeholder.mu`.
9. KSP baut die Highlight-Renderer-Liste (`part.HighlightRenderer`, `Part.CreateRendererLists`) einmal und teilt sich die Cache-Liste mit `FindModelRenderersCached`. Kommen oder gehen Hosts unter `model`, bleiben zerstörte Renderer in der Liste. `RefreshHighlight` setzt beide Caches zurück und baut die Liste neu.
10. Aus demselben Grund gibt es im Editor einen Host-Pool: Ein Neuaufbau ersetzt nur das Netz, nicht das GameObject und den Renderer.
11. Beim Lösen im Editor ist `part.parent` im Moment von `PartDetached` noch gesetzt; das Modul merkt sich das (`detached`).
12. Beim Klonen von Parts entstehen Kopien der Host-GameObjects (siehe Marker).
13. `Part.OnWasCopied` wird am Original mit dem Kopie-Modul als Argument aufgerufen; der gespiegelte Musterwinkel wird deshalb auf dem Argument gesetzt.
14. Umlaufen geschlossener Rümpfe: Ohne Positions-Deduplizierung läuft die Flutung bei `MaxWrap = 180` endlos (v5: 40 000 Kacheln auf 30 m²). Regressionstest im Selbsttest.

## 9. Test- und Prüfwerkzeuge

- **`tests/SelfCheck`**: `dotnet run --project tests/SelfCheck`, endet mit `SelfCheck OK`. Prüft `ShieldMath`, `TilePattern`, den Geometrie-Kern (Ebene je Form und Winkel mit regelmäßigen Kacheln, Fugenbreite, Abdeckung ohne sichtbaren Rumpf, Überlappungsfreiheit, geschlossener Zylinder mit `MaxWrap = 180`, Kugelkalotte, Kanten, Flügel, Tankrand, Rippen, Naht zweier Parts, Nasenkegel, Rumpf-Durchstoß, Leistung) und `TileGlow` (Gleichgewicht, Stabilität bei großem `dt`, Abkühlen, `f = 0`). Es prüft nicht: alles, was Unity-Objekte braucht (Modul, Hosts, Materialien, Events).
- **`tools/Preview`**: Offline-Vorschau. `dotnet run --project tools/Preview -- <craft> <outDir> [key=value ...]` lädt ein `.craft` und die echten `.mu`-Modelle aus `GameData` (über die ModuleManager-Konfiguration), führt denselben `TileBuilder` aus und rendert jedes Decal per Software-Renderer als PNG. `--stack <gameData> teilA,teilB,... <Decal-Höhe> <outDir>` baut einen vertikalen Stapel. Schlüssel: `width height tileSize tileGap thickness maxWrap shape=Hex|Square|Brick angle straightBorder tileLoss texture=<png> cells=4x4 uv=atlas|window|across textureScale spanParts seed views=front,left,...`. Die Vorschau zeigt Geometrie, Fugen, Texturabbildung und Spiegelung; nicht die Spiel-Beleuchtung, Schatten, Glühen oder Modifikationen anderer Mods.
- **Logzeilen** (Abschnitt 5.3) sind die Rückmeldung aus dem Spiel.

## 10. Offene Risiken (nur im Spiel prüfbar)

- Ob die Kacheln im Flug sichtbar und texturiert bleiben, wenn Render-Mods (Scatterer, TextureReplacer, TexturesUnlimited) Materialien tauschen. Der Wächter stellt sie wieder her, ob das reicht, ist ungeprüft.
- Ob `KSP/Specular` die Eigenschaft `_TemperatureColor` hat (Log: `hasTemperatureColor`); sonst gibt es kein Glühen.
- Leistung: Kachelzahl und Vertices bei großen Flächen (Offline: 9 × 30 m auf r = 4,5 m, Kachelgröße 0,2 m unter 1,5 s und unter 450 000 Vertices), Rechenzeit im Spiel und Speicher bei vielen Decals.
- Verhalten beim Abtrennen von Stufen: Flug-Hosts hängen am Ziel-Part und sollten mitwandern; das Neubauen bei `onVesselWasModified` und `onPartWillDie` ist offline nur gegen den Quelltext geprüft.
- Skinned und animierte Parent-Meshes werden nicht gelesen. Meshes mit `isReadable = false` werden übersprungen.
- Thermik: Das Decal liest den Fluss des letzten Schritts (ein Tick Verzug). Wie stark der Schutz im Spiel ausfällt, hängt an `efficiency` und `unitsPerCubicMeter` und braucht Abgleich mit Spielerfahrung.
- Interaktion mit dem Highlight und dem Hover-Collider im Editor bei sehr vielen Collider-Boxen (`MaxColliders` = 256).
