; Aeroway — Windows installer (Inno Setup 6)
; Build: ..\build-installer.ps1

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\build\staging"
#endif

#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#ifndef Configuration
  #define Configuration "Release"
#endif

#ifnstr Configuration "Release"
  #error Installer must not be built from this configuration
#endif

[Setup]
AppId={{8F4E2A91-3C7D-4B6E-9F12-0A1B2C3D4E5F}
AppName=Aeroway
AppVersion={#AppVersion}
AppVerName=Aeroway {#AppVersion}
AppPublisher=Niko
AppPublisherURL=https://github.com/RaccoonLaptop/Aeroway
AppSupportURL=https://github.com/RaccoonLaptop/Aeroway
AppUpdatesURL=https://github.com/RaccoonLaptop/Aeroway/releases
DefaultDirName={localappdata}\Aeroway
UsePreviousAppDir=no
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=Aeroway-Setup
SetupIconFile=..\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardImageFile=..\Assets\installer-wizard.bmp
WizardSmallImageFile=..\Assets\installer-small.bmp
WizardImageBackColor=$050B1B
WizardImageStretch=no
UninstallDisplayName=Aeroway
UninstallDisplayIcon={app}\Aeroway.exe
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительно:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{autoprograms}\Zapret UI.lnk"
Type: files; Name: "{autodesktop}\Zapret UI.lnk"

[Icons]
Name: "{autoprograms}\Aeroway"; Filename: "{app}\Aeroway.exe"; IconFilename: "{app}\Aeroway.exe"; Comment: "Aeroway — обход блокировок Discord, YouTube и др."
Name: "{autodesktop}\Aeroway"; Filename: "{app}\Aeroway.exe"; IconFilename: "{app}\Aeroway.exe"; Tasks: desktopicon; Comment: "Aeroway"

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Scripts\bootstrap-zapret.ps1"" -TargetDir ""{app}\zapret"""; StatusMsg: "Скачивание zapret (Flowseal)..."; Flags: waituntilterminated
Filename: "{app}\Aeroway.exe"; Description: "Запустить Aeroway"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\zapret"
Type: filesandordirs; Name: "{app}\Assets"
Type: filesandordirs; Name: "{app}\Scripts"
Type: files; Name: "{app}\settings.json"

[Code]
function StopZapretBeforeUninstall(): Boolean;
var
  ResultCode: Integer;
  ScriptPath: String;
begin
  ScriptPath := ExpandConstant('{app}\Scripts\stop-zapret-components.ps1');
  if not FileExists(ScriptPath) then
  begin
    Result := True;
    Exit;
  end;
  Result := ShellExec('runas',
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + ScriptPath + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDir: String;
begin
  if CurUninstallStep = usUninstall then
    StopZapretBeforeUninstall();

  if CurUninstallStep = usPostUninstall then
  begin
    AppDir := ExpandConstant('{app}');
    if DirExists(AppDir) then
      DelTree(AppDir, True, True, True);
  end;
end;
