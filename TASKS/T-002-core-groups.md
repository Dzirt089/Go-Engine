# T-002: Core.GroupTracker + Core.Capture

## Роль

Ты — senior .NET разработчик в проекте Go Engine.
Стиль: `AGENTS.md`, `AGENTS_GO.md`. Правила: `GO_RULES.md`. Границы: `PROJECT.md`.

## Цель

После этой задачи можно:
- Найти группу камней, к которой принадлежит точка.
- Получить все дамэ группы.
- Снять группу без дамэ после хода.

## Аудит перед началом

1. Прочитать `GO_RULES.md` — разделы 4, 5, 13.
2. Прочитать `STATE.md` — что уже сделано.
3. Прочитать `DECISIONS.md`.
4. Просмотреть `samples/`.
5. Проверить, что `Board` из T-001 реализован и тесты зелёные.

## Что нужно сделать

1. Реализовать `GoEngine.Core/Group.cs`:
   - `readonly record struct Group(StoneColor Color, IReadOnlyList<Point> Stones, IReadOnlySet<Point> Liberties)`.
   - `bool IsInAtari => Liberties.Count == 1`.
   - `bool IsCaptured => Liberties.Count == 0`.
2. Реализовать `GoEngine.Core/GroupTracker.cs`:
   - Статический класс.
   - `Group FindGroup(Board board, Point p)` — BFS по стороне, только камни того же цвета.
   - `IReadOnlySet<Point> FindLiberties(Board board, Group group)`.
   - `IReadOnlyList<Group> AllGroups(Board board, StoneColor color)`.
3. Расширить `Board`:
   - `Board ApplyMove(Move move)` — теперь с полной логикой:
     1. Поставить камень.
     2. Найти и снять группы противника без дамэ.
     3. Проверить свою группу (если без дамэ — откатить, но это уже T-003).
   - `IReadOnlyList<Point> CapturedStones` — камни, снятые последним `ApplyMove`.
4. Реализовать `GoEngine.Core/CaptureResult.cs`:
   - `readonly record struct CaptureResult(IReadOnlyList<Point> CapturedStones)`.
5. Тесты — все 8 позиций из `GO_RULES.md` раздел 13, кроме ко (T-005) и подсчёта (T-007).

## Что НЕ нужно делать

- Не реализовывать самоубийство (T-003).
- Не реализовывать ко (T-005).
- Не реализовывать подсчёт (T-007).
- Не логировать.

## Ограничения

- Слой: `Core`.
- Зависимости: только `System.*`.
- Новые NuGet-пакеты: нет.

## Definition of Done

- [ ] Все классы реализованы.
- [ ] Юнит-тесты (не менее 12):
  - `Группа_Одиночный_Камень`
  - `Группа_Два_Камня_По_Стороне`
  - `Группа_Не_Соединяется_По_Диагонали`
  - `Группа_В_Атари_Одна_Дамэ`
  - `Захват_Одиночного_Камня`
  - `Захват_Группы_Из_Двух_Камней`
  - `Захват_Не_Происходит_Если_Есть_Дамэ`
  - `Захват_В_Углу`
  - `Захват_На_Краю`
  - `AllGroups_Корректно_Разделяет`
  - `CapturedStones_Пусто_Если_Ничего_Не_Снято`
  - `CapturedStones_Содержит_Снятые`
- [ ] `dotnet test` — зелёный.
- [ ] `STATE.md` обновлён.
- [ ] В ответе — применённые правила.

## Формат ответа

1. Слой, папка, имя файла.
2. Полный код файла.
3. Полный код тестов.
4. Список применённых правил.
5. Обновлённый `STATE.md`.
6. Вопросы.