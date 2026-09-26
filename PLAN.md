# План разработки

8 фаз. Каждая фаза — несколько задач. Задача = отдельный файл в `TASKS/`.

## Фаза 1. Core: доска и ходы
**Deliverable:** `Board`, `Move`, `MoveGenerator`. Можно поставить камень, проверить легальность, снять группу.

- T-001. `Core.Board` + `Core.Move`
- T-002. `Core.GroupTracker` + `Core.Capture`
- T-003. `Core.SuicideCheck`

## Фаза 2. Core: ко и история
**Deliverable:** позиционное суперко работает, история позиций ведётся.

- T-004. `Core.PositionHistory`
- T-005. `Core.KoRule` (positional superko)
- T-006. Интеграционные тесты правил (все 8 позиций из `GO_RULES.md`)

## Фаза 3. Core: подсчёт и партия
**Deliverable:** партия играется от начала до конца, подсчёт корректен.

- T-007. `Core.Scorer` (китайские правила)
- T-008. `Core.GameState` (ход, пас, resign, коми)
- T-009. `Core.Result` (Result<T>, Score, GameResult)

## Фаза 4. AI: случайный
**Deliverable:** AI играет легальные ходы, партия с самим собой проходит без ошибок.

- T-010. `AI.IMoveSelector` + `AI.RandomMoveSelector`
- T-011. `AI.SelfPlayHarness` (партия AI vs AI)
- T-012. Уровни 30–20 кю (случайный + эвристика «съесть в атари»)

## Фаза 5. AI: MCTS
**Deliverable:** MCTS находит тактические ходы на 9×9.

- T-013. `AI.MctsNode`, `AI.MctsTree`
- T-014. `AI.PlayoutPolicy` (random + эвристики)
- T-015. `AI.MctsMoveSelector` (UCB1, бюджет playout'ов)
- T-016. Тесты на цумэго (9×9)

## Фаза 6. AI: уровни сложности
**Deliverable:** 30 кю – 10 кю, слабый AI проигрывает сильному.

- T-017. `AI.DifficultyLevel` (Enumeration)
- T-018. Маппинг уровень → (селектор, бюджет)
- T-019. Интеграционный тест: 15 кю vs 10 кю, 10 партий

## Фаза 7. UI
**Deliverable:** доска рисуется, клики работают, партия играется.

- T-020. Avalonia-проект, окно, Skia-контрол
- T-021. Рендеринг доски и камней
- T-022. Клики, hover, последний ход
- T-023. Панель статуса (коми, счёт, кто ходит)
- T-024. Меню: новая партия, размер доски, уровень AI

## Фаза 8. Интеграция и полировка
**Deliverable:** игра готова к релизу.

- T-025. SGF save/load
- T-026. Undo/Redo
- T-027. Анимация камней и захвата
- T-028. Настройки (persist в JSON)
- T-029. Сборка под Windows/macOS/Linux
- T-030. Финальный smoke-тест

## Фаза 9 (опционально, после релиза). ONNX
**Deliverable:** подключение предобученной KataGo-модели для уровня 5 кю – 1 дан.

- T-031. ONNX Runtime integration
- T-032. KataGo feature encoding
- T-033. MCTS + neural network evaluation

## Метрики прогресса

- Каждая фаза заканчивается работающим `dotnet test`.
- Каждая фаза заканчивается обновлённым `STATE.md`.
- Ни одна задача не считается закрытой без интеграционного теста.