# DESIGN v8: Nachtrag (verbindlich, ergänzt DESIGN.md und DESIGN-v7.md)

Grundlage: Spieltest von 1.1.0 (Nutzer: "wirklich schon sehr gut", Flaps bewegen sich mit, Menü gut, keine Geometriefehler
mehr). KSP.log 16.11. 17:16–17:58: 6631 × `NullReferenceException` in `Renderer.SetPropertyBlock` ← `Part.SetOpacity` ←
`EditorLogic.highlightSelected`. Version 1.2.0.

## A. Modul (Agent "Modul")
1. **Editor-Bug beim Platzieren (Nutzer Punkt 2), Ursache gesichert:** KSP cached die Renderer eines Parts
   (`Part.SetOpacity`, Highlighter). Nach jedem Neuaufbau zeigen die Caches auf zerstörte Kachel-Renderer. Nach jedem
   Erzeugen/Zerstören von Kachel- oder Placeholder-Renderern: `part.ResetModelRenderersCache()`,
   `ResetModelMeshRenderersCache()`, `ResetModelSkinnedMeshRenderersCache()` und Highlighter neu initialisieren
   (`part.highlighter?.ReinitMaterials()` o. ä., per Reflection/Existenz prüfen). Lieber Renderer wiederverwenden als
   zerstören. Danach darf im Log keine solche NRE mehr entstehen (Code-Pfad begründen, wir können nicht starten).
2. **Placeholder komplett weg (Punkt 7):** Die Reentry-Artefakte sind die 7-Sechseck-Placeholder-Modelle (je Decal ein
   Cluster, unter den Kacheln, vom Reentry-FX trotz deaktiviertem Renderer gezeichnet). Ausser im Teile-Icon: MeshRenderer +
   MeshFilter des Placeholders zerstören bzw. das GameObject deaktivieren (in Editor und Flug), danach Caches wie A1.
3. **Kein Decal auf Decal (Punkt 3):** `attachRules = 0,1,0,0,1` (nichts darf am Decal kleben). Während ein Decal gezogen
   wird, bleiben die Kachel-Collider anderer Decals aktiv → Treffer auf Kacheln = nicht anbringbar (rot), man muss auf freien
   Rumpf. Zusätzlich Überlappung: Das jüngere Decal (höhere `persistentId`) bekommt `TileSettings.Occupied` = Punkte der
   Kacheln älterer Decals auf denselben Zielteilen (z. B. Top-Vertices, in eigenen Decal-Raum transformiert); ältere Decals
   ignorieren jüngere (keine Rebuild-Schleifen). Ein älteres Decal, das neu gebaut wird, stösst ein Rebuild der jüngeren an.
4. **Radial-Decoupler wie am Tank (Punkt 4):** Wird ein anderer Part (kein Decal) gezogen, Kachel-Collider aller Decals im
   Editor deaktivieren (`GameEvents.onEditorPartEvent`: PartPicked/PartCreated → aus; PartAttached/PartDropped/PartDeleted →
   an). Der Part klebt dann am Tank (Parent = Tank) wie ohne Decal. Nach `PartAttached` (inkl. Symmetrie-Gegenstücke): liegt
   der Anbaupunkt im Kachelbereich eines Decals auf dem Parent, Part um die Kacheldicke entlang der Anbau-Normalen nach
   aussen schieben (idempotent: Lage aus dem Rumpftreffer berechnen, nicht aufaddieren). Flug: Kollision zwischen Kacheln und
   diesen Parts ignorieren (bestehendes `IgnoreChildColliders` verallgemeinern auf Parts, die im Kachelbereich am Zielteil
   kleben). Kinder des Decals aus alten Saves weiter unterstützen.
5. **Globaler Knopf (Punkt 5):** Editor-AppLauncher-Knopf (eigenes 38×38-Icon, per Skript erzeugt, Sechseck-Stil) öffnet ein
   kleines Fenster: Muster, Kachelart, Kachelgrösse, "Auf alle Hitzeschilde anwenden". Einzelne Decals bleiben einzeln
   einstellbar. Mit B9: Subtype über B9 umschalten (Reflection auf `ModuleB9PartSwitch`, Methode `SwitchSubtype(string)`
   o. ä. in `GameData/B9PartSwitch/Plugins/B9PartSwitch.dll` nachsehen), sonst setzt B9 beim Laden zurück. Undo-fähig:
   `GameEvents.onEditorShipModified` feuern.
6. **Glühen gelber (Punkt 8):** Farbe von `_TemperatureColor`/Emission der Kacheln in Richtung Gelb-Orange verschieben,
   stärker als bisher (Rampe dunkelrot → orange → gelb → gelbweiss), über Config-Werte einstellbar.
7. **Kleine Flaps (Punkt 6):** "Über Teile hinweg" belegt auch Teile mit `ModuleControlSurface`/`ModuleAeroSurface`, die an
   IRGENDEINEM Zielteil kleben (nicht nur an Flügeln), als eigener Körper (SeparateOwner). ~~Fahrwerk, Triebwerke,
   Lufteinlässe: werden nicht gekachelt~~ (ersetzt durch A7a, Nutzer-Zusatz: "ja ich möchte lufteinlässe ebenfalls drüber
   gekachelt. und vorallem fahrwerke, dann ist es mir egal, wenn das rad durch das hitzeschild durchgeht. letztes mal ist es
   mir explodiert"). Triebwerke bleiben ungekachelt.
7a. **Lufteinlässe und Fahrwerke (Nutzer-Zusatz, ersetzt A7 Satz 2):**
   1. Lufteinlässe (`ModuleResourceIntake`), die per Surface-Attach an einem Zielteil kleben (auch Symmetrie), sind mit
      "Über Teile hinweg" ZIELTEILE (statische Körper, Kacheln laufen über sie wie über Rumpf). Nachtrag Nutzer ("mach den lufteinlass aber auf"): nur aussen gekachelt, Öffnung frei; Collect() lässt Dreiecke weg mit n·intakeTransform.forward > 0.5 (Stirn/Lippe) oder n·radial < -0.3 (Kanalwand); ohne Ansaug-Transform keine Kacheln.
      Die Ansaugfunktion bleibt unverändert.
   2. Fahrwerke (`ModuleWheelBase`/`ModuleWheelDeployment`/`ModuleWheelSuspension`, auch Bay-Varianten) sind nie Ziel: keine
      Kacheln auf dem Fahrwerk-Mesh, die Kacheln laufen auf dem Rumpf durch (das Rad darf beim Ausfahren durch die Kacheln).
      (a) Collider von Fahrwerk (inkl. WheelCollider) und Kacheln kollidieren nie (`Physics.IgnoreCollision` für alle Paare,
      auch nach Rebuild, Abkoppeln, Andocken). Kachel-Collider dürfen den Boden berühren, nur nicht das eigene Fahrwerk.
      (b) Thermik: Parts im Kachelbereich (Fahrwerk, Radial-Decoupler aus A4 u. a., die keine Zielteile sind) bekommen dieselbe
      Wärmefluss-Reduktion wie ihr Zielteil, aus demselben Ablator-Vorrat.
   3. Damit gilt: Fahrwerk nie Ziel, Lufteinlass Ziel (bei spanParts), Decoupler nie Ziel.
   4. Nichts im Modul hängt von ReStock ab (Nutzer: "zudem muss es auch auf nicht restock laufen"): Teile und Steuerflächen
      werden über Module und Config-Felder erkannt, nicht über Transform- oder Mesh-Namen; das Placeholder-Modell ist eigenes.
      CockpitCover.cfg darf Ebenen-Sätze je Modellsatz haben (`ABLATORDECAL_COVER` ohne Suffix = Stock, auch ohne
      ModuleManager; `ABLATORDECAL_COVER:NEEDS[ReStock]` danach, gewinnt mit ReStock).
8. **Cockpit-Abdeckung (Punkt 9), Modulseite:** Nur für die spitzen Cockpits `Mark1Cockpit` und `mk2Cockpit_Standard`
   (Namen mit Punkt/Unterstrich beachten). Datei `GameData/AblatorDecals/CockpitCover.cfg` mit Knoten
   `ABLATORDECAL_COVER { part = …; PLANE { normal = x,y,z; point = x,y,z } … }` in Part-Raum (die Ebenen liefert der
   Geometrie-Agent). Ist das Decal auf so einem Cockpit, gibt es im Menü "Fläche" einen Schalter "Cockpit-Form" (Standard an,
   nur bei diesen Teilen sichtbar): dann ClipPlanes setzen (in Decal-Raum umrechnen), Width/Height auf Teilgrösse, MaxWrap 180,
   nur dieses Teil als Ziel.
10. **Fahrwerk gekachelt aussehen lassen (Nutzer):** echte Kacheln auf der Aussenseite des EINGEFAHRENEN Fahrwerks, die mit
   den beweglichen Teilen mitfahren (wie Steuerflächen-Körper), keine Texturlösung. Nur Fahrwerke (`ModuleWheelBase`) an
   einem Zielteil im Footprint. Eingefahrene Pose kurz aus der Animation von `ModuleWheelDeployment` samplen, Dreiecke lesen,
   Pose exakt wiederherstellen; ohne Deployment aktuelle Pose. Behalten: Normale wie Rumpfnormale am nächsten Rumpfpunkt
   (dot > 0.6), Abstand -3 cm bis +8 cm. Jede MeshFilter-Transform-Gruppe = eigener Körper (SeparateOwner), Host unter dem
   Transform (Editor und Flug), keine Collider-Boxen, Thermik wie A7a. Fahrwerk bleibt "nie Rumpf-Ziel". Flutstart je Körper
   liefert der Geometrie-Agent.
9. Version 1.2.0 (csproj, KSPAssembly 1.2), Lokalisierung en/de gleich, README/CHANGELOG.

## B. Geometrie (Agent "Geometrie")
1. `ClipPlanes` umsetzen: Flutung bleibt diesseits; Kacheln an der Ebene gerade geschnitten mit Wand und Fase (wie
   StraightBorder). Flutung muss über die Spitze von Mk1/Mk2-Cockpit laufen (Nutzer: "schön über die Spitze"): spitze Nase
   ohne Löcher und ohne Blöcke, Kantenregel aus v7 §4.
2. Ebenen für `Mark1Cockpit` und `mk2Cockpit_Standard` bestimmen (ReStock-Modelle aus der Testinstallation, wie in der
   Preview): Abdeckung = Unterseite, Seiten bis knapp unter die Fensterunterkante, Spitze ganz bedeckt (Nutzer-Screenshots
   `images\5.webp`, `images\6.webp`: rote Linie). Eine bis drei Ebenen je Cockpit, als Part-Raum-Werte für CockpitCover.cfg.
   Preview-Szene mit Fenstern im Bild (Seite, schräg von oben, Spitze nah).
3. `Occupied` umsetzen: keine neue Kachel über einem Occupied-Punkt; am Übergang sauber (Teilkachel/Schnitt wenn einfach,
   sonst Kachel weglassen).
4. Performance übergrosses Decal (Punkt 1): Decal 20 × 20 m (und 50 × 50) auf kleinem Teil (z. B. 1.25-m-Nase, Mk1-Cockpit)
   messen: Bauzeit, Speicher, SurfaceIndex-Grösse. Wenn teurer als ein passendes Decal: Footprint/Index auf die Bounds der
   Zielteile begrenzen (Ergebnis identisch).
5. SelfCheck grün, §0/v7-Kennzahlen nicht schlechter, Renders nach `preview/v8/`.
