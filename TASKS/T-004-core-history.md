# T-004: Core.PositionHistory

## Роль
Senior .NET разработчик в Go Engine.

## Цель
После этой задачи движок ведёт историю позиций для суперко.

## Аудит
1. `GO_RULES.md` — раздел 6.
2. `STATE.md`.
3. `DECISIONS.md`.

## Что сделать
1. `GoEngine.Core/PositionHash.cs`:
   - `readonly record struct PositionHash(ulong Hash)`.
   - Статический метод `PositionHash From(Board board)` — Zobrist hashing.
   - Таблица Zobrist генерируется один раз (статические readonly поля) с фиксированным seed для детерминизма.
2. `GoEngine.Core/PositionHistory.cs`:
   - Класс (не record).
   - Внутри `HashSet<PositionHash>`.
   - `void Add(Board board)`.
   - `bool Contains(Board board)`.
   - `int Count`.
   - `IReadOnlyCollection<PositionHash> Hashes`.
3. Расширить `Board`:
   - `Board WithHistory(PositionHistory history)` — возвращает копию с привязанной историей.
   - `PositionHistory? History { get; }`.
4. Zobrist: `ulong[colorIndex, pointIndex]` для двух цветов × `BoardSize.Area`.

## НЕ делать
- Не реализовывать сам ко-запрет (T-005).
- Не делать историю мутабельной снаружи.

## Ограничения
Слой `Core`. Только `System.*`.

## DoD
- [ ] `PositionHash` детерминирован (один и тот же board → один и тот же hash).
- [ ] `PositionHistory` хранит и проверяет.
- [ ] Тесты (не менее 6):
  - `PositionHash_Детерминирован`
  - `PositionHash_Разные_Позиции_Разные_Хеши`
  - `PositionHash_Одинаковые_Позиции_Одинаковые_Хеши`
  - `History_Содержит_Добавленное`
  - `History_Не_Содержит_Не_Добавленное`
  - `History_Count_Корректен`
- [ ] `dotnet test` зелёный.
- [ ] `STATE.md` обновлён.