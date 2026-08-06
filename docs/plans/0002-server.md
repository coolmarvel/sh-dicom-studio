---
title: 2차 — 도커 서버 (ASP.NET Core + Oracle) + 로그인
created: 2026-08-06
status: active
---

# 0002 — 2차: 서버·로그인 마일스톤

스택 근거는 `docs/adr/0003-server-stack.md`. 상태 표기: `[ ]/[~]/[x]/[?]`.

## S1 — 서버가 뜬다 (docker compose) ✅ 2026-08-06

- [x] `src/ShDicomStudio.Server` — ASP.NET Core 8 Minimal API 스캐폴드
- [x] `docker-compose.yml` — oracle(gvenzl/oracle-free) + server, healthcheck·기동 순서
- [x] Oracle 연결 재시도 + 스키마 초기화(USERS) + admin 계정 시드
- [x] `GET /health` 가 DB 연결 상태까지 보고 — 라이브 curl 검증 ✅

## S2 — 로그인 (서버 인증) ✅ 2026-08-06

- [x] `POST /api/auth/login` — BCrypt 검증 → JWT 발급 (실패 401) — 라이브 검증 ✅
- [x] 앱 시작 시 로그인 창 — [오프라인으로 계속] 허용, 창 닫으면 종료
- [x] 서버 주소 입력(로그인 창) — 기본 `http://localhost:8080`

## S3 — 검사 메타데이터 서버화

- [ ] `POST /api/studies` — SaveDB 시 검사 메타 업로드 (파일은 아직 로컬)
- [ ] `GET /api/studies?patientId=…` — FindDB 에 서버 검색 탭
- [ ] 사용자 계정 관리 최소 (admin 이 사용자 추가)

## S4 — 다음 단계 연결

- [ ] 3차 준비: Orthanc 컨테이너를 compose 에 추가 (PACS 전송 테스트 대상)
- [ ] `[?]` 파일(DICOM)까지 서버 보관할지 — 3차 설계 시 결정

## 범위 밖

PACS 전송(3차) · Modality Worklist(4차) · 배포용 서버(병원 내 실서버) 구성.
