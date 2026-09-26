# T-025: SGF save/load

## Роль
Senior .NET разработчик в Go Engine.

## Цель
Партия сохраняется и загружается в SGF.

## Что сделать
1. `GoEngine.Core/Sgf/SgfWriter.cs`, `SgfReader.cs`.
2. Поддержка: `SZ`, `KM`, `B`, `W`, `C` (комментарий).
3. Round-trip: save → load → идентичная позиция.

## DoD
- [ ] Round-trip работает.
- [ ] Тесты (не менее 3).
- [ ] `STATE.md` обновлён.