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
- Bootstrapper: fehlt die Windows-App-SDK-Runtime, lädt das Setup
  `windowsappruntimeinstall-x64.exe` nach und installiert still (`--quiet --force`);
  ohne Internet bricht es mit Hinweis ab (online only, D011)
- Deinstall: entfernt Key + Programmdateien; `%APPDATA%\birdy-creditStatus\`
  (Cache) **bleibt**; fremde Credential-Dateien werden nie angefasst

## Version pflegen

- `MyAppVersion` oben in `Setup.iss` **und** `<Version>` in
  `src/BirdyCreditStatus/BirdyCreditStatus.csproj` mittziehen (Installer-Eintrag
  bzw. Laufzeit-Anzeige im Popup-Footer via `AppVersion.Format`)
- CI-Release: Actions → „Release Setup" → Version eingeben — überschreibt per
  `/DMyAppVersion`, legt Tag `vX.Y.Z` + Release mit
  `birdy-creditStatus-Setup-X.Y.Z.exe` an (manueller Trigger, nur nach Major-Update)
- `WinAppSdkUrl` mitziehen, wenn `Microsoft.WindowsAppSDK` (NuGet) ein neues
  Major/Minor bekommt (Runtime-Linie 2.5 ↔ SDK 2.5.1)
