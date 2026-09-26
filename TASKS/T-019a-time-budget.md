# T-019a: Time-based budget для MCTS

## Роль
Senior .NET разработчик в Go Engine. Автономный режим. Правила — `GO_RULES.md`, `AGENTS.md`, `AGENTS_GO.md`.

## Цель
MCTS думает не «N playout'ов», а «T миллисекунд». Это делает силу предсказуемой на разных машинах.

## Аудит
1. `DECISIONS.md` — D-014 (живучие playout'ы), D-016 (план серии).
2. `STATE.md` — T-019 closed_with_caveats.
3. `T-015` — текущая реализация `MctsMoveSelector`.

## Что нужно сделать
1. Расширить `MctsConfig`:
   - `int? PlayoutBudget` — старое поле, для детерминированных тестов.
   - `TimeSpan? TimeBudget` — новое, для игры.
   - `double Ucb1C` — без изменений.
   - **Ровно одно из двух должно быть задано.** Оба заданы — `DomainException`. Ни одно — тоже.
2. `MctsMoveSelector`:
   - Инжектировать `TimeProvider` (не `DateTime.Now`, не `Stopwatch`).
   - В цикле: `while (timeProvider.GetUtcNow() - start < TimeBudget) { ... }`.
   - Если `TimeBudget` не задан, но задан `PlayoutBudget` — старый цикл по счётчику.
   - После завершения — вернуть ход с максимальным числом посещений.
3. `MctsConfig.DefaultForSize(BoardSize)`:
   - 9×9: 2000 мс.
   - 13×13: 4000 мс.
   - 19×19: 6000 мс.
4. `AiFactory` — уровни 10/8/5 кю теперь используют `TimeBudget`, а не `PlayoutBudget`.
5. Тесты — через `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing).

## Что НЕ нужно делать
- Не менять playout policy (это T-019b).
- Не менять формулу UCB1 (это T-019d).
- Не вводить `CancellationToken` — это не отмена, это естественное завершение.

## Ограничения
- Слой `AI`. Зависит от `Core` и `System.*`.
- Новый пакет: `Microsoft.Extensions.TimeProvider.Testing` (только в тестах) — добавить в `PROJECT.md`.

## Definition of Done
- [ ] `MctsConfig` поддерживает оба режима, валидация работает.
- [ ] `MctsMoveSelector` использует `TimeProvider`.
- [ ] `AiFactory` переведён на `TimeBudget`.
- [ ] Тесты (не менее 6):
  - `Mcts_Останавливается_По_TimeBudget` (FakeTimeProvider)
  - `Mcts_Останавливается_По_PlayoutBudget` (старый режим)
  - `MctsConfig_Оба_Заданы_Бросает`
  - `MctsConfig_Ни_Одно_Не_Задано_Бросает`
  - `DefaultForSize_Корректные_Значения`
  - `Mcts_Детерминирован_В_Playout_Режиме`
- [ ] `dotnet build` — 0 предупреждений, `dotnet test` — зелёный.
- [ ] `STATE.md` обновлён, `DECISIONS.md` — запись D-017.
- [ ] Коммит + push в `origin`.

## Формат ответа
Слой → файл → код → тесты → применённые правила → STATE.md → DECISIONS.md → git hash.