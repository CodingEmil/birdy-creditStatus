# Bugfix-Status nach Audit v1.4.4

Auftrag: die im [Audit](2026-09-26-v1.4.4-bug-audit.md) festgehaltenen Fehler beheben.
Basis: `fb09d76` (Audit auf dem veröffentlichten 1.4.4-Stand).
**Alle acht Befunde sind im Code bearbeitet; UI-/Neuinstallations-Abnahme bleibt offen.**
Kein neuer Release, kein neues Tag und keine Installation auf dem Benutzerrechner.
Das öffentliche Release `v1.4.4` bleibt unverändert. Lokale Build-Artefakte enthalten
bereits die Fixes, tragen bis zur nächsten Release-Freigabe aber weiterhin 1.4.4.

## Umsetzung pro Ticket

| Befunde | Ticket / Commit | Änderung und Nachweis |
| --- | --- | --- |
| B01–B04 | [#36](https://github.com/CodingEmil/birdy-creditStatus/issues/36), `d4fbe35` | JSON-Feldtypen prüfen; Null-Konten filtern; CRUD-Erfolg nur nach Speicherung; Remove-UI respektiert Fehlschlag; atomisches Schreiben; Cache-I/O ist verlierbar statt fatal. |
| B05 | [#37](https://github.com/CodingEmil/birdy-creditStatus/issues/37), `320a4aa` | Registrierung + Abrufgeneration pro Konto; alte Antworten nach Entfernen/Ersetzen/neu gestartetem Abruf nicht mehr übernehmen. Verspätete Aufrufer erhalten aktuelle akzeptierte Ergebnisse. |
| B06 | [#38](https://github.com/CodingEmil/birdy-creditStatus/issues/38), `57db8f5` | Codex und Claude koordinieren Rotation pro normalisiertem Auth-Dateipfad, laden nach Sperrerwerb erneut und verwenden fremde Rotation wieder. |
| B07 | [#39](https://github.com/CodingEmil/birdy-creditStatus/issues/39), `acbe507` | Dynamische Menüs tragen `Account`, statische Menüs eine Provider-ID. `AccountMenuTarget` löst beides getrennt auf; kein stiller Fallback auf ein anderes Konto. |
| B08 | [#40](https://github.com/CodingEmil/birdy-creditStatus/issues/40), `da34de7` | Getestete PowerShell-Paketprüfung, temporär vom Setup ausgeführt; exakte stabile Familie, Publisher, X64, Paketstatus und Mindestversion. Abfrage-/Prozessfehler gelten nie als Nachweis. |

## Tests und Builds

- **204 Core-Tests grün** (vorher 177); neue Regressionstests zuerst gegen das
  fehlerhafte Verhalten ausgeführt, danach nach dem jeweiligen Fix erneut geprüft.
- **19 Installer-Prüfungen grün**: synthetische Paketlisten sowie reale
  Script-Eintritts-/Exit-Code-Pfade mit ersetzt arbeitender Paketabfrage.
- **Original-Audit-Repro: 9/9 grün**, zuvor 1 Kontrollfall grün / 8 Fehler.
- **WinUI-Release-Build: 0 Fehler / 0 Warnungen.**
- Self-contained Publish und Inno-Setup erfolgreich erstellt.
- Produktions-Paketprobe auf diesem Rechner read-only erfolgreich (vorhandenes
  X64-Paket 2.5.1.0); nichts installiert, deinstalliert oder geändert.
- Der Release-Workflow führt die Core- und Installer-Tests künftig **vor** Publish
  aus. Diese Änderung wurde nicht durch ein neues Release ausgelöst.

Reproduktion aus dem Repository-Root:

```powershell
dotnet test tests/BirdyCreditStatus.Core.Tests -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File installer/tests/Test-WindowsAppRuntime.Tests.ps1
dotnet run --project docs/reviews/repro-1.4.4/Repro.csproj -c Release
dotnet build src/BirdyCreditStatus/BirdyCreditStatus.csproj -c Release -r win-x64
```

Alle Credential-/Kontendateien der Tests sind synthetisch und isoliert. Es wurden
keine echten Konten oder Token-Rotationen zur Verifikation benutzt.

## Standards

- Bestehende öffentliche Nähte bleiben die hauptsächlichen Testflächen:
  `AccountValidator`, `AccountStore`, `SnapshotCache`, `RefreshService`, Adapter.
- `AtomicFile` konzentriert temporäre Datei, Ersetzen und Cleanup für Konten/Cache;
  die bisherige Datei wird nicht mehr direkt vor erfolgreichem Schreiben geleert.
- Für B05 wurde pro Konto versioniert statt den gesamten Refresh zu serialisieren:
  kleine unveränderte Aufrufoberfläche, unabhängige Netzwerkanfragen bleiben parallel.
- Für B06 liegt die Koordinationsidentität beim Credential-Store statt als
  Datei-/Provider-Sonderwissen im Popup. Standard-Implementierungen koordinieren
  je Store-Objekt, Dateistores über den normalisierten Pfad. Die Lease ist
  abbrechbar und idempotent freigebbar; unterschiedliche Dateien blockieren sich nicht.
- `AccountMenuTarget` extrahiert die bisherige UI-Auflösung in eine reine Testnaht;
  die Typunterscheidung verhindert den Namenskonflikt ohne neue reservierte Namen.
- Der Installer führt denselben Paket-Matcher aus, den die Tests prüfen; keine
  parallele Kopie der Versions-/Architektur-Logik im Inno-Code.

## Spec

Die Fixes stellen bestehende Verträge aus F007/D012, D003 und F005/D011 wieder her,
ohne neue Nutzerfunktionen, Polling oder Änderungen an Auth-Formaten einzuführen.

**Präzisierung zu B08:** Der tatsächliche SDK-2.5.1-Framework-Name lautet
`Microsoft.WindowsAppRuntime.2`, nicht `Microsoft.WindowsAppRuntime.2.5`.
Geprüft wird `Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe`, X64, `Ok`, mindestens
`2.5.1.0`. Kompatible spätere 2.x-Minors sind zulässig. Quelle: installierte NuGet-
Primärmetadaten `Microsoft.WindowsAppSDK.Runtime/2.5.1/include/WindowsAppSDK-VersionInfo.cs`
sowie `Microsoft.WindowsAppSDK.Foundation/2.3.12/include/MddBootstrap.h`
(Minor wird beim Bootstrap ab 2.x ignoriert). Diese Fakten stehen auch in
`installer/README.md` und der Produktionsprobe.

## Noch manuell abzunehmen / Grenzen

1. Im Popup zwei Konten `Arbeit`, `codex` (analog `claude`, `opencode-go`) anlegen:
   Menü der zweiten Karte muss genau diese Karte umbenennen/entfernen. Auch nach
   Entfernen der ersten Karte muss die verbleibende statische Karte richtig zielen.
2. Kontenliste schreibschützen und Hinzufügen/Umbenennen/Entfernen versuchen:
   verständliche Rückmeldung, kein Wechsel der laufenden Konto-/Adapter-Daten.
3. Öffnen/Refresh rasch hintereinander sowie Entfernen während eines langsamen
   Abrufs: keine wiederauferstandenen Karten oder rückwärts springenden Stände.
4. Setup auf frischem Windows 11 x64 mit nur alter 1.x-Runtime: passende Runtime
   wird nachgeladen; anschließend App-Start prüfen. Ebenso vorhandene passende
   Runtime und Offline-/Abfragefehler im echten Installer abnehmen.

Die automatisierten Tests sichern Rechenkern und Auflösung, ersetzen diese
WinUI-/Installations-Klickpfade aber nicht. Die Auth-Koordination gilt für die
App-Instanz, **nicht für konkurrierende externe CLI-Prozesse oder Dateisystem-Aliase
über Symlinks/Hardlinks**. Fremde CLI-Dateiformate und deren Schreibverhalten wurden
nicht geändert. Cache-Daten bleiben absichtlich verlierbar bei I/O-Ausfällen.
