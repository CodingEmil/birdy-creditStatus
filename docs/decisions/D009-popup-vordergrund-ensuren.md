# D009 — Popup: echten Vordergrund erzwingen (Glas ab Öffnen)

## Context

Nach dem Flacker-Fix (Backdrop/Apply je einmal) blieb: Popup öffnet matt/solide,
erst ein Klick hinein macht es glassy. Diagnose per Instrumentierung (Sept 2026):

- WinUI meldet nach `Activate()` `CodeActivated`, doch Win32-`GetForegroundWindow()`
  gehört weiter einer anderen App (`fg==hwnd? False`, pixelverifiziert: solider
  Hintergrund statt Blur).
- `DesktopAcrylicBackdrop` rendert ohne echten Foreground matt — erst echte
  Aktivierung (Klick) bringt volles Glas.

D008 hatte das als „dokumentiertes Verhalten, kein Bug" (Vollbild-Spiel) stehen
lassen. Messung zeigt: Es tritt bei JEDEM Öffnen auf, nicht nur unterm Spiel.

## Decision

- `ShowCore` ruft nach `Activate()` zusätzlich `EnsureForeground()`:
  `ShowWindow(SW_SHOW)` + `SetForegroundWindow`, bei verweigerter Aktivierung mit
  kurzzeitigem `AttachThreadInput` an den Foreground-Thread (dokumentierter
  Trick, sofortiges Detach im `finally`, alles `try/catch`-geschützt).
- Nur beim expliziten Öffnen (Tray-Klick, Zweitstart) — `PreWarm` bleibt
  bewusst hintergründig und stiehlt nie Fokus.
- D008-Consequence „Glas ggf. erst mit erster Interaktion" ist damit aufgehoben;
  Fokus-Stehlen beim Spiel bleibt als bewusst in Kauf genommener Nebeneffekt
  (User-Entscheid: Glas ab Öffnen hat Vorrang).

## Consequences

- Glas ab Öffnen, kein Flackern (kein Backdrop-Neusetzen nötig).
- Klick-daneben-Schließen funktioniert weiter (Maus-Hook + Deactivated nach
  echter Aktivierung).
