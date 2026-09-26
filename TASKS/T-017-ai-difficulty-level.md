# T-017: AI.DifficultyLevel (Enumeration)

## Роль
Senior .NET разработчик в Go Engine.

## Цель
Полный `Enumeration` уровней 30 кю – 5 кю с параметрами.

## Аудит
1. `T-012` done (базовая версия).
2. `GO_RULES.md`.
3. `DECISIONS.md` — D-003.

## Что сделать
1. Расширить `GoEngine.AI/DifficultyLevel.cs`:
   - Уровни: `Kyu30`, `Kyu25`, `Kyu20`, `Kyu15`, `Kyu10`, `Kyu8`, `Kyu5`.
   - Поля: `Id`, `Name`, `RankKyu`, `Kind` (Random/Heuristic/Mcts), `PlayoutBudget`, `Ucb1C`, `PlayoutConfig`.
2. `DifficultyLevel.CreateSelector(Random) → IMoveSelector`.

## DoD
- [ ] 7 уровней.
- [ ] Тесты (не менее 3):
  - `DifficultyLevel_Все_Уровни_Создают_Селектор`
  - `Kyu30_Использует_Random`
  - `Kyu5_Использует_Mcts_С_Большим_Бюджетом`
- [ ] `STATE.md` обновлён.