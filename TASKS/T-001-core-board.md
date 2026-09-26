# T-001: Core.Board + Core.Move

## Роль

Ты — senior .NET разработчик в проекте Go Engine.
Стиль: `AGENTS.md`, `AGENTS_GO.md`. Правила: `GO_RULES.md`. Границы: `PROJECT.md`.

## Цель

После этой задачи можно:
- Создать доску размера 19×19, 13×13 или 9×9.
- Поставить камень на пустую точку.
- Прочитать цвет в любой точке.
- Получить список всех занятых и пустых точек.

## Аудит перед началом

1. Прочитать `GO_RULES.md` — разделы 1, 2, 3.
2. Прочитать `STATE.md` — что уже сделано.
3. Прочитать `DECISIONS.md` — D-004, D-005, D-006.
4. Просмотреть `samples/` — стиль.
5. Проверить, нет ли уже готовых `Point`, `StoneColor`, `Board`.

## Что нужно сделать

1. Создать solution `GoEngine.sln`.
2. Создать проект `GoEngine.Core` (class library, .NET 8).
3. Создать проект `GoEngine.Tests` (xUnit).
4. Реализовать `GoEngine.Core/BoardSize.cs`:
   - `readonly record struct BoardSize(byte Value)`.
   - Валидация: только 9, 13, 19.
   - Статические: `BoardSize.Size9`, `BoardSize.Size13`, `BoardSize.Size19`.
   - `int Area => Value * Value`.
5. Реализовать `GoEngine.Core/Point.cs`:
   - `readonly record struct Point(byte X, byte Y)`.
   - Метод `bool IsOnBoard(BoardSize size)`.
   - Метод `IEnumerable<Point> Neighbors(BoardSize size)` — 2–4 соседа по стороне.
   - `override ToString()` — в формате «D4» для 19×19 (буква без `I`).
6. Реализовать `GoEngine.Core/StoneColor.cs` (Enumeration):
   - `Empty`, `Black`, `White`.
   - `StoneColor Opponent()` — для `Black`/`White`, бросает `DomainException` для `Empty`.
   - Поля: `Id` (int), `Name` (string).
7. Реализовать `GoEngine.Core/MoveType.cs` (Enumeration):
   - `Play`, `Pass`, `Resign`.
8. Реализовать `GoEngine.Core/Move.cs`:
   - `readonly record struct Move`.
   - Поля: `MoveType Type`, `Point Point`, `StoneColor Color`.
   - Фабрики: `Move.Play(Point, StoneColor)`, `Move.Pass(StoneColor)`, `Move.Resign(StoneColor)`.
   - `static Move None` — для «нет хода».
9. Реализовать `GoEngine.Core/Board.cs`:
   - Приватное поле `StoneColor[] _stones` (плоский массив).
   - Публичное свойство `BoardSize Size`.
   - Конструктор `Board(BoardSize size)` — пустая доска.
   - Приватный конструктор для клонирования.
   - `StoneColor At(Point p)`.
   - `bool IsEmpty(Point p)`.
   - `Board ApplyMove(Move move)` — иммутабельно, возвращает новую доску.
     - Для `Pass`/`Resign` — возвращает копию без изменений (проверка легальности будет в T-002).
     - Для `Play` — ставит камень, **без** снятия групп (снятие — T-002).
     - Если точка занята — `DomainException`.
   - `Board Clone()` — быстрая копия.
   - `void MakeMove(Move move)` — мутация на месте (для MCTS), только `Play` на пустую точку.
   - `IEnumerable<Point> AllPoints()` — все точки.
   - `IEnumerable<Point> EmptyPoints()` — пустые точки.
   - `IEnumerable<Point> OccupiedPoints(StoneColor color)` — занятые точкой цвета.

## Что НЕ нужно делать

- Не реализовывать снятие групп (T-002).
- Не реализовывать проверку самоубийства (T-003).
- Не реализовывать ко (T-005).
- Не реализовывать подсчёт (T-007).
- Не использовать `int[,]`.
- Не использовать `enum` для `StoneColor`, `MoveType`.
- Не логировать.
- Не использовать `DateTime.Now`.

## Ограничения

- Слой: `Core`.
- Зависимости: только `System.*`.
- Новые NuGet-пакеты: только `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` в тестовом проекте.
- .NET 8, C# 12.

## Definition of Done

- [ ] Solution и оба проекта созданы.
- [ ] Все 6 классов реализованы.
- [ ] Юнит-тесты (не менее 15):
  - `BoardSize_Создаётся_с_валидным_размером`
  - `BoardSize_Бросает_при_невалидном_размере`
  - `Point_IsOnBoard_Возвращает_корректно`
  - `Point_Neighbors_Угол_Возвращает_двух_соседей`
  - `Point_Neighbors_Край_Возвращает_трёх_соседей`
  - `Point_Neighbors_Центр_Возвращает_четырёх_соседей`
  - `Point_ToString_19x19_Формат_A1`
  - `Point_ToString_19x19_Пропускает_букву_I`
  - `StoneColor_Opponent_Возвращает_противоположный`
  - `StoneColor_Opponent_Для_Empty_Бросает`
  - `Move_Play_Создаёт_корректно`
  - `Move_Pass_Создаёт_корректно`
  - `Move_Resign_Создаёт_корректно`
  - `Board_Новая_Пустая`
  - `Board_ApplyMove_Ставит_камень`
  - `Board_ApplyMove_Возвращает_Новую_Доску_Не_Мутирует_Исходную`
  - `Board_ApplyMove_На_Занятую_Точку_Бросает`
  - `Board_MakeMove_Мутирует_На_Месте`
  - `Board_Clone_Независимая_Копия`
  - `Board_EmptyPoints_Корректно`
  - `Board_OccupiedPoints_Корректно`
- [ ] `dotnet build` — без ошибок и предупреждений.
- [ ] `dotnet test` — зелёный.
- [ ] `STATE.md` обновлён (задача T-001 → done, T-002 → next).
- [ ] В ответе перечислены применённые правила из `AGENTS.md` и `AGENTS_GO.md`.

## Формат ответа

1. Слой, папка, имя файла (для каждого файла).
2. Полный код файла (не diff, не «...»).
3. Полный код тестов.
4. Список применённых правил.
5. Обновлённый `STATE.md` (полностью).
6. Вопросы (если есть) — одним блоком в конце.

## Если данных не хватает

Остановиться. Задать **один** вопрос. Не додумывать.