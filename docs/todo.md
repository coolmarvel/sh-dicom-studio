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

- [ ] **M2: 뷰어를 VPWinGate 수준으로** (`docs/plans/0001-mvp.md`)
  - 그리드 레이아웃(1×1~4×4) + 페이지 넘김 + Select All
  - Image Tools: Rotate/FlipH·V/Invert/순서변경(Cut&Paste)/Delete
  - 기존 DICOM(.dcm) 열기 (fo-dicom 판독 → 뷰어)

## P2 — 가까운 로드맵

- [ ] M3 DICOM 변환·저장 (환자정보 패널 + fo-dicom Secondary Capture)
- [ ] M4 로컬 DB (SQLite SaveDB/FindDB) + 옵션 화면
- [ ] M5 인스톨러 (win exe 우선, mac/linux `[?]`) — `docs/guides/packaging.md`

## P3 — 품질

- [ ] 앱 아이콘 제작 (`ApplicationIcon` — 현재 미설정)

## P4 — 아이디어

- [ ] 2차: ASP.NET Core + Oracle(도커) 서버 + 로그인 · 3차: DICOM Send(Orthanc 테스트) ·
      4차: Modality Worklist — `docs/brief.md` 범위 밖 참고
