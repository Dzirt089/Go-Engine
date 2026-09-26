# Состояние проекта

> Этот файл агент обязан обновлять **после каждой задачи**, до коммита.
> При старте новой сессии агент читает его первым.

## Текущая фаза

**Фаза 1. Core: доска и ходы** — закрыта. Открыта **фаза 2. Core: ко и история**.

## Текущая задача

**T-003. `Core.SuicideCheck`** — завершена

## Статус

`done` — `dotnet build` без ошибок и предупреждений, `dotnet test` зелёный (123 теста),
коммит и push в `origin/main` выполнены.

## Следующая задача

**T-004. `Core.PositionHistory`** — `next`

## Дата начала фазы

2026-09-26

## Последнее обновление

2026-09-26T19:05:51Z

## Что сделано

- [x] **T-001.** Solution `GoEngine.sln`, проекты `GoEngine.Core` и `GoEngine.Tests` (net8.0, C# 12)
- [x] **T-001.** `BoardSize`, `Point`, `StoneColor`, `MoveType`, `Move`, `DomainException`,
      `Enumeration`, `Board` (иммутабельный `ApplyMove`, `Clone`, `MakeMove`)
- [x] **T-002.** `Group`, `GroupTracker` (`FindGroup`, `FindLiberties`, `AllGroups`),
      `CaptureResult`; `Board` снимает группы противника без дамэ, `Board.CapturedStones`
- [x] **T-003.** `Board.IsLegal(Move) → Result<Unit>` — проверка хода без исключений
- [x] **T-003.** `Board.ApplyMove` / `MakeMove` отклоняют самоубийство; ход, снимающий
      последнее дамэ противника, разрешён (`GO_RULES.md`, п. 5)
- [x] **T-003.** `Result`, `Result<T>`, `Unit`, `MoveResult` — созданы здесь, потому что
      `IsLegal` без них не выразить; T-009 закроет их тестами и перенесёт на `GameState`

## Что осталось

- [ ] `Core.PositionHistory` — T-004
- [ ] `Core.KoRule` (суперко) — T-005
- [ ] Интеграционные тесты 8 позиций — T-006
- [ ] `Core.Scorer` — T-007, `Core.GameState` — T-008, `Result` — T-009
- [ ] `MoveGenerator` (базовый) — отдельной задачи в `PLAN.md` нет, хотя генератор ходов
      указан в deliverable фазы 1

## Блокеры

Нет. Git-инфраструктура работает: `D:\Harness\Work-1` — обычный репозиторий с рабочей копией,
ветка `main`, `origin` → `D:\Harness\go-engine.git`. См. `GIT.md`.

## Открытые вопросы к пользователю

1. **GitHub / GitLab.** Зеркала не настроены: `gh` и `glab` не установлены, учётные данные
   не заданы. Работаем только с `origin`, как предписано `GO_RULES.md`.

## Журнал последних изменений

| Дата | Задача | Что сделано |
|---|---|---|
| 2026-09-26 | T-001 | Solution, базовые типы, `Board`; 66 тестов |
| 2026-09-26 | T-002 | `Group`, `GroupTracker`, `CaptureResult`, снятие групп; 100 тестов |
| 2026-09-26 | T-002 | Создан `GIT.md`; коммит `07cc594` и push в `origin/main` |
| 2026-09-26 | T-003 | `Result`, `Result<T>`, `Unit`, `MoveResult`; `Board.IsLegal` |
| 2026-09-26 | T-003 | Самоубийство отклоняется, захват последнего дамэ разрешён; 123 теста |

## Журнал сессии

| UTC timestamp | Задача | Статус | Что сделано |
|---|---|---|---|
| 2026-09-26T18:45:04Z | T-001 | done | Board, Move и базовые типы; 66 тестов; коммит и push не выполнены (не было work tree) |
| 2026-09-26T19:03:51Z | T-002 | done | Group, GroupTracker, CaptureResult, снятие групп в Board; 100 тестов |
| 2026-09-26T19:05:51Z | T-003 | done | Result/Result<T>/Unit/MoveResult, IsLegal, запрет самоубийства; 123 теста |

## Заметки для следующей сессии

- Читать `GO_RULES.md` перед началом.
- `Board`: снятие групп (T-002) и самоубийство (T-003) готовы; ко — T-005.
  `ApplyMove`/`MakeMove` бросают `DomainException`, `IsLegal` возвращает `Result<Unit>` — см. `DECISIONS.md` D-010.
- `IsLegal` проверяет ход на копии доски: корректно, но аллоцирует. Если MCTS упрётся
  в скорость (T-015) — заменить на проверку без копии (считать дамэ гипотетической группы).
- Папки `samples/` в репозитории нет: стиль брался из `AGENTS.md` и `AGENTS_GO.md`.
- .NET 8 SDK не установлен (SDK 10.0.400 и runtime 8.0.30): сборка `net8.0` проходит,
  targeting pack подтягивается из nuget.org. Для офлайн-сборки поставить .NET 8 SDK.
- В рабочей копии появились задачи T-019 – T-033 (их не было в `PLAN.md`): список задач
  в `TASKS/` считается полным, `PLAN.md` отстаёт.

## Команды для проверки

```bash
cd src
dotnet build
dotnet test
```
