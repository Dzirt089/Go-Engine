# T-007: Core.Scorer (китайские правила)

## Роль
Senior .NET разработчик в Go Engine.

## Цель
После этой задачи движок точно считает очки по китайским правилам.

## Аудит
1. `GO_RULES.md` — раздел 9.
2. `DECISIONS.md` — D-002.

## Что сделать
1. `GoEngine.Core/Score.cs`:
   - `readonly record struct Score(double Black, double White)`.
   - `StoneColor Winner` — `Black` / `White` / `Empty` (ничья).
   - `double Margin`.
2. `GoEngine.Core/Scorer.cs`:
   - Статический класс.
   - `Score Calculate(Board board, Komi komi)`.
   - Алгоритм: flood-fill от каждой пустой точки; если все пути выходят к краю через точки одного цвета — точка принадлежит этому цвету.
   - Камни на доске считаются автоматически.
3. `GoEngine.Core/Komi.cs`:
   - `readonly record struct Komi(double Value)`.
   - Валидация: ≥ 0.
   - Статические: `Komi.For19x19`, `Komi.For13x13`, `Komi.For9x9` (значения из `GO_RULES.md` раздел 10).
4. Сэки — при китайских правилах нейтральные точки не считаются ни за кого, специальной обработки нет.

## НЕ делать
- Не реализовывать японские правила.
- Не снимать мёртвые камни.
- Не реализовывать `GameState` (T-008).

## Ограничения
Слой `Core`. Только `System.*`.

## DoD
- [ ] `Scorer.Calculate` работает.
- [ ] Тесты (не менее 8):
  - `Score_Пустая_Доска_9x9_Все_Белые`
  - `Score_Все_Чёрные_9x9_Победа_Чёрных`
  - `Score_Территория_В_Углу`
  - `Score_Территория_В_Центре`
  - `Score_Смешанная_Позиция`
  - `Score_Сэки_Нейтральные_Не_Считаются`
  - `Komi_Валидация`
  - `Komi_Стандартные_Значения`
- [ ] `dotnet test` зелёный.
- [ ] `STATE.md` обновлён.