namespace GoEngine.Problems;

using System.Globalization;
using GoEngine.Core;

/// <summary>Описывает сделанный ход словами: координата, линия, снятия и атари.</summary>
/// <remarks>
/// <para>
/// Жалоба пользователя 2026-10-10: «в основном информация отсутствует о сделанном ходе противника
/// или тобой (когда ты играешь за меня), необходимо добавлять пояснения о сделанных ходах».
/// Заметки разбора стоят не у каждого хода, а между ними экран молчал; здесь считается то,
/// что можно сказать о ходе по правилам, а не по мнению автора.
/// </para>
/// <para>
/// Все факты проверяемы: снятия берутся у доски (<see cref="Board.CapturedStones"/>), атари —
/// у групп (<see cref="GroupTracker"/>), линия и область — из координаты. Ничего не выдумывается:
/// если сказать нечего, строка называет только цвет, точку и линию.
/// </para>
/// </remarks>
public static class MoveDescriber
{
    /// <summary>Собирает факты о ходе.</summary>
    /// <param name="after">Позиция после хода: у неё спрашиваются снятия и группы.</param>
    /// <param name="move">Сделанный ход.</param>
    /// <param name="moveNumber">Номер хода партии (с единицы).</param>
    /// <returns>Факты о ходе: цвет, точка, снятия, атари, линия и область.</returns>
    /// <exception cref="ArgumentNullException">Позиция или ход не заданы.</exception>
    public static MoveFacts FactsOf(Board after, Move move, int moveNumber)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(move);

        if (move.Type != MoveType.Play)
        {
            return new MoveFacts(moveNumber, move.Color, null, 0, [], 0, string.Empty);
        }

        var point = move.Point;
        var size = after.Size.Value;

        return new MoveFacts(
            moveNumber,
            move.Color,
            point,
            after.CapturedStones.Count,
            AtariGroups(after, move),
            LineOf(point, size),
            RegionOf(point, size));
    }

    /// <summary>Собирает строку о ходе для панели разбора.</summary>
    /// <param name="facts">Факты о ходе.</param>
    /// <param name="size">Размер доски: по нему строится подпись точки.</param>
    /// <returns>Строка вида «Ход 27 · белые H7: угол, третья линия. В атари попала группа чёрных (2 камня).».</returns>
    public static string Describe(MoveFacts facts, BoardSize size)
    {
        var number = facts.MoveNumber.ToString(CultureInfo.InvariantCulture);

        if (facts.Point is not { } point)
        {
            return $"Ход {number} · {Colour(facts.Color)}: пас (пропуск хода).";
        }

        var line = facts.Line switch
        {
            1 => "первая линия",
            2 => "вторая линия",
            3 => "третья линия",
            4 => "четвёртая линия",
            _ => $"{facts.Line}-я линия"
        };

        var text = $"Ход {number} · {Colour(facts.Color)} {BoardCoordinates.Label(point, size)}: {facts.Region}, {line}.";

        if (facts.Captured > 0)
        {
            text += $" Снято камней: {facts.Captured.ToString(CultureInfo.InvariantCulture)}.";
        }

        foreach (var (color, stones) in facts.Atari)
        {
            text += $" В атари попала {Adjective(color)} группа ({stones.ToString(CultureInfo.InvariantCulture)} {Stones(stones)}).";
        }

        return text;
    }

    /// <summary>Название цвета в строке: «чёрные» или «белые».</summary>
    /// <param name="color">Цвет.</param>
    /// <returns>Слово для строки.</returns>
    private static string Colour(StoneColor color) => color == StoneColor.Black ? "чёрные" : "белые";

    /// <summary>Прилагательное цвета: «чёрная» или «белая».</summary>
    /// <param name="color">Цвет.</param>
    /// <returns>Слово для строки о группе.</returns>
    private static string Adjective(StoneColor color) => color == StoneColor.Black ? "чёрная" : "белая";

    /// <summary>Слово «камень» в нужном числе.</summary>
    /// <param name="stones">Число камней.</param>
    /// <returns>«камень», «камня» или «камней».</returns>
    private static string Stones(int stones)
    {
        var last = stones % 10;
        var lastTwo = stones % 100;

        if (last == 1 && lastTwo != 11)
        {
            return "камень";
        }

        return last is 2 or 3 or 4 && lastTwo is not (12 or 13 or 14) ? "камня" : "камней";
    }

    /// <summary>Группы соперника, у которых после хода осталось одно дыхание.</summary>
    /// <param name="after">Позиция после хода.</param>
    /// <param name="move">Сделанный ход.</param>
    /// <returns>Цвет и размер каждой такой группы; пусто, если атари нет.</returns>
    private static IReadOnlyList<(StoneColor Color, int Stones)> AtariGroups(Board after, Move move)
    {
        var enemy = move.Color.Opponent();
        HashSet<Point> seen = [];
        List<(StoneColor, int)> groups = [];

        foreach (var neighbour in Neighbours(move.Point, after.Size))
        {
            if (after.At(neighbour) != enemy || seen.Contains(neighbour))
            {
                continue;
            }

            var group = GroupTracker.FindGroup(after, neighbour);

            foreach (var stone in group.Stones)
            {
                _ = seen.Add(stone);
            }

            if (group.IsInAtari)
            {
                groups.Add((enemy, group.Stones.Count));
            }
        }

        return groups;
    }

    /// <summary>Соседние точки на доске.</summary>
    /// <param name="point">Точка.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Точки, лежащие на доске.</returns>
    private static IEnumerable<Point> Neighbours(Point point, BoardSize size)
    {
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var x = point.X + dx;
            var y = point.Y + dy;

            if (x >= 0 && y >= 0 && x < size.Value && y < size.Value)
            {
                yield return new Point((byte)x, (byte)y);
            }
        }
    }

    /// <summary>Линия от ближайшего края: 1 — самая крайняя.</summary>
    /// <param name="point">Точка.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Номер линии.</returns>
    private static int LineOf(Point point, int size) =>
        Math.Min(Math.Min(point.X, point.Y), Math.Min(size - 1 - point.X, size - 1 - point.Y)) + 1;

    /// <summary>Где сделан ход: угол, сторона или центр.</summary>
    /// <param name="point">Точка.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Слово для строки.</returns>
    /// <remarks>Угол — в четырёх линиях от обоих краёв, сторона — в четырёх от одного, остальное центр.</remarks>
    private static string RegionOf(Point point, int size)
    {
        var fromLeft = Math.Min(point.X, size - 1 - point.X);
        var fromTop = Math.Min(point.Y, size - 1 - point.Y);

        if (fromLeft <= 3 && fromTop <= 3)
        {
            return "угол";
        }

        return fromLeft <= 3 || fromTop <= 3 ? "сторона" : "центр";
    }
}