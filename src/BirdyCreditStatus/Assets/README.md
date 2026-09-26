# Birdy-Icon

Ausgewählt und übernommen: **Variante 05 „Sanft schattiert“** aus der
[Eisvogel-Designstudie](../../../docs/design/eisvogel-varianten/README.md).

Gestaltungswunsch: ein **Eisvogel (Alcedo atthis)**, realistisch in der Form und
zugleich minimalistisch. Kräftiger Kopf, langer gerader Schnabel, kompakter Körper,
angelegter Flügel und kurzer Schwanz bleiben als artspezifische Silhouette erhalten.
Dezente Verläufe, wenige Gefiederlinien und ein kleiner Augenreflex geben der
gewählten Variante mehr Tiefe. Transparenter Hintergrund, keine Schrift oder
Schlagschatten. Die Auswahl ersetzt bewusst das frühere reine Flat-Design.

- Kopf / Rücken: Türkis–Petrol-Verlauf `#47CEC9` → `#189BB4` → `#13618E`
- Flügel: Blau-Verlauf `#288FB4` → `#164968`; Schwanz `#245D78`
- Brust: Orange–Kupfer-Verlauf `#FBC782` → `#EEA15B` → `#D57538`
- Schnabel: Blaugrau-Verlauf `#88A5AE` → `#4E7285` → `#2D4B61`
- Rückenstreifen `#5BDFCE`, dezente Licht-/Gefiederakzente `#A0E3D8` / `#6EC9CD`
- Wangenfleck `#D98A48`, helle Kehl-/Halsflecken `#F8EDCF` / `#F6EDD8`
- Auge `#102936` mit Reflex `#F7F2DE`, Füße `#AA6346`

## Dateien und Verwendung

- `BirdyIcon.svg`: editierbare Quelle, 256 × 256.
- `TrayIcon.ico`: RGBA-Frames in 16, 20, 24, 28, 32, 40, 48, 64, 96, 128 und
  256 px für Windows-Skalierungen und Explorer. Jede Größe wird separat aus dem
  SVG mit Supersampling gerendert.
- `BirdyIcon-preview.png`: Vorschau auf hellem/dunklem Hintergrund, einschließlich
  nativer Tray-Größen (bei 100 % Bildzoom beurteilen).

Die ICO-Datei wird für das Tray (`MainWindow.xaml`), die EXE (`ApplicationIcon`),
beide WinUI-Fenster (`AppWindow.SetIcon`) und den Installer (`SetupIconFile`)
verwendet. Startmenü und Deinstallations-Eintrag verweisen auf die EXE.
SVG und Vorschau werden nicht mit der App ausgeliefert.

## Neu erzeugen / prüfen

Aus dem Repository-Root mit Python 3.11+; die Tools sind nur für die
Asset-Bearbeitung nötig, nicht für den .NET-Build. Empfohlen: separates venv.

```text
python -m pip install pillow==12.2.0 resvg-py==0.5.0
python scripts/generate-icon.py
python scripts/generate-icon.py --check
```

`--check` schreibt nichts und vergleicht alle ICO-Frames und die Vorschau mit
frisch gerenderten SVG-Pixeln; geprüft werden auch Größen und Transparenz.

Nach dem Neubauen bzw. Installieren die App neu starten. Eine bereits laufende
Instanz lädt das Icon nicht automatisch neu. Windows kann zusätzlich alte
EXE-/Startmenü-Icons zwischenspeichern.
