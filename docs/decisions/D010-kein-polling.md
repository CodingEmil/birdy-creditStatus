# D010 — Kein Hintergrund-Polling (F004 verworfen)

## Context

F004 sah einen Timer-gesteuerten Polling-Prozess vor (5-Minuten-Intervall, Pause,
Backoff). Faktisch aktualisiert die App bei jedem Öffnen: Cache zuerst, dann
Live-Abruf (`ShowCore`), plus manueller Refresh-Button. User-Frage 20.09.:
Wozu noch pollern, wenn eh beim Öffnen aktualisiert wird?

## Decision

- F004 wird ersatzlos verworfen: kein Timer, keine Pause, kein Backoff.
- Einzige Abruf-Pfade: Öffnen des Popups und Refresh-Button.
- Begründung: Quotas ändern sich im Stunden-/Tages-Rhythmus; Polling lieferte
  fast immer identische Werte bei Mehrkosten (Netzwerk, Akku, ungetestete
  Timer-/Pause-Logik als Crash-Fläche). Proaktive Hinweise (Toasts) sind in v1
  ohnehin Non-Goal — nur dafür hätte Polling einen Mehrwert.

## Consequences

- PROJECT.md (Goal, Use Cases, Success Criteria), ARCHITECTURE.md (Übersicht,
  PollEngine→Abruf, Boundaries, Data Flow, TrayHost-Menü) und ROADMAP.md (F004
  → verworfen) angepasst.
- Sollte v1 später Warnungen/Toasts bekommen, F004-Nachfolger neu grillen —
  nicht einfach reaktivieren.
