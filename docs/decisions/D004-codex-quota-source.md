# D004 — Codex-Quota per HTTP-Adapter mit benutzergewählter Auth-Datei

## Context

F002 braucht die Codex-Stände als zweite Karte im selben Popup. Kandidaten für Quelle und Auth:
(a) vorhandenes Codex-CLI-Login automatisch lesen (wie F001/Claude),
(b) CLI bzw. App-Server als Subprozess treiben (`getAccountRateLimits`),
(c) manuell hinterlegter OpenAI-API-Key (PROJECT.md-Muster),
(d) vom Benutzer gelieferte/gewählte Auth-Datei im Codex-OAuth-Format.
Recherche (`docs/research/F002-codex-quota-source.md`, Primärquelle `openai/codex`):
dediziertes `GET …/api/codex/usage` ohne Inference-Nebeneffekt; Fenster primär (5 h) und
sekundär (Woche) mit `used_percent`; API-Key-Auth trägt kein Plan-Kontingent
(`uses_codex_backend() == false` → Server verweigert das Lesen).

## Decision

Direkter HTTP-Adapter hinter `IQuotaAdapter` (D002-Naht), Fenster **5 Stunden** und **Woche**,
Credentials aus einer vom Benutzer gelieferten/gewählten Datei im `auth.json`-Format
(kein hartverdrahteter CLI-Pfad). `resets_at` wird relativ angezeigt — auf der Codex-Karte
neu und als Retrofit auf der Claude-Karte.

## Why

Nebeneffekt-frei und an Naht 1/2 testbar, symmetrisch zu F001; Dateiwahl auf ausdrücklichen
Nutzerwunsch (Kontrolle über die Credential-Quelle); API-Key ist technisch ungeeignet,
Subprozess hätte Overhead und Seiteneffekte (Session/History).

## Consequences

`SnapshotCache` wird Multi-Anbieter (ein Eintrag je Provider mit eigenem Zeitstempel);
`QuotaWindow` wächst um den Reset-Zeitpunkt; F002 enthält den Claude-`resets_at`-Retrofit.
Request-Header und Token-Rotation sind per Prototyp gegen CLI 0.155.1 zu verifizieren.
