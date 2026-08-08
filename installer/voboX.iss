; voboX 安装包脚本（Inno Setup 6）
; 编译：ISCC.exe voboX.iss

#define MyAppName "voboX"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "voboX"
#define MyAppExeName "voboX.exe"

[Setup]
AppId={{E7B5FA7D-10FD-4948-82C1-F4673E712A89}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; 用户级安装（无需管理员权限），默认装到 %LocalAppData%\Programs\voboX
PrivilegesRequired=lowest
; 输出到 release 文件夹
OutputDir=..\release
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\voboX.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=license.rtf
; 64 位程序
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; 文件版本信息
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
; 自包含单文件 exe（157MB）
Source: "..\release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// 用户修改默认安装路径时弹警告（文件导入功能依赖安装位置）
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if CompareText(WizardDirValue(), ExpandConstant('{localappdata}\Programs\{#MyAppName}')) <> 0 then
    begin
      if MsgBox('您修改了默认安装路径。' + #13#10#13#10 +
                'voboX 的文件导入功能依赖安装位置，修改路径可能导致文件导入异常。' + #13#10#13#10 +
                '确定仍要继续安装到该路径吗？', mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end;
  end;
end;
