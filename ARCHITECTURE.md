# Architecture

## System Overview

Eine einzige Windows-11-Tray-App (x64, `.NET WinUI 3`, glassy Popup). Beim Öffnen des Popups (plus Refresh-Button) fragt die App drei Provider-Adapter ab, normiert die Stände auf ein stabiles Snapshot-Modell und legt sie lokal ab. Das Popup rendert ausschließlich diesen Cache: Klick aufs Tray-Icon zeigt Karten je Anbieter und Fenster mit Rest-Tokens, Reset und Zeitstempel, plus Refresh und Einstellungen.

Siehe `PROJECT.md` für den Projektvertrag.

## Components

### TrayHost

**Responsibility:** Prozess-Root. Tray-Icon mit Zustand, Rechtsklick-Menü (Aktualisieren, Beenden), Single-Instance und Autostart-Registrierung.

### PopupShell

**Responsibility:** Glassy Popup unten rechts (Mica/Acrylic). Karten je Anbieter und nativem Fenster, Zeitstempel der letzten Aktualisierung, Refresh-Button und Einstellungen (Polling-Intervall, Autostart, Keys).

### Abruf (kein PollEngine — siehe D010)

**Responsibility:** Kein Hintergrund-Timer: Abruf ausschließlich beim Öffnen des Popups (Cache zuerst, dann live) sowie per Refresh-Button. Ruft Adapter ausschließlich über das schmale Adapter-Interface auf.

### ProviderAdapters (Claude, Codex, OpenCodeGo)

**Responsibility:** Je Anbieter: Authentifizierung laden, Stand abrufen und auf das normierte `QuotaSnapshot`-Modell abbilden (Anbieter, Fenster, Rest-Tokens, Reset, Zeitstempel). Fehler werden pro Karte als `n/a` sichtbar gemacht und nie als Absturz nach oben gereicht.

### SecretStore

**Responsibility:** Einzige Stelle für den Windows Credential Manager: Keys und Tokens lesen, schreiben und löschen. Kein anderes Modul berührt Secrets.

### SnapshotCache

**Responsibility:** Lokale JSON-Ablage in `%APPDATA%/birdy-creditStatus/`: letzte Snapshots, Zeitstempel und Einstellungen. Unkritisch verlierbar, der nächste Poll baut den Cache wieder auf.

## Boundaries

- Die UI ruft keine Provider direkt auf. Der einzige Pfad lautet `PollEngine → IQuotaAdapter → SnapshotCache → PopupShell`.
- Adapter besitzen alle Provider-Eigenheiten (Quellen, Fenster, Mapping). Core, Planung und UI bleiben davon unberührt.
- Nur `SecretStore` berührt den Credential Manager. Secrets erscheinen nie in JSON, Logs oder UI-Texten.
- Neuer Anbieter bedeutet neuer Adapter; bestehende Module ändern sich nicht.

## Data / Control Flow

- Start: Einstellungen laden, Cache sofort rendern (Ziel unter 5 Sekunden), danach Abruf beim Öffnen.
- Abruf (Öffnen oder Refresh-Button): je Provider `SecretStore → Adapter.fetch → normieren → SnapshotCache schreiben → UI aktualisieren`.
- Einstellungen: Autostart oder Keys ändern → persistieren.
- Fehler: Adapter-Fehler (offline, ungültiger Key) betrifft nur die eigene Karte (`n/a – Key prüfen / offline`).

## External Systems

- Anbieter-Dashboard-Quellen für Claude Code, Codex und OpenCode Go (heute Websites; exakte maschinenlesbare Quellen werden pro Feature-Spec recherchiert und per Prototyp belegt, nicht hier festgelegt).
- Windows Credential Manager für Secrets.
- Dateisystem (`%APPDATA%/birdy-creditStatus/`) für JSON-Cache und Einstellungen.
- Windows-Shell für Tray, Autostart und Installer. Keine Toasts in v1.

## Important Invariants

- Das Popup blockiert nie auf Netzwerk; es zeigt Cache plus Zeitstempel.
- Ein Provider-Fehler stoppt weder andere Provider noch den Prozess.
- Besitz bleibt lokal: Abruf-Logik beim `RefreshService`, Secrets beim `SecretStore`, Provider-Details beim jeweiligen Adapter.
- Eine Instanz; Installer mit Autostart-Option (Standard an).

## Decisions

- `docs/decisions/D001-dotnet-winui-tray.md` — `.NET WinUI 3` als Single-Prozess-Tray-App.
- `docs/decisions/D002-provider-adapter-seam.md` — Provider-Adapter hinter normiertem Snapshot-Modell.
- `docs/decisions/D003-secrets-and-cache.md` — Secrets in Credential Manager, Cache als lokales JSON.
