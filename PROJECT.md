# Go Engine — манифест проекта

## Что это

Кроссплатформенная offline-игра Го (19×19, 13×13, 9×9) для Windows, macOS, Linux, iOS, Android.
Игрок против AI. Сложность AI — от 30 кю до 10 кю (цель первой версии).

**Текущий фактический уровень.** Без улучшений серии T-019a…T-019e движок играет на 20–15 кю:
MCTS с чистыми случайными playout'ами не даёт заявленной силы при реалистичных бюджетах
(`DECISIONS.md`, D-015, D-016). Серия T-019a…T-019e усиливает MCTS без нейросети;
потолок такой серии — 8–10 кю. Уровни 5 кю и выше — только во второй версии, с нейросетью (ONNX).

## Границы проекта

- **Offline-first.** Игра работает без интернета. Внешние зависимости не загружаются.
- **Один процесс.** Никаких серверов, никакой сети.
- **AI — внутри процесса.** MCTS, без нейросети в первой версии.
- **Кроссплатформенность.** Avalonia UI + SkiaSharp.

## Слои и зависимости

```
GoEngine.Core       ← ничего, кроме System.*
GoEngine.AI         ← только Core
GoEngine.App        ← Core, AI, Avalonia, SkiaSharp
GoEngine.Tests      ← Core, AI, xUnit
```

Строгое правило: **слой не может ссылаться на слой выше себя**.

| Слой | Может ссылаться на | Запрещено |
|---|---|---|
| `Core` | `System.*` | Avalonia, AI, IO, БД |
| `AI` | `Core`, `System.*` | Avalonia, IO, БД |
| `App` | `Core`, `AI`, Avalonia, SkiaSharp | — |
| `Tests` | `Core`, `AI`, xUnit | `App` |

## Технологический стек

- .NET 8 (LTS)
- C# 12
- Avalonia UI 11.x
- SkiaSharp
- xUnit + Moq для тестов
- Microsoft.Extensions.TimeProvider.Testing — `FakeTimeProvider` в тестах AI (только тесты)
- `System.CommandLine` — не используется
- Внешние пакеты — минимальны, только необходимые

## Что НЕ используется

- MediatR, AutoMapper, FluentValidation
- Entity Framework, Dapper, любые ORM
- ASP.NET Core, ASP.NET MVC
- Newtonsoft.Json (только `System.Text.Json`)
- Любые пакеты для Go — всё пишем сами

## Именование

- Корневой namespace: `GoEngine`
- Проекты: `GoEngine.Core`, `GoEngine.AI`, `GoEngine.App`, `GoEngine.Tests`
- Файлы: `PascalCase.cs`
- Тесты: `{Что}_{Условие}_{Ожидание}.cs` (например, `Board_После_захвата_снимает_группу.cs`)

## Стиль кода

Полностью соответствует `AGENTS.md` (Style Bible проекта). Дополнения для Go — в `AGENTS_GO.md`.

## Definition of Done для всего проекта

- [ ] Правила игры реализованы полностью и покрыты тестами.
- [ ] AI уровней 30 кю – 10 кю работает.
- [ ] UI запускается на Windows, macOS, Linux.
- [ ] Игра сохраняет и загружает SGF.
- [ ] Игра работает без интернета.
- [ ] Нет утечек памяти при долгой партии (10 000+ ходов).