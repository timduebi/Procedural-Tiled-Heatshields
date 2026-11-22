Sprache: [English](README.md) | **Deutsch**

# Procedural Tiled Heatshield

Procedural Tiled Heatshield (`AblatorDecals`) ist ein Mod für Kerbal Space Program 1.12.5. Das Part "Ablative Decal" (Anzeigename im Spiel: "Ablative Hitzekacheln") wird wie ein Decal auf die Oberfläche eines Parts geklebt. Es legt dünne 3D-Hitzekacheln auf das gewählte Teil. Angebautes Fahrwerk, Lufteinlässe und Steuerflächen können eine eigene Kachelhülle erhalten. Auf beweglichen Teilen fahren die Kacheln mit. Die Kacheln sehen aus wie bei Starship (Sechsecke) oder Space Shuttle (eckige Kacheln). Thermisch ist das Decal ein Ablator-Hitzeschild: Es nimmt beim Wiedereintritt Wärme von den Teilen auf, auf denen es liegt, und verbraucht dabei Ablator. Das Ganze bleibt ein einziges Part.

Version 1.11.5 wurde mit dem vollständigen Selbsttest und dem Vorschauwerkzeug an Stock-, Making-History-, ReStock-, ReStockPlus-, SSPX- und Near-Future-Modellen geprüft. Änderungen stehen im [CHANGELOG](CHANGELOG.de.md), der Wiedereinstieg in [docs/CLAUDE-HANDOFF.md](docs/CLAUDE-HANDOFF.md).

## Installation

1. Den Ordner `GameData/AblatorDecals` in das `GameData` der KSP-Installation kopieren.
2. ModuleManager ist nicht nötig. Optionale Patches liegen bei für B9PartSwitch (Farbfeld-Umschalter), **Community Tech Tree** (`heatManagementSystems`), **Deadly Reentry**, **FAR** und **VAB Organizer**.

Das Part erscheint im Editor in der Kategorie "Thermal" (mit VAB Organizer unter den Hitzeschilden). In Wissenschaft und Karriere wird es zusammen mit dem Mk2-Cockpit freigeschaltet (`supersonicFlight`, mit Community Tech Tree unter `heatManagementSystems`).

### Aus dem Quelltext bauen

```
dotnet build src/AblatorDecals -c Release
```

Der Build braucht die KSP-DLLs. Der Pfad zur KSP-Installation steht in der Eigenschaft `KSPDir` (Standard in `src/AblatorDecals/AblatorDecals.csproj`: `F:\Benutzer\Dokumente\science test`). Überschreiben mit:

```
dotnet build src/AblatorDecals -c Release -p:KSPDir="C:\Pfad\zu\KSP"
```

Der Build legt `AblatorDecals.dll` in `GameData/AblatorDecals/Plugins` und kopiert den ganzen Mod-Ordner nach `<KSPDir>\GameData\AblatorDecals`, falls dieses `GameData` existiert. Ziel ist .NET Framework 4.8.

Bauen, ohne etwas ins Spiel zu kopieren (die KSP-DLLs werden nur gelesen):

```
dotnet build src/AblatorDecals -c Release -p:KSPDir="C:\nonexistent" -p:Managed="C:\Pfad\zu\KSP\KSP_x64_Data\Managed"
```

## Bedienung im Editor

Angeklebt wird das Decal an die Oberfläche eines Parts (Surface-Attach), auch an Flügel und Steuerflächen. Eingestellt wird es im Rechtsklick-Fenster (PAW). Die Anzeigenamen kommen aus `Localization/en-us.cfg` und `de-de.cfg`.

Aufbau des Fensters (von oben nach unten):

1. **Statuszeilen** ohne Gruppe: im Editor "Abgedeckte Fläche" (m²), "Kacheln", "Abgedeckte Teile", "Ablator" (Einheiten), "Kachelmasse" (kg); im Flug Fläche und Schutz (siehe [Anzeigen im Flug](#anzeigen-im-flug)).
2. **Kacheln**: Muster, Kachelart, Kachelgröße, Musterwinkel, Fugenbreite, Dicke.
3. **Fläche**: Max. Umgriff, Bedeckte Höhe, Rumpffläche (Glatter Zylinder / Exaktes Modell), Cockpitform (bei unterstützten Cockpits, Kapseln und Landern).
4. **Feinheiten** (eingeklappt): Textur und Texturmaßstab (nur bei „Schlichte Keramik“), Kachelverlust, Verwitterung, Kachel-Abplatzen, Luken frei lassen, Fenster frei lassen, „Kacheln neu mischen“.

Das Editor-Fenster (`DecalWindow`) zeigt ganz oben **Alle Hitzeschilde hervorheben**, gefolgt von **Vorlagen** und schiffsweiten Einstellungen für Muster, Kachelart, Größe und Dicke.

| Gruppe | Deutsch | Englisch | Bereich | Standard | Wirkung |
|---|---|---|---|---|---|
| Kacheln | Muster | Pattern | Sechsecke, Quadrate | Sechsecke | Kachelform und Anordnung. |
| Kacheln | Kachelart | Tile type | Schwarz, Schlichte Keramik, NASA-Weiß, Stahl | Schwarz | Aussehen (Schwarz = Starship / Shuttle HRSI, NASA-Weiß = Shuttle LRSI, Stahl = Starship-Edelstahl, Schlichte Keramik = durchgehende Keramik). |
| Kacheln | Kachelgröße | Tile size | 0,03 bis 0,5 m | 0,1 m | Gitterabstand inklusive Fuge: bei Sechsecken der Abstand gegenüberliegender Kanten, bei Quadraten die Kantenlänge. |
| Kacheln | Musterwinkel | Pattern angle | -90 bis 90 Grad | 0 | Dreht das Muster um die Decal-Normale. |
| Kacheln | Fugenbreite | Joint width | 0 bis 0,02 m | 0 m | Breite der Fugen zwischen den Kacheln. Gespeicherte Decals behalten ihren Wert. |
| Kacheln | Dicke (Ablator) | Thickness (ablator) | 0,005 bis 0,3 m | 0,025 m | Kachelstärke. Bestimmt die Ablator-Menge (Abgedeckte Fläche mal Dicke mal 1000 Einheiten je m³). |
| Kacheln | Einstellungen auf alle kopieren | Copy settings to all shields | Knopf | | Überträgt Aussehen, Umgriff, Höhe, Fuge, Verwitterung und Kachel-Abplatzen auf alle anderen Decals des Schiffs (feste Größen vorgefertigter Cockpits bleiben erhalten). |
| Fläche | Abdeckung | Coverage | Ganzes Teil / Fläche begrenzen | Ganzes Teil | "Ganzes Teil": das Decal kachelt sein Teil, soweit es innerhalb von Max. Umgriff zur Decal-Normalen zeigt, an jeder Teilnaht gerade geschnitten. "Fläche begrenzen": Breite und Höhe wie bisher. Spielstände vor 1.3 laden als "Fläche begrenzen". |
| Fläche | Breite | Width | 0,2 bis 60 m | 2 m | Breite der Fläche (nur bei "Fläche begrenzen"). |
| Fläche | Höhe | Height | 0,2 bis 60 m | 2 m | Höhe der Fläche (nur bei "Fläche begrenzen"). |
| Fläche | Max. Umgriff | Max wrap angle | 10 bis 180 Grad | 100 | Wie weit die Kacheln um den Rumpf herumlaufen (Winkel zwischen Rumpf und Blickrichtung des Decals). |
| Fläche | Bedeckte Höhe | Covered height | 1 bis 100 % (1-%-Schritte) | 100 % | Anteil der Ausdehnung des Teils entlang der Decal-Normalen, gemessen von der Decal-Seite (100 % = ganzes Teil, 50 % auf der Seite eines Mk3-Tanks = bis zur halben Höhe). Gerader Schnitt, zusätzlich zu Max. Umgriff (der strengere gilt). |
| Fläche | Rumpffläche | Hull surface | Glatter Zylinder / Exaktes Modell | Automatisch | Auf runden Tanks & Stationsmodulen (ReStock, ReStockPBR, SSPX, NFLV) automatisch aktiv: überbrückt horizontale Versteifungsringe glatt und umhüllt herausragende Rohre/Leitern rein optisch mit einem ungekachelten Overlay. Aus bei Mk2, Mk3, Flügeln und Hubs. |
| Fläche | Abwickeln | Unroll | Entlang des Rumpfs / Von vorne gesehen | Entlang des Rumpfs | Wie Breite und Höhe gemessen werden: entlang der Oberfläche wie bei einem Aufkleber, oder als Fläche, die man von vorne sieht (projiziert, gerade Kante von vorne, z. B. für Nasenkegel). |
| Fläche | Rand | Edge | Ganze Kacheln / Gerader Schnitt | Ganze Kacheln | Verhalten am Rand der Fläche. |
| Feinheiten | Textur | Texture | Pro Kachel / Über die Kacheln hinweg | Pro Kachel | Nur bei "Schlichte Keramik": Bildausschnitt je Kachel oder ein Bild über alle Kacheln. |
| Feinheiten | Texturmaßstab | Texture scale | 0 bis 20 m | 2 m | Nur bei "Schlichte Keramik": Breite einer Texturwiederholung. Bei "Über die Kacheln hinweg" streckt 0 das Bild über die Fläche. |
| Feinheiten | Kachelverlust | Missing tiles | 0 bis 20 % | 0 | Anteil der Kacheln, die rein kosmetisch weggelassen werden. |
| Feinheiten | Verwitterung | Weathering | 0 bis 100 % (5-%-Schritte) | 0 % | Grund-Rußtönung bereits im Editor und beim Start sichtbar (ohne Netz-Neubau). |
| Feinheiten | Kachel-Abplatzen | Reentry tile shedding | Aktiv / Aus | Aus (Sandbox), Aktiv (Wissenschaft/Karriere) | Lässt bei extremer Hitze oder niedrigem Ablator in 5-%-Schritten bis zu 25 % der Kacheln abplatzen. Ingenieure können im EVA reparieren (Karriere: 1 Stock-EVA-Reparaturset). |
| Feinheiten | Luken frei lassen | Keep hatches clear | Frei lassen / Überkacheln | Frei lassen (`Überkacheln` bei `mk3CrewCabin`) | Legt fest, ob `Airlock`-Luken ausgespart oder überkachelt werden. |
| Feinheiten | Kacheln neu mischen | Shuffle tiles | Knopf | | Neue Zufallsauswahl der Atlaszellen und fehlenden Kacheln (auch bei den Symmetrie-Gegenstücken). |

### Muster und Kachelart

- **Sechsecke (Hex)**: Starship-Stil. Bei Winkel 0 zeigt die Spitze nach oben.
- **Quadrate (Square)**: Shuttle-Stil, rechtwinkliges Raster.
- Versetzte Quadrate (Brick) werden nicht mehr angeboten. Decals, die schon damit gespeichert sind, laden weiter und zeigen das Muster in ihrer Liste, bis man es wechselt.

| Kachelart | mit Sechsecken | mit Quadraten |
|---|---|---|
| Schwarz | `HexBlack` (Starship) | `SquareBlack` (Shuttle HRSI) |
| Schlichte Keramik | `Ceramic` mit `HexCeramicGlow` | `Ceramic` mit `SquareCeramicGlow` |
| NASA-Weiß | `HexWhite` | `SquareWhite` (Shuttle LRSI) |
| Stahl | `HexSteel` (Starship-Stahl) | `SquareSteel` |

Die Kachelgröße bleibt beim Musterwechsel unverändert (die frühere automatische Umschaltung 0,2 ↔ 0,15 m entfällt).

### B9PartSwitch (optional)

Ist [B9PartSwitch](https://github.com/blowfishpro/B9PartSwitch) installiert (am einfachsten über CKAN, braucht ModuleManager), ersetzt ein Patch (`Patches/B9PartSwitch.cfg`) die eigenen Auswahlfelder "Muster" und "Kachelart" durch zwei B9-Umschalter mit Farbfeldern in der Gruppe "Kacheln". Ohne B9 bleibt alles wie oben beschrieben; der Mod braucht B9 nicht.

Hinweis: B9 merkt sich die gewählte Variante selbst. Decals, die vor der Installation von B9 gespeichert wurden, stehen beim ersten Laden mit B9 auf Sechsecke/Schwarz und müssen einmal neu eingestellt werden.

### Ein Hitzeschild je Teil

Standard ist "Ganzes Teil": Das Decal kachelt die Fläche seines Teils samt daran klebender Klappen, Lufteinlässe und Fahrwerk-Außenflächen, soweit sie innerhalb von "Max. Umgriff" zur Decal-Normalen zeigt. An Teilnähten wird gerade geschnitten. Auf einem Rumpf aus fünf Teilen liegen so fünf Decals sauber nebeneinander.

Ein zweites Decal auf einem schon belegten Teil wird nicht gebaut: Es leuchtet rot, im Fenster steht "Bereich schon belegt", oben erscheint eine Meldung. Nur zwei Decals mit "Fläche begrenzen" dürfen sich ein Teil teilen; das jüngere füllt dann bis an die Kacheln des älteren. Ein Decal mit "Ganzes Teil" geht immer vor.

Wo welches Decal sitzt: Fährt man mit der Maus über ein Decal oder ist sein Fenster offen, erscheint ein orangefarbener Sechseck-Ring am Anbaupunkt; bei offenem Fenster bleibt das ganze Decal hervorgehoben. Am Ring lässt sich das Decal greifen. Angeklickt werden die Kacheln im Editor an ihrer genauen Form.

### Max. Umgriff

Die Kacheln laufen von der Mitte des Decals aus über die Oberfläche, bis der Umgriffwinkel erreicht ist, der Rumpf zu stark knickt oder die Oberfläche endet. Mit 180 Grad umhüllen sie einen geschlossenen Tank vollständig, soweit Breite und Höhe das zulassen. Doppelt belegt wird eine Stelle dabei nie: Eine Kachel wird verworfen, wenn an derselben Stelle auf derselben Seite schon eine liegt.

### Welche Teile ein Decal belegt

Das Teil, an dem das Decal klebt, dazu die Steuerflächen (`ModuleControlSurface`, `ModuleAeroSurface`) und Lufteinlässe (`ModuleResourceIntake`), die per Surface-Attach an diesem Teil kleben, und das eingefahrene Fahrwerk darauf. Gestapelte Nachbarteile und Flügel belegt es nicht; dafür bekommt jedes Teil sein eigenes Decal. Den Schalter "Über Teile hinweg" gibt es seit 1.4 nicht mehr; ältere Spielstände laden, der gespeicherte Wert wird ignoriert.

**Sperrzonen:** Luken (Collider mit Tag `Airlock`, plus 5 cm) bleiben auf allen belegten Teilen frei. RCS-Düsen (`ModuleRCS`/`ModuleRCSFX`, ein Würfel mit halber Kantenlänge `rcsKeepOut` = 0,12 m um jede Düse) bleiben seit 1.5 nur an Cockpits frei, deren Eintrag in `CockpitCover.cfg` `keepRcs = true` hat (Mk2 ja, Mk3 nein); sonst liegen die Kacheln über ihnen. Die Kacheln enden an den Sperrzonen gerade.

### Steuerflächen

Auf Teilen mit einer Steuerfläche (`ModuleControlSurface`, z. B. Elevons, Ruder, Flaps) ist die bewegliche Klappe ein eigener Körper: Keine Kachel liegt über der Scharnierfuge, die Kacheln der Klappe hängen im Flug an der Klappe und bewegen sich mit ihr. Gebaut wird in Neutralstellung (auch wenn die Klappe im Editor ausgefahren ist). Thermisch zählen Klappe und Rest zum selben Teil.

### Teile auf die Kacheln setzen

Am Decal selbst klebt nichts mehr (kein Decal auf Decal, `attachRules = 0,1,0,0,1`). Hält man ein anderes Teil an der Maus (aus der Teileliste, vom Schiff gegriffen, mit Alt kopiert oder als Baugruppe), etwa einen Radial-Decoupler, ein Fahrwerk, ein Solarpanel oder eine RCS-Düse, schalten alle Decals ihre Kachel- und Ring-Collider ab, auch frisch gebaute: Das Teil klebt am Rumpf darunter, wie ohne Decal. Liegt der Anbaupunkt im Kachelbereich, wird es danach um die Kacheldicke nach außen geschoben (auch die Symmetrie-Gegenstücke), sodass es auf den Kacheln sitzt. Solche Teile bekommen keine Kacheln (Ausnahme: eingefahrenes Fahrwerk bekommt seine eigenen, siehe unten; Steuerflächen und Einlässe am Teil des Decals werden mitgekachelt und nicht verschoben), kollidieren im Flug nie mit den Kacheln (auch nach Abtrennen oder Andocken; Fahrwerk samt Rad-Collider) und werden thermisch wie das Teil geschützt, auf dem sie sitzen. Die Kacheln selbst dürfen den Boden berühren. Teile, die in älteren Spielständen am Decal hängen, laden weiter.

Ein Decal lässt sich nicht auf ein anderes setzen: Über fremden Kacheln bleibt die Vorschau rot, man muss auf freien Rumpf. Überlappen sich zwei Decals trotzdem, weicht das jüngere aus und legt keine Kachel auf die des älteren.

### Lufteinlässe und Fahrwerk

Lufteinlässe, die am Teil des Decals kleben, werden außen mitgekachelt; die Öffnung bleibt frei (Flächen, die in den Fahrtwind zeigen, und Kanalwände werden ausgelassen), das Ansaugen ändert sich nicht. Ein Einlass ohne Ansaug-Transform bekommt gar keine Kacheln. Fahrwerk ist nie Rumpf-Ziel; die Kacheln laufen auf dem Rumpf unter der Fahrwerksstelle durch, das Rad darf beim Ausfahren durch die Kacheln gehen. Zusätzlich bekommt Fahrwerk im Kachelbereich Kacheln auf seiner ganzen Außenhaut in eingefahrener Lage (gewölbte Gondeln, Klappen, Fahrwerksschacht-Deckel, auch Skinned Meshes), die beim Ausfahren mitfahren; so sieht der Rumpf eingefahren durchgehend gekachelt aus. Frei bleiben nur Flächen, die zum Rumpf zeigen oder mehr als 2 cm im Rumpf liegen, und was eingefahren verdeckt ist (Rad, Federbein, Schachtinneres).

### Cockpit-Form

Auf den spitzen Cockpits, die in `CockpitCover.cfg` stehen (Mk1-Cockpit `Mark1Cockpit`, Mk2-Cockpit `mk2Cockpit_Standard`, Mk3-Cockpit `mk3Cockpit_Shuttle`), erscheint in "Fläche" der Schalter "Cockpit-Form" (Standard an). Dann bedeckt das Decal das ganze Cockpit bis knapp unter die Fenster: Breite und Höhe ergeben sich aus der Teilgröße (doppelter Abstand des entferntesten Rumpfpunkts), Max. Umgriff ist 180 Grad, andere Teile werden nicht belegt. Aus: wie jedes andere Decal.

### Alle Hitzeschilde auf einmal

Der Knopf mit den Sechsecken in der Editor-Leiste öffnet ein kleines Fenster mit Muster, Kachelart, Kachelgröße und Dicke. "Auf alle Hitzeschilde anwenden" setzt diese vier Werte an allen Decals des Schiffs (mit B9PartSwitch über dessen Umschalter) und lässt sich mit Undo zurücknehmen. Einzelne Decals bleiben danach einzeln einstellbar.

### Rand

"Ganze Kacheln": Am Rand der Fläche liegen nur ganze Kacheln, die Kante ist also gezackt. "Gerader Schnitt": Randkacheln werden gerade am Rand der Fläche abgeschnitten.

Unabhängig davon enden Kacheln an Part-Kanten sauber: Kacheln, die über eine Kante hinausragen würden, werden an der Kante beschnitten oder entfallen.

### Kachelverlust

Lässt zufällig gewählte Kacheln weg, damit die Fläche abgenutzt wirkt. Die Fugen zu den Nachbarn bleiben erhalten, die Nachbarn bekommen dort Seitenwände. Fehlende Kacheln zählen nicht zu Fläche, Masse, Ablator oder Thermik.

### Weitere Hinweise zur Bedienung

- Änderungen werden im Editor im Hintergrund berechnet. Die Anzeige kann während des Schiebens eines Reglers kurz hinter dem Regler herhinken; es sollte nicht ruckeln.
- Ohne Parent (Decal noch nicht angeklebt) zeigt der Mod eine flache Ersatzfläche, damit man Muster und Kachelart beurteilen kann.
- Symmetrie: Alle Einstellungen (auch Kachelart und Abwickeln) gehen an die Symmetrie-Gegenstücke. Bei Spiegel-Symmetrie bekommt das Gegenstück den Musterwinkel mit umgekehrtem Vorzeichen.
- Masse und Kosten ergeben sich aus der abgedeckten Fläche (Standard 1,5 kg und 20 Funds je m², dazu das Part selbst).

## Anzeigen im Flug

Im Rechtsklick-Fenster zeigt das Decal im Flug:

| Anzeige | Bedeutung |
|---|---|
| Im Fahrtwind / Facing airflow | Anteil der Kachelfläche, der dem Luftstrom zugewandt ist (Summe aus Fläche mal Cosinus zur Anströmung, geteilt durch die Gesamtfläche). |
| Hitze abgefangen / Heat blocked | Anteil der Konvektionshitze der abgedeckten Teile, den das Decal gerade aufnimmt. Bei mehreren Teilen steht die Teileanzahl dahinter. |
| Aufnahme / Absorbing | Aktuell aufgenommene Leistung in kW. |
| Aufgenommene Energie / Energy absorbed | Insgesamt aufgenommene Energie in MJ. |
| Heißeste Hülle / Hottest covered skin | Hauttemperatur des heißesten abgedeckten Teils, relativ zu dessen Maximaltemperatur (Wert / Maximum in K). |
| Kacheltemperatur / Tile temperature | Rechnerische Oberflächentemperatur der heißesten Kachelgruppe (nur Optik, siehe unten). |
| Abgedeckte Fläche, Abgedeckte Teile | wie im Editor. |

## Thermik in Kürze

- Das Decal schützt nur Teile, auf denen Kacheln liegen, und nur den Teil der Kachelfläche, der im Fahrtwind liegt. Je Ziel-Part gilt: Frontfläche der Kacheln (Fläche mal Cosinus zur Anströmung, nur positive Anteile) geteilt durch die Frontfläche des Parts (`exposedArea`), gedeckelt bei 100 % über alle Decals auf diesem Part.
- Es greift, sobald die Hauttemperatur des Parts über `ablationTempThresh` (Standard 500 K) liegt. Es nimmt dann diesen Anteil der Konvektionshitze des Parts auf (`thermalConvectionFlux`, mal `efficiency`). Sonnen- und Planetenstrahlung bleiben unberührt.
- Aufgenommene Energie verbraucht Ablator aus einem gemeinsamen Vorrat des Decals. Mit den Standardwerten kostet eine Einheit 2400 kJ (`pyrolysisLossFactor` mal spezifische Wärme mal Dichte des Ablators). Ist der Vorrat leer, schützt das Decal nicht mehr. Reicht er für einen Schritt nicht ganz, bekommen alle Teile denselben Bruchteil.
- Die Ablator-Menge wird im Editor aus Fläche, Dicke und `unitsPerCubicMeter` gesetzt und im Flug nie neu gesetzt.
- **Glühen** ist rein optisch. Jede Kachelgruppe (Bin: Kacheln, deren Ausrichtung innerhalb `glowBinAngle` = 18 Grad liegt, höchstens `glowMaxBins` = 12) hat eine rechnerische Oberflächentemperatur, die von Anströmung und Hauttemperatur abhängt; Hauttemperatur und Wärmeübergang sind seit 1.5 für das ganze Decal gemittelt (nach Kachelfläche), damit das Glühen an Teilnähten nicht springt. Das beeinflusst die Thermik nicht. Seit 1.5 drei Zustände wie beim Vorbild: normal (kein Glühen, unter 550 K), mittel (800 K: orange, dunkle Ränder), Spitze (1300 K: weißgelber Kern, orange Ränder), dazwischen weich überblendet (Smoothstep, zeitlich gedämpft mit `glowFadeTime` = 3 s). Umgesetzt mit dem Shader `KSP/Emissive/Specular`: eine Emissions-Textur je Muster (`HexGlow`, `SquareGlow`, gleiches Atlas-Layout wie die Kacheltextur, je Kachel weicher Verlauf von warmweißem Kern zu dunkelorangem Rand, erzeugt mit `tools/make-glow-textures.ps1`) wird mit `_EmissiveColor` aus `glowRamp` getönt. Keramik nutzt die Emissionsatlanten `HexCeramicGlow` und `SquareCeramicGlow`. Bei den Standardeinstellungen sitzt der helle Kern auf jeder Kachel. Bei eigenem Maßstab oder „Über die Kacheln hinweg“ folgt das Glühbild der gewählten Projektion. Ohne diesen Shader fällt der Mod auf `_TemperatureColor` zurück (flach je Kachelgruppe; Logzeile `glow=`). KSPs eigenes Glühen des Teils (Material-Property-Block) wird von den Kachel-Renderern ferngehalten. Vorschau der drei Zustände: `preview/v11/glow-states.png`.
- **Verkohlung** färbt die Kacheln von Weiß nach Braungrau (`charColor`) mit dem verbrauchten Anteil des Ablators. Sie zeigt also den Verbrauch an, mehr nicht.
- Stirbt das Parent, geht das Decal mit. Stirbt ein anderes abgedecktes Teil, wird das Netz neu gebaut und die Kacheln darauf entfallen.

## Eigene Texturen

Am einfachsten überschreibt man eine mitgelieferte PNG-Datei. Für eine zusätzliche Textur:

1. Eine PNG-Datei in `GameData/AblatorDecals/Textures/` legen. KSP lädt sie als Textur.
2. Einen `ABLATORDECAL_TEXTURE`-Eintrag mit `shape` und `finish` anlegen; die Auswahl läuft nur über Muster und Kachelart, es gilt der erste passende Eintrag (siehe `Textures/Textures.cfg`, auch in einer eigenen `.cfg` möglich):

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

| Feld | Bedeutung |
|---|---|
| `texture` | URL der Textur ohne Endung, wie sie KSP kennt (Ordnerpfad ab `GameData`). |
| `title` | Anzeigename (Text oder Lokalisierungs-Tag), nur zur Beschreibung. Fehlt er, gilt der Dateiname. |
| `shape` | `Hex`, `Square` oder `Any`. `Square` gilt auch für Versetzte Quadrate. `Any` passt zu jedem Muster. Standard: `Any`. |
| `finish` | Kachelart, für die die Textur gilt: `Black` oder `Ceramic` (`White` gibt es seit 1.4 nicht mehr). Gewählt wird die erste Textur, deren `shape` zum Muster und deren `finish` zur Kachelart passt (genaue Form vor `Any`). Passt keine, nimmt der Mod die erste zur Form passende und schreibt eine Warnung ins Log. |
| `cells` | `Spalten, Zeilen` des Atlas. Fehlt das Feld, ist es eine einfache Textur (dann wirken "Textur" und "Texturmaßstab" in den Feinheiten). |
| `emissive` | Optional (1.5): Emissions-Textur im selben Atlas-Layout für das Glühen (siehe `tools/make-glow-textures.ps1`). Fehlt sie, glüht die Kachel flach. Als `emissive` genannte Texturen erscheinen nicht als Kacheltextur. |
| `plainOptions` | Gibt bei einer texturierten Keramikoberfläche die Bedienelemente für Texturprojektion und Maßstab frei. Standardmäßig nutzt die Kachel die Atlaszellen. |
| `randomRotation` | Nur bei Atlanten: dreht den Zellinhalt zufällig um Vielfache von 60 Grad (Sechsecke), 90 Grad (Quadrate) bzw. 180 Grad (Versetzte Quadrate). |

**Atlas**: Eine Textur mit `cells = c, r` besteht aus `c × r` gleich großen Zellen. Jede Kachel bekommt per Hash aus Gitterposition und Decal-Seed eine Zelle zugewiesen. Eine Hex-Zelle füllt das Sechseck mit Spitze oben, dessen Umkreisradius die halbe Zellkante ist; Quadrat-Zellen füllen die Zelle ganz. Die Abbildung ist von außen nicht gespiegelt, Text im Atlas erscheint also lesbar.

**Alpha-Kanal = Glanz**: Der Shader `KSP/Specular` nutzt den Alpha-Kanal der Textur als Glanzmaske. Die Textur ist also nie als durchsichtig gedacht. Hoher Alpha-Wert heißt glänzend.

Die mitgelieferten Atlanten (`HexBlack.png`, `SquareBlack.png`, je 1024 × 1024 px, 4 × 4 Zellen) erzeugt das Skript `tools/make-tile-atlases.ps1` (Windows PowerShell 5.1, `System.Drawing`, deterministisch mit festem Seed; es schreibt auch die nicht mehr benutzten weißen Atlanten `HexWhite.png` und `SquareWhite.png`, die gelöscht werden können):

```
powershell -File tools/make-tile-atlases.ps1 [-OutDir <Ordner>]
```

Ohne `-OutDir` schreibt es nach `GameData/AblatorDecals/Textures`. `Ceramic.png` (einfache Textur) stammt aus `tools/make-ceramic-texture.ps1`. Das Part-Icon-Modell `Assets/placeholder.mu` stammt aus `tools/make-placeholder-mu.ps1`.

## Config-Referenz

Diese Felder gehören zu `MODULE { name = ModuleAblativeDecal }` und erscheinen nicht im Rechtsklick-Fenster. Sie lassen sich in `Parts/ablativeDecal.cfg` oder per ModuleManager-Patch ändern.

| Feld | Standard | Bedeutung |
|---|---|---|
| `ablativeResource` | `Ablator` | Name der Ressource, die verbraucht wird. |
| `unitsPerCubicMeter` | 1000 | Ablator-Einheiten je m³ Kachelvolumen (Fläche mal Dicke). |
| `pyrolysisLossFactor` | 6000 | Wie beim Stock-`ModuleAblator`. Mit spezifischer Wärme und Dichte der Ressource ergibt das die Energie je Einheit. |
| `ablationTempThresh` | 500 | Hauttemperatur (K) des Ziel-Parts, ab der das Decal Wärme aufnimmt. |
| `efficiency` | 1 | Anteil der abgedeckten Konvektionshitze, der abgezogen wird (Kalibrierregler). |
| `shieldNormal` | 0, 1, 0 (im Part: 0, -1, 0) | Außennormale im Part-Raum. `node_attach` zeigt in das Parent hinein, daher steht im Part `0, -1, 0`. |
| `logInterval` | 0 | Sekunden zwischen Thermik-Logzeilen. 0 = aus. |
| `textureFolder` | `AblatorDecals/Textures/` | Ordner, aus dem Texturen gelistet werden. |
| `shader` | `KSP/Emissive/Specular` | Shader der Kacheln (seit 1.5 mit Emission für das Glühen; bis 1.4 `KSP/Specular`). Fehlt er, wird `KSP/Diffuse` benutzt. |
| `specColor` | 0.25, 0.25, 0.25 | `_SpecColor` des Kachelmaterials (RGB). |
| `shininess` | 0.35 | `_Shininess` des Kachelmaterials. |
| `charColor` | 0.42, 0.37, 0.34 | Kachelfarbe bei leerem Ablator (RGB). |
| `glowEnabled` | True | Glühen ein oder aus. |
| `glowEmissivity` | 0.95 | Emissionsgrad der Kacheloberfläche für die Glüh-Rechnung (höher = kühlere Kacheln, weniger Glühen). |
| `glowHeatCapacity` | 30 | Wärmekapazität der Kachelfläche in kJ/m²K (Trägheit des Glühens; 30 ergibt beim Wiedereintritt etwa 20 bis 40 s Verzug). |
| `glowConduction` | 0.02 | Wärmeleitung zur Hülle in kW/m²K. |
| `glowRamp` | `550 1 0.5 0.14 0; 800 1 0.5 0.14 0.55; 1300 1 1 1 1.2` | Tönung des Glühens über der Kacheltemperatur: Stützstellen `K r g b a` (a = Stärke), dazwischen Smoothstep. Standard: normal, mittel (orange × 0,55), Spitze (weiß × 1,2). Der Shader bekommt `r·a, g·a, b·a` als `_EmissiveColor`, mal Emissions-Textur. |
| `glowBinAngle` | 18 | Kacheln, deren Normalen innerhalb dieses Winkels liegen, teilen Temperatur und Farbe (kleiner = feinere Übergänge, mehr Materialien). |
| `glowMaxBins` | 12 | Höchstzahl solcher Gruppen je Kachel-Mesh. |
| `glowFlatColor` | `1, 0.75, 0.45` | Emissionsfarbe für Texturen ohne `emissive`. |
| `glowFadeTime` | 3 | Zeitkonstante in s, mit der die angezeigte Glühfarbe ihrem Zielwert folgt. 0 = sofort. |
| `rcsKeepOut` | 0.12 | Halbe Kantenlänge in m des kachelfreien Würfels um jede RCS-Düse eines Cockpits mit `keepRcs = true`. |
| `massPerArea` | 0.0015 | Masse je m² abgedeckter Fläche in t. |
| `costPerArea` | 20 | Kosten je m² abgedeckter Fläche. |
| `surfaceOffset` | 0.003 | Abstand der Kacheln zur Rumpfoberfläche in m (gegen Durchstoßen). |
| `cornerBendAngle` | 35 | Größter Knickwinkel (Grad) der geglätteten Oberfläche, über den Kacheln noch aufgesetzt werden. |
| `faceBendAngle` | 60 | Größter Knickwinkel (Grad) einzelner Rumpfflächen. |
| `curvatureFactor` | 1 | Mindest-Biegeradius, über den Kacheln laufen, in Kachelgrößen. Größer heißt strenger. |
| `maxTiles` | 60000 | Obergrenze der Kachelzahl je Decal. Wird sie erreicht, steht eine Warnung im Log. |
| `colliderPatchSize` | 1 | Kantenlänge in m der Flächen, die je eine Collider-Box bekommen (Anklicken im Editor). |
| `gearMargin` | 0.3 | Abstand in m um Fahrwerk (Teil, Rad samt Federweg), in dem keine Kachel-Collider-Box entsteht. Die Kacheln bleiben sichtbar. |

Die Standardwerte sind die im Code; `Parts/ablativeDecal.cfg` setzt nur `shieldNormal`. Persistent im Spielstand sind die Editor-Einstellungen, die abgedeckte Fläche und ein zufälliger Seed je Decal (`seed`, bestimmt die Zufallsauswahl von Atlaszellen und fehlenden Kacheln). Die Kachelart steht im eigenen Modul `ModuleAblativeDecalFinish` (Feld `finish`). Spielstände von 1.0 mit dem alten Feld `textureName` werden beim Laden auf die passende Kachelart umgestellt.

## Test-Checkliste für das Spiel

Der Mod wurde nur offline geprüft. Bitte im Spiel in dieser Reihenfolge testen. Meldet zu jedem Schritt zurück, ob er wie beschrieben aussah, und hängt dazu die genannten Zeilen aus `KSP.log` an. Alle Zeilen des Mods beginnen mit `[AblatorDecals]`. Zurückmelden sollt ihr vor allem diese drei Zeilenarten (mit `KSP.log` nach `[AblatorDecals]` durchsuchen): `[AblatorDecals] build …` (je Aufbau, Editor und Flug), `[AblatorDecals] flight check …` (3 s nach Physikstart) und `[AblatorDecals] reentry summary …` (nach dem Wiedereintritt). Warnungen und Fehler (`LogWarning`, `LogError`) bitte immer mitschicken.

Neu in 1.5.0 (bitte zuerst prüfen): Schritte 24 bis 27. Aus 1.4.0: Schritte 4, 8, 9, 16, 20, 21, 22 und 23.

1. **Start und Part-Liste.** KSP starten, Editor öffnen. Das Part "Ablative Hitzekacheln" soll in der Kategorie "Thermal" stehen, mit einem Icon (sieben Sechseck-Kacheln). Prüfen, ob die Beschriftungen im Rechtsklick-Fenster auf Deutsch bzw. Englisch erscheinen und keine `#LOC_...`-Tags sichtbar sind.
2. **Anheften.** Decal an einen Tank kleben. Kacheln sollen sofort auf dem Rumpf liegen, texturiert, von hinten dunkel, Standardgröße 0,1 m. Log: die `build`-Zeile mit `onSurface=True`:
   `[AblatorDecals] build Editor ablativeDecal parts=[...] onSurface=True tiles=... partial=... bins=... colliders=... covered=...m2 time=...ms (core ...ms) texture=... shader=... glow=... emissive=...`
   Bitte `texture`, `shader`, `glow`, `emissive` und `time` melden (erwartet: `shader=KSP/Emissive/Specular glow=_EmissiveColor emissive=AblatorDecals/Textures/HexGlow` bzw. `SquareGlow`, bei Keramik `HexCeramicGlow` bzw. `SquareCeramicGlow`). Steht dort `NONE` oder `KSP/Diffuse`, ist etwas schiefgelaufen.
3. **Rechtsklick-Fenster.** Oben stehen die Statuszeilen (Abgedeckte Fläche, Kacheln, …), darunter die Gruppen "Kacheln" und "Fläche", ganz unten "Feinheiten" eingeklappt. Feinheiten aufklappen: Kachelverlust und "Kacheln neu mischen"; "Textur" und "Texturmaßstab" erscheinen nur bei Kachelart "Schlichte Keramik". Muster bietet nur Sechsecke und Quadrate.
4. **Muster und Kachelart (ohne B9).** Alle vier Kombinationen durchschalten: Sechsecke/Quadrate mit Schwarz und Schlichte Keramik (Weiß gibt es nicht mehr; ein mit Weiß gespeichertes Decal lädt schwarz). Kachelform stimmt, Textur nicht gespiegelt, Fugen dunkel und lückenlos. Log-Zeile `texture=` melden; eine Warnung `no texture for pattern ...` darf nicht kommen.
5. **Regler ohne Ruckeln.** Kachelgröße (jetzt bis hinunter auf 0,03 m), Breite, Höhe, Fugenbreite, Musterwinkel und Dicke mit dem Schieber ziehen. Das Bild darf leicht nachlaufen, aber das Spiel darf nicht ruckeln, und das Rechtsklick-Fenster darf sich beim Ziehen nicht neu aufbauen. Bitte melden, ab welchen Werten es zäh wird (z. B. 0,05 m auf 4 × 4 m), mit `time=` aus der `build`-Zeile.
6. **Abwickeln.** Decal unter einen Mk3-Rumpf (flacher Boden), Breite etwas größer als der Boden. "Abwickeln" auf "Entlang des Rumpfs": Kacheln laufen über die Kante ein Stück die Seitenwand hoch. Auf "Von vorne gesehen": wie bisher, Fläche von vorne gesehen.
7. **Voller Umgriff um einen Tank.** Breite und Höhe so wählen, dass die Fläche den Tank überspannt, "Max. Umgriff" auf 180. Die Kacheln sollen den Tank ohne Doppelungen umhüllen und enden, nicht endlos rechnen. Bauzeit aus der `build`-Zeile (`time=`) melden.
8. **Nase, Flügelkante, gestapelte Tanks.** Decal auf eine Nase und an die Vorderkante eines Flügels kleben: Kacheln reichen bis zur Kante und enden sauber. Zwei bis drei Tanks stapeln, Decal auf den mittleren: Kacheln nur auf diesem Tank, gerade an den Nähten geschnitten (den Schalter "Über Teile hinweg" gibt es nicht mehr). Elevon und Lufteinlass direkt an den Rumpf des Decals kleben: beide werden mitgekachelt, `parts=[...]` in der `build`-Zeile nennt sie.
9. **Teile auf die Kacheln setzen.** Einen Radial-Decoupler (z. B. TT-38K), ein Fahrwerk, ein Solarpanel und eine RCS-Düse in den Kachelbereich eines Decals setzen, jeweils einmal aus der Teileliste, einmal vom Schiff gegriffen und einmal mit Alt kopiert; am Decoupler einen kleinen Booster; mit 2-facher Symmetrie. Alles muss sich anbringen lassen und danach auf den Kacheln sitzen. Erwartet: die Vorschau wird über den Kacheln grün, der Decoupler sitzt danach auf den Kacheln (nicht in ihnen), die Kacheln bauen sich nicht neu auf, Decoupler und Booster bekommen keine Kacheln. Im Flug: Decoupler zünden, der Booster trennt sauber, ohne an den Kacheln hängen zu bleiben oder das Schiff zu schütteln.
10. **Steuerflächen.** Ein Elevon (z. B. Elevon 2 oder ein ReStock-Gegenstück) an einen Flügel, Decal auf den Flügel mit Fläche bis über das Elevon, dann ein zweites Decal direkt auf das Elevon. Erwartet im Editor: Kacheln auf Flügel und Elevon, keine Kachel über der Scharnierfuge, auch wenn man "Ausfahren"/"Deploy" am Elevon einschaltet (Kacheln bleiben dann in Neutralstellung). Im Flug: Steuern (W/S, Q/E) – die Kacheln auf der Klappe bewegen sich mit, die auf dem Flügel nicht. Rechtsklick auf die Kacheln der Klappe bei Vollausschlag öffnet das Fenster des Decals.
11. **B9PartSwitch (nur wenn installiert, z. B. über CKAN).** Statt "Muster" und "Kachelart" stehen zwei B9-Umschalter mit Farbfeldern in der Gruppe "Kacheln". Umschalten: Kacheln bauen sich sofort neu (neue `build`-Zeile mit passendem `texture=`). Beide Umschalter abwechselnd betätigen: Der eine darf den anderen nicht zurücksetzen. Mit Symmetrie: alle Gegenstücke wechseln mit. Speichern/Laden: Auswahl bleibt. Bitte melden, ob die Farbfelder erscheinen.
12. **Rand und Kachelverlust.** "Rand" zwischen "Ganze Kacheln" und "Gerader Schnitt" wechseln, "Kachelverlust" auf 10 bis 20 %, "Kacheln neu mischen" drücken. Fläche und Kachelmasse ändern sich entsprechend.
13. **Symmetrie.** Decal mit 2-facher und radialer Symmetrie anbringen. Alle Gegenstücke bekommen dieselben Einstellungen, auch Kachelart und Abwickeln. Mit Spiegel-Symmetrie und Musterwinkel 30: Das Gegenstück hat -30 Grad.
14. **Speichern und Laden, alte Schiffe.** Schiff speichern, Editor verlassen, laden: Einstellungen und Kacheln unverändert. Ein mit 1.0 gespeichertes Schiff laden (z. B. Dynawing): Kachelart entspricht der früheren Textur, Decals mit "Versetzte Quadrate" laden weiter. Undo und Redo ausprobieren.
15. **Start.** Schiff starten. Die Kacheln müssen auf der Rakete sichtbar und texturiert sein. Log: `[AblatorDecals] build Flight ...` und nach etwa 3 Sekunden Physik `[AblatorDecals] flight check ablativeDecal on <Part>: visible=... enabled=... layer=... vertices=... bins=... texture=... shader=... glow=... emissive=...` (auf einem Elevon erscheinen zwei Zeilen, wenn es zwei Körper hat). `visible=False` oder `texture=NONE` bedeutet ein Problem. Kommt `tile materials replaced on <Teil> (...), restoring`, bitte die ganze Zeile melden (sie nennt den ersetzten Slot und Shader; KSP selbst kopiert Materialien beim Hervorheben im Editor und in der Thermik-Ansicht, das ist harmlos). Rechtsklick auf das Decal öffnet das Fenster, die Flug-Anzeigen stehen oben.
16. **Wiedereintritt.** Mit dem Decal voraus aus dem Orbit wieder eintreten. "Im Fahrtwind" und "Hitze abgefangen" steigen, "Aufnahme" ist größer als 0, der Ablator sinkt, die Kacheln dunkeln (Verkohlung) und glühen auf der angeströmten Seite. Mit Decal auf Flügel und Elevon: Steuern während des Eintritts, "Im Fahrtwind" ändert sich leicht mit dem Ausschlag. Nach 10 Sekunden ohne Aufnahme die Zeile `[AblatorDecals] reentry summary ...` melden. Wer die laufende Thermik mitlesen will, setzt `logInterval = 1` im Part.
17. **Abtrennen einer Stufe.** Eine Stufe abwerfen, auf der Kacheln liegen. Die Kacheln fliegen mit dem Teil mit, an dem sie hängen. Läuft ein Decal über mehrere Teile, wird das Netz neu gebaut (neue `build`-Zeile). Beim Verlust des Parents verschwindet das Decal.
18. **Keine NullReferenceException im Editor (1.2.0).** Decal mehrmals von einem Rumpf über zwei Teile abnehmen, frei herumziehen, wieder anbringen, kopieren (Alt-Klick), mit Symmetrie setzen. Danach im `KSP.log` nach `NullReferenceException` und `SetOpacity` suchen: keine Treffer erwartet.
19. **Kein Decal auf Decal.** Ein zweites Decal über die Kacheln des ersten ziehen: Vorschau rot, Anbringen nicht möglich. Daneben auf freien Rumpf setzen und so vergrößern, dass es das erste überlappt: Das neue legt keine Kacheln auf die alten.
20. **Fahrwerk und Lufteinlass.** Fahrwerk (auch Fahrwerksschacht) und einen Lufteinlass in den Kachelbereich unter einen Mk2-Rumpf setzen. Erwartet: Lufteinlass wird außen überkachelt (Öffnung frei), eingefahrenes Fahrwerk auf seiner ganzen Außenhaut (Gondel, Klappen; Rad und Federbein frei), die Logzeile `gear ...: ... retracted faces=... bodies=... skinned=...` nennt mehr als 0 Flächen auch bei GearSmall; Ein- und Ausfahren, Rollen und Landen auf der Piste ohne Explosion oder Springen. Beim Wiedereintritt überhitzt das Fahrwerk nicht schneller als der Rumpf.
21. **Cockpit-Form** (erst wenn Werte in `CockpitCover.cfg` stehen). Decal unter ein Mk1-, ein Mk2- und ein Mk3-Cockpit (`mk3Cockpit_Shuttle`): Schalter "Cockpit-Form" erscheint nur dort; an: Kacheln bis knapp unter die Fenster, Spitze ganz bedeckt.
22. **Editor-Knopf.** Sechseck-Knopf in der Editor-Leiste, Muster/Kachelart/Größe/Dicke wählen, "Auf alle Hitzeschilde anwenden": alle Decals wechseln (mit B9 auch die B9-Umschalter), Ablator und Kachelmasse folgen der Dicke, ein Undo nimmt es zurück, Speichern/Laden behält es.
23. **Glühen und Sperrzonen (1.4; Glühen und RCS in 1.5 geändert, siehe 24 und 26).** Wiedereintritt: Kacheln glühen dunkelrot bis gedämpft orange, kaum gelb; das Glühen blendet über einige Sekunden ein und aus, ohne Sprung und ohne Flackern zwischen Kachelgruppen, auch nach Umschalten der Thermik-Ansicht (F10/Overlay). Mk2-Cockpit mit RCS: um die Düsen und die Luke bleiben die Kacheln frei (Logzeile `keep-out: rcs=... hatches=...`). Neues Decal: Fugenbreite steht auf 0.
24. **Glühen (1.5).** Wiedereintritt mit Hex- und Quadrat-Kacheln: jede Kachel glüht für sich mit dunklerem Rand, mittlere Hitze orange, größte Hitze weißgelber Kern mit orangem Rand (wie `preview/v11/glow-states.png`); über die gewölbte Unterseite und über Teilnähte hinweg weiche Übergänge ohne Stufen oder Bänder; Ein- und Ausblenden über einige Sekunden. `build`-Zeile: `glow=_EmissiveColor`. Bitte Screenshots bei mittlerer und größter Hitze.
25. **Bedeckte Höhe (1.5).** Decal seitlich an einen Mk3-Tank, "Ganzes Teil", Max. Umgriff 180: "Bedeckte Höhe" 100 % = wie bisher, 50 % = Kacheln bis zur halben Höhe des Tanks, gerade geschnitten; mit kleinem Max. Umgriff gilt der strengere Wert.
26. **RCS-Düsen (1.5).** Mk2-Cockpit: Düsen frei (sobald `keepRcs = true` in `CockpitCover.cfg` steht; Logzeile `keep-out: rcs=<n> ...` mit n > 0); Mk3-Cockpit und andere Teile mit RCS: Düsen überkachelt. Luken überall frei.
27. **Lagerraum und Rampe (1.5).** Decal auf Mk3-Lagerraum und Mk3-Laderampe (`mk3CargoRamp`), im Editor Türen bzw. Rampe öffnen und schließen: die Kacheln fahren mit, nichts bleibt in der Luft stehen, keine weißen Flächen. Dasselbe im Flug. Kommt eine Zeile `tile materials replaced on ...`, bitte mitschicken.

Ebenfalls hilfreich: die komplette `KSP.log` nach dem Test sowie die Angabe der installierten Mods (ReStock, Scatterer, TextureReplacer, TexturesUnlimited, B9PartSwitch und ähnliche greifen in Materialien und Modelle ein).

## Bekannte Grenzen

- **Nur offline geprüft.** Geometrie, Thermik-Mathematik und Glüh-Rechnung laufen im Selbsttest, die Optik in einem Vorschau-Renderer, die KSP-Aufrufe wurden gegen dekompilierte Quellen geprüft. Verhalten im Spiel ist nicht bestätigt. Besonders offen: Aussehen der Kacheln unter Szenen-Beleuchtung und Render-Mods, Glühen (hängt an `_EmissiveColor` von `KSP/Emissive/Specular`, unter Render-Mods wie Deferred ungeprüft), Verhalten beim Abtrennen von Stufen, Leistung bei großen Flächen.
- **Teile im Kachelbereich** kleben am Rumpf und werden auf die Kacheln geschoben. Verschiebt man danach das Decal, bleiben sie, wo sie sind (erst neues Anbringen richtet sie wieder aus).
- **Cockpit-Form**: Die Schnittebenen in `CockpitCover.cfg` gelten für die Stock-Modelle. Andere Modelle brauchen eigene Werte (Knoten mit `:NEEDS[...]`, siehe Kommentar in der Datei). Ohne Ebenen bleibt der Schalter verborgen. Stock und ReStock verwenden für beide Cockpits dieselben Squad-Modelle.
- **Steuerflächen**: Die Klappe bekommt im Flug eine eigene Collider-Box, die mitdreht. Collider-Boxen des Decals, die über Scharnier und festen Teil zugleich reichen, bleiben in Neutralstellung stehen (nur fürs Anklicken und Bodenkontakt relevant).
- **Ein Decal belegt sein Teil** samt daran klebender Steuerflächen, Lufteinlässe und eingefahrenem Fahrwerk, keine Nachbarteile. Auf einem Cockpit mit Cockpit-Form nur das Cockpit selbst.
- **Skinned oder animierte Parent-Meshes werden nicht unterstützt** (nur beim Fahrwerk werden Skinned Meshes in eingefahrener Pose gelesen). Der Mod liest die statischen Meshes (`MeshFilter`) der Teile. Nicht lesbare Meshes werden übersprungen (Log-Warnung `is not readable, skipped`).
- Die Kacheln sind flach und starr. Auf stark gekrümmten Flächen sind sie einzeln zugeschnitten, aber nicht gebogen, und es bleiben vereinzelt Lücken, wo der Rumpf zu stark knickt.
- Teile mit mehr Kacheln als `maxTiles` (Standard 60000) werden abgeschnitten, mit Warnung im Log.
- Im Flug wird das Netz nur beim Start, bei Änderungen am Schiff (abgetrennte oder verlorene Teile), beim Kachel-Abplatzen oder bei der EVA-Reparatur neu gebaut.
- Der Hitzeschutz ist ein Modell, kein Abbild der Stock-Hitzeschilde. Mit `efficiency` und `unitsPerCubicMeter` lässt sich die Stärke abstimmen.
- **Lizenz.** Veröffentlicht unter der [MIT-Lizenz](LICENSE). Aus Conformal Decals wurde kein Code und kein Shader übernommen; die Kacheln entstehen als eigene Geometrie, ohne eigenen Shader.

## Dateien

Siehe `docs/ARCHITECTURE.md` für den Aufbau, `docs/DESIGN.md` für die Spezifikation und `CHANGELOG.de.md` (bzw. `CHANGELOG.md` auf Englisch) für die Änderungen.

Selbsttest der Mathematik: `dotnet run --project tests/SelfCheck` (endet mit `SelfCheck OK`). Offline-Vorschau: `tools/Preview` (siehe `docs/ARCHITECTURE.md`).
