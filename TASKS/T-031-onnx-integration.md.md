# T-031: ONNX Runtime integration

## Роль
Senior .NET разработчик в Go Engine.

## Цель
Подключить ONNX Runtime для будущей нейросетевой оценки (v2).

## Что сделать
1. Добавить `Microsoft.ML.OnnxRuntime`.
2. `GoEngine.AI.Onnx/OnnxEvaluator.cs` — заглушка с интерфейсом.
3. Не подключать к MCTS пока.

## DoD
- [ ] ONNX Runtime работает (загрузка пустой модели).
- [ ] `STATE.md` обновлён.