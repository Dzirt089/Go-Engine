# Состояние проекта

> Этот файл агент обязан обновлять **после каждой задачи**, до коммита.
> При старте новой сессии агент читает его первым.

## Текущая фаза

**Фаза 2. Core: ко и история**

## Текущая задача

**T-005. `Core.KoRule`** (позиционное суперко) — завершена

## Статус

`done` — `dotnet build` без ошибок и предупреждений, `dotnet test` зелёный (154 теста),
коммит и push в `origin/main` выполнены.

## Следующая задача

**T-006. Интеграционные тесты правил (8 позиций)** — `next`

## Дата начала фазы

2026-09-26

## Последнее обновление

2026-09-26T19:08:49Z

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
- [x] **T-004.** `PositionHash` — Zobrist-хеш позиции, таблица на 2 цвета × 361 точку,
      заполняется SplitMix64 от постоянного зерна (одинаков на любой машине и версии .NET)
- [x] **T-004.** `PositionHistory` — `Add`, `Contains(Board)`, `Contains(PositionHash)`, `Count`,
      `Hashes` (снимок); `Board.History`, `Board.WithHistory`; копии доски делят историю партии
- [x] **T-005.** `KoRule.ViolatesSuperko(Board, Move)` — позиционное суперко; без истории запрет
      не действует; пас и сдача повторения не создают
- [x] **T-005.** `Board.ApplyMove` отклоняет ход, повторяющий позицию, и пополняет `History`
      позицией после хода; `Board.PreviewMove` — предпросмотр без изменения доски и истории

## Что осталось

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
| 2026-09-26 | T-004 | `PositionHash` (Zobrist), `PositionHistory`, `Board.History`/`WithHistory`; 139 тестов |
| 2026-09-26 | T-005 | `KoRule` (суперко), `Board.PreviewMove`, история пополняется ходом; 154 теста |

## Журнал сессии

| UTC timestamp | Задача | Статус | Что сделано |
|---|---|---|---|
| 2026-09-26T18:45:04Z | T-001 | done | Board, Move и базовые типы; 66 тестов; коммит и push не выполнены (не было work tree) |
| 2026-09-26T19:03:51Z | T-002 | done | Group, GroupTracker, CaptureResult, снятие групп в Board; 100 тестов |
| 2026-09-26T19:05:51Z | T-003 | done | Result/Result<T>/Unit/MoveResult, IsLegal, запрет самоубийства; 123 теста |
| 2026-09-26T19:14:00Z | T-004 | done | PositionHash (Zobrist, SplitMix64), PositionHistory, Board.History/WithHistory; 139 тестов |
| 2026-09-26T19:08:49Z | T-005 | done | KoRule (суперко), PreviewMove, история пополняется после хода; 154 теста |

## Заметки для следующей сессии

- Читать `GO_RULES.md` перед началом.
- `Board`: снятие групп (T-002) и самоубийство (T-003) готовы; ко — T-005.
  `ApplyMove`/`MakeMove` бросают `DomainException`, `IsLegal` возвращает `Result<Unit>` — см. `DECISIONS.md` D-010.
- `IsLegal` проверяет ход на копии доски: корректно, но аллоцирует. Если MCTS упрётся
  в скорость (T-015) — заменить на проверку без копии (считать дамэ гипотетической группы).
- `Board.Clone` делит историю с исходной доской: это одна партия. Для анализа без суперко
  (MCTS) доску создают без `WithHistory` — тогда `History` равно `null`.
- Начальную позицию партии в историю добавляет тот, кто её создаёт (T-008, `GameState`):
  `Board` этого не делает, поэтому суперко не мешает первому ходу.
- Фаза 2 закрывается задачей T-006 (8 позиций из `GO_RULES.md`, п. 13).
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