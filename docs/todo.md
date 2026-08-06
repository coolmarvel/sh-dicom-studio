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

- [ ] **M1: 창이 뜨고 이미지가 보인다** (`docs/plans/0001-mvp.md`)
  - 파일 열기: JPG/PNG/BMP/TIFF 다중 선택 → 뷰어 표시 (`App/Views/MainWindow.axaml`)
  - 단일 이미지 뷰: Fit 기본 + 휠 Zoom / 드래그 Pan / Reset
  - 좌측 도구 패널 자리 잡기 (VPWinGate Toolbar1 배치 참고)

## P2 — 가까운 로드맵

- [ ] M2 뷰어 완성 (그리드 레이아웃·페이지·Image Tools·dcm 열기) — `plans/0001-mvp.md`
- [ ] M3 DICOM 변환·저장 (환자정보 패널 + fo-dicom Secondary Capture)
- [ ] M4 로컬 DB (SQLite SaveDB/FindDB) + 옵션 화면
- [ ] M5 인스톨러 (win exe 우선, mac/linux `[?]`) — `docs/guides/packaging.md`

## P3 — 품질

- [ ] 앱 아이콘 제작 (`ApplicationIcon` — 현재 미설정)

## P4 — 아이디어

- [ ] 2차: ASP.NET Core + Oracle(도커) 서버 + 로그인 · 3차: DICOM Send(Orthanc 테스트) ·
      4차: Modality Worklist — `docs/brief.md` 범위 밖 참고
