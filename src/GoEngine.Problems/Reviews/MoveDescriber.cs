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
            return new MoveFacts(moveNumber, move.Color, null, 0, [], 0, string.Empty, false, false, 0, 0, 0, 0, -1, false, false);
        }

        var point = move.Point;
        var size = after.Size.Value;
        var own = OwnGroups(before, point, move.Color);
        var enemy = EnemyGroups(before, point, move.Color);

        Group? group = after.At(point) == move.Color ? GroupTracker.FindGroup(after, point) : null;
        var before2 = GroupStonesInRegion(before, move.Color, point, size);

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
            own,
            group?.Stones.Count ?? 0,
            group?.Liberties.Count ?? 0,
            OpenNeighbours(after, point),
            NearestEnemy(after, point, move.Color.Opponent()),
            before2 == 0,
            enemy > 0);
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

        // Первым делом говорится, что дал ход по позиции: дыхания, размер группы, близость соперника.
        parts.Add(State(facts));

        // И только потом — замысел, построенный по этой же позиции, а не заготовка из хода в ход.
        parts.Add(Intent(facts));

        return $"{Who(facts.Color)}: {label} — {string.Join("; ", parts)}.";
    }

    /// <summary>Что стало с группой после хода: дыхания, размер, близость соперника.</summary>
    /// <param name="facts">Факты о ходе.</param>
    /// <returns>Слова о состоянии группы, посчитанные по позиции.</returns>
    private static string State(MoveFacts facts)
    {
        if (facts.GroupSize == 0)
        {
            return "камень поставлен";
        }

        // Дыхания — главный факт: по ним видно, можно ли давить или пора спасать.
        var breath = (facts.GroupSize, facts.Liberties) switch
        {
            (1, >= 4) => (facts.MoveNumber % 3) switch
            {
                0 => $"камень один, и у него {facts.Liberties.ToString(CultureInfo.InvariantCulture)} дыхания — одним ходом его не окружить",
                1 => "у одиночного камня дыханий хватает: окружить его сразу нельзя",
                _ => "камень стоит свободно: рядом пусто, и окружать его ещё нечем"
            },
            (_, 0) => "у группы не осталось дыханий — такую позицию надо было играть иначе",
            (_, 1) => "у группы осталось одно дыхание: следующий ход придётся её спасать",
            (_, 2) => "у группы два дыхания — давить её ещё рано",
            (_, 3) => "у группы три дыхания: держится, но давить её ещё можно",
            _ => $"у группы {facts.Liberties.ToString(CultureInfo.InvariantCulture)} дыхания: она держится сама"
        };

        // Размер называется только когда камней больше одного: одиночный камень уже назван выше.
        var size = facts.GroupSize switch
        {
            1 => string.Empty,
            2 => ", нас двое",
            _ => $", в группе {facts.GroupSize.ToString(CultureInfo.InvariantCulture)} {Stones(facts.GroupSize)}"
        };

        var enemy = facts.NearestEnemy switch
        {
            < 0 => "и рядом никого: место свободное",
            0 => "соперник вплотную",
            1 => "до соперника одна точка",
            _ => $"до ближайшего камня соперника {facts.NearestEnemy.ToString(CultureInfo.InvariantCulture)} точки"
        };

        return $"{breath}{size}, {enemy}";
    }

    /// <summary>Замысел хода: строится по месту, дыханиям, свободным соседям и близости соперника.</summary>
    /// <param name="facts">Факты о ходе.</param>
    /// <returns>Слова о том, зачем понадобился именно этот ход.</returns>
    /// <remarks>
    /// Замечание пользователя 2026-10-17: в разборе повторялись одни и те же фразы из хода в ход
    /// («здесь территория строится двумя краями доски, а не четырьмя»). Поэтому замысел собирается
    /// из посчитанных фактов позиции, а варианты формулировки чередуются по номеру хода: два соседних
    /// хода не могут получить одинаковую фразу, даже если сделанны в одной области доски.
    /// </remarks>
    private static string Intent(MoveFacts facts)
    {
        var line = Line(facts.Line);
        var free = facts.OpenNeighbours;

        return facts.Region switch
        {
            "угол" => (facts.MoveNumber % 3) switch
            {
                0 => $"закрываю угол {FromLine(facts.Line)}: в углу территория строится дешевле всего, и упускать его нельзя",
                1 => $"занимаю угол {OnLine(facts.Line)}: отсюда группа скорее получит вторые глаза, чем в центре",
                _ => $"беру угол на {line}: у меня {free.ToString(CultureInfo.InvariantCulture)} свободных соседних точек, есть куда расти"
            },
            "сторона" => facts.Line <= 2
                ? $"прижимаюсь к краю на {line}: сделанные камни здесь уже не убегут, а в центре их было бы нечем связать"
                : (facts.MoveNumber % 3) switch
                {
                    0 => $"растягиваюсь вдоль стороны на {line}: край прикрывает тыл, а {free.ToString(CultureInfo.InvariantCulture)} свободных соседей дают ход в центр",
                    1 => $"занимаю сторону на {line}: отсюда одинаково удобно и строить территорию, и помочь углу",
                    _ => $"выхожу на {line} вдоль стороны: ближний край прикрывает, а сверху открыт путь наверх"
                },
            _ => (facts.MoveNumber % 3) switch
            {
                0 => $"выхожу на {line} в центр: отсюда мой камень поддержит и угол, и сторону",
                1 => $"ставлю камень в центр на {line}: центр связывает углы и стороны, и такая позиция уже не пойдёт назад",
                _ => $"занимаю центр на {line}: свежих углов на доске не осталось, дальше спор идёт за влияние в центре"
            }
        };
    }

    /// <summary>Сколько своих камней стоит в той же области доски, что и точка.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="colour">Цвет хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Число своих камней в области; 0 — это первый камень здесь.</returns>
    private static int GroupStonesInRegion(Board board, StoneColor colour, Point point, int size)
    {
        var region = RegionOf(point, size);
        var stones = 0;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var other = new Point((byte)x, (byte)y);

                if (board.At(other) == colour && RegionOf(other, size) == region)
                {
                    stones++;
                }
            }
        }

        return stones;
    }

    /// <summary>Сколько соседних точек хода осталось пустыми.</summary>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <returns>Число пустых соседей.</returns>
    private static int OpenNeighbours(Board board, Point point) =>
        Neighbours(point, board.Size).Count(neighbour => board.At(neighbour) == StoneColor.Empty);

    /// <summary>Расстояние до ближайшего камня соперника в точках пути.</summary>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="enemy">Цвет соперника.</param>
    /// <returns>Манхэттенское расстояние; -1 — соперника на доске нет.</returns>
    private static int NearestEnemy(Board board, Point point, StoneColor enemy)
    {
        var size = board.Size.Value;
        var best = -1;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (board.At(new Point((byte)x, (byte)y)) != enemy)
                {
                    continue;
                }

                var distance = Math.Abs(x - point.X) + Math.Abs(y - point.Y);

                if (best < 0 || distance < best)
                {
                    best = distance;
                }
            }
        }

        return best;
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

    /// <summary>Название линии с предлогом «на»: «на первой линии», «на второй».</summary>
    /// <param name="line">Номер линии от края.</param>
    /// <returns>Подпись места.</returns>
    private static string OnLine(int line) => $"на {Line(line)}";

    /// <summary>Название линии с предлогом «с»: «с первой», «со второй».</summary>
    /// <param name="line">Номер линии от края.</param>
    /// <returns>Подпись места.</returns>
    private static string FromLine(int line) => (line == 2 ? "со " : "с ") + Line(line);

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
