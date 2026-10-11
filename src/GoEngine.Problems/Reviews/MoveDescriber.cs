namespace GoEngine.Problems;

using System.Globalization;
using GoEngine.Core;

/// <summary>Говорит за игрока: что он сделал этим ходом и зачем, по правилам и позиции.</summary>
/// <remarks>
/// <para>
/// Жалоба пользователя 2026-10-11: «должна быть обучающая информация, а не пустота… как будто каждый
/// игрок говорит вслух — „я занимаю такую-то позицию для такой-то тактики, хочу реализовать захват,
/// форму, защиту, атаку“… чтобы было видно, как одна сторона ставит ловушку, а вторая рассказывает,
/// как её обойти».
/// </para>
/// <para>
/// Здесь нет выдуманных планов: за игрока говорится только то, что видно по позиции, — где ход
/// (угол, сторона, центр, какая линия), что он дал (снятия, атари), как связан со своими камнями
/// (соединение, разрезание) и что из этого следует по урокам. Так строка остаётся правдой и в чужой
/// партии, где намерений автора никто не знает.
/// </para>
/// </remarks>
public static class MoveDescriber
{
    /// <summary>Собирает факты о ходе.</summary>
    /// <param name="before">Позиция до хода: по ней видно, что было соединено и разрезано.</param>
    /// <param name="after">Позиция после хода: у неё спрашиваются снятия и группы.</param>
    /// <param name="move">Сделанный ход.</param>
    /// <param name="moveNumber">Номер хода партии (с единицы).</param>
    /// <returns>Факты о ходе: цвет, точка, снятия, атари, линия, область, соединение и разрезание.</returns>
    /// <exception cref="ArgumentNullException">Позиции или ход не заданы.</exception>
    public static MoveFacts FactsOf(Board before, Board after, Move move, int moveNumber)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(move);

        if (move.Type != MoveType.Play)
        {
            return new MoveFacts(moveNumber, move.Color, null, 0, [], 0, string.Empty, false, false, 0);
        }

        var point = move.Point;
        var size = after.Size.Value;
        var own = OwnGroups(before, point, move.Color);
        var enemy = EnemyGroups(before, point, move.Color);

        return new MoveFacts(
            moveNumber,
            move.Color,
            point,
            after.CapturedStones.Count,
            AtariGroups(after, move),
            LineOf(point, size),
            RegionOf(point, size),
            own > 0,
            enemy > 1,
            own);
    }

    /// <summary>Говорит за игрока: строка о ходе для панели разбора.</summary>
    /// <param name="facts">Факты о ходе.</param>
    /// <param name="size">Размер доски: по нему строится подпись точки.</param>
    /// <returns>Строка вида «Чёрные: E3 — ставлю группу в атари: у неё остаётся одно дыхание.».</returns>
    /// <exception cref="ArgumentNullException">Факты не заданы.</exception>
    public static string Describe(MoveFacts facts, BoardSize size)
    {
        if (facts.Point is not { } point)
        {
            return $"{Who(facts.Color)}: пас — ходов, которые что-то дают, не осталось; партия идёт к подсчёту.";
        }

        var label = BoardCoordinates.Label(point, size);
        List<string> parts = [];

        // Что дал ход по правилам: это первое, что видит игрок.
        if (facts.Captured > 0)
        {
            parts.Add($"снимаю {facts.Captured.ToString(CultureInfo.InvariantCulture)} {Stones(facts.Captured)} — они уходят в пленные");
        }

        foreach (var (color, stones) in facts.Atari)
        {
            parts.Add($"ставлю {Adjective(color)} группу в атари: у неё остаётся одно дыхание ({stones.ToString(CultureInfo.InvariantCulture)} {Stones(stones)})");
        }

        // Как ход связан с камнями на доске.
        if (facts.Cuts)
        {
            parts.Add("разрезаю: две группы соперника придётся спасать по отдельности");
        }

        if (facts.Connects)
        {
            parts.Add(facts.OwnGroups > 1
                ? "соединяю свои группы: разрезать их больше нельзя"
                : "продолжаю свою группу и держу её крепкой формой");
        }

        // Где сделан ход и что это даёт по урокам.
        parts.Add(Intent(facts, label));

        return $"{Who(facts.Color)}: {label} — {string.Join("; ", parts)}.";
    }

    /// <summary>Что значит место хода по урокам: угол, сторона, центр и линия.</summary>
    /// <param name="facts">Факты о ходе.</param>
    /// <param name="label">Подпись точки.</param>
    /// <returns>Слова о смысле места.</returns>
    private static string Intent(MoveFacts facts, string label)
    {
        _ = label;

        return facts.Region switch
        {
            "угол" => $"занимаю угол на {Line(facts.Line)}: здесь территория строится двумя краями доски, а не четырьмя",
            "сторона" => facts.Line <= 2
                ? $"иду по {Line(facts.Line)} вдоль края: край закрывает территорию, но живёт такая группа хуже"
                : $"расширяюсь по стороне на {Line(facts.Line)}: отсюда растёт территория и остаётся выход в центр",
            _ => $"иду в центр на {Line(facts.Line)}: центр связывает углы и стороны"
        };
    }

    /// <summary>Кто говорит: «Чёрные» или «Белые».</summary>
    /// <param name="color">Цвет.</param>
    /// <returns>Слово для строки.</returns>
    private static string Who(StoneColor color) => color == StoneColor.Black ? "Чёрные" : "Белые";

    /// <summary>Название линии словами.</summary>
    /// <param name="line">Номер линии от края.</param>
    /// <returns>«первой линии», «третьей линии» и так далее.</returns>
    private static string Line(int line) => line switch
    {
        1 => "первой линии",
        2 => "второй линии",
        3 => "третьей линии",
        4 => "четвёртой линии",
        _ => $"{line.ToString(CultureInfo.InvariantCulture)}-й линии"
    };

    /// <summary>Прилагательное цвета: «чёрную» или «белую».</summary>
    /// <param name="color">Цвет группы.</param>
    /// <returns>Слово для строки об атари.</returns>
    private static string Adjective(StoneColor color) => color == StoneColor.Black ? "чёрную" : "белую";

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

    /// <summary>Сколько своих групп стоит рядом с точкой до хода.</summary>
    /// <param name="before">Позиция до хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="color">Цвет хода.</param>
    /// <returns>Число своих групп по соседству.</returns>
    private static int OwnGroups(Board before, Point point, StoneColor color) => Groups(before, point, color);

    /// <summary>Сколько групп соперника стоит рядом с точкой до хода.</summary>
    /// <param name="before">Позиция до хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="color">Цвет хода.</param>
    /// <returns>Число групп соперника по соседству.</returns>
    private static int EnemyGroups(Board before, Point point, StoneColor color) => Groups(before, point, color.Opponent());

    /// <summary>Считает разные группы цвета рядом с точкой.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="color">Цвет групп.</param>
    /// <returns>Число разных групп.</returns>
    private static int Groups(Board board, Point point, StoneColor color)
    {
        HashSet<Point> seen = [];
        var groups = 0;

        foreach (var neighbour in Neighbours(point, board.Size))
        {
            if (board.At(neighbour) != color || seen.Contains(neighbour))
            {
                continue;
            }

            groups++;

            foreach (var stone in GroupTracker.FindGroup(board, neighbour).Stones)
            {
                _ = seen.Add(stone);
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
    public static int LineOf(Point point, int size) =>
        Math.Min(Math.Min(point.X, point.Y), Math.Min(size - 1 - point.X, size - 1 - point.Y)) + 1;

    /// <summary>Где сделан ход: угол, сторона или центр.</summary>
    /// <param name="point">Точка.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Слово для строки.</returns>
    /// <remarks>Угол — в четырёх линиях от обоих краёв, сторона — в четырёх от одного, остальное центр.</remarks>
    public static string RegionOf(Point point, int size)
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
