#!/bin/bash
# PostToolUse hook: C#/XAML 파일을 저장하면 dotnet format 으로 자동 정렬 (ADR-0001 하네스 참고)
# 지시문이 아니라 시스템으로 스타일을 강제한다 — 사람이 매번 챙기지 않아도 포맷이 일정하게 유지된다.
set -euo pipefail

FILE_PATH=$(python3 -c "
import json, os
data = json.loads(os.environ.get('CLAUDE_TOOL_INPUT', '{}'))
print(data.get('file_path', ''))
" 2>/dev/null) || exit 0

# .cs / .axaml 만 대상. 그 외(문서 등)는 조용히 통과.
case "$FILE_PATH" in
  *.cs|*.axaml) ;;
  *) exit 0 ;;
esac

# dotnet 은 전역 PATH 에 없을 수 있으므로 사용자 설치 경로를 우선 탐색한다.
DOTNET_BIN=""
if command -v dotnet >/dev/null 2>&1; then
  DOTNET_BIN="dotnet"
elif [ -x "$HOME/.local/bin/dotnet" ]; then
  DOTNET_BIN="$HOME/.local/bin/dotnet"
elif [ -x "$HOME/.dotnet/dotnet" ]; then
  DOTNET_BIN="$HOME/.dotnet/dotnet"
else
  # dotnet 을 못 찾으면 조용히 통과 (개발 환경이 아직 안 갖춰진 경우 훅이 작업을 막지 않도록).
  exit 0
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SLN="$REPO_ROOT/ShDicomStudio.sln"
[ -f "$SLN" ] || exit 0

# 방금 편집한 파일 하나만 포맷 (전체 솔루션 포맷은 느려서 저장마다 돌리기엔 부담).
DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 \
  "$DOTNET_BIN" format "$SLN" --include "$FILE_PATH" >/dev/null 2>&1 || true

exit 0
