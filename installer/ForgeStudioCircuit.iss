#define MyAppName "ForgeStudio Circuit"
#define MyAppVersion "0.1.0-dev"
#define MyAppPublisher "StaticTechGroup"
#define MyAppExeName "ForgeStudio.Circuit.App.exe"

[Setup]
AppId={{D3598F3A-775D-49C2-9E35-FSCIRCUIT001}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\StaticTechGroup\ForgeStudio Circuit
DefaultGroupName=StaticTechGroup\ForgeStudio Circuit
DisableProgramGroupPage=yes
OutputDir=..rtifacts\installer
OutputBaseFilename=ForgeStudioCircuitSetup-v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..rtifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ForgeStudio Circuit"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\ForgeStudio Circuit"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch ForgeStudio Circuit"; Flags: nowait postinstall skipifsilent
