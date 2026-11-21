# DESIGN v7: Nachtrag zu DESIGN.md (verbindlich, ersetzt widersprechende Stellen)

Grundlage: Nutzer-Feedback nach dem ersten Spieltest des v6-Stands (Screenshots: Mk3-Rumpf nur Boden bedeckt,
Kacheln stehen an Kanten als grobe Blöcke ab, Mk1-Cockpit mit überstehenden Randkacheln). Schwarze Kacheln gelten
als "perfekt"; weisse Kacheln dürfen schwächer sein, aber nicht schlechter werden.

## 0. Ausgangslage Quellcode
`src/AblatorDecals/TileBuilder.cs` und `tests/SelfCheck/Program.cs` enthalten einen ABGEBROCHENEN Umbau
(Brick-Fasenstreifen): DBG-Ausgaben, `ChamferStripChecks` für Brick schlägt fehl. Die installierte DLL ist der
abgenommene Stand davor (Sicherung: `scratchpad\backup-abgenommen\AblatorDecals.dll`, Quellen dazu existieren nicht mehr).
Kennzahlen des abgenommenen Stands ("hull visible inside the tiled area", Preview, maxWrap=180, HexBlack):
Flügel decal1 close=2, Adapter decal2 left=8 close=36, Cockpit decal4 close=5, Nase Square@45 close=99.
Diese Werte dürfen nicht schlechter werden.

## 1. Neue Settings (bereits in TileSettings eingetragen, Namen sind fix)
- `bool Unrolled = true`: Breite/Höhe werden ENTLANG der Oberfläche gemessen (abgewickelt, wie ein Aufkleber), über die
  Gitterkoordinaten der Flutung (a, b · Pitch), nicht als Projektion entlang der Decal-Normalen. Damit kann ein Decal am
  Mk3-Boden über die Kante "bis zur Mitte" der Seitenwand reichen. `false` = bisheriges Verhalten (Projektion; gerade
  Linie von vorne gesehen, z. B. Nasenkegel). maxWrap bleibt zusätzlich als Winkelgrenze.
- `bool[] SeparateOwner` (optional, Index = Owner-Id): `true` = bewegliches Teilstück (Steuerfläche/Ruderklappe).
  Keine Kachel liegt auf zwei Owners, wenn einer davon separat ist; die Grenze wirkt wie eine Kante: Kacheln werden dort
  sauber geschnitten (Wand, Fugenblatt), Flutung darf über die Grenze weiterlaufen, aber jede Kachel gehört genau einem Owner.
- `TileShape.Brick` bleibt im Code, wird aber nirgends mehr angeboten (Nutzer: "versetzte Tiles braucht es nicht").
  Gespeicherte Brick-Decals laden weiter.

## 2. Owner = Körper, nicht Teil
`PartTiles.Owner` / `SurfaceHit.Owner` sind Indizes in die Körperliste des Moduls. Ein Körper = (Part, Transform).
Normale Teile = ein Körper mit `part.partTransform`. Teile mit `ModuleControlSurface`: die Meshes unter dem beweglichen
Transform (`transformName`, meist `ctrlSurface`; bei mehreren Treffern alle) sind ein eigener Körper mit diesem Transform,
`SeparateOwner = true`; der Rest des Teils ist ein zweiter Körper. Im Flug hängt der TileHost des Klappenkörpers unter dem
beweglichen Transform und dreht sich mit. Thermik bleibt pro Part (beide Körper zählen zum selben Part).
Gebaut wird im Editor in Neutralstellung; im Flug wird die Lage relativ zum Klappen-Transform gerechnet.

## 3. Defaults (Nutzer: kleinere Kacheln, sauberere Kanten)
- tileSize 0.1 m (Hex und Square; die automatische Umschaltung 0.2 ↔ 0.15 beim Musterwechsel entfällt),
  thickness 0.025 m, Bereich tileSize 0.03–0.5.
- Nur wenn Messung zeigt, dass es nötig ist: MaxTiles anheben. Performance messen und im Bericht angeben
  (Kacheln, Vertices, Bauzeit für Dynawing-Decals und einen 8×5 m Tank bei 0.2 / 0.1 / 0.07 m).

## 4. Kantenqualität (Prioritäten 1 und 6, wichtigster Punkt)
Ziel: An keiner Kante steht eine Kachel als Block über die Kontur des Rumpfs hinaus. Ganze Kacheln am Footprint-Rand
dürfen nur dort ganz bleiben, wo sie flach aufliegen. Regel: Abstand jeder Kachel-Unterkante zur Oberfläche darunter
≤ max(3 mm, 0.05 · Pitch) in der Kachelfläche; was darüber hinausragt, wird an der Linie abgeschnitten, an der die
Oberfläche wegkippt (wie bei den Flügelkanten), mit Wand und Fase. Konvexe scharfe Kanten (Mk3-Kanten, Mk2-Chines,
Cockpit-Ränder) bekommen einen Schnitt exakt auf der Knicklinie; schmale Fasen dahinter bekommen Streifenkacheln
(bestehender Mechanismus, für Hex und Square). Szenen für die Abnahme siehe 6.

## 5. Gerader Rand vs. ganze Kacheln (Priorität 8)
Der Nutzer sieht auf runden Tanks keinen Unterschied. Prüfen, ob `StraightBorder` wirkt, wenn der Footprint kleiner als
der Tank ist (Breite 2 m, Höhe 2 m, maxWrap 120, und mit Unrolled). Wenn er nicht wirkt: beheben. Mit Unrolled gilt der
gerade Schnitt in den abgewickelten Koordinaten.

## 6. Abnahmeszenen (Preview, jeweils HexBlack und SquareBlack, tileSize 0.1, Kennzahl + eigene Sichtprüfung)
1. Dynawing.craft (enthält Mk3-Cockpit, Mk3-Rumpf, Adapter, Flügel), maxWrap 180 und Standard.
2. Mk3-Rumpf (`mk3FuselageLF_50` o. ä., Namen in GameData/Squad bzw. ReStock nachsehen) mit Decal auf dem flachen Boden,
   Breite = Bodenbreite + 1.2 m, Unrolled: Kacheln laufen über die Kante bis etwa zur halben Seitenwand.
3. Mk1-Cockpit (`Mark1Cockpit`) und Mk2-Cockpit (`mk2Cockpit_Standard` / `mk2Cockpit_Inline`) mit Decal auf der Unterseite,
   Footprint kleiner als das Teil: Rand sauber, keine abstehenden Kacheln (Nutzer-Screenshot 3).
4. Mk2-Rumpf mit den scharfen Chines (Nutzer-Screenshot 2 sieht aus wie eine Rumpfkante seitlich): keine Blöcke.
5. Steuerfläche (`elevon2` o. ä.) und Flügel: Decal belegt beide Körper, keine Kachel über der Scharnierfuge.
6. Nase 3.75 m Square@45 und runder Tank 2 m × 2 m Footprint mit/ohne StraightBorder (sichtbarer Unterschied).
Die Preview muss für Szene 5 die Körpertrennung genauso machen wie das Modul (ModuleControlSurface → eigener Owner).

## 7. Modul / Configs (anderer Agent)
- Oberflächen-Anbau (Priorität 2): `attachRules = 0,1,0,1,1` (andere Teile, z. B. Radial-Decoupler, dürfen auf das Decal).
  Kachel-Collider müssen im Editor treffbar sein (Layer 0, kein Trigger, unter dem Part). Kinder des Decals und deren Unterbaum
  sind nie Zielteile (TargetParts) und bekommen Kollisions-Ignore. Ein Kind darf ein Rebuild nicht auslösen, ausser die
  Zielteile ändern sich.
- B9PartSwitch (Priorität 9), optional (`:NEEDS[B9PartSwitch]`): zwei Umschalter mit Farbfeldern: "Muster" (Sechseck,
  Quadrat) und "Kachelart" (Schwarz, Weiss, Keramik). B9 darf zwei Schalter nicht auf DASSELBE Modul wirken lassen (beim
  Abwählen lädt B9 den Originalknoten des Moduls und setzt sonst das andere Feld zurück). Darum: Feld `pattern` bleibt in
  `ModuleAblativeDecal`, die Kachelart liegt als Feld `finish` in einem eigenen kleinen Modul `ModuleAblativeDecalFinish`,
  das ModuleAblativeDecal liest. B9 setzt Felder über `PartModule.Load` → `OnLoad`; beide Module müssen danach im Editor
  neu bauen (in OnLoad nach dem Start Rebuild anstossen, zusätzlich günstige Änderungsprüfung in Update als Rückfallebene).
  Ohne B9 bleiben die eigenen Auswahlfelder sichtbar; mit B9 werden sie ausgeblendet.
- Texturen: Auflösung über (Muster, Kachelart) aus `Textures.cfg` (Schlüssel `shape`, `finish`). Fehlende Atlas-Kombination
  HexWhite mit `tools/make-tile-atlases.ps1` erzeugen (Stil wie SquareWhite, Zellen sechseckig wie HexBlack).
- Steuerflächen (Priorität 10): Körperliste nach Abschnitt 2 in `Collect()`; Host des Klappenkörpers im Flug unter dem
  Klappen-Transform; Collider der Klappe ebenfalls dort; `SeparateOwner` setzen.
- Menü (Rechtsklick) logisch ordnen, Gruppen: "Kacheln" (Muster, Kachelart, Grösse, Winkel, Fuge, Dicke), "Fläche"
  (Breite, Höhe, Max. Umgriff, Abwickeln an/aus, über Teile hinweg, Rand gerade/ganze Kacheln), "Feinheiten" eingeklappt
  (Textur über Kacheln, Texturmassstab, Kachelverlust, Zufall), Statuszeilen (bedeckte Fläche, Schutz) oben. Klare Namen
  in en-us und de-de (gleiche Tags in beiden Dateien).
- Defaults aus Abschnitt 3 im Modul übernehmen; Brick aus allen Auswahllisten entfernen.
