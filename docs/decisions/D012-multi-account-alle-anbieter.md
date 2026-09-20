# D012 — Multi-Account für alle Anbieter (einheitliche Kontenliste)

## Context

F006 brachte Multi-Account nur für Codex (`accounts.json` als Liste
`{Name, AuthFilePath}`, Fixpfade für Claude `~/.claude/.credentials.json` (D006)
und OpenCode Go (Pi-`auth.json`, D005)). Nutzer ohne alle drei Logins sehen
trotzdem alle Karten (teils `n/a`). Grill: 1:1-gleiches Verhalten für alle drei
Anbieter, keine Karte ohne konfiguriertes Konto.

## Decision

Eine `accounts.json` mit Feld `Provider` (`claude` / `codex` / `opencode-go`);
feldlose Alt-Einträge gelten als `codex`. Auth-Parser je Anbieter unverändert,
nur der Pfad wird frei wählbar (keine Key-Texteingabe in v1). Kontonamen global
eindeutig (alte Reservierten-Liste entfällt), Provider und Datei nach Anlegen
unveränderlich, ein Dialog mit Anbieter-Dropdown. Karten: Blöcke
Claude → Codex → OpenCode Go, innen Anlege-Reihenfolge; kein Konto = keine Karte,
null Konten = leerer Zustand mit Hinzufügen-Button. Migration übernimmt vorhandene
Default-Logins als `Codex` / `Claude` / `OpenCode Go`. Rotation (Claude/Codex)
schreibt isoliert pro Konto-Datei, Go bleibt read-only.

## Why

Ein Store, ein Dialog, ein Cache-Mechanismus statt drei paralleler
Implementierungen. Fixe Einzelkarten hätten Nutzer ohne Login mit `n/a`-Rauschen
bestraft; die Leere-Regel zeigt nur, was existiert. Globale Eindeutigkeit hält
Validierung und Cache-Key (`Kontoname`) simpel; feldlose Altdaten bleiben lesbar.

## Consequences

D005/D006-Fixpfade sind Default-Startwerte (Migration), nicht mehr die Wahrheit.
`CodexAccount` wächst zum Tripel (Umbenennung in `Account` naheliegend),
`AccountValidator`-Reservierung entfällt zugunsten globaler Eindeutigkeit,
`SnapshotCache`-Keys sind Kontonamen über alle Anbieter.
