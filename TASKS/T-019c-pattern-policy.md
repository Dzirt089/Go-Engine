# T-019c: Pattern-based playout policy

## Роль
Senior .NET разработчик в Go Engine. Автономный режим.

## Цель
Playout перестаёт быть «случайным + пара эвристик» и становится «приоритизированным по 3×3-паттернам». Это главный вклад в силу.

## Аудит
1. `GO_RULES.md` — п. 4, 5, 6.
2. `DECISIONS.md` — D-014, D-016.
3. `T-019b` — eye-safe playout уже есть.

## Что нужно сделать
1. `GoEngine.AI/Patterns/Pattern3x3.cs`:
   - `readonly record struct Pattern3x3(ushort Code)`.
   - Кодировка: 9 клеток × 2 бита = 18 бит. Значения: 0 — пусто, 1 — свой, 2 — чужой, 3 — край/вне доски.
   - `Pattern3x3 FromBoard(Board board, Point center, StoneColor me)`.
   - `ushort Code` — уникальный код паттерна.
2. `GoEngine.AI/Patterns/PatternWeights.cs`:
   - `IReadOnlyDictionary<ushort, double>` — заранее подобранные веса.
   - Начать с консервативных: 3×3 паттерны из Crazy Stone / GNU Go (публично документированы). Не выдумывать — использовать известные веса из открытых источников.
   - Альтернатива: веса = 0.5 для всех, кроме явно плохих; подкрутить после замеров.
3. `GoEngine.AI/Patterns/PatternPolicy.cs`:
   - `Move SelectMove(GameState state, Random random, TimeProvider timeProvider)`.
   - Собирает все легальные ходы.
   - Для каждого — код паттерна 3×3, вес из `PatternWeights`.
   - Дополнительные эвристики (в порядке убывания приоритета):
     1. **Atari capture.** Ход снимает ≥1 камень — приоритет максимальный.
     2. **Atari escape.** Ход спасает свою группу в атари.
     3. **Eye-block.** Ход не даёт противнику сделать глаз (заполняет потенциальный глаз противника).
     4. **Cut.** Ход разрезает две чужие группы.
     5. **Extend.** Ход продлевает свою группу.
     6. **Pattern weight** из таблицы.
     7. **Random** — если ничего не сработало.
   - Взвешенный случайный выбор по `weight` (не argmax — иначе теряем разнообразие).
4. `PlayoutPolicy` — заменить внутреннюю логику на делегирование `PatternPolicy`.

## Что НЕ нужно делать
- Не обучать веса. Только известные из открытых источников.
- Не делать нейросеть. Только таблица.
- Не реализовывать RAVE (это T-019d).

## Ограничения
Слой `AI`. Только `Core` и `System.*`.
Один новый файл с весами не должен превышать 2000 строк (иначе сократить таблицу).

## Definition of Done
- [ ] `Pattern3x3` и `PatternWeights` реализованы.
- [ ] `PatternPolicy` учитывает 5 эвристик.
- [ ] `PlayoutPolicy` делегирует.
- [ ] Тесты (не менее 10):
  - `Pattern3x3_Симметрия_Углов`
  - `Pattern3x3_Код_Стабильный`
  - `PatternPolicy_Atari_Capture_Приоритет`
  - `PatternPolicy_Atari_Escape_Приоритет`
  - `PatternPolicy_Cut_Распознан`
  - `PatternPolicy_Extend_Распознан`
  - `PatternPolicy_Eye_Block_Распознан`
  - `PatternPolicy_Все_Ходы_Легальны`
  - `PatternPolicy_Детерминирован_При_Seed`
  - `PatternPolicy_Не_Зацикливается_На_Позиции_Без_Ходов`
- [ ] Замер: Kyu15 (T-019b) vs Kyu15 (T-019c), 10 партий. Результат в `DECISIONS.md`.
- [ ] `STATE.md`, `DECISIONS.md`, коммит + push.

## Формат ответа
Как в T-019a.