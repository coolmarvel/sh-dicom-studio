---
title: ADR-0003 서버 스택 — ASP.NET Core 8 + Oracle Free (docker compose)
created: 2026-08-06
status: accepted
---

# ADR-0003: 서버 스택 확정 (2차)

## 상태

Accepted — ADR-0002 에서 예고한 "2차 착수 시 확정"의 이행. 사용자가 2차 착수를 승인함 (2026-08-06).

## 맥락

1차(오프라인 데스크톱)가 완료됐고(v0.1.7), 2차 목표는 ① 서버·DB 통신 경험 ② 로그인(서버
인증) ③ 검사 메타데이터 서버화다. 사용자 희망: C#/.NET 유지, **Oracle**(병원 실무 DB),
PC 1대에서 도커로 서버를 띄워 실전과 같은 구성으로 연습.

## 결정

| 층 | 선택 |
|---|---|
| API 서버 | ASP.NET Core 8 Minimal API (`src/ShDicomStudio.Server`) |
| DB | Oracle Database Free (`gvenzl/oracle-free:23-slim` 컨테이너) |
| DB 드라이버 | Oracle.ManagedDataAccess.Core (공식 관리형 드라이버) |
| 인증 | ID/PW → BCrypt 해시 검증 → JWT 발급 (Bearer) |
| 구동 | `docker compose up` — oracle + server 2개 컨테이너, 서버는 oracle healthy 후 시작 |

## 근거

- **ASP.NET Core (vs Spring Boot)**: ADR-0002 의 근거 유지 — 클라이언트와 단일 언어(C#),
  DTO 공유, IDE·빌드체인 일원화. 클라이언트는 HTTP 만 쓰므로 후일 교체도 가능.
- **gvenzl/oracle-free**: Oracle 공인 무료 이미지의 사실상 표준. `APP_USER` 환경변수로
  앱 계정 자동 생성, healthcheck 내장. 병원 실무 Oracle 과 SQL 호환.
- **Minimal API**: 엔드포인트 수가 적은 단계라 컨트롤러 계층 없이 시작 — 커지면 분리.
- **JWT**: 데스크톱 클라이언트가 세션 쿠키보다 다루기 쉽고, 3차(PACS 전송 이력 등)의
  API 보호에 그대로 쓴다.

## 결과

- (+) `docker compose up` 한 번으로 실전형 서버 구성 재현. 로컬 PC 1대로 완결.
- (−) oracle-free 이미지가 큼(~1.7GB) — 최초 1회 다운로드 시간.
- (−) Oracle 첫 기동이 느림(수십 초) — 서버에 연결 재시도 루프를 넣어 흡수.
