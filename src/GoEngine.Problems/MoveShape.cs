using GoEngine.Core;

namespace GoEngine.Problems;

/// <summary>Форма, которую образовал ход: название, чем она хороша или плоха и что будет дальше.</summary>
/// <param name="Name">Название формы: «ноби», «косуми», «пасть тигра», «пустой треугольник».</param>
/// <param name="Good">Хорошая ли форма: крепкая, лёгкая или хотя бы не вредная.</param>
/// <param name="Why">Чем форма хороша или плоха и что из этого следует в партии.</param>
/// <param name="Points">Камни, которые образуют форму: по ним вид рисует подсветку.</param>
public readonly record struct ShapeFacts(string Name, bool Good, string Why, IReadOnlyList<Point> Points);

/// <summary>Распознавание формы хода: что игрок построил своим камнем.</summary>
/// <remarks>
/// <para>
/// Замечание пользователя 2026-10-11: «форма — нужна ли отдельная строка вида „форма: косуми, два
/// камня по диагонали“. Хорошая эта форма или нет, чем хороша\плоха тут в перспективе». Форма
/// считается по позиции: как поставленный камень связан со своими и какие точки остались между ними.
/// </para>
/// <para>
/// Выдумывать нечего: распознаются формы из уроков — ноби, косуми, кейма, тоби, никкен тоби, бамбук,
/// пасть тигра и пустой треугольник. Всё остальное честно называется «без формы».
/// </para>
/// </remarks>
public static class MoveShape
{
    /// <summary>Определяет форму, которую образовал ход.</summary>
    /// <param name="after">Позиция после хода.</param>
    /// <param name="move">Сделанный ход.</param>
    /// <returns>Название формы, её оценку и объяснение.</returns>
    public static ShapeFacts Of(Board after, Move move)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(move);

        if (move.Type != MoveType.Play)
        {
            return new ShapeFacts("пас", true, "Пас форму не строит: он передаёт ход и начинает подсчёт.", []);
        }

        var point = move.Point;
        var colour = after.At(point);
        var own = Neighbours(after, point).Where(other => after.At(other) == colour).ToList();
        var nearby = Nearby(after, point, colour);
        var jumps = Jump(after, point, colour);

        // Порядок проверок — от самой характерной формы к простой связи: пустой треугольник и пасть
        // тигра важнее, чем «камень рядом с камнем».
        if (Brick(after, point, colour))
        {
            return new ShapeFacts(
                "кирпич",
                false,
                "плохая форма: четыре камня квадратом — внутри ни глаза, ни территории, а камней больше, чем нужно. "
                + "Такая группа тяжёлая: она бежит, а не строит, и играть лучше шире.",
                [point, .. own]);
        }

        if (EmptyTriangleSquare(after, point, colour) is { } triangle)
        {
            return new ShapeFacts(
                "пустой треугольник",
                false,
                "тяжёлая форма: камней стало больше, а дыханий и территории — нет. Камень внутри формы "
                + "работает впустую, и группа теряет темп. Обычно лучше играть шире — косуми или прыжком.",
                triangle);
        }

        if (TigerMouth(after, point, colour))
        {
            return new ShapeFacts(
                "пасть тигра",
                true,
                "крепкая форма: между камнями осталась точка, и вход в неё невыгоден — камень соперника "
                + "окажется под атари и следующим ходом уйдёт в плен. Держит территорию без лишних камней.",
                [point, .. own]);
        }

        if (Bamboo(after, point, colour))
        {
            return new ShapeFacts(
                "бамбук",
                true,
                "прочная связь: соперник займёт одну соединительную точку, а вы соединитесь через вторую. "
                + "Разрезать такую форму нельзя.",
                [point, .. own]);
        }

        if (own.Count >= 2 && own.Any(other => IsLine(point, other)))
        {
            // Ноби — соединение в линию. Название одно и то же в уроке, задаче и разборе партии
            // (находка QA-повтора 2026-10-17: чип говорил «соединение в линию», а текст — «ноби»).
            return new ShapeFacts(
                "ноби",
                true,
                "камни связаны плотно: разрезать их нельзя. Форма надёжная, но медленная — камень работает "
                + "на соединение, а не на территорию.",
                [point, .. own]);
        }

        if (own.Any(other => IsLine(point, other)))
        {
            return new ShapeFacts(
                "ноби",
                true,
                "плотная связь: два камня в линию имеют общие дыхания, и разрезать их нельзя. Растёт медленно, "
                + "зато надёжно — так строят группы, которым важна жизнь.",
                [point, .. own]);
        }

        if (nearby.Any(other => IsDiagonal(point, other)))
        {
            return new ShapeFacts(
                "косуми",
                true,
                "связано по диагонали: соперник не разрежет одним ходом, а форма растёт быстрее, чем в линию. "
                + "Хороша у края и для лёгкого расширения.",
                [point, .. nearby.Where(other => IsDiagonal(point, other)).Take(1)]);
        }

        if (jumps.Contains(1))
        {
            return new ShapeFacts(
                "тоби",
                true,
                "прыжок через точку: форма растёт быстро и легко, но соединением не является — соперник "
                + "может влезть в промежуток и разрезать. Так играют, когда разрез ему невыгоден.",
                [point, .. nearby.Where(other => IsJump(point, other, 2)).Take(1)]);
        }

        if (jumps.Contains(2))
        {
            return new ShapeFacts(
                "кейма",
                true,
                "ход конём: быстро и легко, между камнями остаётся точка разрезания — за ней нужно следить. "
                + "Хороша, когда играют широко, а не в тесноте.",
                [point, .. nearby.Where(other => IsKnight(point, other)).Take(1)]);
        }

        if (jumps.Contains(3))
        {
            return new ShapeFacts(
                "никкен тоби",
                true,
                "широкий прыжок: занимает сторону быстро, но связь тонкая — соперник может влезть в промежуток. "
                + "Так расширяются, когда места много и группа уже обеспечена.",
                [point, .. nearby.Where(other => IsJump(point, other, 3)).Take(1)]);
        }

        return own.Count > 0
            ? new ShapeFacts("рядом со своими камнями", true, "камень стоит рядом со своими: форма не построена, но группа стала плотнее.", [point, .. own])
            : new ShapeFacts("отдельный камень", true, "камень поставлен сам по себе: формы ещё нет — она появится следующим ходом.", [point]);
    }

    /// <summary>Проверяет пустой треугольник: три своих камня в квадрате 2×2 и пустая точка внутри.</summary>
    /// <summary>Возвращает камни пустого треугольника или <c>null</c>, если формы нет.</summary>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="colour">Цвет хода.</param>
    /// <returns>Три камня формы по порядку обхода квадрата.</returns>
    private static IReadOnlyList<Point>? EmptyTriangleSquare(Board board, Point point, StoneColor colour)
    {
        // Пустой треугольник — квадрат 2×2, где три точки заняты своими камнями, а четвёртая пуста.
        // Ход обязан быть одним из трёх: иначе форму построил не он.
        foreach (var square in Squares(point, board.Size.Value))
        {
            if (!square.Contains(point) || square.Count(other => board.At(other) == colour) != 3)
            {
                continue;
            }

            var fourth = square.First(other => board.At(other) != colour);

            // Четвёртая точка обязана быть пустой: камень соперника в квадрате — это контактная
            // борьба, а не пустой треугольник.
            if (board.At(fourth) != StoneColor.Empty)
            {
                continue;
            }

            // Пустая точка может быть глазом: тогда это не пустой треугольник, а форма жизни —
            // тот самый четвёртый пункт, который и делает группу живой (проверено на задаче
            // «обеспечить жизнь», где движок признавал группу живой, а форма называлась плохой).
            if (!ProblemLife.IsEye(board, fourth, colour))
            {
                return [.. square.Where(other => board.At(other) == colour)];
            }
        }

        return null;
    }

    /// <summary>Проверяет кирпич: четыре своих камня в квадрате 2×2.</summary>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="colour">Цвет хода.</param>
    /// <returns><c>true</c>, если все четыре точки квадрата заняты своими камнями.</returns>
    private static bool Brick(Board board, Point point, StoneColor colour)
    {
        foreach (var square in Squares(point, board.Size.Value))
        {
            if (square.All(other => board.At(other) == colour))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Четыре квадрата 2×2, в которые входит точка.</summary>
    /// <param name="point">Точка.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Квадраты из четырёх точек.</returns>
    private static IEnumerable<IReadOnlyList<Point>> Squares(Point point, int size)
    {
        for (var dx = -1; dx <= 0; dx++)
        {
            for (var dy = -1; dy <= 0; dy++)
            {
                var x = point.X + dx;
                var y = point.Y + dy;

                if (x < 0 || y < 0 || x + 1 >= size || y + 1 >= size)
                {
                    continue;
                }

                yield return
                [
                    new Point((byte)x, (byte)y),
                    new Point((byte)(x + 1), (byte)y),
                    new Point((byte)x, (byte)(y + 1)),
                    new Point((byte)(x + 1), (byte)(y + 1))
                ];
            }
        }
    }

    /// <summary>Свои камни на расстоянии до трёх точек: для косыми, кейма и прыжков.</summary>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="colour">Цвет хода.</param>
    /// <returns>Свои камни рядом.</returns>
    private static IReadOnlyList<Point> Nearby(Board board, Point point, StoneColor colour)
    {
        List<Point> found = [];

        for (var x = Math.Max(0, point.X - 3); x <= Math.Min(board.Size.Value - 1, point.X + 3); x++)
        {
            for (var y = Math.Max(0, point.Y - 3); y <= Math.Min(board.Size.Value - 1, point.Y + 3); y++)
            {
                var other = new Point((byte)x, (byte)y);

                if (board.At(other) == colour)
                {
                    found.Add(other);
                }
            }
        }

        return found;
    }

    /// <summary>Проверяет пасть тигра: вокруг пустой точки стоят три своих камня.</summary>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="colour">Цвет хода.</param>
    /// <returns><c>true</c>, если ход замкнул пасть тигра.</returns>
    private static bool TigerMouth(Board board, Point point, StoneColor colour)
    {
        foreach (var candidate in Neighbours(board, point))
        {
            if (board.At(candidate) != StoneColor.Empty)
            {
                continue;
            }

            var guards = Neighbours(board, candidate).Count(other => board.At(other) == colour);

            // Три своих камня вокруг пустой точки и сама точка — не дыхание чужой группы рядом.
            if (guards >= 3)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Проверяет бамбук: две пары своих камней со сдвигом на одну точку.</summary>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="colour">Цвет хода.</param>
    /// <returns><c>true</c>, если форма — бамбук.</returns>
    private static bool Bamboo(Board board, Point point, StoneColor colour)
    {
        // Бамбук — две пары камней со сдвигом на одну точку: пара по линии, вторая пара через точку
        // по диагонали. Пример из урока: D5, E5 и F6, G6.
        var size = board.Size.Value;

        for (var dx = -3; dx <= 0; dx++)
        {
            for (var dy = -1; dy <= 0; dy++)
            {
                var x = point.X + dx;
                var y = point.Y + dy;

                if (x < 0 || y < 0 || x + 1 >= size || y + 1 >= size)
                {
                    continue;
                }

                // Первая пара — по строке, вторая — через две точки по строке ниже: между ними
                // остаются две соединительные точки, как у бамбука D5, E5 и F6, G6.
                if (x + 3 < size
                    && y + 1 < size
                    && board.At(new Point((byte)x, (byte)y)) == colour
                    && board.At(new Point((byte)(x + 1), (byte)y)) == colour
                    && board.At(new Point((byte)(x + 2), (byte)(y + 1))) == colour
                    && board.At(new Point((byte)(x + 3), (byte)(y + 1))) == colour)
                {
                    return true;
                }

                // То же по вертикали: пара в столбце и вторая пара через две точки в соседнем столбце.
                if (y + 3 < size
                    && x + 1 < size
                    && board.At(new Point((byte)x, (byte)y)) == colour
                    && board.At(new Point((byte)x, (byte)(y + 1))) == colour
                    && board.At(new Point((byte)(x + 1), (byte)(y + 2))) == colour
                    && board.At(new Point((byte)(x + 1), (byte)(y + 3))) == colour)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Ищет свои камни на расстоянии прыжка: 1 — тоби, 2 — кейма, 3 — никкен тоби.</summary>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="colour">Цвет хода.</param>
    /// <returns>Набор найденных расстояний.</returns>
    private static HashSet<int> Jump(Board board, Point point, StoneColor colour)
    {
        HashSet<int> found = [];

        for (var x = 0; x < board.Size.Value; x++)
        {
            for (var y = 0; y < board.Size.Value; y++)
            {
                var other = new Point((byte)x, (byte)y);

                if (board.At(other) != colour)
                {
                    continue;
                }

                var dx = Math.Abs(point.X - other.X);
                var dy = Math.Abs(point.Y - other.Y);

                if (dx == 2 && dy == 0 || dx == 0 && dy == 2)
                {
                    _ = found.Add(1);
                }
                else if (dx == 2 && dy == 1 || dx == 1 && dy == 2)
                {
                    _ = found.Add(2);
                }
                else if (dx == 3 && dy == 0 || dx == 0 && dy == 3)
                {
                    _ = found.Add(3);
                }
            }
        }

        return found;
    }

    /// <summary>Стоит ли камень на расстоянии прыжка по линии.</summary>
    /// <param name="point">Точка хода.</param>
    /// <param name="other">Свой камень.</param>
    /// <param name="distance">Ожидаемое расстояние.</param>
    /// <returns><c>true</c>, если камень стоит в той же строке или столбце на этом расстоянии.</returns>
    private static bool IsJump(Point point, Point other, int distance) =>
        (Math.Abs(point.X - other.X) == distance && point.Y == other.Y)
        || (Math.Abs(point.Y - other.Y) == distance && point.X == other.X);

    /// <summary>Стоит ли камень ходом конём: две точки в сторону и одна вбок.</summary>
    /// <param name="point">Точка хода.</param>
    /// <param name="other">Свой камень.</param>
    /// <returns><c>true</c>, если расстояние коня.</returns>
    private static bool IsKnight(Point point, Point other) =>
        (Math.Abs(point.X - other.X) == 2 && Math.Abs(point.Y - other.Y) == 1)
        || (Math.Abs(point.X - other.X) == 1 && Math.Abs(point.Y - other.Y) == 2);

    /// <summary>Соседние точки в пределах доски.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка.</param>
    /// <returns>Соседей по линиям.</returns>
    private static IEnumerable<Point> Neighbours(Board board, Point point) =>
        Directions
            .Select(direction => Step(point, direction, board.Size.Value))
            .Where(step => step is not null)
            .Select(step => step!.Value);

    /// <summary>Шаг в сторону, если он остаётся на доске.</summary>
    /// <param name="point">Точка.</param>
    /// <param name="direction">Направление.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Соседняя точка или <c>null</c>.</returns>
    private static Point? Step(Point point, (int Dx, int Dy) direction, int size)
    {
        var x = point.X + direction.Dx;
        var y = point.Y + direction.Dy;

        return x < 0 || y < 0 || x >= size || y >= size ? null : new Point((byte)x, (byte)y);
    }

    /// <summary>Стоят ли точки в одной линии рядом.</summary>
    /// <param name="first">Первая точка.</param>
    /// <param name="second">Вторая точка.</param>
    /// <returns><c>true</c>, если соседи по линии.</returns>
    private static bool IsLine(Point first, Point second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y) == 1;

    /// <summary>Стоят ли точки по диагонали рядом.</summary>
    /// <param name="first">Первая точка.</param>
    /// <param name="second">Вторая точка.</param>
    /// <returns><c>true</c>, если соседи по диагонали.</returns>
    private static bool IsDiagonal(Point first, Point second) =>
        Math.Abs(first.X - second.X) == 1 && Math.Abs(first.Y - second.Y) == 1;

    /// <summary>Противоположное направление.</summary>
    /// <param name="direction">Направление.</param>
    /// <returns>Обратное направление.</returns>
    private static (int Dx, int Dy) Opposite((int Dx, int Dy) direction) => (-direction.Dx, -direction.Dy);

    /// <summary>Четыре направления по линиям доски.</summary>
    private static readonly (int Dx, int Dy)[] Directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];
}
