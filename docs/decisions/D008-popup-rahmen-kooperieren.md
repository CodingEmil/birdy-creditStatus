# D008 — Popup-Rahmen: kooperieren statt strippen (weißer Saum)

## Context

Nach T4/T5 meldete der User einen weißen 1px-Saum ums Popup (Foto-Beleg) plus
mattes Glas bis zum ersten Hineinklicken. Der Saum überlebte alles: Acrylic-/
Mica-Wechsel, `DWMWA_BORDER_COLOR`, WS_BORDER an/aus, `DWMWA_VISIBLE_FRAME_
BORDER_THICKNESS`, XAML-Hairline raus, soliden Hintergrund, `DWMWCP_DONOTROUND`,
`WS_EX_WINDOWEDGE`-Strip und `DWMWA_SYSTEMBACKDROP_TYPE` — pixelverifiziert per
headless Visual-Loop (Zweitstart triggert Popup, Screenshot, synthetischer Klick).

## Decision

- Kein rohes Strippen von Stil-Bits (`WS_CAPTION/BORDER/...`) mehr: AppWindow
  verwaltet sie selbst und stellt gestrippte Bits kommentarlos wieder her.
  Belegt per Win32-Logging (SetWindowLong: err=0, aber Vorwerte flip-floppen)
  und Live-Probe (`GWL_STYLE=0x14CF0000` = volle Caption trotz Strip).
- Stattdessen: `OverlappedPresenter.SetBorderAndTitleBar(true, false)` — echter
  1px-Rahmen ohne Titelleiste (keine Caption-Buttons, kein Taskleisten-Eintrag
  dank stabilem `WS_EX_TOOLWINDOW`) — plus `DWMWA_BORDER_COLOR=0x2B2B2B`.
  Der Rahmen wird damit unsichtbar ums Dark-Popup. Pixel-Beleg: 0 weiße Pixel.
- `DWMWA_VISIBLE_FRAME_BORDER_THICKNESS` wurde mit `E_INVALIDARG` abgelehnt
  (gilt nicht für XAML-Backdrop-Fenster) und wieder entfernt.

## Why

Jede „weißer Rahmen = DWM-Border"-Hypothese wurde einzeln falsifiziert; was
blieb, war der DWM-Default-Rahmen eines Caption-Fensters, den AppWindow entgegen
unserer Win32-Eingriffe beibehielt. Wer den Rahmen farbig kontrollieren will,
muss DWM einen echten Rahmen zum Färben geben, statt ihn zu entfernen.

## Consequences

- Saum weg, Ecken weiter rund (`DWMWCP_ROUND`), Glas ab Öffnen (Blur-Beleg im
  Padding über hellem Hintergrund), kein Taskleisten-Eintrag, X schließt weiter
  nur, Beenden per Rechtsklick.
- Das Popup aktiviert sich nur per echtem Mausklick (Vordergrund-Nachweis);
  programmatisches `Activate()` aus dem Hintergrund greift nicht (Foreground-
  Lock, z. B. bei Vollbild-Spiel). Darum kein Fokus-Stehlen: Glas kommt ggf.
  mit der ersten Interaktion voll zur Geltung — dokumentiertes Verhalten,
  kein Bug.
- Hinfällig: `StripCaption`-Methode (entfernt), XAML-Shell-Hairline (entfernt),
  `ThemeShadow`-Pläne bleiben bei D007.
