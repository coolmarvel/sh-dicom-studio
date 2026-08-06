; sh DICOM Studio — Inno Setup 스크립트 (docs/guides/packaging.md 참고)
; 저작자/라이센스: 이성현 (SeongHyun Lee). 설치 시 라이센스 동의 페이지에 표시된다.

#define MyAppName "sh DICOM Studio"
#define MyAppVersion "0.1.10"
#define MyAppPublisher "SeongHyun Lee"
#define MyAppExeName "ShDicomStudio.App.exe"

[Setup]
; AppId 는 이 앱을 유일하게 식별(업그레이드·제거에 사용). 임의 GUID 고정 — 절대 바꾸지 말 것.
AppId={{3A5431A2-5426-424B-853B-62E4FF99410A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (C) 2026 SeongHyun Lee
; 설치 마법사 라이센스 페이지 (UTF-8 BOM — 한글 표시)
LicenseFile=LICENSE.txt
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputBaseFilename=sh-dicom-studio-Setup-{#MyAppVersion}
OutputDir=Output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\ShDicomStudio.App\Assets\appicon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; VersionInfo* 는 Setup.exe 자체 속성(자세히)에 저작자/저작권을 새긴다.
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright=Copyright (C) 2026 SeongHyun Lee
VersionInfoProductName={#MyAppName}
VersionInfoVersion={#MyAppVersion}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 생성"; GroupDescription: "추가 아이콘:"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} 실행"; Flags: nowait postinstall skipifsilent

; ── 코드 서명(Authenticode) 참고 ──────────────────────────────
; 인증서 확보 시 [Setup] 에 SignTool=mysign 을 추가하고 컴파일 옵션으로 서명한다.
; (인증서가 없으면 SmartScreen '알 수 없는 게시자' 경고 — 설치는 가능.)
