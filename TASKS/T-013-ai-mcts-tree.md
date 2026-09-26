# T-013: AI.MctsNode + AI.MctsTree

## Роль
Senior .NET разработчик в Go Engine.

## Цель
Структура MCTS-дерева, готовая к playout'ам.

## Аудит
1. `AGENTS_GO.md` — раздел 7.
2. `DECISIONS.md` — D-003.

## Что сделать
1. `GoEngine.AI/Mcts/MctsNode.cs`:
   - Класс.
   - Поля: `Move Move`, `MctsNode? Parent`, `List<MctsNode> Children`, `int Visits`, `double Wins`, `IReadOnlyList<Move> UntriedMoves`.
   - `bool IsFullyExpanded`.
   - `double Ucb1(double c)`.
2. `GoEngine.AI/Mcts/MctsTree.cs`:
   - Класс.
   - `MctsNode Root`.
   - `MctsNode Select()`.
   - `MctsNode Expand(MctsNode node)`.
   - `void Backpropagate(MctsNode node, StoneColor winner)`.
3. **Никакого UI, никаких логов.**

## DoD
- [ ] Классы реализованы.
- [ ] Тесты (не менее 4):
  - `Ucb1_Не_Посещённый_Бесконечность`
  - `Select_Возвращает_Лист`
  - `Expand_Добавляет_Ребёнка`
  - `Backpropagate_Обновляет_Визиты_И_Победы`
- [ ] `dotnet test` зелёный.
- [ ] `STATE.md` обновлён.