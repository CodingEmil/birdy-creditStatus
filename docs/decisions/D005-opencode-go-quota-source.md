# D005 — OpenCode-Go-Quota per HTTP-Adapter mit Pi-Harness-Key

## Context

F003 braucht die OpenCode-Go-Stände als dritte Karte im selben Popup.
Kandidaten für Quelle und Auth:
(a) manuell hinterlegter API-Key im Credential Manager (PROJECT.md/D003-Muster),
(b) Pi-Harness-Datei `%USERPROFILE%/.pi/agent/auth.json`, Feld `opencode-go.key`,
lesend (Anbieter-Typ `api_key`, keine Rotation),
(c) benutzerwählte Auth-Datei wie Codex (D004-Muster).
Recherche (Primärquelle `birdy-pi/extensions/statusline/index.ts`, keine Secrets):
`GET https://opencode.ai/zen/go/v1/usage` mit Bearer-Key; Payload
`usage.{rolling,weekly,monthly}` je `{status, percent (genutzt), resetsAt (ISO)}`;
`status: ok` und `rate-limited` zählen als Wert. Grill F003 klärt:
drei Fenster (Rolling/Woche/Monat), Prozent immer übrig wie Claude/Codex,
dritte Karte unten, strikt manuell (kein Timer — F004).

## Decision

Direkter HTTP-Adapter hinter `IQuotaAdapter` (D002-Naht), Fenster **Rolling**,
**Woche** und **Monat**, Credentials ausschließlich lesend aus der
Pi-Harness-Datei (b), kein Dateiauswahl-Fallback und kein Credential-Manager
in F003 („erstmal nur opencode-go"). `resetsAt` wird relativ angezeigt,
Prozent als Rest (`100 − percent`, 0..100). Keine Token-Rotation:
401/403 → `n/a – Key prüfen / offline` plus Setup-Hinweis (Key in Pi
hinterlegen), kein Retry, kein Zurückschreiben. Es wird nur das Feld
`opencode-go.key` gelesen; fremde Felder werden ignoriert und nie geloggt.

## Why

Kein File-Pick-UI und kein zweites Secret-Depot in F003; Pi bleibt einzige
Wahrheit für den Go-Key (Single-User, ein Rechner). Read-only hält die Naht
klein (kein `SaveAsync`-Pfad nötig) und symmetrisch zu F001/F002 im
Fehlervertrag (`n/a` pro Karte). API-Key-Rotation existiert technisch nicht.

## Consequences

Fehlt die Pi-Datei oder das Feld (oder JSON kaputt) → Setup-Signal statt
Dateidialog. Spätere Wünsche (manueller Key, Dateiwahl, Credential Manager,
weitere Zen-Varianten außer `opencode-go`) sind bewusste Neuentscheide und
brechen diese Decision auf. Request-Header und Antwortform sind per Prototyp
gegen den echten Key zu verifizieren.
