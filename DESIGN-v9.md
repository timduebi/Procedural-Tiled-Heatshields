# DESIGN v9: Bedienung neu, Nähte, Fixes aus Spieltest 1.2.0 (verbindlich, Version 1.3.0)

Spieltest 1.2.0 (16.11., KSP.log 20:2x, Crafts kopiert nach
`scratchpad\crafts\spaceplane-2025.craft` (SPH, 16 Decals, Mk2-Rumpf, GearSmall ×2, SmallGearBay, airScoop ×2, elevon5 ×4,
StandardCtrlSrf) und `scratchpad\crafts\learstar.craft` (VAB, Shuttle). Die Preview kann .craft lesen: damit nachstellen.
Nutzer-Screenshots: `images\7.webp` (Triebwerks-Gondel/Einlass nur teilweise bedeckt), `8.png` (Mk1-Cockpit: weisse Spitze,
weisse Punkte), `9.webp` (zwei Decals auf gestapelten Rümpfen: weisser Zickzack-Spalt dazwischen), `10.webp` (Flügel mit Klappe:
weisser Spalt Flügel/Klappe, Klappenkacheln versetzt und zackig).
Gut: NRE weg, globaler Knopf geht, Flaps bewegen sich, Cockpit-Form grundsätzlich.

Nutzer-Kernaussage: "grundsätzlich ist das Kacheln platzieren noch zu unintuitiv. die Kacheln überlappen sich komisch, ich
weiss nicht welcher Teil wo ist". Log bestätigt: ein überspannendes Decal frisst über `Occupied` ein Cockpit-Decal bis auf
1 Kachel; Rebuild-Kaskaden beim Ziehen.

## Neues Bedienkonzept (Entscheid Orchestrator)
**Ein Hitzeschild bedeckt sein Teil.** Standardmodus "Ganzes Teil": Das Decal kachelt die Fläche seines Parent-Teils (plus
daran klebende Klappen, Einlässe, Fahrwerk-Aussenflächen), soweit sie innerhalb von "Max. Umgriff" zur Decal-Normalen zeigt.
Keine Breite/Höhe nötig. An Teilnähten wird gerade geschnitten (wie an Klappen). So liegen auf einem Rumpf aus 5 Teilen
5 Decals sauber nebeneinander, jede Naht ist eine gerade Fuge, nichts überlappt.
- Breite/Höhe nur noch, wenn "Fläche begrenzen" an ist (bisheriges Verhalten; alte Saves: an, wenn sie Breite/Höhe haben).
- "Über Teile hinweg" bleibt als Option (Standard aus); auch dann gerader Schnitt an jeder Teilnaht.
- Zweites Decal auf demselben Bereich: wird nicht gebaut, Statuszeile "Bereich schon belegt" + ScreenMessage, Umriss rot.
  Mit "Fläche begrenzen" dürfen mehrere Decals auf einem Teil liegen; das jüngere füllt bis an die Kacheln des älteren
  (keine Zacken-Lücke > Fuge + 3 mm), nie darüber. Ein älteres Decal mit "Ganzes Teil" ist immer zuerst.

## Modul (Agent "Modul")
M1 Modus "Ganzes Teil" (Standard) / "Fläche begrenzen": Width = Height = 2 × grösster Punktabstand der Zielkörper vom
   Decal, SeparateOwner = true für ALLE Körper (Schnitt an jeder Teilnaht), MaxWrap = Menüwert (Standard 100°). Menü:
   Schalter oben in "Fläche"; Breite/Höhe nur sichtbar, wenn begrenzt.
M2 Belegt-Erkennung (siehe oben), keine Rebuild-Kaskade beim Ziehen: während ein Teil an der Maus hängt, keine Rebuilds
   anderer Decals; Occupied nur zwischen Decals mit "Fläche begrenzen" auf demselben Zielteil.
M3 Anklicken: Im Editor exakte MeshCollider auf den Kachel-Meshes (Oberseiten, nicht konvex, kein Rigidbody im Editor) statt
   der 1-m-Boxen ("ich klicke oft durch die Kacheln"). Im Flug die Boxen wie bisher. Collider-Abschaltung beim Ziehen anderer
   Teile muss in ALLEN Pfaden wieder einschalten (PartDropped, PartDeleted, Undo/Redo, Szenewechsel, Teil zurück in die Liste).
M4 Decal in Decal verhindern: Wird ein Decal gezogen, Kachel-Collider anderer Decals AN; KSPs Anbau-Raycast prüfen
   (Assembly-CSharp, EditorLogic): falls er Teile mit allowSrfAttach=0 überspringt und auf den Rumpf darunter trifft, nach
   PartAttached selbst prüfen (Anbaupunkt unter fremden Kacheln?) → Belegt-Status wie oben.
M5 Wo ist welches Decal: Hover oder offenes PAW → ganzes Decal hervorheben (KSP-Highlight) und kleine Anker-Markierung am
   Anbaupunkt (nur Editor, über den Kacheln, z. B. Sechseck-Ring), daran kann man das Decal greifen (eigener kleiner Collider).
M6 Fahrwerk: im Modus "Ganzes Teil" zählt jedes Fahrwerk am Zielteil. Diagnose ins Log je Fahrwerk:
   `[AblatorDecals] gear <part>: deploy=<anim/state|none> retracted faces=<n> bodies=<n>`; ebenso je Einlass.
M7 Glühen langsamer und später gelb: Rampe nach Temperatur: < 900 K kein Glühen, 900 K dunkelrot, 1300 K orange, 1700 K
   gelb, ≥ 2200 K gelbweiss; Glüh-Wärmekapazität so, dass die Farbe der Hauttemperatur mit Zeitkonstante ~20–40 s folgt
   (Config). Im Bericht: welche Temperatur ein normaler Mk2-Wiedereintritt erreicht (aus Log/Thermik-Werten schätzen).

## Geometrie (Agent "Geometrie")
G1 Ganzes Teil: Flutung ohne Footprint-Grenze über alle Zielkörper, Schnitt gerade an jeder Grenze separater Körper, mit Fuge,
   beidseitig, Muster läuft über bündige Nähte im gleichen Gitter (Ausrichtung wie FloodMissedBodies). Stirnflächen an
   Teilnähten (Stack-Flächen, auf denen ein anderes Teil sitzt) bleiben frei.
G2 Klappen (images\10.webp, learstar.craft): Flügelkacheln reichen bis an die Scharnierlinie, Klappenkacheln beginnen dort;
   Spalt nur Fuge + Freigang (≤ 1 cm), Klappengitter am Flügelgitter ausgerichtet, gerader Schnitt beidseitig.
G3 Einlässe/Gondeln (images\7.webp, z. B. nacelleBody, airScoop aus spaceplane-2025.craft): Aussenhülle vollständig innerhalb
   Max. Umgriff, keine Zacken-Ränder mitten auf der Gondel.
G4 Occupied mit Füllung (siehe Konzept): Zacken des älteren Decals auffüllen, Spalt ≤ Fuge + 3 mm.
G5 Mk1-Cockpit (images\8.png): weisse Spitze und weisse Punkte im Spiel – mit den exakten Werten aus der Craft-Datei nachstellen
   (Kachelgrösse, Muster, Cockpit-Form-Masse wie im Modul) und beheben.
G6 Preview: Szenen aus beiden Craft-Dateien im neuen Modus rendern (`preview/v9/`), Kennzahlen; v7/v8-Szenen nicht schlechter.
