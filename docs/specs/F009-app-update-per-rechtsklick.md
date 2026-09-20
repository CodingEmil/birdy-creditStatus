# F009 — App-Update per Rechtsklick (Spec, Issue siehe Tracker)

## Problem Statement

„Jetzt aktualisieren" im Tray-Menü lädt die Stände neu (unsichtbar bei
verstecktem Popup) — der Nutzer will dort das **Anwendungs-Update**.
App-Updates laufen heute nur manuell per Drüberinstallieren (kein Auto-Update,
D011). Das Release-Gerüst existiert (GitHub Releases `vX.Y.Z` + Setup-Asset
aus der Release-Pipeline).

## Solution

Menüeintrag wird zu „App aktualisieren …" und prüft `releases/latest` auf
GitHub gegen die laufende Assembly-Version: Fund → Download nach `%TEMP%` +
Rückfrage-Dialog → bei Ja Setup interaktiv starten und App beenden (Setup
schließt die Instanz selbst, „Jetzt starten" bringt zurück). Kein Fund →
„Du bist aktuell (v…)"-Hinweis. Fehler (offline, kein Release, kein Asset) →
sachlicher Hinweis, nie Absturz. Nur manuell (kein Hintergrund, kein
Auto-Download — D010-Geist). Kein Toast (v1-Non-Goal): Hinweise als kleine
Dialoge im geöffneten Popup. Werte-Refresh bleibt im Popup (Button + Öffnen).

## User Stories

1. As Single-User, I want per Rechtsklick zu prüfen, ob eine neue App-Version da ist, so that ich nicht manuell nach Releases schauen muss.
2. As Single-User, I want bei Fund erst zu entscheiden (installieren/später), so that kein stilles Drüberbügeln passiert.
3. As Single-User, I want bei Aktualität oder Fehlern eine kurze Rückmeldung, so that der Klick nicht tot wirkt.

## Implementation Decisions

- Quelle: `GET https://api.github.com/repos/CodingEmil/birdy-creditStatus/releases/latest`
  (Header `User-Agent`, `Accept: application/vnd.github+json`), öffentlich ohne Token.
- Asset-Wahl: `birdy-creditStatus-Setup-{X.Y.Z}.exe` passend zum Tag, sonst erstes `.exe`;
  ohne Asset → Hinweis (kein Fehler-Crash).
- Versionsvergleich: Tag ohne `v`-Präfix per `Version.TryParse`; Release > laufend → Fund.
- Ablauf: Download streamen → Rückfrage → `Process.Start(setup)` + `ShutdownApp`.
  Download-Pfad `%TEMP%\birdy-creditStatus-Setup-{X.Y.Z}.exe`.
- Rechenkern (`UpdateChecker`: Check + Download) in Core, testbar per Stub-HTTP;
  Menü/ Dialoge/Prozess-Start im Code-Behind (nie Throw).
- Ergebnis-Typen: `Available(Version, DownloadUrl)`, `Current`, `Unavailable(Reason)`.

## Testing Decisions

- `UpdateCheckerTests` per Stub-HTTP: neuer/gleicher/älterer Tag, fehlendes Asset,
  404, kaputtes JSON, Download-Bytes — alles ohne Throw.
- `dotnet build` 0/0; Suite grün.
- Live: Release-Workflow für die Test-Version laufen lassen, dann Menü klicken
  („aktuell"-Pfad); Fund-Pfad per Dialog bis „Später" verifizieren.

## Out of Scope

- Auto-Check beim Start; Auto-Download/-Install; stille Updates.
- Delta-Updates; Kanäle (beta); Toasts.
- Werte-Refresh im Tray-Menü (entfällt dort bewusst).

## Further Notes

- Setzt ein vorhandenes GitHub Release voraus (sonst 404 → Hinweis).
- Installer-Verhalten (taskkill, „Jetzt starten") unverändert (F005-T2).
