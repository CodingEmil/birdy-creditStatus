# Installer (F005-T2)

Baut die verteilbare `Setup.exe` (per-user, ohne Admin, unsigned — D011).

## Voraussetzungen

- Windows 11 x64, .NET 10 SDK (`dotnet --version`)
- Inno Setup 6.7+: `winget install -e --id JRSoftware.InnoSetup`
  (`iscc` liegt danach in `%ProgramFiles(x86)%\Inno Setup 6\`)

## Bauen

Aus dem Repo-Root (PowerShell):

```powershell
# 1. Self-contained publish (braucht danach KEIN .NET auf dem Ziel-PC;
#    nur die Windows-App-SDK-Runtime, die das Setup bei Bedarf nachlädt)
dotnet publish src/BirdyCreditStatus/BirdyCreditStatus.csproj `
  -c Release -r win-x64 --self-contained `
  -o installer/publish/BirdyCreditStatus

# 2. Setup kompilieren
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer/Setup.iss
# => installer/dist/Setup.exe
```

`installer/publish/` und `installer/dist/` sind git-ignoriert (Artefakte, kein Source).

## Was das Setup tut

- Installiert nach `%LocalAppData%\Programs\birdy-creditStatus` (kein Admin nötig)
- Startmenü-Eintrag + Programme-&-Features-Eintrag, **kein** Desktop-Icon
- Autostart-Checkbox (Standard **an**): schreibt `HKCU\...\Run\birdy-creditStatus`
  — derselbe Key wie `RegistryAutostartStore` (T1); Tray-Haken und Checkbox teilen ihn
- Schlussseite „Jetzt starten" (Standard **an**)
- Schließt eine laufende Instanz vor dem Kopieren (`taskkill`, sonst file-in-use)
- Bootstrapper: prüft die stabile Microsoft-Framework-Familie
  `Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe`, Architektur X64, Status `Ok`,
  Version mindestens **2.5.1.0**. Alte 1.x-, x86-, CBS- und Preview-Pakete genügen
  nicht. Kompatible neuere 2.x-Versionen bleiben erlaubt (SDK-Bootstrap-Regel).
  Fehlt die passende Runtime, wird `windowsappruntimeinstall-x64.exe` nachgeladen
  und still installiert (`--quiet --force`); ohne Internet Hinweis (D011).
  Query-/Prozessfehler zählen nie als erfolgreicher Nachweis.
- Deinstall: entfernt Key + Programmdateien; `%APPDATA%\birdy-creditStatus\`
  (Cache) **bleibt**; fremde Credential-Dateien werden nie angefasst

## Version pflegen

- `MyAppVersion` oben in `Setup.iss` **und** `<Version>` in
  `src/BirdyCreditStatus/BirdyCreditStatus.csproj` mittziehen (Installer-Eintrag
  bzw. Laufzeit-Anzeige im Popup-Footer via `AppVersion.Format`)
- CI-Release: Actions → „Release Setup" → Version eingeben — überschreibt per
  `/DMyAppVersion`, legt Tag `vX.Y.Z` + Release mit
  `birdy-creditStatus-Setup-X.Y.Z.exe` an (manueller Trigger, nur nach Major-Update)
- `WinAppSdkUrl` **und** die Prüfung in `Test-WindowsAppRuntime.ps1` bei SDK-Updates
  mitziehen. Maßgeblich: NuGet `Microsoft.WindowsAppSDK.Runtime` →
  `include/WindowsAppSDK-VersionInfo.cs` (Framework-Familie, Publisher und
  `Runtime.Version.DotQuadString`), nicht eine aus der NuGet-Versionsnummer
  geratene Paketidentität.

## Runtime-Prüfung testen

```powershell
# 19 synthetische Fälle, inklusive Exit-Codes bei Query-Fehlern; kein Pester nötig:
powershell -NoProfile -ExecutionPolicy Bypass -File installer/tests/Test-WindowsAppRuntime.Tests.ps1
# Optional: tatsächliche Pakete dieses Rechners nur abfragen (0 = vorhanden,
# 1 = keine passende Runtime, 2 = Abfragefehler), nichts installieren:
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File installer/Test-WindowsAppRuntime.ps1
```

Das Setup extrahiert die getestete PS1 als temporäre Bootstrap-Prüfung (`dontcopy`),
statt die Paketlogik als zweite Kopie in Inno zu pflegen. Nach dem Runtime-Download
wird dieselbe Prüfung erneut ausgeführt. Eine vollständige Installation auf einem
frischen Windows-Rechner ist zusätzlich manuell abzunehmen.
