Sprache: [English](CHANGELOG.md) | **Deutsch**

# Changelog

## 1.9.1 (2026-11-22)

- **FreeIva-Kompatibilität (`mk3CrewCabin` & `Large_Crewed_Lab`):** Unverbundene Stack-Knoten erzeugen nur noch an echten axialen Stirnseiten Schnittebenen (`|dir.y| > 0.7`, ohne `*airlock*`/`*hatch*` über `TileBuilder.FreeNodeCuts`). Behebt, dass FreeIva-Luken-Knoten (`node_stack_front`/`back`, `node_stack_airlockL`/`R`) beim `mk3CrewCabin` beide Seiten und die Tür abschnitten und das Mobile Labor (`Large_Crewed_Lab`) als flache Vorschau zerstörten. Zusätzlich greift `Reseed()`, falls ein Decal direkt auf einem Fenster- oder Luken-Ausschnitt platziert wird.
- **Achsen-Fixierung für lange 5 m-Tanks (`Size4_Tank_04`):** `SmoothAxisLock` richtet Kacheln auf langen glatten Zylindern exakt an der Tankachse aus und verhindert Winkel-Drift über 15 m Länge (keine schrägen Kachel-Artefakte mehr).
- **PAW bereinigt:** Alle `[UI_*]`-Steuerelemente für `areaLimited`, `width`, `height`, `unrolled` und `straightBorder` vollständig entfernt (Felder bleiben rein persistent für alte Spielstände), und `cockpitShape_off` von „Breite und Höhe“ auf „Ganzes Teil“ geändert.
- **Kapsel- & Cockpit-Anpassungen:** `keepRcs = true` bei der Mk1-3-Kapsel (`mk1-3pod`) entfernt (RCS-Düsen jetzt sauber geschlossen, keine Löcher mehr). Eigenes Cover-Modell für das lineare Mk2-Cockpit (`mk2Cockpit_Inline`) entfernt und dessen Fensterboxen in `WindowKeepOuts.cfg` vertieft, damit alle 4 Scheiben bei normaler Kachelung sauber frei bleiben.

## 1.9.0 (2026-11-22)

- **„Ganze Unterseite bekacheln“ mit einem Klick (`DecalWindow`):** Neue Richtungsauswahl (`Auto`, `-Y`, `+Z`, `-Z`, `+X`, `-X`) und Knopf **Ganze Unterseite bekacheln** im Editor-Fenster. Bringt automatisch Decals an allen noch unbedeckten Rumpf-, Tank-, Bucht-, Cockpit- und Kapselteilen an, übernimmt Muster/Kachelart/Größe und legt einen einzelnen Strg+Z-Rückgängig-Schritt an.
- **Docking-Ports & Hitzeschild-Übergänge (`Patches/SurfaceAttach.cfg`):** Surface-Attach für Decals auf `dockingPortLarge`, `dockingPort1`, `HeatShield0/1/1p5/2/3`, `restock-heatshield-1875-1`, `InflatableHeatShield`, `InflatableAirlock`, `restock-airlock-1` und `docking-nose-0625-1` freigeschaltet. Mindesthöhe für *Glatter Zylinder* auf `0,10 m` gesenkt, damit schmale Docking-Ringe und Hitzeschildränder sauber eingewickelt werden.
- **Fenster-Prüfung & -Schutz für alle Kabinen (`WindowKeepOuts.cfg`):** Alle 70 bemannten Teile aus Stock, Making History, ReStock, ReStockPlus, SSPX und Near Future Spacecraft geprüft und **60 Teile / 313 bereinigte Fensterausschnitte** hinterlegt (Schalter **Fenster frei lassen** unter *Feinheiten*), inkl. Tiefen-Polsterung (`TileBuilder.WindowKeepHalf`), damit auch tief sitzende Fenster in glatten Zylindern (`crewCabin`, `Large_Crewed_Lab`, `sspx-aquaculture-375-1` usw.) sauber frei bleiben.
- **Kapsel- & Lander-Cover schützen nur die Luvseite (`CockpitCover.cfg`):** 26 Cover für Stock, Making History, ReStock, ReStockPlus und Near Future Spacecraft, gestaltet wie die Mk1/Mk2/Mk3-Cockpits statt die ganze Kapsel einzupacken: Fenster, Luken und die obere Leeseite bleiben sauber weiss. Konische Wiedereintrittskapseln (`mk1pod_v2`, `mk1-3pod`, `Mk2Pod`, `restock-mk2-pod`, `command-25-1`, `command-mk3-9`) bekommen 3D-Kacheln auf dem ganzen unteren Rand und dem `+Z`-Bauch, mit einer glatten Linie, die seitlich hinter den Fenstern hochläuft (`REGION`), und glatte Keramikränder entlang der Teilachse (neuer Schlüssel `ceramicAxis = true`). Inline-Cockpits (`Mark2Cockpit`, `mk2Cockpit_Inline`) schützen nur die Bauchhälfte (`z >= 0.05`). Kugeln, Lander und die Cupola bekommen eine dünne glatte Keramikschicht (`ceramicAll = true`) nur auf der unteren Schale (`kv1Pod`–`kv3Pod`, `restock-pod-sphere-1..3`), dem Kragen unter der Glaskuppel (`cupola`) oder der fensterabgewandten Seite (`landerCabinSmall`, `mk2LanderCabin_v2`, `MEMLander`, Near-Future-Lander und Utility-Pod).
- **Schlanke Keramikschicht (`ceramicAll`):** Die Vollkeramik liegt jetzt 5 mm statt 25 mm über dem Rumpf, Handläufe, Leisten und Ecken blähen sich nicht mehr auf. Sie setzt keine Kacheln, flache Stapelböden bleiben frei.
- **Keine bekachelten Stapel- und Docking-Flächen mehr:** Im Modus „Ganzes Teil“ schneidet jeder Stapelknoten, auch ohne angebautes Teil, so bleiben flache Endschotten, Docking-Flächen (`dockingPortLarge`, `dockingPort1/2/3`, `docking-nose-0625-1`) und Hitzeschild-Enden sauber. Vertiefte Taschen in Endflächen (Mk2-Profil) bleiben frei statt gezackt bekachelt. Der glatte Zylinder lässt flache Deckel und Endflansche ganz weg, und Teile mit glattem Zylinder bekommen das dunkle Fugenbett unter den Kacheln, damit nirgends Weiss durchscheint.
- **Glatter Zylinder auch für Stationslabore:** Die automatische Erkennung akzeptiert runde Teile, deren vertiefte Fenster oder erhabene Rahmen nur wenige Sektoren verschieben (`Large_Crewed_Lab`); Mk3-artige flache Bäuche bleiben ausgeschlossen. Fensterrahmen und Handläufe bekommen die Überstandshaut, nur die Scheiben bleiben ausgespart.
- **Engere Fenster- und Lukenaussparungen:** Fenster-Sperrzonen haben 5 mm Rand in der Ebene (vorher 15 mm). `--scan-windows` fasst Scheiben nur noch bis 2 cm Abstand zusammen (vorher 15 cm), `mk1-3pod` hat jetzt seine vier echten Scheiben statt einer 80-cm-Box (`Mk2Pod` und `mk2LanderCabin_v2` ebenfalls erneuert). Luken-Sperrzonen reichen mindestens 14 cm tief (`TileBuilder.HatchKeepHalf`), so werden vertiefte Lukentüren freigeschnitten, nicht nur ihr Rahmen.
- **Wartungsbuchten (`ServiceBay_125_v2`, `ServiceBay_250_v2`, `service-tank-*`):** Innere Scharnierzylinder und Tür-Innenflächen ausgefiltert (`TileBuilder.DoorOuterFace`); alle 4 beweglichen Buchttüren erhalten eine saubere, geschlossene Außenhaut (`DoorSkin`), die sauber mit den Türen öffnet/schließt. Der Buchtrahmen bekommt dieselbe geschlossene Aussenhaut, und die Hautkanten laufen am Fuss leicht aus und schliessen Haarfugen, damit zwischen Türen und Rahmen keine weissen Flecken oder Schlitze mehr sichtbar sind.
- **Reentry-Glühen & Verkohlung (Charring) überarbeitet + Sandbox-Unterstützung:**
  - **Schnelleres Abkühlen & weicherer Beginn:** `glowHeatCapacity` (`30 -> 10`), `glowConduction` (`0,02 -> 0,08`), `glowFadeTime` (`3 -> 1,5 s`), Glühbeginn erst ab `720 K` mit Smoothstep-Farbübergängen (tiefes Kirschrot → Glut-Orange → Gold → Weißgelb) und größerer Richtungsgewichtung (`LocalWeight` `0,15..1,25`), sodass Nasen und Vorderkanten zuerst aufglühen.
  - **Sichtbare Verkohlung auf allen 4 Kachelarten & in Sandbox:** Verstärkte Rußkurve (`Pow(used * 3,0 * dirFactor, 0,65)`) mit Matt-Übergang im `_SpecColor` — aschbraune matte Oxidationsspuren auf **Schwarz** und dunkler Ruß auf **NASA-Weiß**, **Stahl** und **Keramik**. Verkohlung ist nun auch im **Sandbox-Modus** aktiv; **EVA-Reparatur** wird in Sandbox ausgeblendet (nur Wissenschaft & Karriere).
- **UI & Alt-Kopie-Fix:** Breite, Höhe, Abdeckung (`areaLimited`), Abwickeln und Rand im PAW ausgeblendet (alte Spielstände laden weiter). Alt-Kopieren eines Decals auf das `mk3CrewCabin` übernimmt nicht mehr fälschlich `keepHatches = true` vom vorherigen Teil (`initParent` setzt die Standardwerte bei Teilewechsel zurück).

## 1.8.0 (2026-11-22)

- **Neue Kachelarten (`NASA-Weiß` & `Stahl`):** **NASA-Weiß** (Shuttle LRSI, `HexWhite` / `SquareWhite`) und **Stahl** (gebürsteter Edelstahl mit dezenter Hitzetönung und Glanz, `HexSteel` / `SquareSteel`) ergänzen **Schwarz** und **Schlichte Keramik** im PAW, Editor-Fenster, `Textures.cfg` und `B9PartSwitch.cfg`.
- **Automatischer „Glatter Zylinder“ & optisches Rohr-Overlay für runde Teile (ReStock, ReStockPBR, SSPX, NFLV):** Runde Tanks und Stationsmodule mit 3D-Versteifungsringen, Rohren oder Leitern nutzen automatisch eine glatte 64-Segment-Rotationshülle (Schalter **Rumpffläche: Glatter Zylinder / Exaktes Modell** unter *Fläche*; automatisch aus auf flachbodigen/unrunden Rümpfen wie Mk2, Mk3, Flügeln oder Hubs). Horizontale Ringe und Schweißnähte werden glatt überbrückt (keine weißen Streifen mehr), während herausragende Rohre und Leitern rein optisch ein nicht-gekacheltes, zur Kachelart passendes Overlay erhalten (glüht und rußt mit, wie Fahrwerke und Lufteinlässe — ohne eigene Collider, Masse oder Kosten).
- **Kompatibilität für runde Teile & Mods:**
  - **Service- & Frachträume (`service-tank-375` / Near Future Spacecraft u. a.):** Behoben, dass beim Anbringen eines Decals direkt auf einer Tür ein schwebendes flaches Rechteck erschien (`no usable surface`). Automatische Erkennung der geschlossenen Türposition sowie Unterstützung mehrteiliger Falttüren (`DoorA..DoorE`).
  - **TweakScale- & B9PartSwitch-Überwachung:** Erkennt Größenänderungen (`TweakScale`) und B9-Mesh-Variantenwechsel am Trägerteil (NFLV, SSPX, ReStock) im Editor automatisch und baut die Kacheln direkt neu.
  - **5 m- & 7,5 m-Tanks (`ReStock`, `ReStockPBR`, `NFLV`):** Abgewickelte „Ganzes Teil“-Fläche auf $\pi \cdot R_{\text{max}}$ erweitert (kurze breite 5 m-/7,5 m-Tanks wickeln volle 180° sauber ohne Abschneiden), Standard-`maxTiles` von `40.000` auf `60.000` erhöht und Inselflutung (`FloodIslands`) auch im Modus *Exaktes Modell* aktiviert.
- **Mk3-Passagiermodul (`mk3CrewCabin`) & Luken-Schalter:** Neuer Schalter **Luken frei lassen** (`keepHatches`) unter *Feinheiten* — beim `mk3CrewCabin` standardmäßig auf **Überkacheln** (Bodenluke/Türe und Fenster werden sauber zugekachelt), bei allen anderen Teilen auf **Frei lassen**.
- **Editor-Komfort:** Neue **Vorlagen** (*Starship*, *Shuttle*, *Shuttle Weiß*, *Delta Stahl*) und Schalter **Alle Hitzeschilde hervorheben** im Editor-Fenster (`DecalWindow`) sowie **Einstellungen auf alle kopieren** direkt im PAW.
- **Verwitterung, richtungsabhängige Verkohlung, Schildstatus & Warnungen:** Regler **Verwitterung** (0–100 %, direkte Einfärbung ohne Netz-Neubau), richtungsabhängiger Rußverlauf je Glüh-Bin (angeströmte Kacheln verkohlen bis ~2× dunkler), Fluganzeige **Schildstatus** (*Nominal / Grenzwertig / Ablator fast leer! / Aufgebraucht!*) sowie einmalige Warnmeldungen unter 10 % und bei 0 % Ablator.
- **Optionales Kachel-Abplatzen & EVA-Reparatur durch Ingenieure:** Neuer Schalter **Kachel-Abplatzen** (Sandbox standardmäßig *Aus*, Wissenschaft & Karriere *Aktiv*), lässt bei extremer Hitze oder niedrigem Ablator schrittweise bis zu 25 % der Kacheln abplatzen. Ingenieure können im EVA über **Hitzekacheln reparieren (EVA)** Kacheln und Ablator wiederherstellen (Sandbox & Wissenschaft kostenlos; Karriere verbraucht 1 Stock-`evaRepairKit`).
- **Mod-Patches & Release-Paketierung:** Optionale ModuleManager-Patches für **Community Tech Tree** (`heatManagementSystems`), **Deadly Reentry** und **FAR**. MIT-`LICENSE` ergänzt, interne Design-Dokumente nach `docs/` verschoben und `tools/package-release.ps1` für saubere CKAN-/Linux-kompatible Release-ZIPs erstellt.

## 1.7.4 (2026-11-21)

- **Glatte schwarze ungekachelte Keramik an Spitze & Seiten von Mk1 und Mk2 (`ceramicSideDeg`):** Beim Mk2- (`58°`) und Mk1-Cockpit (`50°`) werden Nasenspitze und beide Seiten/Chines als glatte, exakt an der Maske geschnittene schwarze Keramikschale (mit richtungsabhängigen Glüh-Bins und bündigen Stapelkanten) umgesetzt, während Bauch und Oberseite vor den Fenstern echte 3D-Kacheln (Hex/Quadrat) behalten. Mk3 bleibt vollständig mit echten 3D-Kacheln bestückt. Mk1- und Mk2-Cockpithüllen neu gebacken.
- **Glüh-Bug beim Hochpitchen nach dem Reentry behoben:** Konvektive Kühlung in kalter Luft wirkt nun auch im Horizontalflug über die gesamte Grenzschicht (`TileGlow.EffectiveFacing`), die Abkühlung bleibt nicht mehr an der warmen Resthaut hängen, und die richtungsabhängige Glüh-Gewichtung (`TileGlow.BinTemp`) greift nur noch, solange die Außenluft heiß genug zum Aufheizen ist (`> 550 K`). Beim Hochpitchen im langsamen Flug nach dem Reentry glüht das Hitzeschild nicht mehr rot auf.
- **~15× schnellerer Wechsel zwischen Hex und Quadrat auf Mk2-Teilen:** Das alte `Fill()` entfällt auf normalen Mk2-Rumpfteilen (dort schließt das schnelle Fugenbett `AddTankJointBed` bereits in < 2 ms), wodurch `mk2DockingPort` von ~2.100 ms auf ~128 ms und `mk2CrewCabin` von ~1.886 ms auf ~139 ms sinken. Laufende Editor-Jobs werden bei sofortigem Umschalten direkt abgebrochen.

## 1.7.3 (2026-11-21)

- **Cockpits (Mk1 / Mk2 / Mk3) mit sauberen echten 3D-Kacheln:** Die frühere Cockpit-Randfüllung (die gekrümmte Randkacheln entfernte und flache schwarze Flecken sowie Löcher erzeugte) wurde durch exaktes Masken-Clipping direkt beim Setzen der Kacheln und ein zurückgesetztes dunkles Fugenbett (`AddMaskJointBed`) ersetzt. Nasenkappen (`CapShell`/`CurvedShell`) greifen nur noch an echt doppelt gekrümmten Nasenspitzen, sodass 1D-Kanten (Mk2-Chine) und Bauchflächen (Mk3) vollständig aus echten 3D-Kacheln bestehen. Asymmetrischen Ausschnitt am Mk3-Cockpit (`CockpitCover.cfg`) symmetrisch gemacht. Alle sechs vorgefertigten Cockpithüllen (`Hex` und `Square`) neu gebacken (~3× kleinere Dateien, 100 % Maskenabdeckung, 0 überstehende Kacheln).
- **Lücken zwischen bekachelten Teilen behoben:** Nach innen gerichtete Stapelknoten von Fracht- und Serviceräumen (`top2`/`bottom2`, 20–100 mm innerhalb der Außenwand) werden nun an die äußere Stirnwand geschoben statt 20 mm Außenkacheln abzuschneiden (behebt 40-mm-Spalten zwischen gestapelten Cargo Bays). Flache Konen ziehen Schnittebenen nicht mehr hinter die Naht zurück, kleine Randstücke an Stapelnähten bleiben erhalten und Fasen direkt an Stapelnähten entfallen (bündiger Übergang ohne V-Fuge).
- **Performance & Stabilität:** Standardwerte für `glowBinAngle` / `glowMaxBins` von `6°` / `48` auf `18°` / `12` (Overlays auf `24°` / `8` gedeckelt) angepasst → ~4–10× weniger Materialien und Draw-Calls je Decal. GC-Allokationen in `FixedUpdate` (50 Hz) entfernt, Frühausstieg im Vakuum und im gepackten Zustand ergänzt, Frontflächen-Summe auf Bin-Ebene umgestellt. `SurfaceIndex.CurvedShell` per Spatial-Hash von $O(V^2)$ auf $O(V)$ beschleunigt. Hintergrund-Build-Jobs im Editor abbrechbar gemacht, Spiegelungs-Normalen und Material-Leaks in `TileHost` behoben, Labels und Input-Locks in `DecalWindow` gecacht, „Auf alle anwenden“ überschreibt feste `tileSize`/`thickness` vorgefertigter Cockpits nicht mehr, und `AblatorDecals.version` (KSP-AVC) ergänzt.

## 1.7.2 (2026-11-21)

- Cockpit-Feinschliff: über Kanten (Mk2-Chine) läuft ein Gitter weiter (exakt abgewickelt statt zweiter Flut), die Randfüllung reicht unter die geschnittenen Randkacheln (keine hellen Punkte, Streifen ~1 cm), Füllstücke ohne Bild dunkel; alle Cockpits neu gebacken. Schutzhaube des Docking-Ports bekommt die Randfüllung, die Spitze wird geschlossen. Vorschau: Unterseiten dunkel wie im Spiel.

## 1.7.1 (2026-11-21)

Nach dem Spieltest von 1.7.0. Offline geprüft (Build 0/0, SelfCheck grün), Renders in `preview/v15/`.

- **Cockpitmasken glatt** (Mk1/Mk2/Mk3): Kacheln, die mehr als 5 mm aus der Maske ragen, fallen weg; die Randfüllung schliesst genau an der Maskenkontur (dunkel, ohne Zacken), auch über die Mk2-Chine. Mk3: der schwarze Fleck unter dem Cockpit (eine grosse Bauchfläche als „Nasenkappe“ erkannt) ist weg. Alle sechs Cockpits neu gebacken.
- **mk2DockingPort** ist wieder ein normales Rumpfteil (abgeschirmte Docking-Ports nur noch als Kappe mit einem Stapelknoten).
- **Winglets/Klappen**: Haut bis 150° Umgriff (kein heller Rand mehr), und ein Winglet über der Naht zum Nachbarteil wird auf beiden Seiten der Naht bekachelt (Stapelschnitte nur noch für Türen).
- **Docking-Port mit Kappe**: unter der geschlossenen Haube keine Kacheln mehr (Sockel und Klappen sehen sich gegenseitig als Hindernis).
- **Glühen**: weniger Kontrast zwischen den Kacheln (flachere Emissionstexturen), Fugen glühen mit (`KSP/Emissive/Diffuse`), und die Kacheln glühen je nach Anströmung stärker oder schwächer (Nase, Unterseite, Vorderkanten heisser; weicher Smoothstep-Verlauf). → `preview/v15/glow.png`.

## 1.7.0 (2026-11-21)

Nach dem Spieltest von 1.6.2. Offline geprüft (Build 0/0, SelfCheck grün), Renders je Punkt in `preview/v14/`.

- **Echte Kacheln statt projizierter Haut** auf Rümpfen, Mk2-Teilen, Lagerräumen und Cockpits; die geschlossene Haut bleibt nur für Fahrwerk, Einlässe, Winglets und Klappen. Damit sind auch die Abstände an den Teilnähten weg (die Haut wurde an den Teilenden entlang der gerundeten Kante angehoben und wich von der Naht zurück).
- **Quadrat so weit wie Hex** über die Chine von Mk2-Tanks und Mk2/Mk3-Lagerräumen: Über einer scharfen Kante wird die Kachel auf die Fläche dahinter abgewickelt (Hex kam mit seinen schrägen Schritten schon hinüber).
- **Cockpits neu gebacken** (gleiche Masken): Inselfluten legten ihre Kacheln zwischen die der Hauptflut (beim Quadrat das Voronoi-Muster); jetzt halten sie eine Zelle Abstand, die Kacheln sind regelmässig.
- Lückenfüllung nur noch am Rand (höchstens 1,5 Kacheln von einer echten Kachel).
- **Abgeschirmter Docking-Port** (Clamp-O-Tron Shielded) auf einem bekachelten Teil: echte Kacheln auf den Schutzklappen, die mit der Animation mitfahren; die Andockfläche bleibt frei, der Sockel bekommt die Haut.
- VAB Organizer: Unterkategorie Hitzeschilde (`Patches/VABOrganizer.cfg`). Tech-Tree: `supersonicFlight` wie das Mk2-Cockpit, `entryCost = 4000`.
- Vorschau: `acc=1` (Kacheln vorheriger Decals mitzeichnen, für Nähte), `--mudump <.mu>`.

## 1.6.2 (2026-11-20)

Nach dem Spieltest von 1.6.0/1.6.1. Offline geprüft (Build 0/0, SelfCheck grün), Renders je Punkt in `preview/v13/`.

- **Geschlossene Haut auf allen Mk2-Rumpfteilen** (Rumpftanks, Kabine, Dockingport, Adapter; nicht Cockpits, Lagerräume, Lander-Kapsel): Quadrat geht so weit hoch wie Hex, keine Kante an der Chine, keine Fugen an Teilnähten. Innenflächen (Röhre des Dockingports) bleiben frei.
- **Neue Abbildung der Haut** (auch Cockpithüllen, Fahrwerk, Einlässe, Flossen): rundherum nach Bogenlänge statt Winkel (keine seitlich gestreckten oder gestauchten Kacheln, kein Fächer am Ende des Einlasses), flache Objekte (Winglets, Flossen, Klappen) gerade durch ihre dünne Achse projiziert (keine Zickzack-Ränder). Alle sechs Cockpitdateien neu gebacken.
- **Teilnähte bündig**: Kacheln, die an einer Ebene oder Maske geschnitten werden, haben keine Fase mehr; die Oberseiten zweier Teile stoßen ohne V-Fuge aneinander (auch Cockpit an Rumpf).
- **Fahrwerk**: Seitenflächen nicht mehr sägezahnartig weggelassen (Ecke statt Mittelpunkt entscheidet); die Oberseite (über 95° von der Decal-Normalen) bleibt frei; Fahrwerk auf der Gegenseite des Teils gehört nicht zum Decal.
- **Glühen**: Die Stufen in 1.6.0 kamen vom Glühwert je Kachelgruppe (6°-Bins, gierig nach Normale gruppiert) und je Teil (eigene Hauttemperatur); seit 1.6.1 ein gemeinsamer Wert je Fahrzeug für alle Kacheln, Überzüge und Cockpithüllen. Vorschau: `glow=<Emissionstextur> glowK=<K>`.
- Vorschau: Ansichten `edgeB`/`edgeT` (Kachelende an Stapelflächen), Debug `gearWrap`, `gearDebug`.

## 1.6.1 (2026-11-20)

- Mk3-Cockpit überarbeitet: Nasenkappe mit kantigem Kinn, dazu das dunkle Seitenpaneel hinter der Luke mit Stufe; Unterseite bekachelt, Oberseite frei. Alle sechs Cockpit-Dateien neu gebacken (Maskenabdeckung 100 %).
- Mk1/Mk2: Schnittebene an der Stapelfläche leicht nach hinten verschoben (kein Streifen an der Stirnkante).
- Glühen kräftiger bei grosser Hitze: zusätzliche Stufen 1700 K (weiss × 1,65) und 2000 K (weiss × 2,8).

## 1.6.0 (2026-11-20)

- Vorgefertigte Cockpithüllen für Mk1 C, Mk2 B mit 12 cm höherer Linie und Mk3 B. Alle drei unterstützen Hex und Quadrat sowie angebaute Fahrwerke.
- Geschlossene Fahrwerks- und Einlassschalen mit Kacheltextur. Die Lichtreflex-Fläche des Fahrwerks bleibt frei. Klappen folgen der belegten Seite.
- Lückenfüllung an Cockpitmasken und Mk2-Tanks. Ein dunkles Fugenbett schließt die Tankseite bis zur Umgriffsgrenze beider Muster.
- Überarbeitete Glühtexturen für Quadrat/Schwarz, Hex/Keramik und Quadrat/Keramik. Das Wiedereintrittsprotokoll enthält die maximale Kacheltemperatur und Glühintensität.
- Messwerte und Reproduktion im [Übergabeprotokoll](docs/CLAUDE-HANDOFF.md).

## 1.5.0 (2026-11-19)

Nach dem Spieltest von 1.4.0. Nur offline geprüft (Build ohne Fehler und Warnungen, SelfCheck grün, KSP-Aufrufe und Shader gegen die Spieldateien geprüft).

### Modul

- **Glühen in drei Zuständen** (Vorbild `24.webp`): normal (kein Glühen, unter 550 K), mittel (800 K: orange, dunkle Ränder), Spitze (1300 K: weißgelber Kern, orange Ränder), dazwischen Smoothstep und wie bisher zeitlich gedämpft. Neuer Shader `KSP/Emissive/Specular` (in `resources.assets` geprüft: `_Emissive`, `_EmissiveColor`, dazu `_TemperatureColor`); die Glühfarbe geht nach `_EmissiveColor` und wird mit einer Emissions-Textur je Muster multipliziert (`Textures/HexGlow.png`, `SquareGlow.png`, 512 px, gleiches 4×4-Atlas-Layout, je Kachel weicher Verlauf warmweißer Kern → dunkelorange Rand, kein Rauschen; `tools/make-glow-textures.ps1`, Schlüssel `emissive` in `Textures.cfg`). Schlichte Keramik (keine Kachel-UVs) glüht flach (`glowFlatColor`). Ohne Emissions-Shader Rückfall auf `_TemperatureColor`. Rampe `glowRamp = 550 1 0.5 0.14 0; 800 1 0.5 0.14 0.55; 1300 1 1 1 1.2` und Shader stehen jetzt auch im Part-cfg. Vorschau `preview/v11/glow-states.png`.
- **Glüh-Stufen behoben.** Ursachen: (1) Glüh-Bins von 20 Grad: alle Kacheln eines Bins teilen eine Farbe, benachbarte Bins lagen auf gewölbten Rümpfen bis etwa 200 K auseinander (Gleichgewicht ∝ (n·v)^¼) → sichtbare Bänder; jetzt `glowBinAngle` = 6 Grad, `glowMaxBins` = 48. (2) Hauttemperatur und Wärmeübergang kamen je Teil (`skinTemperature`, `finalCoeff/convectionArea`), an jeder Teilnaht (auch Klappe, Tür, Fahrwerk) sprang die Farbe; jetzt für das ganze Decal nach Kachelfläche gemittelt. (3) Die alte Rampe hatte fünf Stützstellen mit Smoothstep je Abschnitt, also vier Plateaus zwischen 1000 und 2500 K, die eine glatte Temperaturverteilung in Farbstufen zerlegten; jetzt drei Zustände. Die Schwelle 0,002 (Farbänderung, ab der neu gesetzt wird) ist unter einer 8-Bit-Stufe und war nicht die Ursache.
- **Neuer Regler "Bedeckte Höhe"** (Fläche, 1 bis 100 %, 1-%-Schritte, Standard 100 %): Anteil der Ausdehnung des Zielteils entlang der Decal-Normalen, von der Decal-Seite gemessen, als zusätzliche gerade Schnittebene; Max. Umgriff gilt weiter (der strengere gewinnt). Mk3-Tank seitlich, 50 % = halbe Höhe. 0 % wäre "keine Kacheln" und würde von `TileBuilder` umgedreht (Startpunkt jenseits der Ebene), deshalb beginnt der Regler bei 1 %.
- **RCS-Düsen** bleiben nur noch an Cockpits frei, deren Eintrag in `CockpitCover.cfg` `keepRcs = true` hat (Mk2 ja, Mk3 nein); überall sonst werden sie überkachelt. Luken (`Airlock`) bleiben überall frei.
- **Mk3-Lagerraum/Laderampe**: Im Editor hingen nur Fahrwerks-Kacheln am bewegten Transform; Klappen-, Tür- und Rampen-Kacheln lagen unter dem Decal und blieben beim Öffnen in geschlossener Lage in der Luft stehen (Rampe "fährt nicht mit"; in `23.png` die schwebende Türschale, deren Unterseiten mit dem weißen Rückseiten-Material zu sehen waren). Jetzt hängen sie auch im Editor an ihrem Transform (ohne Mesh-Collider, der sonst das Nachbarteil statt des Decals anwählen würde). Rückseiten der Kacheln dunkel wie die Fugen statt weiß.
- **"tile materials ... restoring"**: Ursache gefunden, harmlos: KSP selbst kopiert die Materialien aller Renderer unter dem Modell eines Teils (`EditorLogic.highlightSelected` über `renderer.material` beim Anbringen im Editor, `MaterialColorUpdater.CreateRendererList` über `renderer.materials` in der Thermik-Ansicht); der Schutz stellt sie wie bisher wieder her (jetzt auch die Emissions-Textur). Neue Logzeile `tile materials replaced on <Teil> (slot <i>: '<Material>' shader '<Shader>'), restoring` (einmal je Decal).
- Logzeilen `build ...` und `flight check ...`: `hasTemperatureColor=` ersetzt durch `glow=_EmissiveColor|_TemperatureColor|none`, `build` zusätzlich `emissive=<Textur>`.
- `[assembly: KSPAssembly("AblatorDecals", 1, 5)]`, Version 1.5.0; Lokalisierung en/de (`coverHeight`, Part-Beschreibung).

### Geometrie

- **Overlay für angebaute Objekte** (Klappen, Steuerflächen inkl. Heckflossen wie `tailfin`/`R8winglet`, Lufteinlässe, Lagerraum-Türen und Mk3-Laderampe, eingefahrenes Fahrwerk): Das Haupt-Decal kachelt nur noch sein Teil; jedes Objekt bekommt eine eigene Kachelung seiner ganzen sichtbaren Außenhaut (Einlass: Öffnung frei; Tür/Rampe: Innenseite frei; Fahrwerk: die eingefahrene Schale), im eigenen Raum gebaut (Gitter am Teil-Transform verankert, ein Flutstart je Flächenstück). Das Ergebnis hängt nur von Muster, Kachelgröße, Fuge, Dicke ab, nicht von Lage, Drehung oder Größe des Haupt-Decals (behebt den Einlass-Zufall aus `17.webp`/`18.webp` und den Klappen-Zickzack aus `19.webp`). Overlay-Kacheln gehören thermisch und ablativ zum Decal und hängen am bewegten Transform. `TileBuilder.BuildOverlay`, `TileSettings.Overlay/Obstacle/Inward/PassUnder`.
- **Haupt-Decal endet am Fuß der Objekte**: Objekte sind Hindernisse; die Kacheln werden an ihrem Umriss gerade geschnitten und laufen nicht mehr unter Einlässe und Flossen (Hüllpunkte im Objekt-Volumen); unter eingefahrenem Fahrwerk laufen sie weiter durch.
- **Lufteinlässe**: Lippe und hinteres Ende werden bekachelt, frei bleibt nur das Ansaugloch (zur Luft gewandte Flächen hinter der Lippe) und der Kanal am Mund (Regel für Kanalwände nur noch in den vorderen 35 cm: beim IntakeRadialLong liegt die Einlass-Achse neben dem Körper und nahm hinten Außenflächen weg → weiße Flecken). Flächen, die auf das eigene Innere des Objekts schauen, bekommen keine Overlay-Kacheln mehr (vorher lagen Kacheln innen und stachen durch die Haut: die Streifen an den Flanken).
- Reine Flügel und Strakes (`ModuleLiftingSurface` ohne Steuerfläche) bekommen kein Overlay, sie brauchen ein eigenes Decal. Ein angebautes Teil mit eigenem Decal gehört nur diesem Decal.
- **Lagerraum-Türen und Mk3-Rampe** übernehmen Max. Umgriff, Bedeckte Höhe und die Stapel-Schnitte des Haupt-Decals (Normale in geschlossener Pose, gerader Schnitt): ein Mk3-Lagerraum mit Decal unten bleibt oben weiß. Das hängt nur von Drehung und Einstellungen des Decals ab, nicht von seiner Lage.
- Welche Fahrwerks- und Einlassflächen gelten, wird im Weltraum entschieden (vorher im Decal-Raum), damit nichts vom Decal abhängt.
- **Cockpit-Masken (`REGION` in `CockpitCover.cfg`)**: geschlossene Konturen auf einer Kartenebene, durch das Teil verlängert, optional auf eine Seite begrenzt; Kacheln werden an den Kanten gerade geschnitten (glatte Kontur = glatter Rand), auch beide Seiten schmaler Bänder; nicht erreichte Inseln bekommen eigene Fluten. Format im Kopf der Datei.
- **Vorgefertigte Cockpit-Hüllen** (Mk2, Mk3): fertige Kachel-Netze im Teil-Raum des Cockpits in `GameData/AblatorDecals/Covers/<Teil>_<Hex|Square>.bin` (eigenes Binärformat `CoverMesh`, erzeugt mit `tools/Preview --bake-cover <GameData> <Teil> <Ordner>` aus dem Eintrag in `CockpitCover.cfg`). Mit Cockpit-Form lädt das Modul die Datei statt zu bauen (Hosts, Glühen, Thermik, Ablator, Collider wie sonst); Kachelgröße, Winkel, Fuge und Dicke sind dort fest (Regler ausgeblendet, Statuszeile "Vorgefertigt"), Ausführung/Textur wirkt weiter. Ziegel-Muster und Mk1 werden wie bisher berechnet.
- **Mk3-Cockpit** im Shuttle-Stil nach der Skizze des Nutzers: Nasenkappe (vorn ringsum bis zur Chine hinunter) mit dem "Kinn" als rundem Bogen über der Frontscheibe (von den Fenstern neben dem mittleren Paar bis zum Scheitel gut ein Drittel des Wegs zur Nasenspitze), Unterseite unter der Chine; alles andere weiß, RCS überkachelt. Zwei glatte Konturen, keine Inseln. `tools/Preview --mask` kann dunkle Paneele einer Textur als Ausgangspunkt in REGIONs übertragen.
- **Mk2-Cockpit** nach der Skizze des Nutzers: ganz bekachelt außer einer glatten Schleife oben um Fensterrahmen, die runden RCS-Düsen davor (nur Modell/Textur, das Teil hat kein RCS-Modul, deshalb fand die Sperrzone sie nicht) und die MK-2-/KERBAL-Schrift dahinter; Luke frei. Mk1 unverändert. Würfelförmige Sperrzonen (RCS-Düsen anderer Teile) werden rund freigehalten (Kugel, jede Kachel tangential geschnitten).
- `SurfaceIndex.Raycast` zerlegt lange Strahlen (Overlay-Sichtbarkeit über die Breite eines Lagerraums), sonst gleiche Ergebnisse.
- Vorschau (`tools/Preview`): Overlays wie im Modul mit Abdeckung je Objekt, Fußspalt, Prüfsumme (`nudge=<m>,<Grad>` für den Determinismus-Test), Ansichten `views=ov`, `ovOnly=1`; Fahrwerk in eingefahrener Pose aus dem Animations-Clip, Skinned Meshes; Masken-Werkzeug `--mask`, `maskShow=1`; `keepRcs` wie das Modul. SelfCheck: Overlay-Determinismus (Decal 5 cm/15° verschoben), Abdeckung, Schnitt am Objektfuß, Masken-Band, runde RCS-Aussparung (`SelfCheck v11` für nur diese). Renders `preview/v11/`.

## 1.4.0 (2026-11-16)

Nach dem Spieltest von 1.3.0. Nur offline geprüft (Build ohne Fehler und Warnungen, SelfCheck grün, KSP-Aufrufe gegen Assembly-CSharp.dll geprüft).

### Behoben

- **Teile ließen sich nicht überall auf die Kacheln setzen** (Fahrwerk, Solarpanels, RCS-Düsen). Die Kachel-Collider wurden nur bei "Teil gegriffen" und "Teil aus der Liste" abgeschaltet; ein mit Alt kopiertes Teil (`PartCopied`) und jeder andere Weg, ein Teil an die Maus zu bekommen, ließ sie an. KSPs Oberflächen-Raycast traf dann das Decal (`allowSrfAttach = 0`) und das Teil ließ sich dort nicht anbringen. Jetzt prüft jedes Decal in jedem Frame, ob ein Teil ohne Decal-Modul an der Maus hängt, und schaltet alle Kachel- und Ring-Collider ab, auch die gerade neu gebauten.
- **Klappen und Lufteinlässe fehlten** im Modus "Ganzes Teil": Steuerflächen (`ModuleControlSurface`, `ModuleAeroSurface`) und Lufteinlässe, die am Teil des Decals kleben, werden wieder mitgekachelt.
- **Fahrwerk unbedeckt**: Kacheln liegen jetzt auf der ganzen Außenhaut des eingefahrenen Fahrwerks, auch auf gewölbten Gondeln (GearMedium) und Klappen (SmallGearBay). Weg fallen nur Flächen, die zum Rumpf zeigen, mehr als 2 cm im Rumpf liegen oder eingefahren verdeckt sind (Rad, Federbein: ein Strahl entlang der Flächennormalen trifft innerhalb 0,5 m das eigene Fahrwerk). Skinned Meshes (GearSmall) werden in eingefahrener Pose mitgelesen. Bei "Ganzes Teil" findet das Fahrwerk den Rumpf auch weit vom Anbaupunkt des Decals.

### Neu

- **Sperrzonen**: Auf den belegten Teilen bleiben RCS-Düsen (`ModuleRCS`/`ModuleRCSFX`, Würfel mit halber Kantenlänge `rcsKeepOut` = 0,12 m) und Luken (Collider mit Tag `Airlock`, plus 5 cm) frei (`TileSettings.KeepOut`).
- **Dicke im Editor-Fenster**: Das Fenster des Editor-Knopfs setzt jetzt auch die Kacheldicke an allen Decals (ein Undo-Schritt, Ablator und Masse folgen).
- **Glühen mit Überblendung**: Die angezeigte Glühfarbe folgt ihrem Zielwert zeitlich gedämpft (`glowFadeTime` = 3 s, unabhängig von der Bildrate), beim Aufheizen wie beim Abkühlen; zwischen den Stützstellen der Rampe Smoothstep statt linear.
- Cockpit-Form auch für das Mk3-Cockpit (`mk3Cockpit_Shuttle`), sobald `CockpitCover.cfg` Ebenen dafür hat.

### Geändert

- **"Über Teile hinweg" entfernt**: Ein Decal belegt sein Teil samt angeklebter Klappen, Einlässe und Fahrwerk. Der gespeicherte Wert alter Spielstände wird geladen und ignoriert. Auf einem Cockpit mit Cockpit-Form bleibt es beim Cockpit allein. Am Teil des Decals angeklebte Klappen und Einlässe werden nicht mehr auf die Kacheln geschoben (sie bekommen eigene Kacheln).
- **Glühen viel schwächer**: neue Rampe `1000 0.35 0.03 0 0; 1200 0.35 0.03 0 0.12; 1400 0.6 0.1 0.02 0.25; 1900 0.8 0.25 0.05 0.4; 2500 0.9 0.45 0.15 0.55` (dunkelrot bis gedämpft orange, kaum gelb), Farbe an den Shader vormultipliziert (`rgb·a`), `glowEmissivity` 0,95. KSPs eigenes Glühen des Teils (Property-Block über `Part.temperatureRenderer`) addiert nicht, es würde unsere Farben überschreiben; nach dem Umschalten der Thermik-Ansicht (`Part.ResetMPB`) nimmt das Decal seine Renderer wieder aus dieser Liste.
- **Fugenbreite Standard 0 m** (gespeicherte Decals behalten ihren Wert).
- **Weiße Kacheln entfernt** (HexWhite, SquareWhite: Textures.cfg, B9-Umschalter, Auswahl, Fenster, Lokalisierung, Texturen). Mit Weiß gespeicherte Decals laden schwarz. Bei einer bestehenden Installation `GameData/AblatorDecals/Textures/HexWhite.png` und `SquareWhite.png` von Hand löschen (sie stören nicht, belegen aber Speicher).
- Neue Logzeilen: `keep-out: rcs=... hatches=... on [...]` (höchstens alle 5 s je Decal); die Fahrwerkszeile endet jetzt auf `skinned=<n>`.
- `[assembly: KSPAssembly("AblatorDecals", 1, 4)]`, Version 1.4.0.

### Geometrie

- **Mk2-Lagerraum bekam keine Kacheln** ("no usable surface of mk2CargoBayS"). Ursache: Mk2- und Mk3-Lagerräume hängen über die nach innen zeigenden Knoten `top2`/`bottom2` am Nachbarn; die Schnittebene an diesem Stack-Knoten zeigte entlang der Knotenrichtung nach innen und schnitt den ganzen Lagerraum weg. Die Ebene zeigt jetzt immer von der Teilmitte weg (Modul), und `TileBuilder` dreht jede Schnittebene um, hinter der der Anbaupunkt des Decals liegt.
- **Lagerraum-Türen** (die Transforms, die die Animation des `ModuleCargoBay` bewegt) sind eigene Körper wie Klappen: in geschlossener Lage gekachelt, die Kacheln fahren im Flug mit der Tür; Logzeile `bay <Teil>: anim=... closed=... doors=<n>`.
- **Umgriffsgrenze ist eine Linie auf dem Rumpf** (Mk3, Muster): Ob eine Fläche innerhalb von "Max. Umgriff" liegt, entscheidet jetzt ihre Flächennormale, nicht die geglättete Normale am Kachelmittelpunkt; Kacheln werden an dieser Linie gerade geschnitten. Hex, Quadrat, Ziegel und jede Kachelgröße enden damit an derselben Linie. Liegt eine Knickkante (> 25°) innerhalb ±20° der Grenze, endet die Bekachelung genau an der Kante: Auf dem Mk3-Rumpf deckt jeder Umgriff von etwa 35° bis 89° die Unterseite exakt bis zur Seitenkante.
- **Sperrzonen** (`TileSettings.KeepOut`): Kacheln werden an der Boxfläche gerade geschnitten, Kacheln mit Mittelpunkt in der Box fallen weg und die Nachbarn wachsen bis an die Box.
- **Mk3-Cockpit-Form** (`mk3Cockpit_Shuttle`, `CockpitCover.cfg`, Stock und ReStock): Unterseite, Seiten bis knapp unter die Fensterunterkante, Nase vor der Frontscheibe ganz bedeckt. Bei Cockpit-Form und "Ganzes Teil" begrenzt die Fläche den Weg um breite Teile nicht mehr (vorher endete die Bekachelung auf dem Mk3-Cockpit vor der Nase).
- **Lufteinlässe**: Ein Einlass, auf den die Flutung nur teilweise kam (einer von zwei symmetrischen `airScoop`), bekommt auf großen unbedeckten Flächen eigene Kacheln; Flanken, die nur über dem Rumpf stehen, gelten nicht mehr als dünne Kante.
- **Fuge 0**: Wo zwei Flutungsfronten zusammentreffen, bleiben je 0,3 mm frei (die Fugenfläche schließt sie), sonst überlappten die leicht gekippten Kacheln.
- Vorschau (`tools/Preview`): Schnittebenen wie im Modul aus den `attN`-Zeilen, Sperrzonen, Lagerraum-Türen, Teile-Texturen (`hullTex=`), Kantenansichten (`edgeL/edgeR`), Decal versetzen (`place=`), Auswahl (`only=`), Körper einfärben (`tint=1`); SelfCheck-Fälle für Umgriffslinie, Sperrzone, gedrehte Schnittebene, Fuge 0 und Fläche bei "Ganzes Teil".

## 1.3.0 (2026-11-16)

Nach dem Spieltest von 1.2.0. Nur offline geprüft (Build ohne Fehler und Warnungen, SelfCheck grün, KSP-Aufrufe gegen Assembly-CSharp.dll geprüft).

### Neu

- **Ein Hitzeschild je Teil**: neuer Standard "Ganzes Teil". Das Decal kachelt sein Teil (mit Klappen, Einlässen, Fahrwerk), soweit es innerhalb von "Max. Umgriff" (Standard jetzt 100 Grad) zur Decal-Normalen zeigt, mit geradem Schnitt an jeder Teilnaht. Breite und Höhe nur noch bei "Fläche begrenzen" (Spielstände vor 1.3: so geladen). "Über Teile hinweg" steht für neue Decals auf aus.
- **Bereich schon belegt**: Ein zweites Decal auf einem belegten Teil wird nicht gebaut (rot hervorgehoben, Statuszeile, Bildschirmmeldung). Nur zwei Decals mit "Fläche begrenzen" teilen sich ein Teil.
- **Wo ist welches Decal**: Sechseck-Ring am Anbaupunkt bei Mauszeiger oder offenem Fenster, Hervorhebung bei offenem Fenster, Ring als Griff.
- Logzeilen je Fahrwerk (`gear ...: deploy=... retracted faces=... bodies=...`) und je Lufteinlass (`intake ...: transform=... faces kept=... dropped=...`).

### Geändert

- **Anklicken**: Im Editor genaue Mesh-Collider auf den Kacheln statt 1-m-Boxen (man klickt nicht mehr durch, Decal auf Decal wird zuverlässig rot). Im Flug weiter Boxen.
- Abgeschaltete Kachel-Collider gehen in jedem Fall wieder an, sobald nichts mehr an der Maus hängt (auch nach Undo/Redo und Zurücklegen in die Teileliste).
- Während ein Teil an der Maus hängt, bauen andere Decals nicht neu (keine Kaskaden beim Ziehen). Füllen um ältere Kacheln nur noch zwischen Decals mit "Fläche begrenzen".
- **Glühen langsamer und später gelb**: unter 900 K kein Glühen, 900 K dunkelrot, 1300 K orange, 1700 K gelb, ab 2200 K gelbweiß; Wärmekapazität 30 kJ/m²K.
- `[assembly: KSPAssembly("AblatorDecals", 1, 3)]`, Version 1.3.0.

## 1.2.0 (2026-11-16)

Nach dem Spieltest von 1.1.0. Nur offline geprüft (Build ohne Fehler und Warnungen, KSP-Aufrufe gegen die dekompilierte Assembly-CSharp.dll und B9PartSwitch.dll geprüft); Test im Spiel steht aus.

### Behoben

- **6631 NullReferenceExceptions in `Part.SetOpacity` beim Platzieren im Editor.** Ursache: Beim Neuaufbau wurde ein überzähliger Kachel-Host nur deaktiviert und verzögert zerstört, hing aber noch unter dem Modell. `Part.FindModelComponents` sammelt auch inaktive Objekte, also landete der Renderer in der frisch gebauten Highlight-Liste und war am Frame-Ende zerstört; `SetOpacity` prüft nicht auf null. Jetzt verlässt jeder Host vor dem Zerstören sofort die Hierarchie, und nach jedem Erzeugen/Zerstören von Renderern werden alle Renderer-Caches des Parts und der Highlighter neu aufgebaut (auch bei kopierten Parts).
- **Reentry-Artefakte**: Das Platzhalter-Modell (7 Sechsecke) wird auf dem Teil im Editor und Flug entfernt statt nur ausgeblendet; Reentry-Effekte zeichneten es trotz abgeschaltetem Renderer. Das Teile-Icon bleibt.

### Neu

- **Kein Decal auf Decal**: Nichts kann mehr am Decal kleben (`attachRules = 0,1,0,0,1`); über fremden Kacheln bleibt die Vorschau rot. Überlappen sich zwei Decals, legt das jüngere (höhere `persistentId`) keine Kachel auf die des älteren; ändert sich das ältere, baut das jüngere neu.
- **Teile auf Kacheln wie am Tank**: Während ein anderes Teil an der Maus hängt, sind die Kachel-Collider aus; das Teil klebt am Rumpf und wird danach um die Kacheldicke auf die Kacheln geschoben (aus dem Rumpftreffer berechnet, mehrfaches Anbringen addiert nichts). Im Flug kollidieren solche Teile (und alte Kinder des Decals) nie mit den Kacheln, auch nach Abtrennen oder Andocken.
- **Fahrwerk**: wird nie gekachelt, Kacheln laufen darunter durch; Fahrwerk-Collider (auch Rad-Collider) und Kacheln kollidieren nicht. Weil die Federungsabfrage der Räder eine Szenenabfrage ist, die `IgnoreCollision` nicht beachtet, entsteht im Bereich des Fahrwerks (Teil, Rad ± Radius + Federweg, plus `gearMargin` = 0,3 m) keine Kachel-Collider-Box; die Kacheln bleiben sichtbar.
- **Gekacheltes Fahrwerk**: Fahrwerk im Kachelbereich bekommt auf der Außenseite der eingefahrenen Lage eigene Kacheln, die mit Klappen und Federbein mitfahren (eingefahrene Pose kurz aus der Ausfahr-Animation gelesen, danach exakt zurückgesetzt). Rad, Federbein und Innenseiten bleiben frei, Collider-Boxen gibt es dort keine.
- **Thermik für Teile im Kachelbereich** (Fahrwerk, Radial-Decoupler u. a.): gleiche Wärmefluss-Reduktion wie das Teil, auf dem sie sitzen, aus demselben Ablator.
- **Lufteinlässe und kleine Steuerflächen**: "Über Teile hinweg" belegt jetzt auch Lufteinlässe (außen, die Öffnung bleibt frei) und Steuerflächen, die an irgendeinem abgedeckten Teil kleben (nicht nur an Flügeln). Steuerflächen bleiben eigene Körper.
- **Cockpit-Form** für Mk1- und Mk2-Cockpit (Schalter in "Fläche", Ebenen in `CockpitCover.cfg`: Vereinigung der normalen Ebenen, Schnitt mit `cut = true`).
- Die flache Vorschau eines Decals ohne Untergrund ist auf 4 × 4 m begrenzt (20 × 20 m kostete 2 s und 222 MB).
- **Editor-Knopf** mit Fenster: Muster, Kachelart, Kachelgröße für alle Hitzeschilde des Schiffs (mit B9 über dessen Umschalter, ein Undo-Schritt). Icon von `tools/make-app-icon.ps1`.

### Geändert

- **Glühen gelber**: eigene Farbrampe dunkelrot, orange, gelb, gelbweiß statt der Stock-Schwarzkörperfarbe, einstellbar über `glowRamp`.
- Nichts hängt von ReStock ab (Teile über Module erkannt, eigenes Platzhalter-Modell, Cockpit-Ebenen für Stock-Modelle, ReStock-Sätze optional per `:NEEDS`).
- `[assembly: KSPAssembly("AblatorDecals", 1, 2)]`, Version 1.2.0.

## 1.1.0 (2026-11-15)

Nach dem ersten Spieltest von 1.0. Nur offline geprüft (Build, Configs, Atlas angesehen); Test im Spiel steht aus, siehe Test-Checkliste in der README (neue Schritte 3, 4, 6, 9, 10, 11, 16).

### Neu

- **Teile auf die Kacheln setzen** (z. B. Radial-Decoupler): `attachRules = 0,1,0,1,1`. Was am Decal hängt, wird nie mit Kacheln belegt, löst keinen Neuaufbau aus und kollidiert im Flug nicht mit den Kacheln (auch nicht nach dem Abtrennen).
- **Steuerflächen**: Die Klappe eines Teils mit `ModuleControlSurface` ist ein eigener Körper. Keine Kachel liegt über der Scharnierfuge, die Kacheln der Klappe hängen im Flug an der Klappe und bewegen sich mit, samt einer eigenen Collider-Box. Gebaut wird in Neutralstellung. Thermik und Glühen folgen dem Ausschlag, gezählt wird pro Teil.
- **Über Flügel hinweg**: "Über Teile hinweg" belegt neben gestapelten Rumpfteilen auch Flügel- und Steuerflächenteile, die an einem Flügelteil kleben.
- **Kachelart** (Schwarz, Weiß, Schlichte Keramik) statt der Textur-Liste. Die Textur ergibt sich aus Muster und Kachelart (`shape` und neuer Schlüssel `finish` in `Textures.cfg`). Neuer Atlas `HexWhite.png` (weiße Sechsecke mit Teilenummern), erzeugt von `tools/make-tile-atlases.ps1`; die drei bestehenden Atlanten erzeugt das Skript unverändert bytegleich.
- **Abwickeln**: Breite und Höhe werden standardmäßig entlang des Rumpfs gemessen (wie ein Aufkleber); umschaltbar auf "Von vorne gesehen".
- **B9PartSwitch (optional)**: Mit B9 ersetzen zwei Umschalter mit Farbfeldern ("Muster", "Kachelart") die eigenen Auswahlfelder (`Patches/B9PartSwitch.cfg`, `:NEEDS[B9PartSwitch]`). Die Kachelart liegt dafür im neuen Modul `ModuleAblativeDecalFinish`, weil B9 zwei Umschalter nicht auf dasselbe Modul wirken lässt. Nach einem B9-Wechsel baut das Decal im Editor neu.
- **"Kacheln neu mischen"**: neue Zufallsauswahl von Atlaszellen und fehlenden Kacheln.

### Geändert

- **Rechtsklick-Menü neu geordnet**: Statuszeilen oben, dann die Gruppen "Kacheln" (Muster, Kachelart, Kachelgröße, Musterwinkel, Fugenbreite, Dicke) und "Fläche" (Breite, Höhe, Max. Umgriff, Abwickeln, Über Teile hinweg, Rand), "Feinheiten" eingeklappt (Textur, Texturmaßstab, Kachelverlust, Kacheln neu mischen). Klarere Namen in Deutsch und Englisch.
- **Kleinere Standard-Kacheln**: Kachelgröße 0,1 m (Bereich 0,03 bis 0,5 m), Dicke 0,025 m. Die automatische Umschaltung der Kachelgröße beim Musterwechsel entfällt.
- **Saubere Kanten**: Über scharfen Knicken (Mk3-Bodenkante, Mk2-Chines, Cockpit-Ränder) enden die Kacheln genau auf der Knicklinie mit Gehrung, statt als Block auf einer Mittelebene zu schweben (Mk3 vorher bis 20 mm Luft, jetzt unter 5 mm). Randkacheln über wegkippendem Rumpf werden an der Kipplinie geschnitten. Die Abdeckung läuft über Knicke bis 60 Grad weiter.
- **Gerader Rand** schneidet jetzt auch im abgewickelten Modus gerade; der Unterschied zu "Ganze Kacheln" ist sichtbar, außer wenn der Rand zufällig auf dem Raster liegt (Quadrate bei 0 Grad und Breite als Vielfaches der Kachelgröße).
- **Versetzte Quadrate** werden nicht mehr angeboten. Gespeicherte Decals mit diesem Muster laden weiter.
- Spielstände von 1.0: Das alte Feld `textureName` wird beim Laden in die Kachelart übersetzt und danach geleert.
- `[assembly: KSPAssembly("AblatorDecals", 1, 1)]`.

## 1.0.0 (2026-11-13)

Neufassung gegenüber den Teststand-Versionen v1 bis v5. Nur offline geprüft (Selbsttest, Vorschau-Renderer, Code-Review); Test im Spiel steht aus, siehe Test-Checkliste in der README.

### Neu

- **Kachelgeometrie neu geschrieben** (`TilePattern`, `TileBuilder`): Kacheln sind eben und starr, ihr Umriss wird aus den tatsächlichen Nachbarpositionen geschnitten. Keine verzogenen oder gewölbten Kacheln, keine hellen Ritzen, kein Stapeln. Die lückenlose Abdeckung kommt von einem dunklen Fugenblatt unter den Kacheln.
- **Drei Muster**: Sechsecke (Starship), Quadrate und Versetzte Quadrate (Space Shuttle), mit Musterwinkel von -90 bis 90 Grad.
- **Kacheln erreichen Flügelkanten, Flügelspitzen und Rumpfecken** und enden an Part-Kanten sauber (Teilkacheln mit Schnitt an der Kante).
- **Rand wählbar**: ganze Kacheln oder gerader Schnitt am Rand der Fläche.
- **Fehlende Kacheln** (0 bis 20 %, kosmetisch).
- **Fehler behoben, der Kacheln endlos um geschlossene Rümpfe laufen ließ** (v5: bei Max. Umgriff 180 Grad 40 000 Kacheln auf 30 m²). Die Flutung verwirft jetzt Kacheln, die auf derselben Seite zu dicht an bestehenden liegen. Regressionstest im Selbsttest.
- **Abdeckung gestapelter Rumpfteile**: Die Kacheln laufen über per Stack-Node verbundene Teile (ohne Triebwerke, Decoupler, Docking-Ports, Fairings, andere Decals), schaltbar. Ein Netz je Ziel-Part, Thermik je Ziel-Part.
- **Texturen**: Atlanten mit Zellen (`HexBlack`, `SquareBlack`, `SquareWhite`, je 4 × 4 Zellen), Alpha als Glanz; einfache Textur (`Ceramic`) wahlweise je Kachel oder über das ganze Decal. Optionale Metadaten über `ABLATORDECAL_TEXTURE` (`shape`, `cells`, `randomRotation`, `title`). Die Textur-Liste im Editor zeigt nur Texturen, die zur Form passen. Die Atlas-Abbildung ist von außen nicht gespiegelt. Generator `tools/make-tile-atlases.ps1`.
- **Bauen ohne Ruckeln im Editor**: Rumpf-Dreiecke werden auf dem Hauptthread gesammelt (und zwischengespeichert), `SurfaceIndex` und `TileBuilder` laufen auf einem Worker. Höchstens ein Build je Modul, veraltete Ergebnisse werden ersetzt.
- **Glühen** (rein optisch): Oberflächentemperatur je Kachelgruppe (`TileGlow`), Farbe über `_TemperatureColor`. Config: `glowEnabled`, `glowEmissivity`, `glowHeatCapacity`, `glowConduction`.
- **Verkohlung**: Kacheln färben sich mit dem verbrauchten Ablator von Weiß nach `charColor`.
- **Kacheln im Flug am Ziel-Part** statt unter dem Modell des Decals. Sie bleiben beim Verbiegen der Rakete am richtigen Teil und werden von KSPs Glüh-Code nicht angefasst. Schutz vor Geister-Kopien beim Klonen von Parts, Wächter für Materialien und Texturen.
- **Bedienung**: Rechtsklick-Fenster in der Gruppe "Hitzekacheln" (Muster, Textur, Kachelgröße, Musterwinkel, Fugenbreite, Breite, Höhe, Dicke, Max. Umgriff, Abdeckung, Rand, Fehlende Kacheln, Textur-Abbildung, Texturgröße). Beim Musterwechsel wird eine passende Textur gewählt und die Kachelgröße angepasst. Spiegel-Symmetrie spiegelt den Musterwinkel.
- **Lokalisierung** Deutsch und Englisch (`Localization/de-de.cfg`, `en-us.cfg`).
- **Part-Icon** (eigenes Modell `Assets/placeholder.mu`, unabhängig von Stock-Modellen und ReStock). Kein Drag Cube.
- **Masse und Kosten** nach abgedeckter Fläche (`massPerArea`, `costPerArea`).
- **Diagnose im Log**: `build`-Zeile je Aufbau, `flight check` 3 s nach Physikstart, `reentry summary`. Die sekündliche Thermikzeile ist jetzt aus (`logInterval = 0`).
- **Anzeigen** im Flug und im Editor ergänzt (u. a. Kacheltemperatur, Abgedeckte Teile, Kachelmasse).
- Assembly-Version 1.0.0.0, `[assembly: KSPAssembly("AblatorDecals", 1, 0)]`.

### Unverändert

- Thermik-Prinzip: Das Decal nimmt Konvektionshitze der abgedeckten Teile auf, soweit seine Kacheln im Fahrtwind liegen (je Teil bei 100 % gedeckelt), und verbraucht gemeinsamen Ablator (`ShieldMath`).

### Geändert

- `TestTweaks.cfg` (Reaktionsräder für den Test) liegt jetzt in `GameData/AblatorDecalsTest/` und gehört nicht mehr zum Mod.

### Bekannte Grenzen

Siehe README, Abschnitt "Bekannte Grenzen".
