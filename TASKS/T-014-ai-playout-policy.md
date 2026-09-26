# T-014: AI.PlayoutPolicy

## Роль
Senior .NET разработчик в Go Engine.

## Цель
Быстрая политика для playout'ов — random + эвристики.

## Аудит
1. `GO_RULES.md` — разделы 4, 5, 6.
2. `T-013` done.

## Что сделать
1. `GoEngine.AI/PlayoutPolicy.cs`:
   - `Move SelectMove(GameState state, Random random)`.
   - Эвристики в порядке приоритета:
     1. Съесть группу в атари (если ход разрешён).
     2. Спасти свою группу в атари.
     3. С вероятностью X% — «соседний» ход (рядом с камнями).
     4. Иначе — случайный легальный ход.
   - Не вызывать MCTS внутри playout.
2. `PlayoutConfig`:
   - `readonly record struct PlayoutConfig(double AtariProbability, double NeighborProbability)`.
   - Дефолты: `AtariProbability = 0.9`, `NeighborProbability = 0.5`.

## DoD
- [ ] `PlayoutPolicy` работает.
- [ ] Тесты (не менее 4):
  - `Playout_Всегда_Легальный_Ход`
  - `Playout_Съедает_Атари_Если_Возможно`
  - `Playout_Спасает_Атари_Если_Возможно`
  - `Playout_Детерминирован_При_Фиксированном_Seed`
- [ ] `dotnet test` зелёный.
- [ ] `STATE.md` обновлён.