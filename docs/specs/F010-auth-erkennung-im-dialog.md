# F010 — Auth-Erkennung im Hinzufügen-Dialog (Spec)

## Problem Statement

Im Dialog „Konto hinzufügen" müssen Anbieter und Auth-Datei von Hand gewählt
werden, obwohl beides aus den drei Standarddateien ableitbar ist: Jedes Format
trägt einen eindeutigen Marker (`claudeAiOauth` / `tokens.*` /
`opencode-go.key`). Wer nicht weiß, wo seine Logins liegen, bricht ab oder
wählt falsch (Fehlermeldung statt Erfolg).

## Solution

Ein Knopf „Automatisch erkennen" im Hinzufügen-Dialog scannt nur die drei
Default-Pfade (Codex `~/.codex/auth.json`, Claude
`~/.claude/.credentials.json`, Pi-`~/.pi/agent/auth.json`), verifiziert je
Datei das Format per Marker und belegt bei Treffer Anbieter + Pfad vor:
0 Treffer → Hinweis im Dialog, 1 Treffer → direkt vorbelegen, mehrere Treffer
→ Auswahlliste. Bereits konfigurierte Pfade werden übersprungen bzw. als
eingerichtet markiert; OpenCode Go nur bei vorhandenem Key. Name + Validierung
bleiben wie heute; Startverhalten und Migration unverändert.

## User Stories

1. As Single-User, I want dass der Dialog meine vorhandenen Standard-Logins per Knopf erkennt und vorbelegt, so that ich kein Dateiwissen brauche.
2. As Single-User, I want bei mehreren gefundenen Logins eine Auswahlliste, so that ich gezielt eines zum Einrichten wähle.
3. As Single-User, I want dass bereits eingerichtete Pfade nicht erneut vorgeschlagen werden, so that ich keine Doppelkonten anlege.

## Implementation Decisions

- Neue Core-Naht `DefaultAuthDiscovery.Detect(existingPaths)`: scannt nur die
  drei Defaults, Format-Check je Marker, Go nur mit Key, filtert konfigurierte
  Pfade; gibt Kandidaten `{Provider, DisplayName, Path}` zurück (UI-frei).
- `AccountValidator.ValidateAuthFile` bleibt Validierungs-Naht, unverändert.
- UI nur Verkabelung im Hinzufügen-Dialog (Knopf, Trefferliste/Vorbelegung,
  Kein-Treffer-Hinweis); kein Ordner-Scan, keine Start-Automatik.
- Fehler/Fehlendes je Datei = still überspringen (nie Throw, D003-Geist).

## Testing Decisions

- `DefaultAuthDiscovery` (höchste Naht): je Provider ein Treffer, Marker-Mismatch
  fällt raus, Go ohne Key fällt raus, konfigurierte Pfade gefiltert, fehlende
  Dateien = leere Liste.
- `dotnet build` 0 Warnungen / 0 Fehler; Suite grün.

## Out of Scope

- Ordner-Scans (z. B. alle `*-auth.json` für Zweitlogins); automatisches Anlegen
  beim Start; Provider-/Dateiwechsel an bestehenden Konten; manuelle
  Key-Eingabe.

## Further Notes

- Gegrillt: nur Defaults (kein Ordner-Scan), Startverhalten unverändert,
  Mehrfachtreffer als Liste statt Auto-Anlegen.
- Spec-Issue erhält Label `spec`.
