# D006 — Claude-Auth per Datei statt Credential Manager

## Context

PROJECT.md, D003 und Spec #1 sagen Credential Manager für Keys/Tokens.
Gebaut ist `FileClaudeCredentialStore`: liest das vorhandene
Claude-Code-OAuth-Login desselben Users (`~/.claude/.credentials.json`,
Schlüssel `claudeAiOauth`), schreibt nur bei Token-Rotation zurück.
Symmetrisch zu D004 (Codex-Datei) und D005 (Pi-Datei, read-only).
Grill F001 (Runde 1, 2026-09-19): Datei als kanonisch bestätigt.

## Decision

Claude-Auth kommt aus der CLI-Datei, kein Credential Manager in v1.
Fehlende/korrupte Datei → Setup-Signal (`LoginRequired`), nie Throw.
Rotation schreibt in dieselbe Datei zurück.

## Why

Kein zweites Secret-Depot; die CLI bleibt einzige Wahrheit (sonst Drift
zwischen App-Kopie und CLI-Login). Kein App-eigenes Secret, nur
Wiederverwendung des User-Logins. Konsistent mit Codex-/Go-Muster.

## Consequences

PROJECT.md/D003 sind für Claude überholt und werden in `to-spec` (F001
as-built) korrigiert. Kein Credential-Manager-Code für Claude in v1.
Eine spätere Rückkehr zum Credential Manager wäre ein bewusster
Neuentscheid mit Migrationspfad.
