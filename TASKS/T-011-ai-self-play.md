# T-011: AI.SelfPlayHarness

## Роль
Senior .NET разработчик в Go Engine.

## Цель
AI может играть партию с самим собой без ошибок.

## Аудит
1. `T-010` done.
2. `GO_RULES.md` — разделы 7, 8.

## Что сделать
1. `GoEngine.AI/SelfPlayHarness.cs`:
   - `GameResult PlayGame(IMoveSelector black, IMoveSelector white, BoardSize size, Komi komi, int seed)`.
   - Внутри — цикл до `GameStatus != InProgress`.
   - Возвращает `GameResult` и полную историю.
2. `GoEngine.AI/SelfPlayResult.cs`:
   - `readonly record struct SelfPlayResult(GameResult Result, IReadOnlyList<Move> Moves, Board FinalBoard)`.
3. Не логировать.

## DoD
- [ ] Harness работает.
- [ ] Тесты (не менее 3):
  - `SelfPlay_9x9_Партия_Завершается`
  - `SelfPlay_Все_Ходы_Легальны`
  - `SelfPlay_Детерминирован_При_Фиксированном_Seed`
- [ ] `dotnet test` зелёный.
- [ ] `STATE.md` обновлён.