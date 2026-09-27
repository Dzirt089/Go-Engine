#!/usr/bin/env bash
# Полная проверка Go Engine: сборка, тесты и проверочные режимы приложения.
#
# Одна команда для «definition of done». Флаг --skip-stress пропускает проверку нагрузки:
# она играет сто партий и занимает около десяти секунд.
set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
src="$root/src"
app="$src/GoEngine.App.Desktop/GoEngine.App.Desktop.csproj"
failures=0

step() {
  local title="$1"
  shift
  echo "Go Engine: $title"
  if ! "$@"; then
    echo "  не пройдено: $title"
    failures=$((failures + 1))
  fi
}

step "сборка решения" dotnet build "$src/GoEngine.sln" --nologo
step "тесты" dotnet test "$src/GoEngine.sln" --nologo

modes=(--smoke --check --state --settings --sgf --animation --e2e)
if [ "${1:-}" != "--skip-stress" ]; then
  modes+=(--stress)
fi

for mode in "${modes[@]}"; do
  step "режим $mode" dotnet run --project "$app" --no-build -- "$mode"
done

if [ "$failures" -gt 0 ]; then
  echo "Go Engine: проверок не пройдено — $failures."
  exit 1
fi

echo "Go Engine: всё зелёное."