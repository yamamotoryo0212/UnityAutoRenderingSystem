[Setup]
AppId={{B3A7F2E1-9C84-4D6B-A1E0-5F3C8D2B7E94}
AppName=Auto Rendering System
AppVersion=1.0.0
AppPublisher=AutoRendering
UninstallDisplayName=Auto Rendering System
DefaultDirName={autopf}\AutoRenderingSystem
DefaultGroupName=Auto Rendering System
OutputDir=installer_output
OutputBaseFilename=AutoRenderingSystemSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
Uninstallable=yes
CreateUninstallRegKey=yes

[Types]
Name: "full"; Description: "Full installation"
Name: "agent"; Description: "Agent only (rendering PC)"
Name: "service"; Description: "Service only (control PC)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "agent"; Description: "AutoRendering Agent"; Types: full agent custom
Name: "agentconfig"; Description: "Agent Config Tool"; Types: full agent custom
Name: "service"; Description: "AutoRendering Service"; Types: full service custom

[Files]
Source: "publish\Agent\*"; DestDir: "{app}\Agent"; Components: agent; Flags: ignoreversion recursesubdirs
Source: "publish\AgentConfig\*"; DestDir: "{app}\AgentConfig"; Components: agentconfig; Flags: ignoreversion recursesubdirs
Source: "publish\Service\*"; DestDir: "{app}\Service"; Components: service; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\AutoRendering Agent"; Filename: "{app}\Agent\AutoRenderingAgent.exe"; Components: agent
Name: "{group}\Agent Config"; Filename: "{app}\AgentConfig\AutoRenderingAgentConfig.exe"; Components: agentconfig
Name: "{group}\AutoRendering Service"; Filename: "{app}\Service\AutoRenderingService.exe"; Components: service
Name: "{group}\Uninstall"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AutoRendering Service"; Filename: "{app}\Service\AutoRenderingService.exe"; Components: service; Tasks: desktopicon
Name: "{autodesktop}\Agent Config"; Filename: "{app}\AgentConfig\AutoRenderingAgentConfig.exe"; Components: agentconfig; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcuts"; GroupDescription: "Additional icons:"
Name: "agentautostart"; Description: "Start Agent automatically on Windows startup"; Components: agent

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AutoRenderingAgent"; ValueData: """{app}\Agent\AutoRenderingAgent.exe"""; Flags: uninsdeletevalue; Components: agent; Tasks: agentautostart

[Run]
Filename: "{app}\AgentConfig\AutoRenderingAgentConfig.exe"; Description: "Open Agent Config"; Flags: nowait postinstall skipifsilent; Components: agentconfig
Filename: "{app}\Service\AutoRenderingService.exe"; Description: "Launch AutoRendering Service"; Flags: nowait postinstall skipifsilent unchecked; Components: service
