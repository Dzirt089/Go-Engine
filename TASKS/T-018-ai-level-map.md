# T-018: Маппинг уровень → селектор

## Роль
Senior .NET разработчик в Go Engine.

## Цель
Единая точка входа: «дай AI такого-то уровня».

## Аудит
1. `T-017` done.

## Что сделать
1. `GoEngine.AI/AiFactory.cs`:
   - `IMoveSelector Create(DifficultyLevel level, Random random)`.
2. Никаких магических строк — только `DifficultyLevel`.

## DoD
- [ ] `AiFactory` работает.
- [ ] Тесты (не менее 2):
  - `Factory_Создаёт_Корректный_Селектор`
  - `Factory_Недопустимый_Уровень_Бросает`
- [ ] `STATE.md` обновлён.