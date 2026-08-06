---
title: TODO
created: 2026-08-06
updated: 2026-08-06
domain: development
---

# TODO (미해결·향후 작업만 — 완료분은 session-log 로)

우선순위: **P1** 다음 릴리스에서 다뤄야 함 · **P2** 가까운 로드맵 · **P3** 품질 · **P4** 아이디어.
항목에는 대상 파일 경로와 (있다면) 과거 사고 근거를 함께 적는다.

## P1 — 다음 릴리스에서 다뤄야 함

- [ ] **M3: DICOM 변환·저장** (`docs/plans/0001-mvp.md`)
  - 환자·검사 정보 입력 패널 (좌측 Information 자리에)
  - fo-dicom Secondary Capture 생성 — UID 발급 규칙 확정 (테스트 `DicomLoadTests` 의 생성 코드가 출발점)
  - Save As + 라운드트립 검증

## P2 — 가까운 로드맵
- [ ] M4 로컬 DB (SQLite SaveDB/FindDB) + 옵션 화면
- [ ] M5 인스톨러 (win exe 우선, mac/linux `[?]`) — `docs/guides/packaging.md`

## P3 — 품질

- [ ] 인스톨러 사용자 실기 테스트 피드백 반영 (v0.1.0 — 바탕화면 전달됨, 2026-08-06)

## P4 — 아이디어

- [ ] 2차: ASP.NET Core + Oracle(도커) 서버 + 로그인 · 3차: DICOM Send(Orthanc 테스트) ·
      4차: Modality Worklist — `docs/brief.md` 범위 밖 참고
