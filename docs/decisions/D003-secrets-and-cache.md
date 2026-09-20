# D003 — Secrets in Credential Manager, Cache als lokales JSON

## Context

Keys und Tokens müssen privat bleiben, Snapshots und Einstellungen (Intervall, Autostart) müssen einen Neustart überleben. Zur Wahl standen eine lokale Datenbank (zum Beispiel SQLite) gegenüber einfachem JSON, sowie Settings-Datei gegenüber Credential Manager für Secrets.

## Decision

Secrets ausschließlich im Windows Credential Manager, gelesen und geschrieben nur über ein `SecretStore`-Modul. Snapshots, Zeitstempel und Einstellungen als JSON in `%APPDATA%/birdy-creditStatus/`. Kein SQLite in v1.

## Why

Klare Besitzgrenze: nur ein Modul berührt Secrets, nichts davon landet in JSON oder Logs. JSON reicht für einen Snapshot-Cache und wenige Settings und hält Lokalität hoch. WenigerCeremony als eine DB für diesen Umfang.

## Consequences

Einfach zu verstehen und zu sichern. Bei wachsendem Verlauf oder Abfragen wäre ein späterer Wechsel auf SQLite ein bewusster Neuentscheid. Verlust des JSON-Cache ist unkritisch: nächster Abruf baut ihn wieder auf.
