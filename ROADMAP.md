# Roadmap

Based on `PROJECT.md` and `ARCHITECTURE.md`.

| ID | Feature | Outcome | Depends on | Status |
| --- | --- | --- | --- | --- |
| F001 | Tray + Claude manuell | Tray-Icon, glassy Popup mit Claude-Ständen je Fenster, Zeitstempel, Refresh-Button, Key-Ablage im Credential Manager | — | done |
| F002 | Codex-Karte | Codex-Stände erscheinen als eigene Karte im selben Popup, inkl. Fehlerzustand `n/a` | F001 | done |
| F003 | OpenCode-Go-Karte | OpenCode-Go-Stände erscheinen als eigene Karte im selben Popup, inkl. Fehlerzustand `n/a` | F001 | done |
| F004 | Automatisches Polling | Verworfen per D010: Abruf beim Öffnen + Refresh-Button genügt, kein Hintergrund-Timer | F001 | verworfen |
| F005 | Installer + Autostart | Einfacher Installer, optionale Autostart-Registrierung (Standard an) | F001 | done |
| F006 | Multi-Account | Mehrere Konten je Anbieter als eigene Karten (zuerst Codex), Name + Auth-Datei je Konto | F005 | done |
| F007 | Multi-Account alle Anbieter | Claude- und OpenCode-Go-Konten 1:1 wie Codex (Tripel `{Provider, Name, Auth-Datei}`, ein Dialog, keine Karte ohne Konto) | F006 | done |
| F008 | Popup-Größe folgt Inhalt | Fensterhöhe atmet mit der Kartenliste (Cap = min(860, Arbeitshöhe − Rand), kein Minimum, Breite fix 380, darüber Scroll) | F007 | done |
| F009 | App-Update per Rechtsklick | „App aktualisieren …" prüft GitHub Releases gegen laufende Version; bei Fund Download + Rückfrage + Setup-Start, sonst „aktuell"-Hinweis; nur manuell | F008 | done (#30) |
| F010 | Auth-Erkennung im Hinzufügen-Dialog | „Automatisch erkennen"-Knopf scannt die 3 Default-Logins per Format-Marker und belegt Anbieter + Pfad vor (Liste bei mehreren Treffern) | F007 | spec |
