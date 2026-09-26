; birdy-creditStatus — Inno-Setup-Skript (F005-T2, Spec #16, D011).
;
; Per-user ohne Admin (unsigned): installiert den SELF-CONTAINED
; `dotnet publish`-Output nach %LocalAppData%\Programs\birdy-creditStatus.
; Startmenü- + Programme-&-Features-Eintrag, KEIN Desktop-Icon.
; Autostart per HKCU\...\Run (Wahrheit ist der Key, D011) — derselbe Key,
; den RegistryAutostartStore (T1) liest/schreibt; die Checkbox teilt ihn sich
; mit dem Tray-Haken.
; Bootstrapper: prüft die Windows-App-SDK-Runtime und lädt sie bei Bedarf nach
; (kleines Setup, online only). Self-contained publish => kein .NET-Check nötig.
; Deinstall entfernt Key + Programmdateien, der JSON-Cache in
; %APPDATA%\birdy-creditStatus\ bleibt; fremde Credential-Dateien werden nie angefasst.
; Kein Timer-/Polling-Code (D010), kein Auto-Update, kein Release-Ablauf.
;
; Bauen: siehe installer/README.md. Inno Setup 6.7+ erforderlich.
; Publish-Quelle: installer/publish/BirdyCreditStatus\ (wird NICHT committet).

#if Ver < EncodeVer(6, 7, 0)
  #error Inno Setup 6.7+ erforderlich (Download-API). Installieren via: winget install -e --id JRSoftware.InnoSetup
#endif

#ifndef MyAppVersion
  #define MyAppVersion "1.4.5"
#endif
#define MyAppName "birdy-creditStatus"
#define MyAppExe "BirdyCreditStatus.exe"
#define MyRunValueName "birdy-creditStatus"
#define WinAppSdkUrl "https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-x64.exe"

#if !DirExists(SourcePath + "publish\\BirdyCreditStatus")
  #error Publish fehlt: erst `dotnet publish ... -o installer/publish/BirdyCreditStatus` ausführen (siehe installer/README.md).
#endif

[Setup]
AppId={{6ce0e7fa-33c8-4df3-b5f4-0bde999983ad}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=dist
OutputBaseFilename=Setup
SetupIconFile=..\src\BirdyCreditStatus\Assets\TrayIcon.ico
UninstallDisplayIcon={app}\{#MyAppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Unsigned (D011): SmartScreen-Warnung auf Fremdrechnern ist in v1 akzeptiert.

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
; Autostart-Checkbox (Standard an). checkedonce: frisch = an, drüberinstallieren
; übernimmt die vorherige Wahl. Schreibt denselben Run-Key wie T1.
Name: autostart; Description: "Mit Windows starten"; GroupDescription: "Autostart:"; Flags: checkedonce

[Files]
Source: "publish\BirdyCreditStatus\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
; Extract only for the bootstrap probe; not installed into the application directory.
Source: "Test-WindowsAppRuntime.ps1"; Flags: dontcopy

[Icons]
; Nur Startmenü — bewusst kein Desktop-Icon (D011).
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"

[Registry]
; HKCU-Run-Key ist Wahrheit (D011): Installer-Checkbox und Tray-Haken (T1) teilen ihn.
; uninsdeletevalue: Deinstall entfernt den Key, Cache bleibt (kein UninstallDelete auf AppData).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyRunValueName}"; ValueData: """{app}\{#MyAppExe}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
; Schlussseite „Jetzt starten" (Standard an); bei silent kein Autostart der App.
Filename: "{app}\{#MyAppExe}"; Description: "Jetzt starten"; Flags: nowait postinstall skipifsilent

[Code]
const
  ExeName = '{#MyAppExe}';

function HasWindowsAppRuntime: Boolean;
var
  ResultCode: Integer;
begin
  { Fail closed: exact stable framework identity, X64, healthy status and minimum
    package version. Predicate and synthetic tests live in the extracted script. }
  Result := False;
  ResultCode := -1;
  try
    ExtractTemporaryFile('Test-WindowsAppRuntime.ps1');
    if Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
      ExpandConstant('{tmp}\Test-WindowsAppRuntime.ps1') + '"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      Result := ResultCode = 0;
  except
    Result := False;
  end;
end;

function EnsureWindowsAppRuntime: String;
var
  TmpExe: String;
  ResultCode: Integer;
begin
  Result := '';
  if HasWindowsAppRuntime then
    Exit;
  try
    WizardForm.StatusLabel.Caption := 'Windows-App-SDK-Runtime wird geladen …';
    // Ablage: {tmp} + BaseName (Int64-Rueckgabe = Status; keine Exception = Erfolg).
    DownloadTemporaryFile('{#WinAppSdkUrl}', 'WindowsAppRuntimeInstall.exe', '', nil);
    TmpExe := ExpandConstant('{tmp}\WindowsAppRuntimeInstall.exe');
  except
    Result := 'Die Windows-App-SDK-Runtime konnte nicht geladen werden (online only). ' +
      'Bitte Internet prüfen und Setup erneut starten.';
    Exit;
  end;
  { Dokumentierte stille Installation (Microsoft Learn, „deploy unpackaged apps"):
    --quiet (keine Interaktion), --force (MSIX-Pakete aktualisieren). }
  if (not FileExists(TmpExe)) or
     (not Exec(TmpExe, '--quiet --force', '', SW_SHOW, ewWaitUntilTerminated, ResultCode)) or
     (ResultCode <> 0) or (not HasWindowsAppRuntime) then
    Result := 'Die Windows-App-SDK-Runtime ließ sich nicht installieren. ' +
      'Bitte manuell von https://aka.ms/windowsappsdk installieren und Setup erneut starten.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  { Laufende Instanz schließen (sonst file-in-use beim Drüberinstallieren).
    taskkill trifft nur diesen Prozessnamen; läuft nichts, ist der Code ungleich 0 —
    das ist kein Fehler. Tray-App hält keinen ungespeicherten Zustand (Cache ist JSON). }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im ' + ExeName + ' /t',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  { Runtime-Bootstrapper (online only, D011). }
  Result := EnsureWindowsAppRuntime();
end;
