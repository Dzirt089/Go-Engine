# T-028: Настройки — persist в JSON

## Роль
Senior .NET разработчик в Go Engine.

## Что сделать
1. `AppSettings` — `record`, JSON.
2. Путь: `%AppData%/GoEngine/settings.json` (Windows), `~/.config/goengine/settings.json` (Linux), `~/Library/Application Support/GoEngine/settings.json` (macOS).
3. Восстановление при старте.

## DoD
- [ ] Настройки сохраняются/загружаются.
- [ ] Тесты (не менее 2).
- [ ] `STATE.md` обновлён.