# D011 — Installer (Inno, per-user) und Autostart per Registry-Run

## Context

F005 braucht einen einfachen Installer plus Autostart (Standard an). Kandidaten für den Installer: Inno Setup (simple `Setup.exe`), MSIX (deklarativ, StartupTask) und ZIP (kein Installer). Kandidaten für Autostart: Registry (`HKCU\...\Run`), Startup-Ordner-Verknüpfung und (nur MSIX) StartupTask. Grill F005 klärt zudem: Verteilung auf andere Einzel-PCs, Runtime-Versorgung, Update- und Deinstall-Verhalten, Signierung.

## Decision

- Installer: **Inno Setup, per-user ohne Admin** (`%LocalAppData%\Programs\birdy-creditStatus`), **unsigned** (SmartScreen-Warnung in v1 akzeptiert). Ausstattung: Startmenü- und Programme-&-Features-Eintrag, **kein Desktop-Icon**, Autostart-Checkbox (Standard an), „Jetzt starten" (Standard an), laufende Instanz wird beim Setup geschlossen.
- Runtime: **Bootstrapper mit Download** — Inno prüft die Windows-App-SDK-Runtime und lädt sie bei Bedarf nach (kleines Setup, kein Offline-Versprechen).
- Autostart: **Registry `HKCU\...\Run`**, Wahrheit ist der Key. Steuerung an zwei Stellen: Installer-Checkbox plus Tray-Menü-Haken.
- Update: **manuell durch Drüberinstallieren**, kein Update-Check, kein Feed.
- Deinstall: **Autostart-Key und Programmdateien weg, JSON-Cache in `%APPDATA%\birdy-creditStatus\` bleibt**; fremde Credential-Dateien (`~/.claude`, Codex-Datei, Pi-Datei) werden nie angefasst.
- Verteilung: **andere Einzelrechner (Single-User pro Rechner)**, Verteilweg v1 ist die **lokal gebaute Setup-Datei**, kein GitHub-Release-Ablauf. Kein Multi-User, kein Sync.

## Why

Inno ist der kleinste Weg zu „Doppelklick → fertig" für Fremdrechner: kein Store, keine Signaturpflicht, MSIX-Sideload-Hürden entfallen. Per-user vermeidet UAC bei Install und Update. Registry-Run ist eine Zeile statt `.lnk`-Pfadpflege und vom Installer wie aus der App toggelbar. Bootstrapper hält das Setup klein; die Rechner sind eh online. Manuelles Update und Cache-behaltene Deinstall minimieren Scope bei reversiblem Verlust (nächster Abruf baut den Cache wieder auf).

## Consequences

Kein MSIX-Pfad (kein StartupTask, kein Store-Update); ein späterer Wechsel (z. B. signiertes MSIX, Auto-Update, GitHub-Releases, Offline-Setup) ist ein bewusster Neuentscheid und bricht diese Decision auf. Unsigned bedeutet SmartScreen-Warnung pro Fremdrechner. `PROJECT.md` (Single-User) gilt pro Rechner unverändert.

## Amendment 2026-09-20 — GitHub-Release-Pipeline

Verteilung läuft nun doch über GitHub Releases (`.github/workflows/release.yml`):
manueller Trigger nach Major-Update, baut `Setup.exe` per `dotnet publish` +
Inno auf `windows-latest` und hängt `birdy-creditStatus-Setup-X.Y.Z.exe`
(unsigned) ans Release. Version kommt per Workflow-Eingabe (`/DMyAppVersion`);
`Setup.iss` behält `1.0.0` als Fallback für lokale Builds. Kein Auto-Update,
kein Signieren — am Verteilungsmodell (Single-User pro Rechner) ändert sich nichts.
