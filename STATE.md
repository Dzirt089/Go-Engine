# Состояние проекта

> Этот файл агент обязан обновлять **после каждой задачи**, до коммита.
> При старте новой сессии агент читает его первым.

## Текущая фаза

**Фаза 1. Core: доска и ходы**

## Текущая задача

**T-002. `Core.GroupTracker` + `Core.Capture`** — завершена

## Статус

`done` — `dotnet build` без ошибок и предупреждений, `dotnet test` зелёный (100 тестов),
коммит и push в `origin/main` выполнены.

## Следующая задача

**T-003. `Core.SuicideCheck`** — `next`

## Дата начала фазы

2026-09-26

## Последнее обновление

2026-09-26T19:03:51Z

## Что сделано в текущей фазе

- [x] **T-001.** Solution `GoEngine.sln` (классический формат), проекты `GoEngine.Core` и
      `GoEngine.Tests` (net8.0, C# 12)
- [x] **T-001.** `BoardSize` (только 9 / 13 / 19), `Point` (`IsOnBoard`, `Neighbors`, формат «D4»),
      `StoneColor` (`Empty` / `Black` / `White`, `Opponent()`), `MoveType`, `Move`
      (фабрики `Play` / `Pass` / `Resign`, `Move.None`), `DomainException`,
      `Enumeration` (`FromId<T>` / `FromName<T>`)
- [x] **T-001.** `Board` — иммутабельный `ApplyMove`, `Clone`, `MakeMove`, `At`, `IsEmpty`,
      `AllPoints`, `EmptyPoints`, `OccupiedPoints`
- [x] **T-002.** `Group` — `readonly record struct` с камнями и дамэ, `IsInAtari`, `IsCaptured`
- [x] **T-002.** `GroupTracker` — `FindGroup` (BFS по стороне), `FindLiberties`,
      `AllGroups`
- [x] **T-002.** `CaptureResult` — результат хода: список снятых камней, `None`, `HasCaptures`
- [x] **T-002.** `Board.ApplyMove` / `Board.MakeMove` снимают группы противника без дамэ
      (порядок из `GO_RULES.md`, п. 4) и записывают снятые камни в `Board.CapturedStones`
- [x] **T-002.** 34 новых теста (всего 100): группы, дамэ, атари, захваты в углу, на краю,
      двух групп одним ходом, `CapturedStones`; позиции 1 и 2 из `GO_RULES.md`, п. 13

## Что осталось в текущей фазе

- [ ] `Core.SuicideCheck` — задача T-003
- [ ] `MoveGenerator` (базовый) — отдельной задачи в `PLAN.md` нет:
      `PLAN.md` относит генератор ходов к deliverable фазы 1, но в списке задач его нет

## Блокеры

Нет. Прежний блокер «нет work tree» снят: `D:\Harness\Work-1` — обычный репозиторий
с рабочей копией, ветка `main`, `origin` → `D:\Harness\go-engine.git`. Коммит `f9ab4cb`
и push в `origin/main` проходят. Подробности — `GIT.md`.

## Открытые вопросы к пользователю

1. **GitHub / GitLab.** Зеркала не настроены: `gh` и `glab` в системе не установлены
   (проверено), учётные данные не заданы. Работаем только с `origin` — как предписано
   `GO_RULES.md`. Если зеркала нужны — потребуются установка CLI и аутентификация.

## Журнал последних изменений

| Дата | Задача | Что сделано |
|---|---|---|
| 2026-09-26 | T-001 | Созданы `GoEngine.sln`, `GoEngine.Core`, `GoEngine.Tests` (net8.0, C# 12) |
| 2026-09-26 | T-001 | Реализованы `BoardSize`, `Point`, `StoneColor`, `MoveType`, `Move`, `Board`, `DomainException`, `Enumeration` |
| 2026-09-26 | T-001 | 66 юнит-тестов; `dotnet build` — 0 предупреждений, `dotnet test` — зелёный |
| 2026-09-26 | T-002 | Реализованы `Group`, `GroupTracker`, `CaptureResult`; `Board` снимает группы без дамэ |
| 2026-09-26 | T-002 | 34 новых теста (всего 100) — группы, дамэ, атари, захваты, `CapturedStones` |
| 2026-09-26 | T-002 | Восстановлена git-инфраструктура: work tree, ветка `main`, `origin`; создан `GIT.md` |
| 2026-09-26 | T-002 | Коммит и push в `origin/main` выполнены |

## Журнал сессии

| UTC timestamp | Задача | Статус | Что сделано |
|---|---|---|---|
| 2026-09-26T18:45:04Z | T-001 | done | Board, Move и базовые типы; 66 тестов; коммит и push не выполнены (не было work tree) |
| 2026-09-26T19:03:51Z | T-002 | done | Group, GroupTracker, CaptureResult, снятие групп в Board; 100 тестов; коммит и push в origin/main |

## Заметки для следующей сессии

- Читать `GO_RULES.md` перед началом.
- `Board` иммутабелен на публичном API. Снятие групп уже есть (T-002);
  самоубийство — T-003, ко — T-005. Сейчас ход, убивающий свою группу, **не отклоняется**:
  `ApplyMove` оставляет камень без дамэ на доске. Это ожидаемо до T-003.
- `Board.CapturedStones` относится только к последнему ходу: `MakeMove` сбрасывает список.
- Папки `samples/`, на которую ссылаются `AGENTS.md` и `AGENTS_GO.md`, в репозитории нет:
  стиль брался только из этих двух файлов.
- .NET 8 SDK в системе не установлен (SDK 10.0.400 и runtime 8.0.30): сборка `net8.0`
  проходит, targeting pack подтягивается из nuget.org. Для офлайн-сборки поставить .NET 8 SDK.
- Расхождение из прошлой сессии («задачи лежат в `TSAKS/`») снято: папка называется `TASKS/`.

## Команды для проверки

```bash
dotnet build
dotnet test
```
