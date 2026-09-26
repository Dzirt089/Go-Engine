# Go Engine — дополнение к Style Bible

> Это дополнение к `AGENTS.md`. При конфликте — приоритет у `AGENTS.md`.
> `AGENTS.md` описывает общий стиль, этот файл — Go-специфику.

## 1. Слои

- `GoEngine.Core` — **не ссылается ни на что, кроме `System.*`**.
- `GoEngine.AI` — ссылается только на `Core`.
- `GoEngine.App` — ссылается на `Core`, `AI`, Avalonia, SkiaSharp.
- `GoEngine.Tests` — ссылается на `Core`, `AI`, xUnit, Moq.

Нарушение ссылок — критический баг архитектуры.

## 2. Value-типы

- `Point`, `Move`, `Komi`, `Score`, `BoardSize` — `readonly record struct`.
- Не использовать `class` для value-типов.
- `default(Point)` — валидная точка `(0,0)`, не использовать как «нет значения». Для «нет значения» — `Point?` или `Move.None`.

## 3. `Board`

- Публичный API иммутабелен: `ApplyMove` возвращает новый `Board`.
- Внутреннее представление — массив `StoneColor[]` размером `size * size` (плоский, не `[,]`).
- Для MCTS — `Clone()` + `MakeMove()` (мутация на месте) для скорости.
- Публичный API `Board` **никогда** не отдаёт внутренний массив.

## 4. `Move`

- `record struct Move` с полями `Type`, `Point`, `Color`.
- `MoveType` — `Enumeration`: `Play`, `Pass`, `Resign`.
- Фабрики: `Move.Play(Point, StoneColor)`, `Move.Pass(StoneColor)`, `Move.Resign(StoneColor)`.

## 5. `Enumeration`

- `StoneColor` — `Empty`, `Black`, `White`. У `Black`/`White` есть метод `Opponent()`.
- `GameStatus` — `InProgress`, `FinishedByTwoPasses`, `FinishedByResign`, `FinishedByMoveLimit`.
- `DifficultyLevel` — от `Kyu30` до `Kyu10` (в v1), поля: `Id`, `Name`, `RankKyu`, `PlayoutBudget`.

## 6. Ошибки

- Нелегальный ход — **не исключение**. Возвращать `Result<MoveResult>` или `MoveResult.Invalid(reason)`.
- Нарушение инварианта движка (например, отрицательный размер доски) — `DomainException`.
- Ошибки ввода-вывода (файл SGF не найден) — `Result<T>` с ошибкой.

## 7. MCTS

- Детерминирован при фиксированном `seed`.
- `Random` — только через инжектированный `Random`, не через `new Random()`.
- Все времена — через `TimeProvider`, не `DateTime.Now`.
- Бюджет playout'ов — параметр, не константа в коде.

## 8. Логи

- Только через `[LoggerMessage]` source generator.
- На русском.
- В `Core` и `AI` — **никаких логов**. Логи только в `App` и в тестах.

## 9. Тесты

- xUnit.
- Имена на русском: `Ход_в_ко_запрещён`, `Группа_без_дамэ_снимается`, `MCTS_находит_ход_в_цумэго`.
- Один тест — одно утверждение (кроме случаев проверки состояния графа).
- Тесты правил — на позициях из `GO_RULES.md` (раздел 13).
- Тесты AI — детерминированные (фиксированный seed).
- Интеграционные тесты AI vs AI — не менее 10 партий на 9×9.

## 10. Запрещено

- `int[,]` для доски — только `StoneColor[]` (плоский).
- `DateTime.Now` / `DateTime.UtcNow` напрямую — только `TimeProvider`.
- `new Random()` без seed в коде AI.
- Логи в `Core` и `AI`.
- `async void`, `.Result`, `.Wait()`.
- `catch (Exception) { }`.
- `enum` для `StoneColor`, `GameStatus`, `DifficultyLevel`.
- AutoMapper, MediatR, FluentValidation.
- Пакеты, не указанные в `PROJECT.md`.

## 11. Формат ответа

1. Сначала — какой слой, какая папка, какое имя файла.
2. Затем — полный файл целиком.
3. Затем — полный текст тестов к файлу.
4. В конце — «Какие правила из Style Bible и AGENTS_GO применены».
5. Если правило конфликтует с запросом — спросить, не догадываться.

## 12. Что делать при неопределённости

- Открыть `GO_RULES.md`, найти соответствующий раздел.
- Открыть `samples/`, найти ближайший по смыслу файл.
- Если ничего не подходит — задать один вопрос и остановиться.