# Состояние проекта

> Этот файл агент обязан обновлять **после каждой задачи**, до коммита.
> При старте новой сессии агент читает его первым.

## Текущая фаза

**Фаза 5. AI: MCTS**

## Текущая задача

**T-015. `AI.MctsMoveSelector`** — завершена

## Статус

`done` — `dotnet build` без ошибок и предупреждений, `dotnet test` зелёный (293 теста),
коммит и push в `origin/main` выполнены.

## Следующая задача

**T-016. Тесты на цумэго (9×9)** — `next`

## Дата начала фазы

2026-09-26

## Последнее обновление

2026-09-26T19:30:41Z

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
- [x] **T-006.** `RulesIntegrationTests` — 8 позиций из `GO_RULES.md`, п. 13, каждая со ссылкой
      на раздел в комментарии; расхождений с правилами не найдено, `DECISIONS.md` не менялся
- [x] **T-006.** Канонические позиции вынесены в `TestPositions` (ко, самоубийство в углу,
      захват с самоубийством, сэки) — без дублирования между наборами тестов
- [x] **T-007.** `Komi` (валидация ≥ 0, `For19x19` / `For13x13` / `For9x9`, `For(size)`),
      `Score` (`Winner`, `Margin`), `Scorer.Calculate(Board, Komi)` — китайские правила:
      камни + пустые области, окружённые одним цветом; нейтральные не считаются ни за кого
- [x] **T-007.** Позиции 7 и 8 в `RulesIntegrationTests` теперь проверяют и точный счёт
- [x] **T-008.** `GameStatus` (`InProgress`, `FinishedByTwoPasses`, `FinishedByResign`,
      `FinishedByMoveLimit`, `IsFinished`), `GameResult` (статус, счёт, победитель, причина)
- [x] **T-008.** `GameState.NewGame` (лимит по умолчанию `BoardSize.DefaultMoveLimit` = 2 × площадь,
      начальная позиция сразу в истории), `Play` → `Result<MoveResult>`, `Finish` → `Result<GameResult>`;
      завершение двумя пасами, сдачей и по лимиту ходов
- [x] **T-009.** `Result` / `Result<T>` используются всем публичным API `Core`: `Board.IsLegal`,
      `GameState.Play`, `GameState.Finish`; тесты на `Result`, `Result<T>` и на их применение
- [x] **T-010.** Создан проект `GoEngine.AI` (net8.0, C# 12, ссылка только на `Core`),
      добавлен в решение и в ссылки тестов
- [x] **T-010.** `IMoveSelector` (`Name`, `SelectMove(GameState)`), `AiException`,
      `RandomMoveSelector` — инжектированный `Random`, собирает легальные ходы, при их
      отсутствии пас; перегрузка `SelectMove(Board, StoneColor)` для анализа без партии
- [x] **T-011.** `SelfPlayHarness.PlayGame` — партия до `GameStatus.IsFinished`;
      нелегальный ход селектора — `AiException` (нарушение инварианта AI)
- [x] **T-011.** `SelfPlayResult` — итог партии, все ходы и финальная доска;
      партия двух случайных селекторов на 9×9 детерминирована при фиксированном зерне
- [x] **T-012.** `HeuristicMoveSelector` — эвристики по приоритету: съесть группу в атари,
      спасти свою группу в атари, занять угол в начале, иначе случайный легальный ход;
      доля случайности задаёт силу уровня
- [x] **T-012.** `DifficultyLevel` (`Kyu30`, `Kyu25`, `Kyu20`) с полями `RankKyu`,
      `PlayoutBudget`, `RandomnessPercent` и фабрикой `CreateSelector(Random)`
- [x] **T-012.** Тест силы: Kyu20 выигрывает у Kyu30 не менее 55 партий из 100 на 9×9
- [x] **T-013.** `LegalMoves` — общий перебор легальных ходов для всех селекторов (был продублирован)
- [x] **T-013.** `Board.WithoutHistory` — копия доски без истории: анализ не пополняет историю партии
- [x] **T-013.** `MctsNode` (ход, родитель, дети, визиты, победы, неразобранные ходы, `Ucb1`)
      и `MctsTree` (`Select`, `Expand`, `Backpropagate`); узлы хранят позиции без истории
- [x] **T-014.** `AtariHeuristics` — общие эвристики атари для селектора и playout'а (без дублирования)
- [x] **T-014.** `PlayoutConfig` (0.9 атари / 0.5 соседний ход) и `PlayoutPolicy`:
      захват атари → спасение атари → ход рядом с камнями → случайный ход
- [x] **T-015.** `MctsConfig` (бюджет 5000, UCB1 1.41) и `MctsMoveSelector`:
      спуск → расширение → playout → передача результата; выбор хода по посещениям,
      при равенстве — по доле побед; `Search` открыт для тестов и разбора
- [x] **T-015.** Быстрая проверка `Board.IsLegal` без копии доски (копия только при
      проверке суперко), захват в playout'ах ищется по группам, а не перебором ходов
- [x] **T-015.** Playout'ы не убивают свои группы: ход, оставляющий свою группу в атари,
      отбрасывается, а если таких ходов нет — playout заканчивается пасом

## Что осталось

- [ ] Цумэго (5–10 позиций) — T-016
- [ ] Полный `DifficultyLevel` (15–5 кю, MCTS) — T-017, `AiFactory` — T-018, тест силы — T-019
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
| 2026-09-26 | T-006 | 8 позиций `GO_RULES.md` п. 13 в `RulesIntegrationTests`; 162 теста; фаза 2 закрыта |
| 2026-09-26 | T-007 | `Komi`, `Score`, `Scorer` (китайские правила); 182 теста |
| 2026-09-26 | T-008 | `GameStatus`, `GameResult`, `GameState`; `BoardSize.DefaultMoveLimit`; 215 тестов |
| 2026-09-26 | T-009 | Тесты `Result`/`Result<T>` и их применения в `Core`; 223 теста; фаза 3 закрыта |
| 2026-09-26 | T-010 | Проект `GoEngine.AI`, `IMoveSelector`, `RandomMoveSelector`, `AiException`; 231 тест |
| 2026-09-26 | T-011 | `SelfPlayHarness`, `SelfPlayResult`; партия AI vs AI на 9×9; 240 тестов |
| 2026-09-26 | T-012 | `HeuristicMoveSelector`, `DifficultyLevel` (30/25/20 кю), тест силы; 252 теста |
| 2026-09-26 | T-013 | `LegalMoves`, `Board.WithoutHistory`, `MctsNode`, `MctsTree`; 272 теста |
| 2026-09-26 | T-014 | `AtariHeuristics`, `PlayoutConfig`, `PlayoutPolicy`; 283 теста |
| 2026-09-26 | T-015 | `MctsConfig`, `MctsMoveSelector`, быстрый `IsLegal`, живучие playout'ы; 293 теста |

## Журнал сессии

| UTC timestamp | Задача | Статус | Что сделано |
|---|---|---|---|
| 2026-09-26T18:45:04Z | T-001 | done | Board, Move и базовые типы; 66 тестов; коммит и push не выполнены (не было work tree) |
| 2026-09-26T19:03:51Z | T-002 | done | Group, GroupTracker, CaptureResult, снятие групп в Board; 100 тестов |
| 2026-09-26T19:05:51Z | T-003 | done | Result/Result<T>/Unit/MoveResult, IsLegal, запрет самоубийства; 123 теста |
| 2026-09-26T19:14:00Z | T-004 | done | PositionHash (Zobrist, SplitMix64), PositionHistory, Board.History/WithHistory; 139 тестов |
| 2026-09-26T19:30:00Z | T-005 | done | KoRule (суперко), PreviewMove, история пополняется после хода; 154 теста |
| 2026-09-26T19:12:19Z | T-006 | done | 8 позиций правил из GO_RULES п. 13; 162 теста; фаза 2 закрыта, открыта фаза 3 |
| 2026-09-26T19:13:28Z | T-007 | done | Komi, Score, Scorer (китайские правила), точный счёт позиций 7–8; 182 теста |
| 2026-09-26T19:13:52Z | T-008 | done | GameStatus, GameResult, GameState (пас, сдача, лимит ходов, счёт); 215 тестов |
| 2026-09-26T19:15:15Z | T-009 | done | Result/Result<T> в публичном API Core, тесты; 223 теста; фаза 3 закрыта |
| 2026-09-26T19:16:40Z | T-010 | done | Проект GoEngine.AI, IMoveSelector, RandomMoveSelector, AiException; 231 тест |
| 2026-09-26T19:19:07Z | T-011 | done | SelfPlayHarness и SelfPlayResult, партия AI vs AI на 9×9; 240 тестов |
| 2026-09-26T19:21:18Z | T-012 | done | HeuristicMoveSelector и DifficultyLevel 30–20 кю, тест силы 100 партий; 252 теста |
| 2026-09-26T19:22:29Z | T-013 | done | LegalMoves, Board.WithoutHistory, MctsNode, MctsTree; 272 теста; открыта фаза 5 |
| 2026-09-26T19:30:41Z | T-014 | done | AtariHeuristics, PlayoutConfig, PlayoutPolicy; 283 теста |
| 2026-09-26T19:30:41Z | T-015 | done | MctsConfig, MctsMoveSelector, быстрый IsLegal, живучие playout'ы; 293 теста |

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
- `Scorer.Calculate` считает очки по площади: камни + пустые области, граничащие только с одним
  цветом. Область, граничащая с обоими цветами, нейтральна — так же ведёт себя сэки.
- `GameState.Play` сам проверяет: партия идёт, ход не пустой, цвет совпадает с `ToMove`,
  ход легален. При отказе партия не меняется вообще.
- `GoEngine.AI` зависит только от `Core`; публичный API AI синхронный, случайность — только
  через инжектированный `Random` (проверено тестом на детерминизм).
- `SelfPlayHarness.PlayGame` принимает seed, но сам случайность не создаёт: за неё отвечают
  селекторы, созданные вызывающим кодом с тем же зерном. Seed проверяется (не отрицательный)
  и фиксирует партию в отчёте.
- Сила уровня задаётся долей случайности: 30 кю — 100% (чистый случай), 25 кю — 60%,
  20 кю — 15%; остальное — эвристики. См. `DECISIONS.md` D-012.
- Полный прогон тестов занимает ~14 с: тест силы играет 100 партий на 9×9.
- Дерево MCTS работает на досках без истории (`Board.WithoutHistory`): варианты анализа
  не попадают в историю партии — это проверено тестами.
- `MctsNode.Wins` считается с точки зрения сделавшего ход: у корня `Move.None`, поэтому
  его победы всегда 0, а ход выбирается по числу посещений.
- Эвристики атари вынесены в `AtariHeuristics`: их используют и `HeuristicMoveSelector`,
  и `PlayoutPolicy` — правила захвата описаны в одном месте.
- Скорость: один playout на 9×9 — около 20 мс, поэтому тесты используют бюджеты 5–25,
  а не 5000. Для игры нужен бюджет поменьше или оптимизация (см. `DECISIONS.md` D-013).
- Цумэго-тест T-015 использует гонку захвата: у чёрных один легальный ход, и он решает партию.
  Полный набор цумэго — T-016, там же потребуется решить вопрос бюджета.
- Для AI: `Board` без истории (MCTS и playout не должны засорять историю партии);
  проверка ходов — через `Board.IsLegal`; случайность — только с seed.
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