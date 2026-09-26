# T-010: AI.IMoveSelector + AI.RandomMoveSelector

## Роль
Senior .NET разработчик в Go Engine.

## Цель
Интерфейс выбора хода + случайный селектор (уровень 30 кю).

## Аудит
1. `PROJECT.md` — границы слоёв.
2. `AGENTS_GO.md` — раздел 7.

## Что сделать
1. `GoEngine.AI/IMoveSelector.cs`:
   - `Move SelectMove(GameState state)`.
   - `string Name { get; }`.
2. `GoEngine.AI/RandomMoveSelector.cs`:
   - Инжектированный `Random`.
   - Собирает все легальные ходы.
   - Если легальных нет — `Move.Pass`.
   - Никогда не ходит в занятую точку.
3. `GoEngine.AI/AiException.cs` — только для нарушения инвариантов.

## НЕ делать
- Не реализовывать MCTS (T-013).
- Не реализовывать уровни (T-017).

## Ограничения
Слой `AI`. Зависит от `Core` и `System.*`.

## DoD
- [ ] Селектор работает.
- [ ] Тесты (не менее 4):
  - `Random_Возвращает_Легальный_Ход`
  - `Random_Не_Ходит_В_Занятую_Точку`
  - `Random_При_Отсутствии_Ходов_Пас`
  - `Random_Детерминирован_При_Фиксированном_Seed`
- [ ] `dotnet test` зелёный.
- [ ] `STATE.md` обновлён.