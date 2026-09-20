# Domain Context

Geklärt über F001, F002, F005, F006 (`grill-with-docs`, Multi-Account-Grill alle Anbieter).

## Terms

### Fenster

Ein Kontingent-Zeitraum eines Anbieters. Für Claude in F001 genau zwei: **Session** und **Woche**.
Für Codex in F002 genau zwei: **5 Stunden** und **Woche**.
Für OpenCode Go in F003 genau drei: **Rolling**, **Woche** und **Monat**.

### Rest

Anteil des jeweils noch verfügbaren Kontingents in Prozent. Anzeige als „X % übrig" mit Balken (100 % = voll verfügbar, 0 % = aufgebraucht).

### Karte

Darstellung genau eines Kontos im Popup (Titel = Kontoname, ggf. mit kleinem
Anbieter-Badge). Blöcke in fester Reihenfolge Claude → Codex → OpenCode Go,
innerhalb je Anlege-Reihenfolge. Ohne konfiguriertes Konto gibt es keine Karte
(auch kein `n/a`); bei null Konten zeigt das Popup einen leeren Zustand mit
Hinzufügen-Button.

### Konto

Ein Tripel aus Anbieter (`claude` / `codex` / `opencode-go`), frei wählbarem
global eindeutigem Namen und Auth-Datei: wiederverwendeter CLI-Login
(z. B. Zweitlogin per `CODEX_HOME` bzw. Dateikopie), kein eigener OAuth-Flow
in der App. Auth-Format je Anbieter wie bisher (Claude: `.credentials.json` mit
`claudeAiOauth`; Codex: `auth.json` mit `tokens.*`; OpenCode Go: JSON mit
`opencode-go.key`), Pfad frei wählbar, keine manuelle Key-Eingabe in v1.
Verwaltung im Popup über einen einheitlichen Dialog (Anbieter + Name + Datei),
umbenennen je Karte, entfernen je Karte; Provider und Datei sind nach dem
Anlegen unveränderlich (Dateiwechsel = löschen + neu). Entfernen löscht nur
Listen- und Cache-Eintrag, nie die Auth-Datei. Dieselbe Datei darf mehrere
Konten speisen (getrennte Karten, Rotation schreibt isoliert pro Datei).
Migration: feldlose `accounts.json`-Einträge gelten als `codex`; fehlende Liste
übernimmt vorhandene Default-Logins als Konten `Codex` (`~/.codex/auth.json`),
`Claude` (`~/.claude/.credentials.json`) und `OpenCode Go` (Pi-`auth.json` mit Key).

### Popup

Das kleine Fenster unten rechts auf dem Hauptbildschirm (immer Primary, nie Klick-Monitor), geöffnet per Klick auf das Tray-Icon. Einzige UI in v1.

### Snapshot

Der zuletzt abgeholte Stand inklusive Zeitstempel („zuletzt aktualisiert um HH:MM").

### Reset

Zeitpunkt, zu dem sich ein Fenster erneuert. Anzeige relativ („in …") auf jeder Karte.

### Installer

Die verteilbare Setup-Datei (Inno, per-user, unsigned): installiert nach `%LocalAppData%\Programs\birdy-creditStatus`, mit Startmenü- und Programme-&-Features-Eintrag, ohne Desktop-Icon. Die Windows-App-SDK-Runtime wird bei Bedarf per Download nachgezogen. Update manuell durch Drüberinstallieren; eine laufende Instanz wird dabei geschlossen.

### Autostart

Start bei Anmeldung per Registry (`HKCU\...\Run`, Standard an). Wahrheit ist der Registry-Key; Steuerung über Installer-Checkbox (Standard an) plus Tray-Menü-Haken. Deinstall entfernt den Key, der JSON-Cache in `%APPDATA%\birdy-creditStatus\` bleibt.

### Verteilung

„Andere Nutzer" meint andere Einzelrechner (Single-User pro Rechner). Kein Multi-User, kein Sync. Verteilweg in v1: lokal gebaute Setup-Datei weitergeben, kein Release-Ablauf.

## Rules / Invariants

- Prozent bedeutet immer **übrig** (Rest), nie genutzt.
- Fehler zeigen pro Karte `n/a – Key prüfen / offline`, ohne den Rest zu blockieren.
