# T-008: Core.GameState

## Роль
Senior .NET разработчик в Go Engine.

## Цель
После этой задачи можно играть партию: ход, пас, resign, коми, завершение.

## Аудит
1. `GO_RULES.md` — разделы 7, 8, 10.
2. `DECISIONS.md` — D-002.

## Что сделать
1. `GoEngine.Core/GameStatus.cs` (Enumeration):
   - `InProgress`, `FinishedByTwoPasses`, `FinishedByResign`, `FinishedByMoveLimit`.
2. `GoEngine.Core/GameResult.cs`:
   - `readonly record struct GameResult(GameStatus Status, Score? Score, StoneColor? Winner, string? Reason)`.
3. `GoEngine.Core/GameState.cs`:
   - Класс.
   - Поля: `Board Board`, `StoneColor ToMove`, `Komi Komi`, `int MoveNumber`, `int MoveLimit`, `GameStatus Status`, `IReadOnlyList<Move> Moves`, `PositionHistory History`.
   - `GameState NewGame(BoardSize size, Komi komi, int moveLimit = 0)`.
   - `Result<MoveResult> Play(Move move)` — применить ход, обновить состояние, проверить завершение.
   - `Result<GameResult> Finish()` — принудительное завершение (после двух пасов).
   - Автоматическое завершение при `MoveNumber >= MoveLimit` (если `MoveLimit > 0`).
   - Автоматическое завершение при двух последовательных пасах.
4. `BoardSize.DefaultMoveLimit` — `2 * size.Area`.

## НЕ делать
- Не реализовывать UI.
- Не реализовывать AI.

## DoD
- [ ] `GameState` работает.
- [ ] Тесты (не менее 10):
  - `NewGame_Начинает_С_Чёрных`
  - `Play_Меняет_Цвет`
  - `Play_Увеличивает_Номер_Хода`
  - `Pass_Один_Не_Завершает`
  - `Pass_Два_Завершает_По_Правилу_Двух_Пасов`
  - `Resign_Завершает_Победой_Соперника`
  - `MoveLimit_Завершает_Партию`
  - `Play_Нелегальный_Ход_Возвращает_Ошибку`
  - `Finish_Возвращает_Корректный_Score`
  - `Moves_Содержит_Все_Ходы`
- [ ] `dotnet test` зелёный.
- [ ] `STATE.md` обновлён.