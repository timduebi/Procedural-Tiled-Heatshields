# AblatorDecals v6 – Bauanleitung (verbindliche Spezifikation)

Stand 2026-11-12. Ziel: finale, geprüfte Version des KSP-Mods "Ablative Decal" (KSP 1.12.5, Unity 2019.4.18f1,
.NET Framework 4.8). Diese Datei ist die Vorgabe für alle Arbeitspakete. Wer davon abweicht, begründet es im
Abschlussbericht. `ARCHITECTURE.md` beschreibt den alten Stand (v4/v5) und dient nur als Hintergrund.

## 0. Projekt, Pfade, Regeln

- Projekt: `F:\Benutzer\Dokumente\Procedura heatshield`
  - `src/AblatorDecals/` Plugin (net48, referenziert KSP-DLLs), `tests/SelfCheck/` Konsolen-Selbsttest (reine Mathematik),
    `tools/Preview/` Offline-Vorschau (lädt `.craft` + echte `.mu`-Modelle, rendert PNG), `GameData/AblatorDecals/` Mod-Ordner.
- KSP-Testinstallation: `F:\Benutzer\Dokumente\science test` (mit ReStock, Scatterer, TextureReplacer, TexturesUnlimited, KSPCF …).
  Der Plugin-Build (`dotnet build src/AblatorDecals -c Release`) kopiert `GameData/AblatorDecals` automatisch dorthin.
- Dekompilierte KSP-Quellen (zum Nachschlagen von APIs, **nie raten**):
  `C:\Users\TIMDBE~1\AppData\Local\Temp\claude\C--Users-Tim-D-bendorfer--claude-spares-spare-wC3T50\cb26ef41-7926-4506-a79d-77fe5e404a3a\scratchpad\acs\` (eine `.cs` pro Klasse, z. B. `Part.cs`, `FlightIntegrator.cs`).
- Arbeitsdateien und Vorschau-Bilder: `C:\Users\TIMDBE~1\AppData\Local\Temp\claude\3e73efbe35821636\cb26ef41-7926-4506-a79d-77fe5e404a3a\scratchpad\`
  (Unterordner `preview-v6\` für Renderings). Sicherung des v5-Stands liegt dort unter `backup-v5\`.
- Test-Craft mit 4 Decals: `F:\Benutzer\Dokumente\science test\saves\Heatshield test\Ships\VAB\Dynawing.craft`.
- Regeln:
  - KSP **nie** starten (Entscheidung des Nutzers, er testet selbst im Spiel). Keine Spielstände des Nutzers verändern.
  - Code-Kommentare englisch, knapp, im Stil des Bestands. Keine unnötigen Abstraktionen. Kein Code aus Conformal Decals übernehmen.
  - `SurfaceIndex`, `TilePattern`, `TileBuilder`, `ShieldMath`, `TileGlow` sind **reine Mathematik**: nur verwaltete
    Unity-Typen (`Vector2/3`, `Mathf`, `Quaternion * Vector3`). Verboten dort, weil nativ und außerhalb von Unity nicht lauffähig:
    `Matrix4x4.inverse`, `Quaternion.Inverse/Euler/LookRotation/AngleAxis`, `Mesh`, `Debug.Log`, `Time` usw.
  - Jede nichttriviale Logik bekommt einen Check in `tests/SelfCheck` (`dotnet run --project tests/SelfCheck`, endet mit `SelfCheck OK`).

## 1. Was der Mod ist

Ein Part "Ablative Decal" wird auf die Oberfläche eines Parts geklebt. Es legt echte 3D-Hitzekacheln auf den Rumpf darunter
(und auf per Stack-Node verbundene Rumpf-Parts), ist thermisch ein Ablator-Hitzeschild und bleibt ein einziges Part.

Nutzer-Feedback zu v1–v5, das v6 erfüllen muss:
1. Keine verzogenen oder gewölbten Kacheln, keine hellen Ritzen, kein Stapeln von Kacheln.
2. Kacheln müssen Flügelkanten/-spitzen und Rumpfecken erreichen; an Part-Kanten sauber enden.
3. Saubere Ränder; am Decal-Rand wahlweise ganze Kacheln oder gerader Schnitt.
4. Schöne, abwechslungsreiche Kacheln: Starship-Sechsecke **und eckige Space-Shuttle-Kacheln**.
5. Kein Ruckeln im Editor. Part-Icon vorhanden. Kacheln von hinten weiß.
6. Thermik wie bisher (funktioniert laut Nutzer): Ablator wird verbraucht, schützt nur, wo Kacheln im Fahrtwind liegen.

Bekannter v5-Fehler (Regressionstest Pflicht): Die Flutung hat keine Positions-Deduplizierung. Mit `maxWrap = 180` läuft sie
endlos um geschlossene Rümpfe (40 000 Kacheln, 1361 m² auf einem 30-m²-Part, 2 s Bauzeit).

## 2. Geometrie-Kern (Paket A)

### 2.1 Dateien
- `src/AblatorDecals/SurfaceIndex.cs` (bleibt, darf optimiert werden; Vertrag unten).
- `src/AblatorDecals/TilePattern.cs` (neu), `src/AblatorDecals/TileBuilder.cs` (neu geschrieben).
- `tests/SelfCheck/*`, `tools/Preview/*` an die neue API anpassen und erweitern.
- `ModuleAblativeDecal.cs` wird in Paket C neu geschrieben. Paket A baut **nur** `tests/SelfCheck` und `tools/Preview`
  (das Plugin-Projekt kompiliert zwischenzeitlich nicht, das ist erwartet).

### 2.2 Öffentliche API (Vertrag für Paket C, nicht ändern ohne Rückmeldung)

```csharp
public enum TileShape { Hex, Square, Brick }
public enum UvMode { Atlas, Window, Across }

public class TileSettings            // plain data, safe to copy to a worker thread
{
    public TileShape Shape = TileShape.Hex;
    public float TileSize = 0.2f;     // lattice pitch incl. joint: hex flat-to-flat, square/brick edge length
    public float AngleDeg;            // pattern rotation about the decal normal
    public float Gap = 0.004f;        // joint width
    public float Thickness = 0.04f;
    public float Width = 2, Height = 2;   // footprint seen along the decal normal
    public float MaxWrapDeg = 120;
    public bool StraightBorder;       // false: whole tiles at the footprint border
    public float TileLoss;            // 0..0.2, fraction of tiles left out (cosmetic)
    public float SurfaceOffset = 0.003f;
    public float BevelWidth = 0.006f, BevelDepth = 0.005f;
    public float CornerBendDeg = 35, FaceBendDeg = 60, CurvatureFactor = 1;
    public UvMode UvMode = UvMode.Window;
    public int AtlasCols = 4, AtlasRows = 4;
    public bool AtlasRandomRotation;
    public float TextureScale = 2;    // m per repeat (Window/Across; 0 with Across = stretch over footprint)
    public float TextureAspect = 1;   // texture height / width
    public int MaxTiles = 40000, MaxBins = 16;
    public float BinAngleDeg = 20;
    public float ColliderPatchSize = 1; public int MaxColliders = 256;
    public int Seed;                  // per-decal salt for all random choices
}

public class PartTiles               // tiles lying on one owner (SurfaceHit.Owner)
{
    public int Owner;
    public Vector3[] Vertices, Normals; public Vector2[] Uvs;   // decal part local space
    public int[][] BinTriangles;      // one submesh per glow bin (tile tops, bevels, walls)
    public Vector3[] BinNormals;      // unit, area-weighted mean normal per bin
    public float[] BinAreas;
    public int[] JointTriangles, BackTriangles;
    public Vector3[] TileAreaVectors; // tile normal * covered area, one per tile (thermal)
    public float CoveredArea;
}

public class TileResult
{
    public bool OnSurface;            // false: nothing found near the origin
    public int TileCount, PartialCount; public bool HitTileLimit;
    public float CoveredArea;
    public PartTiles[] Parts;         // only owners that got tiles
    public TileBuilder.Box[] Colliders;   // thin boxes over tile patches, decal space
}

public class TileBuilder
{
    // origin: point at/near the surface (footprint centre); n: outward decal normal; u, v: footprint axes.
    // Seen from outside, u points right and v up (v = u x n), so atlas images are not mirrored.
    public static TileResult Build(SurfaceIndex surface, TileSettings s, Vector3 origin, Vector3 n, Vector3 u, Vector3 v);
}
```
`SurfaceIndex`-Konstruktor und `Closest`/`Raycast`/`SurfaceHit` bleiben wie im Bestand. Ein Index pro Build (interner
Zustand ist nicht threadsicher), `Build` darf auf einem Worker-Thread laufen.

### 2.3 Muster (`TilePattern`)
Gitterkoordinaten `(a, b) ∈ ℤ²`, Position `a·e1 + b·e2` (2D, Decal-Ebene), alles um `AngleDeg` gedreht. `s = TileSize`.

| Shape | e1 | e2 | Nachbarschritte (a,b) | Kanten (Normale, Besitzer, Abstand h) |
|---|---|---|---|---|
| Hex (Spitze oben bei 0°) | (s, 0) | (s/2, s·√3/2) | (1,0) (0,1) (−1,1) (−1,0) (0,−1) (1,−1) | je Schritt: Normale = Schrittrichtung, h = s/2 |
| Square | (s, 0) | (0, s) | (1,0) (0,1) (−1,0) (0,−1) | je Schritt, h = s/2 |
| Brick | (s, 0) | (s/2, s) | wie Hex | rechts (1,0); links (−1,0); oben n=(0,1) Besitzer {(0,1), (−1,1)}; unten n=(0,−1) Besitzer {(0,−1), (1,−1)}; h = s/2 |

Die nominale Zelle ist der Schnitt der Kanten-Halbebenen `p·n ≤ h` (Sechseck bzw. Quadrat), Zellfläche = |e1 × e2|.

### 2.4 Belegung (Flutung mit Positions-Deduplizierung)
1. Start: `surface.Closest(origin, …)`; Walker `{P, N, U}` mit `U` = Projektion von `u` in die Tangentialebene.
2. BFS über das Gitter. Von jeder Kachel zu jedem Nachbarschritt über die Oberfläche **laufen** (wie v5 `Walk`:
   Schritte ≤ s/4, Schritt per Strahl auf die Fläche setzen, `U` parallel mitführen; Abbruch bei: kein Treffer, zu wenig
   Fortschritt, seitlichem Wegrutschen, `MaxWrap`, Biegeradius < `max(CurvatureFactor·s, 2.5·SmoothRadius)` über das
   gleitende Fenster der letzten 4 Schritte).
3. Lauf erfolgreich → Stelle als besucht markieren. **Kandidat verwerfen**, wenn schon eine Kachel mit Mittelpunkt näher als
   `0.6·s` existiert und deren Normale mit der neuen einen Winkel < 90° bildet (räumlicher Hash über Mittelpunkte).
   Das stoppt das Umlaufen geschlossener Rümpfe und verhindert Stapel. Sonst Kachel anlegen und weiterfluten, sofern der
   Mittelpunkt im Fußabdruck liegt (`StraightBorder`: Fußabdruck um `s` erweitert anlegen, aber nur von Kacheln im
   eigentlichen Fußabdruck weiterfluten).
4. Lauf scheitert (Part-Kante) → "virtuelle" Kachel: Mittelpunkt = letzter guter Punkt + Rest des Schritts in der
   Tangentialebene, Rahmen vom letzten guten Punkt. Gleiche Abstands- und Fußabdruck-Regeln. Nicht weiterfluten.
5. Abbruch bei `MaxTiles` (`HitTileLimit`).

### 2.5 Adaptive Zellen (ersetzt starre Sechsecke; löst Ritzen, Überlappung, Verzug)
Jede Kachel bleibt **eben und starr**, ihr Umriss wird aber aus den tatsächlichen Nachbarpositionen geschnitten.
Auf Ebene/Zylinder ergibt das exakt regelmäßige Kacheln, auf doppelt gekrümmten Flächen (Nase, Kuppel) leicht
individuell zugeschnittene Kacheln mit konstanter Fugenbreite, wie bei echten Hitzeschilden.

Für Kachel mit Mittelpunkt `C` und Rahmen `(U, V, N)` (2D-Koordinaten relativ zu `C`):
- Für jede Musterkante `e` (Normale `n_e`, nominal `h`): Besitzer-Nachbarn, die existieren und plausibel liegen
  (Abstand zu `C` < 1.6·|Schritt|). Gibt es welche: `h_e = min_j ((C_j − C)·n3_e) / 2` mit `n3_e = U·n_e.x + V·n_e.y`,
  begrenzt auf `[0.35·h, 1.5·h]`. Kachelkante bei `h_e − Gap/2`, Fugenblatt-Kante bei der **unbegrenzten** Mittellinie + ε.
  Gibt es keine: Randkante bei `h − Gap/2` (Fugenblatt 0.5 mm innerhalb).
- "Fremde" Kacheln (Mittelpunkt näher als `1.25·s`, kein Gitternachbar, Normalen-Skalarprodukt > 0.3): echte
  Mittelsenkrechte, Kachelkante bei `|β|/2 − Gap/2`, Fugenblatt bei `|β|/2 + ε`.
- `StraightBorder`: vier weitere Halbebenen aus dem Fußabdruck-Rechteck, in die Kachelebene projiziert (Randkanten).
- Umriss = konvexes Clipping eines großen Startquadrats an allen Halbebenen; jede Ergebniskante merkt sich ihre Herkunft
  (Nachbar k / fremd / Rand / Fußabdruck). Fläche < 2 % der Zellfläche → Kachel entfällt.
- ε = `Gap/4`, mindestens 0.5 mm.

### 2.6 Aufsetzen, Teilkacheln
- Umrisspunkte (Ecken + 2 Zwischenpunkte je Kante) per Strahl entlang `−N` auf die Fläche setzen (`Drop` wie v5:
  gültig, wenn Treffer, innerhalb `MaxWrap`, geglättete Neigung ≤ `CornerBendDeg` **oder** echte Dreiecksneigung ≤ `FaceBendDeg`).
- Alle gültig → volle Kachel. Sonst Teilkachel: gültige Punkte behalten, Übergänge per Binärsuche entlang der Kante
  (10 Schritte). Weniger als 3 Punkte oder Fläche < 2 % → entfällt.
- Kachelnormale = Ebenen-Fit (Newell) durch die aufgesetzten Punkte; Kachelebene liegt `SurfaceOffset + Thickness`
  über dem **höchsten** aufgesetzten Punkt (Rumpf-Facetten dürfen nie durchstoßen).
- Besitzer-Part = `SurfaceHit.Owner` unter dem Kachelmittelpunkt.

### 2.7 Netz je Kachel (Reihenfolge der Submeshes: Bins…, Joint, Back)
- **Oberseite**: Umriss um `BevelWidth` eingerückt (Gehrung, begrenzt), Fächer vom Schwerpunkt, Normale `N`.
- **Fase**: Ring zwischen eingerückter Oberkante und Außenkante auf Höhe `Oberseite − BevelDepth`; Außenring-Normale
  `normalize(N + auswärts)`, Vertices der Oberkante mitbenutzen (weich schattiert). `BevelWidth ≤ 0` → keine Fase.
- **Seitenwände** nur an Kanten ohne vollen, gezeichneten Nachbarn (Rand, Fußabdruck, Teilkachel-Schnitt, Nachbar ist
  Teilkachel oder ausgelassen): eigene Vertices, Normale auswärts, vom Außenring bis zum Rumpf (+`SurfaceOffset`).
- **Fugenblatt** (Submesh Joint, dunkel): ebenes Polygon aus den Fugenblatt-Halbebenen in der Kachelebene, 1 mm unter der
  Außenkante (also knapp unter der Fase). Es schließt die Fugen lückenlos und liegt über dem Rumpf.
  Für Teilkacheln kein Fugenblatt.
- **Rückseite** (Submesh Back, weiß): Fächer auf Rumpfhöhe, Normale `−N`.
- `TileLoss`: Kachel wird per Hash(a, b, Seed) ausgelassen, bleibt aber Nachbar für die Zellberechnung; Nachbarn bekommen
  dort Wände. Ausgelassene Kacheln zählen nicht zu Fläche und Thermik.
- Dreiecke immer so orientieren, dass die Unity-Vorderseite `Cross(b−a, c−a)` entlang der gewünschten Normale zeigt.

### 2.8 UVs
Kachel-lokale Koordinaten `q` = Punkt relativ zum Gitterzentrum, um `−AngleDeg` zurückgedreht (Musterrahmen).
- `Atlas`: Zelle = Hash(a, b, Seed) mod (Cols·Rows). Zellkante: Hex `2s/√3`, Square/Brick `s`. `uvZelle = q/Kante + 0.5`,
  auf `[0.004, 0.996]` begrenzt; optional (`AtlasRandomRotation`) Drehung um Vielfache von 60° (Hex) bzw. 90° (Square) bzw.
  180° (Brick). `uv = ((cx + uvZelle.x)/Cols, (cy + uvZelle.y)/Rows)`. Wände/Fase nehmen die UV ihres Außenpunkts.
- `Window`: wie v5 "Per tile" (zufälliger Ausschnitt je Kachel, Maßstab `TextureScale`).
- `Across`: ein Bild über alle Kacheln (Gitterposition + q), `TextureScale = 0` streckt über den Fußabdruck.
- Bild darf von außen betrachtet nicht gespiegelt sein (Text im Atlas muss lesbar erscheinen) → in der Vorschau prüfen.

### 2.9 Bins, Collider
- Bins je Besitzer: gierig nach Kachelnormale clustern (neuer Bin, wenn Winkel zu allen Bins > `BinAngleDeg`; ab `MaxBins`
  nächstgelegenen nehmen). `BinNormals` flächengewichtet.
- Collider wie v5 (`FitBox` über Kachel-Patches, `MaxColliders`), Decal-Raum, über alle Besitzer.

### 2.10 Abnahme Paket A
SelfCheck (bestehende Invarianten anpassen, plus):
1. Ebene, je Shape, Winkel 0° und 30°/45°: alle Kacheln regulär (Fläche = Zellfläche ± 1 %), Fugenbreite = Gap ± 0.5 mm,
   Abdeckung im Fußabdruck-Inneren 100 % durch Kachel oder Fugenblatt (kein Rumpf sichtbar), keine Überlappung.
2. **Regression Umlauf**: geschlossener Zylinder (r = 1.25 m, h = 4 m), `MaxWrapDeg = 180`, Fußabdruck größer als der
   Zylinder: Flutung endet, Kachelzahl = Mantelfläche / Zellfläche ± 8 %, keine zwei Mittelpunkte näher als 0.55·s.
3. Kugelkalotte/Ogive (doppelt gekrümmt): keine überlappenden Kacheln (paarweise Prüfung der Umrisse in der Kachelebene
   benachbarter Kacheln), kleinste Fuge ≥ 0.4·Gap, ≥ 97 % der erreichbaren Fläche durch Kachel oder Fugenblatt bedeckt.
4. Kante 90°, Flügel mit runder Nase, Flügelspitze (schräg zulaufend), Tankrand, Rippen, Naht zweier Parts: wie v5-Tests.
5. `StraightBorder`: alle Oberseiten-Vertices im Fußabdruck (± 1 mm). `TileLoss = 0.1`: 10 % ± 3 % weniger Fläche.
6. Atlas-UVs liegen in ihrer Zelle; Bins ≤ MaxBins; Summe der Bin-Flächen = CoveredArea; je Besitzer konsistent.
7. Rumpf-Durchstoß: kein Vertex der Rumpf-Testnetze liegt über einer Kacheloberseite oder einem Fugenblatt (Stichprobe).
8. Leistung: 9 × 30 m auf r = 4.5 m, s = 0.2: < 1.5 s (.NET), Vertices < 450 000.

Vorschau (`tools/Preview`): an die API anpassen; Software-Renderer um Textur-Sampling (PNG per `System.Drawing`),
Fugen-/Rückseiten-Farbe und die Optionen `shape=`, `angle=`, `straightBorder=`, `tileLoss=`, `texture=<png>`,
`cells=4x4` erweitern. Rendern und **ansehen** (PNG lesen) nach `…\scratchpad\preview-v6\`:
- `Dynawing.craft` mit Standardwerten und mit `maxWrap=180` (alle 4 Decals: Flügel, Mk3-Cockpit, Adapter).
- `--stack` ReStock-Nase 3.75 m + Tank: Hex, Square 0.15 @45°, Brick; je `front`, `left`, `close`.
- Ein Tank mit `maxWrap=180` (voller Umgriff) und einer mit `straightBorder=true`.
Im Bericht je Szenario: Kachelzahl, Bauzeit, was auf dem Bild zu sehen ist, verbleibende Auffälligkeiten.

## 3. Texturen und Lokalisierung (Paket B)

### 3.1 Atlanten
Generator `tools/make-tile-atlases.ps1` (PowerShell 5.1 mit eingebettetem C#, `System.Drawing`; Muster:
`tools/make-ceramic-texture.ps1`). Ausgabe nach `GameData/AblatorDecals/Textures/`, je 1024 × 1024 px, 4 × 4 Zellen à 256 px,
32-bit PNG. **Alpha = Glanz** (KSP/Specular nutzt den Alpha-Kanal als Gloss), also nie transparent gedacht.

| Datei | Form | Aussehen |
|---|---|---|
| `HexBlack.png` | Sechseck, Spitze oben, Umkreisradius = halbe Zellkante (Hex-Breite also 86.6 % der Zelle) | Starship-Kachel: mattschwarze Keramik, fleckiges weiches Rauschen (wie die bestehende `Ceramic.png`, die dem Nutzer gefiel). Je Zelle eigener Grundton (Helligkeit 65–100 % eines Basisgraus von ca. 0.16, leichte Warm-/Kalt-Tönung ±5 %). Entlang des Sechseck-Rands ein 3–5 px breiter, leicht hellerer, abgenutzter Saum; vereinzelt kleine Abplatzer/Kratzer. Außerhalb des Sechsecks den Saumton fortsetzen. Alpha 0.25–0.45 mit Rauschen. |
| `SquareBlack.png` | Quadrat, füllt die Zelle | Shuttle-HRSI: tiefschwarz (0.05–0.11), glasig (Alpha 0.5–0.8), feiner hellerer Kantensaum, in etwa der Hälfte der Zellen eine kleine gelblich-weiße Teilenummer (z. B. `V070-191026-012`, 2 Zeilen, ca. 9 px hoch, 60 % Deckkraft), leicht unterschiedliche Position. Einzelne Kacheln etwas grauer/verwittert. |
| `SquareWhite.png` | Quadrat | Shuttle-LRSI: weiß bis hellgrau (0.80–0.92), matte Keramik (Alpha 0.15–0.3), grauer Kantensaum, dunkle Teilenummer wie oben, einzelne leicht verschmutzte Kacheln. |

`Ceramic.png` bleibt (einfache Textur ohne Zellen). Das Skript muss deterministisch sein (fester Seed) und die Bilder
nach dem Erzeugen ansehen (PNG lesen) und beurteilen.

### 3.2 `GameData/AblatorDecals/Textures/Textures.cfg`
```
ABLATORDECAL_TEXTURE
{
	texture = AblatorDecals/Textures/HexBlack
	title = #LOC_AblatorDecals_tex_hexblack
	shape = Hex            // Hex | Square (gilt auch für Brick) | Any
	cells = 4, 4           // fehlt → einfache Textur (Window/Across)
	randomRotation = True
}
```
Einträge: `HexBlack` (Hex, 4×4, Rotation an), `SquareBlack` (Square, 4×4, aus), `SquareWhite` (Square, 4×4, aus),
`Ceramic` (shape Any, keine cells). PNGs ohne Eintrag gelten als einfache Textur mit Dateiname als Titel.

### 3.3 Lokalisierung
`GameData/AblatorDecals/Localization/en-us.cfg` und `de-de.cfg`, Format:
```
Localization
{
	en-us
	{
		#LOC_AblatorDecals_part_title = Ablative Heat Tiles
	}
}
```
Tags (Präfix `#LOC_AblatorDecals_`), englisch / deutsch:

| Tag | en-us | de-de |
|---|---|---|
| part_title | Ablative Heat Tiles | Ablative Hitzekacheln |
| part_manufacturer | AblatorDecals | AblatorDecals |
| part_description | Heat shield tiles that are applied like a decal. Width and height set the area seen from the front; the tiles follow the hull, run across stacked hull parts and only protect where they face the airflow. Thickness sets the ablator amount. | Hitzeschild-Kacheln, die wie ein Decal aufgeklebt werden. Breite und Höhe bestimmen die Fläche von vorne gesehen; die Kacheln folgen dem Rumpf, laufen über gestapelte Rumpfteile und schützen nur dort, wo sie im Fahrtwind liegen. Die Dicke bestimmt die Ablator-Menge. |
| part_tags | ablative ablator decal heat shield tile tiles hex shuttle starship reentry conformal | ablativ ablator decal hitze schild hitzeschild kachel kacheln hex shuttle starship wiedereintritt |
| group | Heat tiles | Hitzekacheln |
| pattern | Pattern | Muster |
| pattern_hex | Hexagons | Sechsecke |
| pattern_square | Squares | Quadrate |
| pattern_brick | Staggered squares | Versetzte Quadrate |
| texture | Texture | Textur |
| tex_hexblack | Starship (black hex) | Starship (schwarz, Sechseck) |
| tex_squareblack | Shuttle HRSI (black) | Shuttle HRSI (schwarz) |
| tex_squarewhite | Shuttle LRSI (white) | Shuttle LRSI (weiß) |
| tex_ceramic | Plain ceramic | Schlichte Keramik |
| tileSize | Tile size | Kachelgröße |
| patternAngle | Pattern angle | Musterwinkel |
| tileGap | Joint width | Fugenbreite |
| width | Width | Breite |
| height | Height | Höhe |
| thickness | Thickness | Dicke |
| maxWrap | Max wrap angle | Max. Umgriff |
| spanParts | Covers | Abdeckung |
| span_on | Stacked parts too | Auch gestapelte Teile |
| span_off | This part only | Nur dieses Teil |
| border | Border | Rand |
| border_straight | Straight cut | Gerader Schnitt |
| border_whole | Whole tiles | Ganze Kacheln |
| tileLoss | Missing tiles | Fehlende Kacheln |
| mapping | Texture mapping | Textur-Abbildung |
| mapping_across | Across decal | Über das ganze Decal |
| mapping_tile | Per tile | Pro Kachel |
| textureScale | Texture scale | Texturgröße |
| coveredArea | Covered area | Abgedeckte Fläche |
| tileCount | Tiles | Kacheln |
| partsCovered | Parts covered | Abgedeckte Teile |
| ablatorUnits | Ablator | Ablator |
| tileMass | Tile mass | Kachelmasse |
| facing | Facing airflow | Im Fahrtwind |
| heatBlocked | Heat blocked | Hitze abgefangen |
| absorbing | Absorbing | Aufnahme |
| energyAbsorbed | Energy absorbed | Aufgenommene Energie |
| hottestSkin | Hottest covered skin | Heißeste Hülle |
| tileTemp | Tile temperature | Kacheltemperatur |
| parts_fmt | <<1>> (<<2>> parts) | <<1>> (<<2>> Teile) |

## 4. Modul `ModuleAblativeDecal` (Paket C)

Neu schreiben auf Basis des Bestands (`src/AblatorDecals/ModuleAblativeDecal.cs`, Thermik-Teil übernehmen).
Alle KSP-APIs gegen die dekompilierten Quellen prüfen. Bereits verifiziert:
`Part.AddExposedThermalFlux`, `Part.thermalConvectionFlux`, `Part.exposedArea`, `Part.skinTemperature/skinMaxTemp`,
`Part.ptd` (`PartThermalData.postShockExtTemp`, `.finalCoeff`, `.convectionArea`), `Vessel.srf_velocity`, `Part.attachMode`
(`AttachModes.STACK`), `GameEvents.onEditorPartEvent/onVariantApplied/onEditorUndo/onEditorRedo/onEditorShipModified/`
`onPartWillDie/onVesselWasModified/OnCollisionIgnoreUpdate`, `GameDatabase.Instance.GetConfigNodes(string)`,
`GetAllTexturesInFolder`, `GetTexture(url, false)`, `PhysicsGlobals.GetBlackBodyRadiation(float, Part)`,
`PhysicsGlobals.TemperaturePropertyID` (`_TemperatureColor`), `IPartMassModifier`, `IPartCostModifier`
(`ModifierChangeWhen.FIXED`), `KSPField.groupName/groupDisplayName`, `PartModule.OnWasCopied(PartModule, bool)`,
`Part.symMethod` (`SymmetryMethod.Mirror`), `KSP.Localization.Localizer.Format`.

### 4.1 Gelernte Fallen (alle beachten)
1. `MaterialColorUpdater` (Part-Glühen) ruft `renderer.materials` für alle Renderer unter `model` auf und **klont** damit
   deren Materialien. Texturen/Farben immer auf den eigenen Materialinstanzen setzen und per Wächter (1×/s, Editor und
   Flug) prüfen, dass `renderer.sharedMaterials` noch die eigenen sind.
2. `UI_FloatEdit` ignoriert `guiFormat/guiUnits`; `sigFigs` und `unit` am Attribut setzen.
3. `KSPField` kann kein `Color` parsen → `Vector3`.
4. `node_attach`-Richtung zeigt in den Parent hinein; `shieldNormal = 0,-1,0` in der Part-Config.
5. `onEditorShipModified` feuert bei jedem Klick → nur neu bauen, wenn sich die Ziel-Parts geändert haben.
6. PAW nie während eines Slider-Zugs neu aufbauen (`MonoUtilities.RefreshPartContextWindow` nur nach Chooser-Wechsel).
7. Netze > 65 000 Vertices brauchen `IndexFormat.UInt32`.
8. ReStock entfernt Stock-Modelle → eigenes `Assets/placeholder.mu` (bleibt Icon-Modell, wird im Spiel versteckt).

### 4.2 Aufbau zur Laufzeit
- **Ziel-Parts**: Parent plus (wenn `spanParts`) per Stack-Node verbundene Rumpf-Parts (BFS, keine Triebwerke, Decoupler,
  Docking-Ports, Fairings, andere Decals; innerhalb der Reichweite; max. `maxCoveredParts`). Bestand `TargetParts/IsHull`.
- **Ein Netz je Ziel-Part** (`PartTiles`). GameObject mit `MeshFilter`/`MeshRenderer`:
  - Editor: unter dem `model`-Transform des Decal-Parts (Hover-Highlight funktioniert).
  - Flug: als Kind des **Transforms des Ziel-Parts** (nicht unter dessen `model`), damit Kacheln beim Verbiegen der
    Rakete am richtigen Part bleiben und KSP die Materialien nicht anfasst. Vertices beim Zuweisen vom Decal-Raum in den
    Host-Raum umrechnen. Marker-Komponente mit **nicht serialisiertem** Besitzer-Feld; `Start()` zerstört das GameObject,
    wenn der Besitzer fehlt (Schutz vor Geister-Kopien beim Klonen von Parts). `OnDestroy` des Moduls räumt alle auf.
- **Materialien**: je Host und Bin eine Instanz (Shader aus Config, Standard `KSP/Specular`, Fallback `KSP/Diffuse`),
  `mainTexture` = gewählte Textur, `_SpecColor`/`_Shininess` falls vorhanden. Fugenmaterial dunkel (0.04, 0.04, 0.045),
  Rückseitenmaterial weiß; beide ohne Glanz (`KSP/Diffuse`). Materialien wiederverwenden bzw. beim Neuaufbau zerstören.
- **Collider**: dünne Boxen aus `TileResult.Colliders` unter dem Decal-Part (Editor und Flug, klickbar für das PAW).
  Im Flug nach jedem (Neu-)Aufbau `GameEvents.OnCollisionIgnoreUpdate.Fire()`, damit KSP die neuen Collider in die
  Same-Vessel-Ignorierliste aufnimmt.
- **Vorschau ohne Parent**: flache Ersatzfläche wie im Bestand (`FlatSurface`).

### 4.3 Bauen ohne Ruckeln
- Editor: Eingaben (Einstellungen kopieren, Rumpf-Dreiecke der Ziel-Parts einsammeln) auf dem Hauptthread, dann
  `SurfaceIndex` + `TileBuilder.Build` auf einem Worker (`ThreadPool.QueueUserWorkItem`), Ergebnis im nächsten `Update`
  übernehmen. Pro Modul höchstens ein laufender Build; kommt währenddessen eine Änderung, danach einmal neu starten.
  Veraltete Ergebnisse verwerfen. Ausnahmen im Worker fangen und auf dem Hauptthread loggen. Eingesammelte Rumpf-Dreiecke
  zwischenspeichern, solange Ziel-Parts und relative Lage unverändert sind (Slider ändern nur die Einstellungen).
- Flug: synchron in `OnStartFinished` (einmal). Kein zweiter Aufbau beim Entpacken. Neu bauen bei
  `onVesselWasModified` (eigenes Vessel) nur, wenn sich die Ziel-Parts geändert haben, und wenn ein Ziel-Part stirbt.
  Im Flug die Ablator-Menge nie neu setzen.

### 4.4 Bedienung (Editor-PAW, Gruppe `#LOC_AblatorDecals_group`)
Persistente Felder (Namen final): `pattern` (Chooser Hex/Square/Brick), `textureName` (Chooser, gefiltert nach Form;
Titel aus `Textures.cfg`), `tileSize` (0.05–1 m, Standard 0.2), `patternAngle` (0–90°, Schritt 5), `tileGap`
(0–0.02 m, Standard 0.004), `width`, `height` (0.2–60 m, Standard 2), `thickness` (0.005–0.3 m, Standard 0.04),
`maxWrap` (10–180°, Standard 120), `spanParts` (Standard an), `straightBorder` (Standard aus), `tileLoss` (0–20 %,
Standard 0), `textureAcross`, `textureScale` (nur bei einfachen Texturen sichtbar), `coveredArea`.
- Musterwechsel: passt die Textur nicht zur Form, die erste passende wählen; beim Wechsel auf Square/Brick `tileSize`
  auf 0.15 setzen, wenn es noch auf dem Hex-Standard 0.2 steht (und umgekehrt).
- Anzeigen Editor: Covered area, Tiles, Parts covered, Ablator, Tile mass. Flug: Facing airflow, Heat blocked,
  Absorbing, Energy absorbed, Hottest covered skin, Tile temperature, Covered area, Parts covered.
- Symmetrie: alle Felder an `symmetryCounterparts` weitergeben. Bei `SymmetryMethod.Mirror` bekommt das Gegenstück
  `−patternAngle` (auch in `OnWasCopied(…, asSymCounterpart: true)`).
- Alle Texte über die Tags aus 3.3.

### 4.5 Thermik (unverändertes Prinzip)
Pro Ziel-Part `p`: `frontal_p = Σ Kachelfläche·max(0, n·v̂)`, `coverage_p = frontal_p / p.exposedArea` (über alle Decals
auf `p` auf 100 % gedeckelt), `Q_p = coverage_p · efficiency · max(0, p.thermalConvectionFlux)`, sobald
`p.skinTemperature > ablationTempThresh`. Gemeinsamer Ablator-Vorrat (`ShieldMath.Absorb`), `p.AddExposedThermalFlux(−Q_p)`.
Ablator-Menge im Editor = `coveredArea · thickness · unitsPerCubicMeter`. `reentry summary`-Logzeile bleibt.
Sekündliche Logzeile nur bei `logInterval > 0` (Standard jetzt **0**).

### 4.6 Glühen (neu, rein optisch)
Reine Klasse `TileGlow` (testbar): Oberflächentemperatur je Bin, linearisiert-implizit integriert:
`C·dT/dt = h·f·(T_ext − T) − ε·σ·(T⁴ − T_skin⁴) − k·(T − T_skin)` mit `f = max(0, n_bin·v̂)`,
`h = ptd.finalCoeff / ptd.convectionArea` (kW/m²K, 0 wenn `ptd` fehlt oder Fläche ≤ 0), `T_ext = ptd.postShockExtTemp`,
`σ = 5.670374e-11 kW/m²K⁴`, Config: `glowEmissivity = 0.85`, `glowHeatCapacity = 6` (kJ/m²K), `glowConduction = 0.02`.
Start bei `T_skin`. Farbe: `PhysicsGlobals.GetBlackBodyRadiation((float)T, part)` als `_TemperatureColor` auf dem
Bin-Material (nur wenn der Shader die Eigenschaft hat und `glowEnabled`), aktualisieren bei |ΔT| > 3 K.
SelfCheck: Gleichgewicht (bei h = 0.05, T_ext = 6000, T_skin = 1500, f = 1 liegt T zwischen 1650 und 1850 K),
Stabilität bei dt = 0.5 s, Abkühlen auf T_skin ohne Anströmung, f = 0 bleibt bei T_skin.

### 4.7 Verkohlung
`_Color` der Kachelmaterialien: weiß → (0.42, 0.37, 0.34) mit `pow(verbrauchter Anteil, 0.8)` (Config `charColor`).

### 4.8 Masse, Kosten, Part-Config
- `IPartMassModifier`: `coveredArea · massPerArea` (Standard 0.0015 t/m²), `IPartCostModifier`: `coveredArea · costPerArea`
  (Standard 20), beide `ModifierChangeWhen.FIXED`.
- `Parts/ablativeDecal.cfg`: lokalisierte Texte, `DRAG_CUBE { none = True }` (Stock-Präzedenz: strutConnector),
  `maximum_drag = 0`, `minimum_drag = 0`, Kategorie Thermal, `TechRequired = start`, Ablator-Resource bleibt.
- `Patches/TestTweaks.cfg` (Mk1-3-Reaktionsräder) gehört nicht in den Mod: nach `GameData/AblatorDecalsTest/` verschieben
  (auch in der Testinstallation).
- `[assembly: KSPAssembly("AblatorDecals", 1, 0)]`, AssemblyVersion 1.0.0.0.

### 4.9 Abnahme Paket C
- Plugin baut mit 0 Warnungen; SelfCheck grün (inkl. `TileGlow`).
- Selbst-Review gegen 4.1–4.8; jede verwendete KSP-API in den dekompilierten Quellen nachgeschlagen (Liste im Bericht).
- Bericht: Dateien, offene Punkte, was nur im Spiel prüfbar ist.

## 5. Prüfung ohne Spielstart (Paket D, nach C)
KSP wird nicht gestartet. Ersatz für den In-Game-Test:
- Unabhängiges Code-Review des Moduls gegen die dekompilierten KSP-Quellen: jede verwendete API mit Signatur, Aufruf-
  Reihenfolge der PartModule-Hooks (`OnStart`, `OnStartFinished`, `OnWasCopied`, `OnDestroy`), Lebensdauer von GameObjects,
  Materialien, Events (jedes `Add` hat ein `Remove`), Thread-Grenzen (keine Unity-API im Worker), Nullfälle (kein Parent,
  kein Vessel, Part stirbt, Szenenwechsel).
- Diagnose im Log für den Test des Nutzers: je (Neu-)Aufbau eine `build`-Zeile (Ziel-Parts, Kacheln, Bins, Bauzeit, Textur,
  Shader, `hasTemperatureColor`), `flight check` 3 s nach Physikstart, `reentry summary`.
- Eine kurze Test-Checkliste für den Nutzer in der README.

## 6. Dokumentation (Paket E, am Schluss)
`README.md` (Installation, Bedienung, eigene Texturen, Config-Referenz), `ARCHITECTURE.md` auf v6, `CHANGELOG.md`.
