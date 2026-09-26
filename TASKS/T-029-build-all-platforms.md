# T-029: Сборка под Windows/macOS/Linux

## Роль
Senior .NET разработчик в Go Engine.

## Что сделать
1. `dotnet publish -c Release -r win-x64 --self-contained`.
2. Аналогично для `osx-x64`, `linux-x64`.
3. Скрипты `build.ps1` / `build.sh`.
4. Проверить, что запускается на каждой платформе.

## DoD
- [ ] Три сборки.
- [ ] Скрипты работают.
- [ ] `STATE.md` обновлён.