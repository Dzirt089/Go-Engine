# T-020: Avalonia-проект

## Роль
Senior .NET разработчик в Go Engine.

## Цель
Пустое окно Avalonia запускается.

## Аудит
1. `PROJECT.md` — стек.
2. `DECISIONS.md` — D-001.

## Что сделать
1. Создать `GoEngine.App` — Avalonia 11.x.
2. `App.axaml`, `MainWindow.axaml`, `Program.cs`.
3. Подключить SkiaSharp.
4. Окно 800×600, заголовок «Go Engine».

## DoD
- [ ] `dotnet run --project GoEngine.App` открывает окно.
- [ ] `STATE.md` обновлён.