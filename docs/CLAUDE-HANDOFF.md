# Stand 1.7.1 (21. November 2026)

1.7.1: Maskenrand über `Fill` (überstehende Kacheln weg, Randstücke an der Kontur geschnitten), Kappe nur für kleine Dreiecke, `ShieldedPort` nur mit einem Stapelknoten, Klappen-/Winglet-Haut 150°, lokales Glühen (`TileGlow.LocalWeight`, Fugen über `KSP/Emissive/Diffuse`). Renders `preview/v15/`.


Änderungen gegenüber 1.6.2 im [CHANGELOG](../CHANGELOG.md), Renders in `preview/v14/` (`render.sh`). Kernpunkte im Code: `TileSettings.PatternedHull` ist entfernt, `RunSkin` läuft nur noch für Overlays mit `Skin` (Fahrwerk, Einlässe, Flossen/Klappen, Sockel des abgeschirmten Docking-Ports). `Snap` wickelt beim Quadrat über konvexe Kanten ab, `Claim` lässt Inselfluten nicht mehr zwischen fertige Kacheln. `ShieldedPort` (Modul und Vorschau) behandelt die Schutzklappen wie Lagerraumtüren. Cockpits mit `--bake-cover` neu gebacken (Maskenabdeckung 99,8–99,97 %). Offen im Spiel: Docking-Port-Animation, Glühen, Nähte aus der Nähe.

Der folgende Text beschreibt den Stand 1.6.0.

# Übergabe an Claude · AblatorDecals 1.6.0

Stand 20. November 2026. Das Release liegt unter `GameData/AblatorDecals` und ist in der KSP-Testinstallation `F:\Benutzer\Dokumente\science test` installiert. Alle 22 Paketdateien wurden nach dem Kopieren per SHA-256 verglichen. DLL-Version 1.6.0.0, SHA-256 `9D2C8048F438D562B6E4B3DACD0D6C9C1C06D92729AC7325D7DF6C1BDCC80353`. Die vorherige Installation liegt im temporären Ordner `AblatorDecals-before-1.6.0-a9e6942777b942af92b0edfff758673c`.

## Was beim Abbruch offen war

Der erste vollständige Selbsttest scheiterte auf einer ebenen Fläche bei Hex und 30° Musterwinkel. Der Overlay-Test fand zusätzlich zwei Kacheln im Grundriss des Anbauteils. Die damalige Lückenfüllung war bei jedem Bau aktiv, einschließlich freier Decals mit Ganzkachelrand. Heute aktiviert das Modul sie für Cockpitmasken und Mk2-Rumpftanks. Bei Kachelverlust und konkurrierender Belegung bleibt die gewünschte Aussparung erhalten. Jede Füllfläche prüft Schnitt und Sperrzone, zusätzlich werden ihre Eckpunkte gegen Anbauten geprüft. Der Test am Objektfuß läuft ausdrücklich mit aktiver Füllung.

## Wo der Code sitzt

- `TileBuilder.cs` enthält die normale Kachelung, `Fill`, das Fugenbett der Mk2-Tanks und `RunSkin` für Fahrwerke und Einlässe. Das Fugenbett folgt den Modellflächen und der Umgriffsgrenze. Es zählt nicht doppelt für Masse oder Wärmeaufnahme. Bei begrenzter, abgerollter Fläche übernimmt die Gitterfüllung die Begrenzung.
- `ModuleAblativeDecal.cs` wählt die Geometrie, lädt vorgefertigte Cockpits und baut die erreichten Anbauten dazu. Auf dem Fahrwerk wurde die ausladende Fläche des Landelichts als Kachelträger gelesen. Lichtreflex- und transparente Effektflächen werden vor dem Sichtbarkeitstest ausgesondert. Bereits mit dem eingefahrenen Modell geprüfte Fahrwerksflächen werden im zweiten Bauschritt übernommen. Die Klappen folgen der Decalseite mit höchstens 100° Umgriff. Für Frachtraumtüren gilt der am Decal eingestellte Wert.
- `CockpitCover.cfg` ist die Quelle für Mk1 C, Mk2 B mit 12 cm höherer Seitenlinie und Mk3 B. Alle sechs Dateien in `Covers` sind neu gebacken. Der Selbsttest liest ihre Geometrie und prüft den binär identischen Rückweg.
- `Textures/Textures.cfg` verbindet die vier Kombinationen mit ihrem Emissionsatlas. Quadrat/Schwarz sowie beide Keramikausführungen haben neue Verläufe. Die Keramik verwendet dieselbe diffuse Textur für beide Formen. Die normale Projektion verwendet Atlaszellen, ein eigener Texturmaßstab oder die flächenübergreifende Projektion verwendet bei prozeduralen Netzen die gewählten UVs. Vorgefertigte Cockpits bringen ihre UVs mit. Die Temperaturkurve bleibt bei 550, 800 und 1300 K. Die Wiedereintrittszusammenfassung enthält `peak glow temp` und `peak intensity`.

## Offline-Abnahme

Release-Build von Plugin und Vorschau mit 0 Warnungen und 0 Fehlern. Der vollständige Selbsttest endet mit `SelfCheck OK`. Das [Testprotokoll](../preview/v12/verification/selfcheck.log) enthält die einzelnen Prüfungen. Alle sechs erneut gebackenen Dateien stimmen per SHA-256 mit den ausgelieferten Cockpitnetzen überein.

Die folgenden Zahlen sind Flächenstichproben an den KSP-Modellen. Die Cockpitmessung prüft die sichtbare Fläche mindestens 2 cm innerhalb der Maske. Die Bilder und Konsolenprotokolle liegen je Szene unter `preview/v12/verification`.

| Szene | Ergebnis |
|---|---|
| Mk1, Hex / Quadrat | 99,94 / 99,97 % Maskenabdeckung |
| Mk2, Hex / Quadrat | 99,94 / 99,75 % beim Backen |
| Mk3, Hex / Quadrat | 99,97 / 99,97 % |
| GearSmall am Deltaflügel und am vorgefertigten Mk2-Cockpit | Je 100,0 % der gemessenen Außenhaut |
| SmallGearBay und GearMedium | Je 100,0 % |
| airScoop und zwei IntakeRadialLong | 99,1 % bzw. je 99,8 %. Die Ansaugöffnungen bleiben frei. |
| smallCtrlSrf auf Dynawing, elevon5 im gespeicherten VAB-Flugzeug | Je 0 Kacheln mit deutlich entgegengesetzter Normale (Skalarprodukt kleiner als −0,7) |
| Mk2-Kurztank, 100° Umgriff, beide Muster | 100,00 % der geprüften Seitenwand innerhalb des Umgriffs. Front- und Kantenansicht melden 0 sichtbare Rumpfpixel im belegten Innenbereich. |
| Gespeichertes VAB-Flugzeug, Decal 15, begrenzte Fläche | Hex und Quadrat erreichen in den Decalachsen dieselben Grenzen, u −1,246 bis 1,246 m und v −1,040 bis 0,835 m. Front- und Kantenansicht jeweils 0 sichtbare Rumpfpixel. Alle 16 Decals des Flugzeugs wurden gerendert. |

**Beim nächsten Spieltest** zuerst die Fahrwerksanimation im Editor und im Flug sowie das Glühen beim Wiedereintritt prüfen. Die Softwarevorschau zeigt die eingefahrene Pose und simuliert die Emission. Live-Beleuchtung, Render-Mods und die Bewegung einzelner Fahrwerksknochen sind damit noch offen. An einzelnen geschnittenen Kacheln liegen die Ecken außerhalb der idealen Cockpitkontur, bis 18 mm am Mk1 und 34 mm am Mk2. In der schrägen Nahansicht des einzelnen Mk2-Tanks erkennt der Pixeldetektor noch einzelne Pixel an der Stirnkante. Die Frontal- und Kantenmessung oben bezieht sich auf die ausdrücklich genannten Ansichten.

## So reproduzierst du den Stand

Aus dem Projektordner in PowerShell. Der normale Plugin-Build installiert direkt in die Testinstallation. Für die Offline-Kompilierung gibt man nur den Ordner der Referenz-DLLs frei.

```powershell
$gd = 'F:\Benutzer\Dokumente\science test\GameData'
dotnet build src/AblatorDecals -c Release '-p:KSPDir=C:\nonexistent' '-p:Managed=F:\Benutzer\Dokumente\science test\KSP_x64_Data\Managed'
dotnet run --project tests/SelfCheck -c Release
dotnet build tools/Preview -c Release
$preview = 'tools/Preview/bin/Release/net48/Preview.exe'
foreach ($part in 'Mark1Cockpit','mk2Cockpit_Standard','mk3Cockpit_Shuttle') {
    & $preview --bake-cover $gd $part "$env:TEMP/ablator-v16/$part"
    if ($LASTEXITCODE -ne 0) { throw "Abdeckung oder Bake für $part fehlgeschlagen" }
}
$craft = 'F:\Benutzer\Dokumente\science test\saves\Heatshield test\Ships\VAB\Autogespeichertes Raumfahrzeug.craft'
foreach ($shape in 'Hex','Square') {
    & $preview $craft "$env:TEMP/flight-tank-$shape" "gameData=$gd" whole=1 only=15 "shape=$shape" views=front,edgeR
}
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/make-glow-textures.ps1 -VariantsDir preview/v12/glow
```

Die Ordner `flight-tank-Hex`, `flight-tank-Square` und `last-flight` dokumentieren den tatsächlichen Spielstand. `tank-Hex` und `tank-Square` verwenden einen einzeln erzeugten Mk2-Kurztank. Die Übersichtsbilder stehen in [Geometrie](../preview/v12/verification/overview.png) und [Glühen](../preview/v12/glow/overview.png).
