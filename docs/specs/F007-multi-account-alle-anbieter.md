# F007 — Multi-Account für alle Anbieter (Spec, Issue #24)

## Problem Statement

Nicht jeder Nutzer hat alle drei Logins (Claude, Codex, OpenCode Go).
Heute sind Claude und OpenCode Go fixe Einzelkarten (Fixpfade per D005/D006):
ohne Login zeigen sie dauerhaft `n/a`-Rauschen. Multi-Account (Dialog,
Umbenennen/Entfernen je Karte, Cache je Konto) existiert nur für Codex (F006).
Gewünscht: 1:1-gleiches Verhalten für alle drei Anbieter; ohne konfiguriertes
Konto gibt es keine Karte.

## Solution

Einheitliche Kontenliste `accounts.json` mit Feld `Provider`
(`claude` / `codex` / `opencode-go`); ein Dialog (Anbieter + Name + Datei);
Karten je Konto (Titel = Kontoname, ggf. Anbieter-Badge) in Blöcken
Claude → Codex → OpenCode Go, innen Anlege-Reihenfolge; kein Konto = keine Karte;
null Konten = leerer Zustand mit Hinzufügen-Button. Auth-Parser je Anbieter
unverändert, nur Pfad frei wählbar. Migration übernimmt vorhandene
Default-Logins und deutet feldlose Alt-Einträge als `codex`.

## User Stories

1. As Single-User, I want mehrere Claude-Konten (Name + `.credentials.json`-Datei) als eigene Karten, so that ich Haupt- und Zweitlogin nebeneinander sehe.
2. As Single-User, I want mehrere OpenCode-Go-Konten (Name + JSON mit `opencode-go.key`) als eigene Karten, so that ich mehrere Keys getrennt verfolgen kann.
3. As Single-User, I want dass Anbieter ohne Konto keine Karte zeigen (auch kein `n/a`), so that das Popup nur meine Realität abbildet.
4. As Single-User, I want Konten einheitlich hinzufügen/umbenennen/entfernen (ein Dialog, je Karte), so that ich kein zweites Bedienmodell lernen muss.
5. As Bestandnutzer, I want dass meine Codex-Liste plus Default-Logins (Codex/Claude/Go) automatisch zu Konten werden, so that nach dem Update nichts neu anzulegen ist.

## Implementation Decisions

- Ein Store: `accounts.json` als Liste `{Provider, Name, AuthFilePath}`; feldlose Einträge = `codex` (D012).
- Auth-Formate unverändert (Claude: `claudeAiOauth`; Codex: `tokens.*`; Go: `opencode-go.key`), Pfad frei wählbar; keine Key-Texteingabe in v1.
- Namen global eindeutig; alte Reservierten-Liste (`Claude`, `OpenCode Go`) entfällt.
- Provider und Datei nach Anlegen unveränderlich (Dateiwechsel = löschen + neu); nur Name änderbar.
- Cache-Key = Kontoname über alle Anbieter (`SnapshotCache`, `RefreshService`-Dictionary wie F006-T2); Umbenennen wandert mit (`Migrate`), Entfernen löscht Eintrag (`Remove`).
- Abruf weiter `RefreshService.RefreshAllAsync` (Upsert/RemoveAdapter zur Laufzeit), Isolation je Karte, nur Erfolge überschreiben Cache.
- Rotation (Claude/Codex) schreibt isoliert in die jeweilige Konto-Datei; Go bleibt read-only; gleiche Datei für mehrere Konten erlaubt.
- Kartenordnung: Blöcke Claude → Codex → OpenCode Go, innen Anlege-Reihenfolge; leerer Zustand: `Keine Konten konfiguriert – über „Konto hinzufügen" einrichten.` + Button.

## Testing Decisions

(Getestet wird an bestehenden Nähten; neue Fabrik nur Verkabelung.)

- `IAccountStore` (`AccountStore`/`InMemoryAccountStore`): Provider-Runde (Add/Load/Save/Rename/Remove), global eindeutige Namen, feldlos = `codex`, Migration der drei Defaults.
- `AccountValidator`: Name global eindeutig + keine Reservierten mehr; `ValidateAuthFile` je Provider-Format (Claude/Codex/Go) mit deutschen Meldungen analog F006-T3.
- `RefreshService.RefreshAllAsync` (höchste Verhaltensnaht): gemischte Konten mehrerer Anbieter, Erfolg/Fehler/Setup-Isolation je Karte, Cache nur bei Erfolg, Upsert/Remove ohne Neustart.
- `SnapshotCache`: Save/LoadAll/Migrate/Remove mit Kontonamen-Schlüsseln über Anbietergrenzen hinweg.
- Adapter je Konto (`ClaudeQuotaAdapter` mit PfadOverride + Kontoname, `CodexQuotaAdapter` wie F006, `OpenCodeGoQuotaAdapter` mit PfadOverride + Kontoname) per Stub-HTTP: Mapping unverändert, Provider = Kontoname.
- UI-Logik ohne Framework: Kartenliste (Blockreihenfolge, kein `n/a` ohne Konto, Empty-State) über `CardDisplayBuilder`-Ebene.

## Out of Scope

- Manuelle Key-Eingabe per Textfeld; eigener OAuth-Flow in der App.
- Provider- oder Dateiwechsel an bestehendem Konto (nur löschen + neu).
- Rückkehr zum Credential Manager; neue Auth-Formate.
- Alphabetische/anbieterübergreifende Sortierung; Verlauf/Graphen; Polling (D010); Toasts; Installer-Änderung.

## Further Notes

- Bricht D005/D006-Fixpfade zu Default-Startwerten auf (siehe D012).
- Naheliegend, kein Zwang: `CodexAccount` → `Account` (Tripel) umbenennen; `ReservedNames` entfernen.
- F007-ID ist Vorschlag (F006 ist `done`); Spec-Issue erhält Label `spec`.
