---
title: 패키징 가이드 — .NET 자체포함 게시 + OS별 인스톨러
created: 2026-08-06
updated: 2026-08-06
domain: packaging
---

# 패키징 가이드 — sh DICOM Studio (윈/맥/리눅스 설치본)

> 원본: sh-ip-scanner `docs/guides/packaging.md` (**C#/Avalonia/Inno 파이프라인 검증 완료** 2026-08-04)
> 를 이 프로젝트 값으로 옮긴 것. Windows 절차는 검증본 그대로, macOS/Linux 는 M5 에서 검증 후 갱신한다.

## 목표 산출물 (버전당 3-OS)

| OS | 파일 | 상태 |
|---|---|---|
| Windows | `sh-dicom-studio-Setup-<버전>.exe` (Inno Setup) | 파이프라인 검증됨 (sh-ip-scanner) |
| macOS | `sh-dicom-studio-<버전>-arm64.dmg` / `-x64.dmg` | `[?]` M5 에서 검증 (맥 실기 필요) |
| Linux | `sh-dicom-studio-<버전>-x86_64.AppImage` | `[?]` M5 에서 검증 |

- 설치본은 **자체포함(self-contained)** — 대상 PC 에 .NET 런타임이 없어도 실행된다.

## 공통 규칙 (발사대에서 승격된 것)

- **릴리스 전 검증 통과가 먼저다**: `dotnet build -c Release` + `dotnet test` + `dotnet format --verify-no-changes`.
- 산출물 폴더(`publish/`, `installer/Output/`, `release/`)는 `.gitignore` 대상 — **설치 파일을 git 에 커밋하지 않는다.**
- 공개 배포 자산 파일명은 **ASCII** (`sh-dicom-studio-Setup-0.1.0.exe`). 한글 파일명 금지(URL/도구 호환).
- 업로드 스크립트는 **버전을 파라미터로** 받게 만든다.
- macOS 내부 번들 이름은 ASCII, 표시명만 한글 가능 (발사대 가이드의 pdf-editor 사고 참고).

## 1단계 — 자체포함 게시 (dotnet publish)

```bash
dotnet publish src/ShDicomStudio.App/ShDicomStudio.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -o publish/win-x64
```

- RID 를 바꿔 OS별로 게시한다: `win-x64` / `osx-arm64` / `osx-x64` / `linux-x64`.
- (선택) 소스 보호: .NET IL 은 디컴파일이 쉽다 — 배포 확대 시 Obfuscar 등을 게시 후 단계에 검토.

## 2단계 — Windows 인스톨러 (installer/sh-dicom-studio.iss)

```ini
[Setup]
AppName=sh DICOM Studio
AppVersion=0.1.0
AppPublisher=SeongHyun Lee
AppCopyright=Copyright (C) 2026 SeongHyun Lee
VersionInfoCompany=SeongHyun Lee
VersionInfoCopyright=Copyright (C) 2026 SeongHyun Lee
LicenseFile=..\LICENSE.txt
DefaultDirName={autopf}\sh DICOM Studio
DefaultGroupName=sh DICOM Studio
OutputBaseFilename=sh-dicom-studio-Setup-0.1.0
OutputDir=Output
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\sh DICOM Studio"; Filename: "{app}\ShDicomStudio.App.exe"
Name: "{autodesktop}\sh DICOM Studio"; Filename: "{app}\ShDicomStudio.App.exe"

[Run]
Filename: "{app}\ShDicomStudio.App.exe"; Description: "실행"; Flags: nowait postinstall skipifsilent
```

- `.iss` 본문 필드는 ASCII 유지, 한글 라이센스 전문은 `LicenseFile` 로 — 그 `LICENSE.txt` 는
  **UTF-8 BOM** 저장 (루트 `LICENSE` 를 BOM 붙여 복사해 만든다). 설치 마법사에 동의 페이지로 표시된다.
- WSL 에서 빌드 (sh-ip-scanner 검증 명령):

```bash
cd installer
WINEDEBUG=-all wine "C:\\Program Files\\Inno Setup 6\\ISCC.exe" sh-dicom-studio.iss
cp Output/sh-dicom-studio-Setup-<버전>.exe /mnt/c/Users/user/Desktop/   # 바탕화면 전달
```

## 3단계 — macOS dmg / Linux AppImage `[?]`

- macOS: `dotnet publish -r osx-arm64`(및 `osx-x64`) → `.app` 번들 구성 → `create-dmg`.
  서명·notarization 없으면 Gatekeeper 경고(우클릭-열기로 우회 가능). **맥 실기에서만 굽는다.**
- Linux: `dotnet publish -r linux-x64` → AppImage 도구(appimagetool)로 패키징.
- 두 절차 모두 M5 에서 첫 검증 후 이 문단을 실측 명령으로 갱신할 것.

## 검증 체크리스트

- 인스톨러 설치 → 바로가기 → 실행 → 메인 창 확인.
- .NET 런타임이 없는 PC 에서 실행(자체포함 검증).
- 탐색기 속성 > 자세히에 제작자·저작권 표시 확인 (csproj VersionInfo).

## 열린 항목

- `[?]` 코드 서명 인증서 — 없으면 SmartScreen 경고. 병원 내 배포엔 수용, 확대 시 재검토.
- `[?]` macOS/Linux 빌드 환경 확보 (CI 또는 실기).
