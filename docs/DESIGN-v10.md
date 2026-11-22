# DESIGN v10: Spieltest 1.3.0 (verbindlich, Version 1.4.0)

Nutzer: "jetzt ist das Platzieren viel besser!" KSP.log 16.11. 22:28–22:51 (Testinstallation `F:\Benutzer\Dokumente\science test\KSP.log`,
nur lesen). Neues Craft (Mk3-Shuttle, VAB) kopiert nach `scratchpad\crafts\dynawing.craft` (mk3Cockpit.Shuttle, mk3CargoBayL,
mk3FuselageMONO, GearMedium ×2, wingShuttleElevon1/2, elevon3, smallCtrlSrf, AdvancedCanard, Flügel). Weiter
`scratchpad\crafts\spaceplane-2025.craft` (Mk2, airScoop, GearSmall, elevon5), `learstar.craft`. Screenshots `images\11.webp`
(Mk3: eingefahrenes Fahrwerk weiss, nur oben zackig bekachelt), `12.png` (kleine Klappe unter Flügel unbedeckt), `13.webp` +
`14.webp` (Mk3-Rumpf: entweder fast ganz herum oder nicht bis zur Kante), `15.webp` (Hex oben, Quadrat unten: Quadrat beginnt
an anderer Stelle als Hex), `16.webp` (Wiedereintritt: alles viel zu gelb; Klappen sägezahnig; Fahrwerk unbedeckt).

## Befunde (Orchestrator, gesichert)
- **Klappen (Punkt 3) und Einlässe (Punkt 9) fehlen:** `TargetParts()` (ModuleAblativeDecal.cs ~1346) gibt bei `spanParts == false`
  (Standard seit 1.3) nur `part.parent` zurück; `Glued()`-Kinder (Steuerflächen, Einlässe) kommen nie dazu. DESIGN-v9 M1 verlangte
  sie im Modus "Ganzes Teil".
- **Mk2-Lagerraum (Punkt 8):** Log `no usable surface of mk2CargoBayS under the decal, showing flat preview`.
- **Fahrwerk (Punkt 1):** Log `gear GearSmall: … retracted faces=0` bzw. `4`; GearMedium `faces=125`, aber nur die flache Oberseite
  bleibt (Filter dot > 0.6 und −3…+8 cm in `AddGear` wirft die gewölbte Gondel weg). GearSmall vermutlich SkinnedMeshRenderer
  (Collect/AddGear lesen nur MeshFilter).

## Modul (Agent "Modul": ModuleAblativeDecal.cs, TileHost, TileGlow, DecalWindow, Part-cfg, Textures.cfg, Lokalisierung, README/CHANGELOG)
M1 "Über Teile hinweg" entfernen (Punkt 6): Feld bleibt persistent (alte Saves laden), unsichtbar, wird ignoriert. Zielteile =
   Parent + alle per Surface-Attach an ihm klebenden Steuerflächen (`ModuleControlSurface`/`ModuleAeroSurface`) und Einlässe
   (`ModuleResourceIntake`) (Ursache Punkt 3 + 9). Einlässe: aussen bedeckt, Öffnung frei (bisheriger Filter); Log je Einlass bleibt.
M2 Alles auf dem Schild platzierbar (Punkt 10): Fahrwerke, Solarpanels, RCS-Düsen, beliebige Teile – nicht nur Decoupler. Ursache
   suchen (Kandidaten: Rebuild während ein Teil an der Maus hängt erzeugt neue, aktive MeshCollider; Anker-Ring-Collider; Teile aus
   der Teileliste; Symmetrie). Regel: solange ein Nicht-Decal-Teil gehalten wird, sind ALLE Kachel- und Anker-Collider aller Decals
   aus, auch frisch gebaute. Nach dem Anbringen: Anheben auf die Kacheln (LiftOntoTiles) für jedes Surface-Attach-Teil; Fahrwerk
   wird angehoben UND bleibt Ziel der Fahrwerkskacheln (A10). Begründen, warum es vorher nur mit Decouplern ging.
M3 Fahrwerk bedeckt (Punkt 1): ganze Aussenhaut des EINGEFAHRENEN Fahrwerks, auch gewölbte Gondeln (GearMedium) und Klappen
   (SmallGearBay). Kein ±cm-Fenster mehr; weg nur Dreiecke, die zum Rumpf zeigen (dot(n, Rumpfnormale) < −0.2) oder tiefer als
   2 cm im Rumpf liegen, plus Rad/Federbein (Dreiecke, die im eingefahrenen Zustand verdeckt sind: Strahl entlang n trifft
   innerhalb 0.5 m eigenes Fahrwerk). SkinnedMeshRenderer per `BakeMesh` in eingefahrener Pose (Körper = Renderer-Transform bzw.
   Root-Bone). Log-Zeile wie bisher, plus `skinned=<n>`.
M4 Türen und RCS frei (Punkt 7, generisch, nicht nur Mk2): Auf jedem Zielteil Sperrzonen: um jeden Thruster-Transform von
   `ModuleRCS`/`ModuleRCSFX` (Radius = Config `rcsKeepOut` 0.12 m) und um jeden Collider mit Tag "Airlock" (Bounds + 5 cm). Als
   `TileSettings.KeepOut` an die Geometrie (Format siehe G4). Mit der Option, das im Menü abzuschalten nicht nötig.
M5 Cockpit-Form auch für `mk3Cockpit_Shuttle` (Punkt 2); Ebenen liefert die Geometrie in CockpitCover.cfg.
M6 Glühen viel weniger intensiv (Punkt 12): Rampe neu, gedämpft, kaum gelb:
   `1000 0.35 0.03 0 0; 1200 0.35 0.03 0 0.12; 1400 0.6 0.1 0.02 0.25; 1900 0.8 0.25 0.05 0.4; 2500 0.9 0.45 0.15 0.55`
   und `glowEmissivity` so, dass die Kacheln im Wiedereintritt dunkelrot bis gedämpft orange wirken. Prüfen, ob unsere Emission
   zusätzlich zu KSPs eigenem `_TemperatureColor`/Emissive-Glühen auf den Kacheln addiert (dann eins von beiden aus).
   Fade (Nutzer: "die Glow-Farbe soll mit einem Fade kommen"): kein Sprung, weder beim Einsetzen noch beim Farbwechsel. Intensität
   startet bei 0 und blendet weich ein (erster Rampenpunkt mit Intensität 0, glatte Interpolation, z. B. Smoothstep zwischen den
   Punkten), und die angezeigte Farbe folgt dem Zielwert zeitlich gedämpft (Config `glowFadeTime` ~3 s, exponentiell, framerate-
   unabhängig), auch beim Abkühlen. Kein Flackern zwischen Kacheln/Teilen.
M7 Globales Fenster (Nutzer-Zusatz): zusätzlich **Dicke** (Kacheldicke für alle) im selben Fenster wie Muster/Form, auf alle
   anwenden wie die anderen Werte (Undo, Ablator/Masse neu).
M8 Fuge Standard 0 (Nutzer-Zusatz): `tileGap` Standard 0 m (auch im Fenster). Alte Saves behalten ihren Wert. Geometrie muss bei
   0 sauber bleiben (G6).
M9 Weisse Kacheln entfernen (Nutzer-Zusatz: "die sehen nicht gut aus"): Varianten HexWhite/SquareWhite (Textures.cfg, B9-Subtypes,
   Fenster, Lokalisierung) raus; alte Saves mit weisser Variante laden als schwarze desselben Musters. Ceramic bleibt, falls es
   nicht weiss ist (sonst ebenfalls raus und auf HexBlack).
M10 Version 1.4.0 (csproj, KSPAssembly 1,4), Lokalisierung en = de, README/CHANGELOG.

## Geometrie (Agent "Geometrie": TileBuilder.cs, SurfaceIndex.cs, tools/Preview, tests/SelfCheck, CockpitCover.cfg)
G1 Mk3 Umgriff bis zur Kante (Punkt 4, `13.webp`/`14.webp`, dynawing.craft): Liegt eine Knickkante (> 25°) innerhalb ±20° der
   Umgriffsgrenze, endet die Bekachelung exakt an der Kante (gerader Schnitt entlang der Kante, Kachel nicht halb um die Kante).
   Auf dem Mk3-Rumpf muss es eine Einstellung geben, bei der die Unterseite genau bis an die Seitenkante (Chine) bedeckt ist.
G2 Grenze unabhängig vom Muster (Punkt 11, `15.webp`): gleiche Decal-Lage und Max. Umgriff → Hex, Quadrat, Ziegel und jede
   Kachelgrösse enden an derselben Linie (Abweichung ≤ Fuge + 3 mm). Umgriffsgrenze als Schnittlinie, nicht "Kachelmitte drin".
G3 Mk3-Cockpit-Ebenen (Punkt 2) für `mk3Cockpit_Shuttle` in CockpitCover.cfg (Stock-Block, ReStock-Block falls Modell abweicht):
   Unterseite, Seiten bis knapp unter die Fensterunterkante, Spitze ganz bedeckt, schön über die Spitze wie Mk1/Mk2.
G4 `TileSettings.KeepOut`: Array von orientierten Boxen im Decal-Raum (Zentrum, 3 Halbachsen-Vektoren); keine Kachel ragt hinein,
   Kacheln an den Boxflächen gerade geschnitten (wie CutPlanes, aber lokal). Preview: Mk2-Cockpit mit RCS-Düsen und Tür frei.
G5 Mk2-Lagerraum (Punkt 8): mit der Preview (mk2CargoBayS/L aus der Testinstallation) herausfinden, warum keine Fläche entsteht,
   beheben. Darf dafür `Collect()` in ModuleAblativeDecal.cs ändern (nur diese Methode; das Modul-Agent arbeitet parallel in der
   Datei: vor jedem Edit neu lesen). Türen (ModuleAnimateGeneric-Transforms) als eigene bewegte Körper wie Klappen, wenn einfach;
   sonst feste Hülle bekacheln und Türen frei lassen.
G6 Fuge 0: Kacheln mit Gap 0 ohne Z-Fighting/Überlappung (Seitenwände bündig), SelfCheck-Fall dazu.
G7 Klappen im Modus "Ganzes Teil" (nach M1): spaceplane-2025 (elevon5) und dynawing (elevon3, smallCtrlSrf,
   wingShuttleElevon) – Klappenkacheln vorhanden, Muster wie v1.3-Twins, keine Sägezähne an gepfeilten Klappen (`16.webp`).
G8 Einlass (airScoop) aussen vollständig bedeckt, Öffnung frei (offener Punkt aus 1.3).
G9 Renders nach `preview/v10/` (dynawing: Mk3-Unterseite, Kante, Cockpit; spaceplane-2025: Klappen, Einlass, Mk2-Cockpit
   RCS/Tür, Lagerraum), v8/v9-Szenen nicht schlechter, SelfCheck grün.
