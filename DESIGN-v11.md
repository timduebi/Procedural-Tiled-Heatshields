# DESIGN v11: Spieltest 1.4.0 (verbindlich, Version 1.5.0)

KSP.log 19.11. ab 19:43 (Testinstallation, nur lesen). Test-Craft des Nutzers (Kopie):
`scratchpad\crafts\sph-testobjekt-1911.craft` (mk3Cockpit.Shuttle, mk3FuselageLFO.50/.25, mk3CargoBayS, mk3CargoRamp, mk2CargoBayL,
2× tailfin, 2× airScoop, 2× IntakeRadialLong, 2× GearSmall, wingShuttleDelta/Strake, R8winglet, 11 Decals) – damit nachstellen;
dazu `vab-auto-1911.craft` (Dynawing-Stand) und `dynawing.craft`. Screenshots `images\17.webp`, `18.webp` (Mk3-Heck mit zwei
IntakeRadialLong: kleine Verschiebung des Decals → ganz anderes Ergebnis, Einlässe halb bedeckt), `19.webp` (Mk3-Rumpf mit Flügeln:
weisser Zickzack an der Klappe), `20.webp` (Foto Space Shuttle: Vorbild Mk3-Cockpit), `21.webp` (ReStock-Mk3-Cockpit von oben,
rote Linien), `22.webp` (Mk3-Tank seitlich: Kacheln nur bis zur unteren Kante, rote Linie = gewünscht bis zur halben Höhe),
`23.png` (mk3CargoBay: Kacheln auf den Türen weiss/kaputt), `24.webp` (Vorbild Glühen: links Spitze weissgelb, rechts mittel orange,
dunklere Ränder, wenig Details).
Gut: alles auf dem Schild platzierbar; Fahrwerkskacheln fahren mit der Schale mit, wenn bedeckt; Glühen besser; Mk3-Unterseite
endet richtig an der Kante.

Nutzer-Antworten: Mk3-Cockpit "am Space Shuttle inspirieren, das ReStock-Cockpit hat schon dunkelgraue Stellen, nimm einfach die".
Mk2-Cockpit "wie jetzt, nur schöner, mit Aussparung für Düsen und cooleren Elementen". Höhe: zusätzlich zum Umgriff (der strengere
gilt). Heckflosse: Schild auf dem Rumpf, die Flosse daran wird nicht bedeckt (Log: `parts=[mk3FuselageLFO.25, tailfin, tailfin]`).

## Konzept "Overlay" (Entscheid Orchestrator, Nutzer: "mach es einfach so, dass diese ein Overlay bekommen und nicht auf die
## restliche Geometrie einen Einfluss haben, auch bei den Einlässen")
Angebaute Objekte – Fahrwerk (eingefahrene Schale), Einlässe, Steuerflächen/Klappen, Heckflossen (`tailfin`, alles mit
`ModuleControlSurface`/`ModuleLiftingSurface`, das per Surface-Attach am Zielteil klebt), Lagerraum-Türen und Mk3-Laderampe –
werden NICHT mehr von der Flutung des Haupt-Decals erreicht. Jedes Objekt bekommt ein eigenes Overlay:
- eigene Kachelung seiner ganzen Aussenhaut (Einlass: Öffnung frei; Fahrwerk: nur die im eingefahrenen Zustand sichtbare Schale;
  Klappen/Flossen: beide Seiten und Kanten), Gitter im Objekt-Raum verankert (Ursprung/Achsen des Objekt-Transforms), Flutstart je
  zusammenhängendem Flächenstück → **deterministisch**: Das Ergebnis hängt NICHT von Lage, Drehung oder Grösse des Haupt-Decals ab
  (nur von Muster, Kachelgrösse, Fuge, Dicke).
- Das Haupt-Decal kachelt nur das Zielteil selbst; an Objekten, die auf dem Rumpf sitzen, laufen seine Kacheln bis an den Fuss des
  Objekts (gerader Schnitt an dessen Umriss, kein Spalt > Fuge + 3 mm), unter eingefahrenem Fahrwerk durch wie bisher.
- Overlay-Kacheln gehören thermisch/ablativ zum Haupt-Decal, hängen am bewegten Transform (fahren mit), keine Collider auf
  Fahrwerk-Overlays (wie bisher).
- Pflicht-Test (SelfCheck + Preview): Haupt-Decal um 5 cm / 15° verschieben → Overlay-Kacheln identisch.

## Modul (Agent "Modul")
M1 Glühen wie `24.webp`: natürlich, wenig Detail, dunklere Ränder je Kachel. Drei Zustände: normal (kein Glühen), mittel (orange,
   dunkle Ränder, rechts im Bild), Spitze (weissgelber Kern, orange Ränder, links im Bild); dazwischen weich überblendet (zeitlich
   wie bisher gedämpft UND räumlich ohne Stufen). Umsetzung als Vorschlag: farbige Emissions-Textur je Muster (per Skript wie
   `tools/make-tile-atlases.ps1` erzeugt, gleiches Atlas-Layout, je Kachel weicher Verlauf: Kern hell weissgelb → Rand dunkelorange/
   dunkel, kein Rauschen), Shader mit `_Emissive`/`_EmissiveColor` (z. B. `KSP/Emissive/Specular` oder Bumped-Variante, in
   Assembly/Shader-Liste prüfen), Tönung `_EmissiveColor` über die Temperatur: mittel = orange × ~0.5, Spitze = weiss × ~1.2,
   Smoothstep dazwischen. Ursache der heute sichtbaren Stufen finden (Bins? Schwelle 0.002? Teilgrenzen?) und beheben. Rampe und
   Intensitäten über Config.
M2 Regler "Höhe" (0–100 %, 1-%-Schritte, Standard 100 %) im Menü "Fläche" zusätzlich zu Max. Umgriff, auch im globalen Fenster
   nicht nötig. Höhe = Anteil der Ausdehnung des Zielteils entlang der Decal-Normalen, gemessen von der Decal-Seite (100 % = ganzes
   Teil). Umsetzung als zusätzliche CutPlane (gerader Schnitt). Mk3-Tank seitlich: 50 % = halbe Höhe (`22.webp`).
M3 RCS-Aussparungen nur noch bei Cockpits mit Eintrag in CockpitCover.cfg und dort nur, wenn der Eintrag `keepRcs = true` hat
   (Mk2 ja, Mk3 nein). Luken (Airlock) bleiben überall frei.
M4 Mk3-Lagerraum (`23.png`): Kacheln auf den Türen weiss/zerfetzt – Ursache finden (Material/Textur der Tür-Hosts? Log zeigt viele
   "tile materials or texture changed by another mod, restoring"), beheben. Mk3-Laderampe (`mk3CargoRamp`, Log `doors=3`): Kacheln
   müssen mit der Rampe mitfahren (Editor und Flug) – prüfen, warum nicht (Host-Parent, Animation-Modul, Pose).
M5 Version 1.5.0, Lokalisierung en = de, README/CHANGELOG (Modul-Teil).

## Geometrie (Agent "Geometrie")
G1 Overlay (siehe Konzept) komplett: Builder-Seite (Objekt-Raum-Gitter, Flutstart je Flächenstück, Einlass-Öffnung frei, Haupt-
   Decal-Schnitt am Objektfuss) UND die Verdrahtung im Modul (`Collect`, `AddGear`, `TargetParts`-Folgen, `MakeJob`, Job-Ergebnis →
   Hosts). Der Modul-Agent arbeitet parallel in ModuleAblativeDecal.cs an M1–M4: vor jedem Edit neu lesen, nur die für G1 nötigen
   Stellen ändern. Abdecken: elevon5, wingShuttleElevon1/2, smallCtrlSrf, elevon3, tailfin, airScoop, IntakeRadialLong,
   GearSmall/GearMedium/SmallGearBay (Preview filtert Fahrwerk wie das Modul seit 1.4: ganze sichtbare Schale), mk2/mk3-Lagerraumtüren,
   mk3CargoRamp. Kennzahl: Abdeckung der Aussenhaut je Objekt ≥ 95 % (ohne Öffnung), identisch bei verschobenem Haupt-Decal.
G2 Mk3-Cockpit (ReStock-Modell der Testinstallation): bekachelt werden die Unterseite und genau die dunkelgrauen Bereiche der
   ReStock-Cockpit-Textur (Vorbild Shuttle, `20.webp`/`21.webp`), Fenster frei, RCS überdeckt. Zur Laufzeit keine Texturen lesen
   (KSP-Texturen sind nicht lesbar): Maske offline aus der DDS (Preview-DDS-Leser) ableiten und als Geometrie in CockpitCover.cfg
   ablegen (z. B. Ebenen-/Polygon-Regionen im Part-Raum, neues Format erlaubt, Modul liest es mit – Lader darfst du erweitern), mit
   sauberer, glatter Grenze (gerade/gebogen geschnitten, kein Treppen-Rand). Stock-Modell (ohne ReStock): eigener Block, Shuttle-Stil
   nachempfunden.
G3 Mk2-Cockpit "wie jetzt, nur schöner": glatte, geschwungene Grenzlinie statt sichtbarer Ebenen-Knicke (z. B. mehrere Ebenen
   zu einer glatten Kurve), RCS-Düsen rund ausgespart mit sauberem Rand (Kacheln radial geschnitten, Fase), dazu "coolere
   Elemente" nach eigenem Ermessen, sparsam und im Shuttle-Stil (z. B. Randreihe entlang der Grenze, dunkler Fensterrahmen falls die
   Textur einen hat). Mk1 unverändert.
G4 Klappen-Zickzack (`19.webp`) und Einlass-Zufall (`17.webp`/`18.webp`) sind mit G1 erledigt; mit dynawing.craft und einem
   Nachbau von 17/18 (mk3FuselageLFO.50 + 2× IntakeRadialLong, Decal an 3 Lagen) belegen.
G5 Renders `preview/v11/`, SelfCheck grün, v10-Szenen nicht schlechter, CHANGELOG (Geometrie-Teil).
