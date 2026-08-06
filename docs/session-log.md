---
title: 세션 로그
created: 2026-08-06
updated: 2026-08-06
domain: development
---

# 세션 로그 (최신이 위)

이 파일이 **"언제 무슨 일이 있었나"의 SSOT**다. 세션마다 최상단에 블록 추가.

블록 형식: `## YYYY-MM-DD — 제목` 아래에 **요청/피드백 → 수정 → 검증 → 다음** 순서로 간결하게.

## 2026-08-06 — S3: 검사 메타 서버 동기화 v0.1.10

- **수정**:
  - 서버 `StudyStore` + `POST/GET /api/studies` (JWT RequireAuthorization, StudyUid MERGE
    upsert, 최대 200건). **함정: Oracle 바인드 변수명 예약어(:uid/:ref/:mod) → ORA-01745** —
    suid/refp/moda 로 회피.
  - 클라 `ServerClient.UploadStudyAsync/SearchStudiesAsync` (Bearer). SaveDB·업데이트·InsExam
    후 자동 업로드(실패해도 로컬 저장 유효, 상태바에 동기화 결과 표기).
  - FindDB "서버에서 검색" 체크 — 조회 전용(열기/삭제/내보내기 차단 안내), 로그인 시에만 활성.
- **검증**: build/test 34/34/format ✅ · 라이브: 401(무인증)→200 업로드→upsert→검색 ✅ ·
  synctest E2E(클라 로그인→로컬 저장→업로드→서버 검색 1건) ✅ · 스모크 ✅ →
  인스톨러 0.1.10 바탕화면 교체.
- **다음**: S3 잔여(계정 관리) · 3차 준비(Orthanc + DICOM 전송 모달 — PPW 참고 설계 기록됨).

## 2026-08-06 — PPW 5.1 차용: JPG 오버레이 내보내기 + 우클릭 메뉴 v0.1.9

- **자료**: 사용자가 PPW 5.1(메디컬스탠다드 PACS) 매뉴얼 PDF 2건 제공 (루트, gitignore).
  차용 요청: 검사 우클릭 메뉴 · 검사 보내기 모달(AETitle 등) · JPG 내보내기 시 모서리
  환자정보 오버레이.
- **수정**:
  - Core `ImageExporter` — JPG 내보내기(품질 92) + 오버레이: TL 환자ID/이름/성별나이/생년월일,
    TR 검사명/Modality/검사일/n·total, BL 코멘트, BR 판독의/앱명. 흰 글씨+검은 그림자,
    해상도 비례 폰트, 한글 시스템 폰트 자동 탐색(맑은고딕→Noto CJK…).
    ImageSharp.Drawing 은 **2.1.7 고정** — 3.x 는 유료 라이센스 요구(함정).
  - 뷰어 툴바 [JPG 내보내기] — 오버레이 포함/원본 선택 → 폴더 → 저장.
  - FindDB 우클릭 메뉴: 검사 열기 / JPG 내보내기(오버레이) / 삭제 / "DICOM Storage SCP로
    전송…(3차 예정)" 비활성 자리. 검사 보내기 모달 설계는 plans/0002 S4 에 기록.
  - 판독 상태(Verify/Unread/Dictated…)는 판독 워크플로라 범위 밖으로 결정.
- **검증**: build/test 34/34/format ✅ (오버레이 픽셀 diff 검증) · overlaytest 산출물 눈 확인 ✅ ·
  스모크 ✅ → 인스톨러 0.1.9 바탕화면 교체.
- **다음**: S3 — 검사 메타데이터 서버화 (/api/studies, FindDB 서버 탭, 계정 관리).

## 2026-08-06 — 2차 착수: 도커 서버(Oracle+API) + 로그인 v0.1.8 (S1·S2 완료)

- **요청**: 2차(도커 서버) 착수 승인.
- **수정**:
  - ADR-0003(서버 스택 확정) · plans/0002-server.md (S1~S4).
  - `src/ShDicomStudio.Server` — ASP.NET Core 8 Minimal API. Oracle 연결 재시도 →
    USERS 스키마 생성 → admin/admin1234(BCrypt) 시드. `GET /health`(DB 상태 포함) ·
    `POST /api/auth/login`(JWT HS256, 12h).
  - `docker-compose.yml` — gvenzl/oracle-free:23-slim(APP_USER 자동 생성, healthcheck,
    데이터 볼륨) + server(멀티스테이지 Dockerfile, oracle healthy 후 기동). `.dockerignore`.
  - 앱 로그인 창(LoginWindow) — 서버주소/ID/PW, [오프라인으로 계속], 창 닫으면 종료.
    성공 시 AppSession(JWT 보관) + 메인 타이틀에 사용자 표시. `ServerClient.LoginAsync`.
- **검증**: build/test 32/32/format ✅ · **라이브 검증**: compose 기동 → /health `{db:ok}` ·
  admin 로그인 JWT 발급 · 오답 401 · 클라이언트 LoginAsync E2E(성공/실패 메시지) ✅ ·
  로그인 창 ShotTool 캡처 ✅ · 스모크 ✅ → 인스톨러 0.1.8 바탕화면 교체.
- **주의**: compose 의 비밀번호·JWT_SECRET 은 로컬 개발용 — 외부 노출 시 변경.
- **다음**: S3(검사 메타 서버화 — /api/studies, FindDB 서버 탭, 계정 관리).

## 2026-08-06 — 검사 업데이트(현재 화면 반영) v0.1.7

- **피드백**: FindDB 로 연 검사(2장)에서 뷰어 Delete 로 1장을 지웠는데 그 상태를 저장할
  방법이 없다 (InsExam 은 추가만 됨).
- **수정**: `LocalDatabase.UpdateStudy` — 이미지 구성·편집·환자정보를 통째로 덮어씀
  (Study UID 유지·새 Series·옛 파일 정리). SaveDB 를 열린 검사 상태에서 누르면
  3지선다 대화상자(`Dialogs.ChooseAsync`): 기존 검사 업데이트 / 새 검사로 저장 / 취소.
- **검증**: build/test 32/32/format ✅ (UpdateStudy: 2장→1장·이름 수정·UID 유지·파일 정리) ·
  스모크 ✅ → 인스톨러 0.1.7 바탕화면 교체.
- **다음**: 사용자 실기 → 옵션 화면(잔여) 또는 2차(도커 서버) 착수.

## 2026-08-06 — PDF 열기 + InsExam v0.1.6 (사용자 질문에서 파생)

- **질문/피드백**: ① 삭제가 없다? → FindDB 창의 [삭제] 안내. ② PDF 열어도 dcm 으로
  저장되나? → PDF 미지원이었음 → 구현. ③ 같은 환자 추가 저장이 별개 검사가 됨 —
  update 아닌가? → "저장=새 검사"가 DICOM 기본, 검사에 잇는 건 InsExam → 구현.
  ④ FindDB vs Worklist 차이 → 로컬 보관소 검색(과거·결과) vs 서버 예약 목록 조회(미래·예약).
- **수정**:
  - `ImageLoader.LoadAll` — PDF 를 pdfium(PDFtoImage 4.0.2, SkiaSharp 2.88 계열로 버전
    고정)으로 페이지당 1장 렌더(150dpi). 픽커 필터·빈 화면 안내에 PDF 추가.
  - `DicomStudy(existingStudyUid)` — 같은 Study·새 Series 로 이어가는 생성자.
    `LocalDatabase.AppendToStudy` — 파일·메타 이어붙이고 ImageCount 갱신.
  - InsExam 버튼 활성화 — FindDB 로 연 검사(OpenedStudyId 추적) + 새로 불러온 이미지
    (IsFromDb=false)만 추가. 완료/검증 실패는 대화상자.
  - 테스트: 손으로 조립한 2페이지 PDF 렌더 / InsExam Study·Series UID 규칙 (31/31).
- **검증**: build/test/format ✅ · 스모크 ✅ → 인스톨러 0.1.6 바탕화면 교체.
- **다음**: 사용자 실기(PDF·InsExam) → 옵션 화면 또는 2차(도커 서버) 착수.

## 2026-08-06 — M4 핵심(로컬 DB) + 재태깅 흐름 v0.1.5

- **질문**: "익명으로 변환한 DICOM 을 다시 열어 정보 수정 후 저장하면 덮어쓰나?" →
  아니오 — 항상 새 DICOM(새 UID·새 파일)이 생기고 원본은 그대로. 다만 ① 열 때 헤더가
  폼에 안 채워지고 ② 같은 폴더 익명 재저장 시 파일명 충돌 덮어쓰기 가능성이 있어 함께 수정.
- **수정**:
  - `DicomHeaderReader` — .dcm 열면 헤더(환자정보)를 읽어 폼이 비어 있을 때 자동 채움.
  - 폴더 저장 파일 번호를 기존 파일 다음부터 — 덮어쓰기 원천 차단 (InstanceNumber 는 검사 내 1..N 유지).
  - **M4 로컬 DB**: `Core/Database/LocalDatabase` (SQLite + `dicom/<StudyUid>/` 파일 보관,
    저장 1건 = 검사 1건) · SaveDB 버튼 · FindDB 창(검색 조건/결과 그리드/열기→뷰어+폼 채움/
    확인 후 삭제) · `Dialogs.ConfirmAsync` 추가. InsExam·옵션 화면은 잔여.
- **검증**: build ✅ · test 27/27 (LocalDatabase 저장/검색/재판독/삭제 캐스케이드) ✅ ·
  format ✅ · 스모크 ✅ → 인스톨러 0.1.5 바탕화면 교체.
- **다음**: 사용자 실기(DICOM 저장 + SaveDB/FindDB) → M4 잔여(InsExam·옵션) 또는 2차(도커
  서버) 착수 판단.

## 2026-08-06 — 저장 결과 가시화 + 진단 장치 v0.1.4

- **피드백**: "DICOM 저장이 안 된다" (구체 증상 미상 — 0.1.3 실기).
- **분석**: 리눅스 E2E(savetest)·CLI 자가진단은 모두 성공 → 재현 불가. 유력 후보:
  ① 검증 실패(Patient ID 미입력) 안내가 상태바 한 줄이라 "아무 일도 안 일어남"으로 보임,
  ② async void 예외로 조용한 실패/크래시. wine 이 32bit 라 배포 exe 직접 진단은 불가.
- **수정** (원인 불명이어도 다음 실패는 반드시 보이게):
  - 저장 흐름 전체 try/catch — 검증 실패·성공·오류를 **모달 대화상자**로 표시
    (`Views/Dialogs`, 오류 텍스트 복사 가능). 성공 시 저장 폴더 경로 표시.
  - 전역 크래시 로그 `%AppData%/sh-dicom-studio/crash.log` (Program.LogCrash).
  - 배포 바이너리 자가진단 CLI `--selftest-save <폴더>` (GUI 없이 변환·저장·재판독).
- **검증**: build/test 23/23/format ✅ · savetest E2E ✅ · selftest CLI ✅ · 스모크 ✅ →
  인스톨러 0.1.4 바탕화면 교체.
- **다음**: 사용자 재시도 — 실패 시 팝업 내용/crash.log 회수. 이후 M4 로컬 DB(SQLite).
  방향 확인됨: 로컬 DB(M4) → 도커 서버+Oracle(2차) → PACS 전송(3차, Orthanc 테스트).

## 2026-08-06 — M3 완료: DICOM 변환·저장 + 자동 레이아웃 v0.1.3

- **요청**: M3 진행 + 피드백 3건 — 장수 자동 그리드(2→2×1, 3→3×1, 4+→2×2 등 보기 좋게),
  날짜 픽커의 '6' 을 달력 아이콘으로, 페이지 배지(1/2)와 화살표 높이 정렬.
- **수정**:
  - Core `Dicom/DicomStudy`+`ExamInfo` — Secondary Capture 생성. UID 규칙: 저장 1회 =
    Study/Series 1쌍, 이미지별 SOP UID·InstanceNumber. RGB 8bit, ConversionType WSD,
    SpecificCharacterSet ISO_IR 192(한글). 나이는 DICOM AS("045Y") 포맷.
  - VM `SaveDicomAsync` — 선택(없으면 전체) → 폴더 픽커 → `<PatientID>_00001.dcm`.
    검증(ValidateForSave): 이미지 유무·PatientID/익명. AutoClear 반영. 저장 버튼·SaveAs 타일 활성화.
  - `AutoLayout()` — 장수→(1×1/2×1/3×1/2×2/3×2/3×3/4×3/4×4), Open 시 자동 적용.
  - 날짜 픽커: Fluent 기본 버튼이 '오늘 날짜 숫자가 든 미니 달력'이라 숫자로 보임 →
    `PART_Button` Template 을 mdi 달력 아이콘으로 교체 (Content 설정으로는 안 먹힘 — 함정).
  - 툴바 버튼·페이지 배지 높이 30 통일 정렬.
  - ShotTool: `loadedN` 장면(자동 레이아웃 확인)·`savetest`(저장 E2E — 헤드리스는 디스패처
    수동 펌프 필요) 추가.
- **검증**: build ✅ · test 23/23 ✅ (DicomStudyTests: 태그·UID 규칙·익명·라운드트립 색상) ·
  format ✅ · 스모크 ✅ · ShotTool 캡처(2장→2×1, 4장→2×2, 달력 아이콘) ✅ ·
  savetest E2E: 2장 저장 → 재판독 800×1000 일치 ✅ → 인스톨러 0.1.3 바탕화면 교체.
- **다음**: 사용자 실기 테스트(실제 PACS 뷰어로 dcm 열어보기 권장) → M4 로컬 DB.

## 2026-08-06 — 이미지 규격 버그 수정 + 썸네일 제거 v0.1.2

- **피드백**: 확대 안 했는데 이미지가 셀 규격에 안 맞음(스크린샷 — feedback-archive 보관).
  우측 썸네일 사이드바는 빼고 그 공간까지 뷰어로.
- **원인**: `ImageViewer` 호스트가 Grid 라 셀보다 큰 이미지의 arrange 가 셀 크기로 클램프
  → Image(Stretch=None)가 **center-crop** 으로 그려지고, 그 위에 Fit Matrix 가 겹쳐
  배율·위치가 이중으로 어긋남. **Canvas 로 교체** — 자식을 원본 크기·좌상단 원점으로
  배치하므로 Matrix 좌표계와 일치. (함정 박제: CLAUDE.md 코드 지도)
- **수정**: ImageViewer 호스트 Grid→Canvas · 우측 썸네일 ListBox 제거(뷰어 전폭 확장).
- **검증**: build/test 19/19/format ✅ · 실행 스모크 ✅ · **ShotTool 캡처로 규격 눈 확인**
  (2×1 에서 가로폭 꽉 참·상하 레터박스 균등·잘림 없음) → 인스톨러 0.1.2 바탕화면 교체.
- **다음**: 사용자 재확인 → M3.

## 2026-08-06 — UI 전면 개편 v0.1.1 (사용자 피드백 반영)

- **피드백**: 스크린샷 비교(Electron 판 vs v0.1.0) — 조잡·어두워서 안 보임·바둑판 픽커 없음·
  라이센스 미노출·도구 그룹 없음. + "매뉴얼(VPWinGate) 구성 그대로 구현할 것".
  → `docs/feedback-archive/2026-08-06-ui-개편/처리내역.md`
- **수정**:
  - 라이트 크롬 + 다크 뷰어 테마 (App.axaml 팔레트·타일 버튼 스타일). Avalonia 11.2.2→11.2.8
    (아이콘 라이브러리 Projektanker.Icons.Avalonia.MaterialDesign 요구).
  - 사이드바 = VPWinGate Toolbar1 그대로: MAIN TOOLS 12버튼(미구현 비활성+예정 툴팁) ·
    EXAM INFORMATION 전체 필드(ExamInfoViewModel — M3 의 DICOM 헤더 입력) · IMAGE TOOLS 16버튼.
  - 바둑판 레이아웃 픽커 `Controls/LayoutPicker`(6×6 호버) — 툴바 Flyout. 콤보 제거.
  - 하단: DICOM 저장 버튼(M3 예정 비활성) + 제작·저작권 표기. 뷰어 빈 상태 안내 문구.
  - `tools/ShotTool` 신설 (sh-ip-scanner 방식 이식) — 헤드리스 캡처로 UI 를 직접 확인하며 다듬음.
  - VPWinGate_Manual.pdf 를 프로젝트 루트에 참고용으로 보관 (gitignore — 매뉴얼 저작권).
- **검증**: build ✅ · test 19/19 ✅ · format ✅ · 실행 스모크 ✅ · ShotTool 캡처 눈 확인 ✅ →
  인스톨러 0.1.1 재빌드, 바탕화면 교체(0.1.0 제거).
- **다음**: 사용자 실기 확인 → M3 (저장 버튼 활성화가 목표).

## 2026-08-06 — 앱 아이콘 + Windows 인스톨러 v0.1.0 (M5 일부 선행)

- **요청**: 사용자가 실기 테스트를 위해 인스톨러 요청 + "아이콘 멋있게".
- **수정**:
  - 아이콘 신규 제작 (Pillow 슈퍼샘플링 스크립트) — 네이비→시안 그라데이션, 스캔 프레임 +
    펄스 라인 + 필름 스트립 모티프. `Assets/appicon.{ico,png}` (ico 는 16~256px 멀티사이즈).
    csproj `ApplicationIcon` + MainWindow `Icon` 연결, 템플릿 잔재 avalonia-logo.ico 삭제.
  - `installer/sh-dicom-studio.iss` — sh-ip-scanner 검증본 기반 (AppId GUID 신규 고정
    `3A5431A2-…`, 한국어 UI, 라이센스 동의 페이지 = LICENSE UTF-8 BOM 사본, VersionInfo 저작권).
  - `.gitignore` 에 `installer/Output/`·`installer/LICENSE.txt` 추가.
- **검증**: Release build/test 19/19 ✅ → `dotnet publish` win-x64 자체포함 단일파일(82MB) →
  wine ISCC 컴파일 성공 → `sh-dicom-studio-Setup-0.1.0.exe`(33MB) **바탕화면 전달** ✅
- **다음**: 사용자 실기 테스트 피드백 대기 · M3 (환자정보 + DICOM 변환·저장).

## 2026-08-06 — M2 완료: 그리드 뷰어 + Image Tools + DICOM 열기

- **요청**: 레포 생성(사용자가 `gh repo create` 직접 실행 — 분류기 차단 때문) 후 M2 진행.
- **수정**:
  - Core `ImageTransformer` — Rotate90/180·Flip·Invert 를 **픽셀에 직접 적용** (뷰 변환 아님 —
    M3 DICOM 변환이 EncodedBytes 를 그대로 쓰는 설계). `DicomRuntime` — ImageSharp 렌더러 1회 등록.
  - Core `ImageLoader` — `.dcm` 지원 (fo-dicom 렌더 → PNG, 멀티프레임 첫 프레임).
  - App — 그리드 레이아웃(1×1~4×4, ItemsControl+UniformGrid) · 페이지 넘김 · 셀 클릭 선택(빨간
    테두리, Classes.selected) · Select All · Image Tools 버튼 · Cut&Paste 순서변경 · Delete ·
    썸네일 클릭 → 해당 페이지 점프.
  - 함정 기록: **ItemsPanelTemplate 안에서는 컴파일 바인딩 타입 추론 불가** — `vm:` 캐스트가
    런타임 크래시. `ReflectionBinding` 으로 해결 (패널은 DataContext 상속).
- **검증**: build ✅ · test 19/19 ✅ (변환 픽셀 위치 검증 5종 + DICOM 파일 생성·로드 라운드트립) ·
  format ✅ · WSLg 실행 스모크 8초 무크래시 ✅
- **다음**: M3 — 환자정보 패널 + Secondary Capture DICOM 생성·저장.

## 2026-08-06 — M1 완료: 이미지 열기 + 뷰어

- **요청**: 레포 생성·푸시 후 M1 시작. **커밋/푸시를 에이전트에 위임** (CLAUDE.md·AGENTS.md 규칙 갱신).
  `gh repo create` 는 권한 분류기에 막혀 사용자가 직접 실행하기로.
- **수정**:
  - Core `Imaging/ImageLoader` — JPG/PNG/BMP/TIFF 로드. TIFF 만 PNG 트랜스코드(Avalonia/Skia 미지원),
    나머지는 원본 바이트 + `Image.Identify` 로 크기만 읽음.
  - App `Controls/ImageViewer` — Matrix 단일 변환으로 Fit(기본)/휠 Zoom(포인터 중심)/드래그 Pan/
    실제크기/Reset. 창 리사이즈 시 Fit 모드면 자동 재-Fit.
  - App `MainWindow` — 좌측 도구 패널(열기/모두 닫기 + M2·M3 자리) · 우측 썸네일 ListBox ·
    중앙 뷰어 툴바 · 하단 상태바. 파일 픽커는 코드비하인드(StorageProvider), 로드는 VM.
- **검증**: build ✅ · test 12/12 ✅ (ImageLoader 4포맷 라운드트립·TIFF 시그니처·확장자 판정) ·
  format ✅ · WSLg 실행 스모크 8초 무크래시 ✅
- **다음**: M2 (그리드 레이아웃·Image Tools·dcm 열기). 레포 생성되면 푸시.

## 2026-08-06 — 킥오프 완료

- **요청**: VPWinGate(DICOM Studio) 의 C# 재구현. 1차는 워크리스트·서버 제외 —
  이미지 열기 → DICOM 변환 → 뷰어 + 로컬 DB. 3-OS 인스톨러 배포 목표.
- **결정**: 이름 `sh-dicom-studio` · 스택 .NET 8 + Avalonia 11.2.2 + fo-dicom 5.2.6 + SQLite
  (ADR-0002) · 로그인은 2차(서버 인증)로 미룸 · 기능 명세 원본은 VPWinGate 매뉴얼 30p
  (`/mnt/c/Users/user/Desktop/VPWinGate_Manual.pdf`).
- **수정**: 브리프 확정(status: confirmed) · MVP 계획(plans/0001, M1~M5) ·
  스캐폴드 App/Core/Core.Tests (sh-ip-scanner 구조 미러링 — Avalonia 12 템플릿이 .NET 10 용이라
  net8.0+11.2.2 로 수동 재작성) · format.sh 훅 PostToolUse 연결 · packaging.md(sh-ip-scanner
  검증본 이식) · csproj 저작자 메타데이터 · CLAUDE.md/AGENTS.md TODO(kickoff) 전부 해소.
- **검증**: `dotnet build` ✅ · `dotnet test` 2/2 ✅ · `dotnet format --verify-no-changes` ✅
- **다음**: M1 — 파일 열기 다이얼로그(JPG/PNG/BMP/TIFF) → 뷰어 표시, Zoom/Pan/Fit.
  첫 커밋은 사용자가 직접 (메시지는 킥오프 마지막에 전달됨).
