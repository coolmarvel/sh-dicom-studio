# CLAUDE.md — sh-dicom-studio 작업 가이드

이 파일은 **세션이 바뀌어도 맥락을 즉시 복구**하기 위한 진입점이다. Claude Code는 세션 시작 시
이 파일을 자동으로 읽는다. (도구 중립 절대 규칙은 `AGENTS.md`.)

> [project-seed](https://github.com/coolmarvel/project-seed) 에서 2026-08-06 에 생성됨.
> `<!-- TODO(kickoff) -->` 가 남아 있으면 킥오프가 끝나지 않은 것이다 — 채우기 전에 기능 작업을 시작하지 않는다.

## 🟢 세션 시작 부팅 프로토콜 (매 세션 첫 작업 전에 반드시 수행)

새 세션에서 작업을 시작하면, **코드를 건드리기 전에** 순서대로:

1. `docs/session-log.md` 읽기 — 마지막으로 무엇을 했고 지금 어디쯤인지 (**진행 이력 SSOT**)
2. `docs/todo.md` 읽기 — 남은 일 (P1~P4)
3. 최근 `docs/plans/*.md` 1개 읽기 — 진행 중 기능의 설계 의도
4. 사용자 피드백 확인: **프로젝트 루트의 스크린샷/캡처 파일 = 미처리 피드백**으로 읽는다.
   반영 후 `docs/feedback-archive/YYYY-MM-DD-<주제>/` 로 이동.
5. `git status` 에 모르는 변경이 있으면 session-log 와 대조 — 다른 세션의 흔적일 수 있다. 출처 불명이면 사용자에게 확인.

## 🔴 변경 후 자동 규칙 (사용자가 매번 요청하지 않아도 수행)

1. 코드를 바꾸면 **같은 턴에** `docs/session-log.md`(최상단 블록 추가)·`docs/todo.md`
   (+릴리스급이면 `docs/changelog.md`)를 갱신한다. 문서 규칙은 `docs/writing-guide.md`.
2. 검증을 통과하기 전에는 커밋 메시지 작성/산출물 전달을 하지 않는다:
   `dotnet build ShDicomStudio.sln && dotnet test ShDicomStudio.sln && dotnet format ShDicomStudio.sln --verify-no-changes`
3. 버전을 판단해 올린다 (아래 "버전 정책").
4. 산출물 전달: 릴리스급이면 인스톨러(`sh-dicom-studio-Setup-<버전>.exe`)를 바탕화면
   (`/mnt/c/Users/user/Desktop/`)에 복사. 개발 중 확인은 `dotnet run` 안내로 충분.

## 버전 정책 (semver `MAJOR.MINOR.PATCH`)

**MINOR 승격은 사용자만 선언한다.** 에이전트가 판단해서 올리지 않는다.

- **PATCH** — 기본값. 다듬는 중인 기능의 수정 하나 반영할 때마다 +1.
- **MINOR** — 사용자가 "이 기능은 더 수정할 게 없다, 넘어가자"라고 선언한 그 시점에만.
- **MAJOR** — 대규모 재설계/호환 깨짐. 사용자와 상의.

## 커밋 컨벤션

Conventional Commits — `<type>: <한국어 제목>` + 리스트형 본문. **검증 통과 후 에이전트가
직접 커밋·푸시한다** (2026-08-06 사용자 위임). 메시지 끝에 `Co-Authored-By` 트레일러.

type: `feat` `fix` `refactor` `chore` `docs` `style` `test` `perf` `ci` `build` `revert` `init` `remove` `rename` `hotfix`

## 협업 규칙

- **진행 이력의 SSOT 는 `docs/session-log.md`** — git history 가 아니다 (커밋이 성긴 단위라서).
- 세션이 끊겨도 이어서 작업할 수 있게 **모든 진행 사항을 `docs/` 에 파일로 기록**한다.
- 설계 논쟁이 생기면 `docs/brief.md`(왜/무엇 SSOT)로 돌아와 판정한다. 브리프 밖 기능은 스코프 확인 먼저.
- `.env` 는 직접 수정하지 않는다 — `.env.example` 수정 또는 사용자에게 요청 (훅이 차단함).
- 규칙이 미확정이면 이 파일에 `<!-- TODO -->` 로 남기고, 확정되는 순간 채운다.

## 이 프로젝트가 뭔가

병원 검사장비가 출력하는 JPG/PDF 등 일반 이미지를 환자·검사정보와 묶어 **DICOM 파일로
변환·저장·조회**하는 크로스플랫폼(윈/맥/리눅스) 데스크톱 앱. 상용 프로그램
**VPWinGate(DICOM Studio)의 C# 재구현**이 목표이며, 기능 명세의 원본은 VPWinGate 매뉴얼이다
(기능 대응표는 `docs/brief.md`·`docs/plans/0001-mvp.md`). 1차는 서버 없이 오프라인 완결:
이미지 열기 → 뷰어(그리드·이미지 도구) → DICOM 변환 → 로컬 DB(SQLite) 검색.
로그인·서버(ASP.NET Core + Oracle, 도커)·DICOM Send·Worklist 는 2차 이후 (`docs/brief.md` 범위 밖 참고).

## 문서 인덱스 (docs/)

| 파일 | 용도 |
|---|---|
| `docs/writing-guide.md` | **문서 지배 규칙** (SSOT·frontmatter·코드 1:1 대조). 문서 쓰기 전 필독 |
| `docs/brief.md` | 프로젝트 **왜/무엇 SSOT** — 킥오프 산출물 |
| `docs/session-log.md` | 세션별 진행 이력. **"언제 무슨 일" SSOT** (최신이 위) |
| `docs/todo.md` | 미해결·향후 작업만 (P1~P4). 완료분은 session-log 로 |
| `docs/changelog.md` | 릴리스 단위 사람용 요약 |
| `docs/adr/*.md` | 구조적 결정 기록 — 왜 이렇게 했는가 (`NNNN-kebab.md`) |
| `docs/plans/*.md` | 앞으로 만들 것 — 기능 단위 구현 계획 (역할 구분은 `plans/README.md`) |
| `docs/guides/*.md` | 현재 구현된 동작·코드 위치 (기능별) |
| `docs/feedback-archive/` | 처리 완료한 사용자 피드백 보관소 |

## 자주 쓰는 명령

```bash
dotnet run --project src/ShDicomStudio.App          # 앱 실행 (개발)
dotnet build ShDicomStudio.sln                      # 빌드
dotnet test ShDicomStudio.sln                       # 테스트
dotnet format ShDicomStudio.sln --verify-no-changes # 포맷 검사 (자동 수정은 --verify 빼고)
dotnet run --project tools/ShotTool -- out.png loaded # 헤드리스 UI 스크린샷 (장면: main|loadedN|login)
docker compose up -d --build                        # 2차 서버 (oracle + API) — Docker Desktop 필요
curl http://localhost:8080/health                   # 서버 상태 (db 연결까지 보고)
# 패키징: docs/guides/packaging.md — dotnet publish + Inno Setup(wine) → 바탕화면 전달
```

## 코드 지도 (수정 시 어디를 보나)

| 위치 | 역할 |
|---|---|
| `src/ShDicomStudio.Core/` | UI 없는 도메인 로직 — DICOM 변환(fo-dicom)·로컬 DB(SQLite)·모델. **테스트 대상은 전부 여기로** |
| `src/ShDicomStudio.App/` | Avalonia UI (MVVM). `Views/`(axaml) ↔ `ViewModels/`(CommunityToolkit.Mvvm) |
| `tests/ShDicomStudio.Core.Tests/` | xUnit — Core 만 참조 (Avalonia 의존 금지) |
| `src/ShDicomStudio.Server/` | 2차 API 서버 (ASP.NET Core 8 Minimal API + Oracle) — docker compose 로 구동 |
| `installer/` | Inno Setup 스크립트 (WSL+wine 으로 컴파일 — packaging.md) |
| `tools/ShotTool/` | 헤드리스 UI 스크린샷 도구 — **XAML 을 고치면 캡처로 눈 확인** |

**새 기능 추가 체크리스트** = ① Core 에 모델·로직 ② Core.Tests 에 테스트 ③ ViewModel
④ View(axaml) ⑤ `docs/` 갱신(session-log·todo·필요시 guides).

- 함정: Avalonia 최신 템플릿(v12)은 .NET 10/C# 13 용 — 이 프로젝트는 **net8.0 + Avalonia 11.2.2**
  고정 (sh-ip-scanner 검증 조합). 패키지 추가 시 net8.0 호환을 확인할 것.
- 함정: `ItemsPanelTemplate` 안은 컴파일 바인딩 타입 추론이 안 된다 — `vm:` 타입 캐스트 바인딩은
  빌드는 되지만 **런타임 크래시**. `ReflectionBinding` 을 쓸 것 (2026-08-06 M2 에서 실사고).
  XAML 을 고치면 빌드 성공만 믿지 말고 `timeout 8 dotnet run` 실행 스모크까지 돌린다.
- 함정: RenderTransform(Matrix)으로 이미지를 다룰 때 호스트는 **Canvas** 여야 한다 — Grid 등은
  자식 arrange 를 셀 크기로 클램프해 Stretch=None 이미지가 center-crop 되고 변환 좌표계가
  어긋난다 (2026-08-06 v0.1.2 실사고). 렌더링 결과는 ShotTool 캡처로 눈 확인.

## 하네스 (Harness Engineering)

지시문이 아니라 시스템으로 제약한다. 도입 근거: `docs/adr/0001-harness-engineering.md`.

| 축 | 내용 |
|---|---|
| Hooks (`scripts/hooks/`) | `env-guard.sh`(.env 편집 차단) · `git-add-guard.sh`(`git add -A/.`·.env staging 차단) · `format.sh`(.cs/.axaml 저장 시 `dotnet format` — PostToolUse 연결됨) |
| Commands (`.claude/commands/`) | `/review-security` · `/deploy-check` |
| MCP (`.mcp.json`) | `context7`(라이브러리 문서) · `playwright`(브라우저 QA — 데스크톱 앱이라 사용 빈도 낮음, 필요 없으면 제거 가능) |
| Skills | 없음 (도입 시 skills-lock.json 방식으로 락) |
