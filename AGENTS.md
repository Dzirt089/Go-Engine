# Go Engine — Style Bible

> Правила обязательны для всего кода в репозитории.
> При конфликте правил и примеров из `samples/` — приоритет у samples.
> Правила игры — `GO_RULES.md`. Границы проекта — `PROJECT.md`.
> Состояние — `STATE.md`. Решения — `DECISIONS.md`.

## 1. Архитектура

Слои строго по зависимостям: `Core ← AI ← App`.

| Слой | Ссылается на | Запрещено |
|---|---|---|
| `GoEngine.Core` | `System.*` | Avalonia, AI, IO, БД |
| `GoEngine.AI` | `Core`, `System.*` | Avalonia, IO, БД |
| `GoEngine.App` | `Core`, `AI`, Avalonia, SkiaSharp | — |
| `GoEngine.Tests` | `Core`, `AI`, xUnit | `App` |

- **Core** — `Board`, `Move`, `Point`, `Group`, `GroupTracker`, `KoRule`, `Scorer`, `GameState`, `Result`.
- **AI** — `IMoveSelector`, MCTS, эвристики, `DifficultyLevel`.
- **App** — Avalonia, SkiaSharp, Views, ViewModels, SGF-персистенция, настройки.

**Правило:** слой не может ссылаться на слой выше себя. Нарушение — критический баг архитектуры.

## 2. Базовые типы

- `Point`, `Move`, `Komi`, `Score`, `BoardSize` — `readonly record struct`.
- `StoneColor`, `MoveType`, `GameStatus`, `DifficultyLevel` — `Enumeration` (не `enum`).
- `Enumeration`: поля `Id` (int), `Name` (string), `Descriptions` (опционально). Поиск — `FromName<T>`, `FromId<T>`.
- `default(Point)` — валидная точка `(0,0)`. Не использовать как «нет значения». Для «нет значения» — `Point?` или `Move.None`.

## 3. Board — контракт

- Публичный API **иммутабелен**: `ApplyMove(Move) → Board` возвращает новую доску.
- Внутреннее представление — плоский `StoneColor[]` размером `size * size`. **Не `int[,]`, не `StoneColor[,]`.**
- Для MCTS — `Clone()` + `MakeMove(Move)` (мутация на месте) для скорости.
- Публичный API **никогда** не отдаёт внутренний массив.
- `CapturedStones` — результат последнего `ApplyMove`, только для чтения.

## 4. Результаты и исключения

- Ожидаемые исходы (нелегальный ход, ко) — `Result` / `Result<T>`, **не исключения**.
- Нарушение инварианта движка (отрицательный размер, рассинхрон групп) → `DomainException`.
- Ошибки ввода-вывода (SGF не найден) → `Result<T>` с ошибкой.
- Никогда не бросать `Exception` / `ApplicationException` / `InvalidOperationException` в `Core` и `AI`.
- Сообщения исключений — на русском.

## 5. Код-стиль

- `sealed` на всех классах, которые не предназначены для наследования.
- File-scoped namespace.
- Primary constructors там, где уместно.
- Приватные поля — `_camelCase`. Параметры лямбд — `_`, если не используются.
- Проверки на входе: `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIfNegativeOrZero`.
- Возвращать `IReadOnlyList<T>` (через `.AsReadOnly()`), **не `List<T>`**.
- Коллекции инициализировать `[]`, не `new List<T>()` в теле.
- Публичные API `Core` — синхронные. `async` в `Core` не нужен.
- `AI` — синхронный, `ValueTask<Move>` там, где есть смысл.

## 6. Время и случайность

- **Никаких `DateTime.Now` / `DateTime.UtcNow` напрямую.** Только `TimeProvider`.
- **Никаких `new Random()` в коде AI.** Только инжектированный `Random` или `Random` с seed.
- MCTS детерминирован при фиксированном seed. Тесты используют seed.

## 7. Логи

- **В `Core` и `AI` — никаких логов.** Логи только в `App` и в тестах.
- Только `[LoggerMessage]` source generator.
- Сообщения — на русском, структурированные параметры.

## 8. Документация и комментарии

- XML-доки на русском: `<summary>`, `<param>`, `<returns>`, `<remarks>` где нужно.
- Комментарии в коде — **на русском**, объясняют **почему**, а не «что».
- Ссылки на регрессии — можно («Регрессия G-K1: ...»).
- В `Core` комментарии особенно важны: сложные места (суперко, сэки, подсчёт) должны быть объяснены.

## 9. Тесты

- xUnit + Moq (Moq только там, где он реально нужен; для `Core` — не нужен).
- Имена тестов — на русском, описывают сценарий: `Группа_без_дамэ_снимается`, `Ход_в_ко_запрещён`.
- Один тест — одно утверждение (кроме случаев проверки состояния графа).
- Тесты правил — на позициях из `GO_RULES.md`, раздел 13.
- Тесты AI — детерминированные (фиксированный seed), без «плавающих» порогов.
- Интеграционные тесты AI vs AI — не менее 10 партий на 9×9.
- Комментарии к тестам — регрессия: «Регрессия G-K1: ...».

## 10. SGF и персистенция

- Только `System.Text.Json` для настроек. Никакого Newtonsoft.
- SGF — по стандарту FF[4], минимальная поддержка: `GM[1]`, `SZ`, `KM`, `B`, `W`, `C`, `RE`.
- Никаких ORM, никакой БД в v1.

## 11. Запрещено

- `async void`.
- `.Result` / `.Wait()`.
- `catch (Exception) { }` без логирования и обоснования.
- Магические строки/числа — только `Enumeration` или `const`.
- `public` поля.
- AutoMapper, MediatR, FluentValidation, Mapster.
- `enum` для `StoneColor`, `MoveType`, `GameStatus`, `DifficultyLevel`.
- `int[,]`, `StoneColor[,]` для доски.
- `DateTime.Now`, `DateTime.UtcNow` напрямую (только `TimeProvider`).
- `new Random()` без seed в `AI`.
- Логи в `Core` и `AI`.
- Пакеты, не указанные в `PROJECT.md`.

## 12. Формат ответа при генерации кода

1. Сначала — какой слой, какая папка, какое имя файла.
2. Затем — **полный файл целиком** (не diff, не «...»).
3. Затем — полный код тестов к этому файлу.
4. В конце — коротко: «Какие правила из Style Bible применены».
5. Если правило из Style Bible противоречит запросу — **спросить, а не догадываться**.
6. Если данных не хватает — задать **один** вопрос и остановиться.

## 13. Что делать при неопределённости

1. Открыть `GO_RULES.md` — найти соответствующий раздел.
2. Открыть `samples/` — найти ближайший по смыслу файл. Копировать структуру, имена, комментарии, порядок членов класса.
3. Открыть `DECISIONS.md` — возможно, вопрос уже решён.
4. Если ничего не подходит — задать один вопрос и остановиться.