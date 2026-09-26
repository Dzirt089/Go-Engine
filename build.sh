#!/usr/bin/env bash
# Сборка Go Engine под Windows, macOS и Linux.
#
# Публикует самодостаточные сборки в каталог artifacts, предварительно прогоняя тесты.
# Использование: ./build.sh [win-x64] [osx-x64] [linux-x64]
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
src="$root/src"
artifacts="$root/artifacts"

runtimes=("$@")
if [ ${#runtimes[@]} -eq 0 ]; then
  runtimes=(win-x64 osx-x64 linux-x64)
fi

echo "Go Engine: проверка тестов"
dotnet test "$src/GoEngine.sln" --nologo

for rid in "${runtimes[@]}"; do
  output="$artifacts/$rid"

  echo "Go Engine: сборка $rid"
  dotnet publish "$src/GoEngine.App/GoEngine.App.csproj" \
    -c Release -r "$rid" --self-contained true -o "$output" --nologo

  # Проверка запуска возможна только на своей системе: проверочный режим не открывает окно.
  if [ "$rid" = "$(dotnet --info | grep -o 'RID:.*' | awk '{print $2}')" ] || [ "$rid" = "linux-x64" ] && [ "$(uname -s)" = "Linux" ]; then
    echo "Go Engine: проверка $rid"
    "$output/GoEngine.App" --smoke
  else
    echo "Go Engine: $rid собрана; запуск проверяется на целевой системе"
  fi
done

echo "Go Engine: сборки готовы в $artifacts"
