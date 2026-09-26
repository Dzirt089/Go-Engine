# T-015: AI.MctsMoveSelector

## Роль
Senior .NET разработчик в Go Engine.

## Цель
MCTS-селектор, играющий на уровне 10–5 кю.

## Аудит
1. `T-013`, `T-014` done.
2. `DECISIONS.md` — D-003.

## Что сделать
1. `GoEngine.AI/Mcts/MctsMoveSelector.cs`:
   - `IMoveSelector`.
   - Конструктор: `Random`, `PlayoutPolicy`, `int playoutBudget`.
   - `SelectMove(GameState state)`:
     1. Создать корень.
     2. Пока `budget` не исчерпан: select → expand → playout → backpropagate.
     3. Вернуть ход с максимальным числом посещений.
   - Детерминирован при фиксированном `Random`.
2. `MctsConfig`:
   - `record struct MctsConfig(int PlayoutBudget, double Ucb1C)`.
   - Дефолт: `Budget = 5000`, `Ucb1C = 1.41`.
3. Быстрый `Board.Clone()` + `MakeMove` в горячем цикле.

## DoD
- [ ] MctsMoveSelector работает.
- [ ] Тесты (не менее 4):
  - `Mcts_Всегда_Легальный_Ход`
  - `Mcts_Детерминирован_При_Фиксированном_Seed`
  - `Mcts_Находит_Ход_В_Цумэго_9x9`
  - `Mcts_Бюджет_Влияет_На_Силу`
- [ ] `dotnet test` зелёный.
- [ ] `STATE.md` обновлён.