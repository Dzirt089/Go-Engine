# T-019d: RAVE (Rapid Action Value Estimation)

## Роль
Senior .NET разработчик в Go Engine. Автономный режим.

## Цель
RAVE сглаживает статистику по ходам между симуляциями и ускоряет сходимость MCTS. Классический +1–2 кю.

## Аудит
1. `DECISIONS.md` — D-016.
2. `T-013`, `T-015` — текущий `MctsNode` и UCB1.

## Что нужно сделать
1. Расширить `MctsNode`:
   - `Dictionary<Move, int> RaveVisits`.
   - `Dictionary<Move, double> RaveWins`.
   - Или плоский `int[]`/`double[]` по индексу хода — быстрее.
2. Формула UCB1-RAVE:
   ```
   β = sqrt(k / (3·n + k))          // k = ~1000
   Q_rave = wins_rave / visits_rave
   Q      = wins / visits
   score  = (1-β)·Q + β·Q_rave + c·sqrt(ln(N)/n)
   ```
   - `c = Ucb1C` из `MctsConfig`.
   - `k = MctsConfig.RaveK`, дефолт 1000.
3. `MctsTree.Backpropagate`:
   - Обновить обычные визиты по всему пути.
   - Обновить RAVE-визиты: для каждой симуляции — все ходы, сделанные в playout, получают RAVE-обновление.
4. Тесты.

## Что НЕ нужно делать
- Не менять playout policy (T-019c уже сделал).
- Не переходить на neural eval.

## Ограничения
Слой `AI`. Только `Core` и `System.*`.

## Definition of Done
- [ ] RAVE в `MctsNode` / `MctsTree`.
- [ ] UCB1-RAVE в `Select`.
- [ ] `MctsConfig.RaveK`.
- [ ] Тесты (не менее 6):
  - `Rave_β_Стремится_К_0_При_Больших_N`
  - `Rave_β_Стремится_К_1_При_Малых_N`
  - `Rave_Visits_Обновляются_При_Backpropagate`
  - `Rave_Не_Ломает_Обычный_UCB1_При_K∞`
  - `Rave_Детерминирован_При_Seed`
  - `Rave_Влияет_На_Выбор_Хода` (на позиции с явным фаворитом)
- [ ] Замер: Kyu15 (T-019c) vs Kyu15 (T-019d), 10 партий. Результат в `DECISIONS.md`.
- [ ] `STATE.md`, `DECISIONS.md`, коммит + push.

## Формат ответа
Как в T-019a.