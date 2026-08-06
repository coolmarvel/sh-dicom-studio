---
title: ADR-0002 스택 결정 — .NET 8 + Avalonia + fo-dicom + SQLite
created: 2026-08-06
status: accepted
---

# ADR-0002: 스택 결정

## 상태

Accepted — 2026-08-06 사용자(이성현) 선택.

## 맥락

브리프의 제약: ① 윈도우·맥·리눅스 3-OS 데스크톱 앱 + 인스톨러 배포, ② 1차는 오프라인
완결(서버 없음), ③ DICOM 파일 생성·판독이 핵심, ④ 비용 0원, ⑤ 사용자가 C#/.NET 을
배우고 싶다는 명시적 희망(자바 배경). 2차 이후 서버(HTTP API + Oracle, 도커)가 예정돼
있어 그때의 확장도 고려해야 한다.

## 결정

| 층 | 선택 |
|---|---|
| 언어/런타임 | C# / .NET 8 (LTS) |
| UI 프레임워크 | **Avalonia UI** (MVVM, CommunityToolkit.Mvvm) |
| DICOM | **fo-dicom** (Fellow Oak DICOM) — 생성·판독·(후차)네트워크 전부 |
| 이미지 코덱 | SkiaSharp (Avalonia 가 이미 사용) |
| 로컬 DB | SQLite (Microsoft.Data.Sqlite) — 검사 메타데이터 검색용 |
| 패키징 | OS별 self-contained publish → exe(Inno Setup)/dmg/AppImage. 상세는 `docs/guides/packaging.md` |
| (2차) 서버 | ASP.NET Core Web API + Oracle, docker compose — 착수 시 별도 ADR 로 확정 |

## 근거

- **Avalonia vs .NET MAUI vs WPF**: MAUI 는 리눅스 데스크톱 미지원, WPF 는 윈도우 전용 —
  3-OS 요구를 만족하는 C# UI 프레임워크는 Avalonia 가 유일하다. WPF 와 XAML 문법이 거의
  같아 자료 호환도 좋다. (Electron/Tauri 는 C# 학습 목표와 어긋나 제외.)
- **fo-dicom**: C# DICOM 라이브러리의 사실상 표준. 1차(파일 생성·판독)만이 아니라 3·4차의
  Storage SCU(C-STORE)·Modality Worklist(C-FIND) 까지 한 라이브러리로 커버돼 후차 확장 시
  스택 교체가 없다.
- **SQLite**: 1차 로컬 DB 는 단일 파일·설치 부담 0 인 SQLite 가 적합. Oracle 은 서버
  구성요소이므로 2차에 도커로 도입한다.
- **서버를 ASP.NET Core 로 기울여 두는 이유**: 클라이언트는 HTTP 로만 통신하므로 서버
  언어는 기술적으로 자유롭다(자바 배경을 살려 Spring Boot 도 가능). 다만 혼자 개발에서는
  클라이언트·서버 단일 언어(C#)가 IDE·빌드체인·DTO 공유 면에서 유리해 ASP.NET Core 를
  기본안으로 둔다. 2차 착수 시점에 최종 확정하며, Spring Boot 로 바꿔도 클라이언트는
  영향이 없다.

## 결과

- (+) 한 언어로 데스크톱→서버까지 확장, DICOM 전 기능 단일 라이브러리, 3-OS 배포 가능.
- (−) Avalonia 는 WPF 보다 커뮤니티가 작다 — 대신 WPF 자료가 대부분 이식된다.
- (−) 리눅스/맥 인스톨러 빌드는 각 OS 러너(또는 CI)가 필요 — 패키징 마일스톤에서 해결.
