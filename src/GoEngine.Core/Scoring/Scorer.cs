namespace GoEngine.Core;

/// <summary>Подсчёт очков по китайским правилам (<c>GO_RULES.md</c>, п. 9).</summary>
/// <remarks>
/// Очки = камни на доске + пустые точки, окружённые только одним цветом. Точка считается
/// окружённой цветом C, если из неё нельзя уйти по пустым точкам, не пройдя мимо камня другого
/// цвета: практически это значит, что вся пустая область, в которой лежит точка, граничит
/// только с камнями цвета C. Область, граничащая с обоими цветами, — нейтральная и не считается
/// ни за кого; сэки отдельной обработки не требует. Мёртвые камни не снимаются: считаем по факту
/// расположения на доске.
/// <para>
/// Область, прижатая к краю доски, считается по своей границе: край не принадлежит никому и
/// владения не отменяет (п. 69–70). Формулировка п. 71 про «все пути до края» — пояснение к
/// окружённым областям; буквально она отрицала бы и угловую территорию, поэтому опорное
/// определение — граница области.
/// </para>
/// </remarks>
public static class Scorer
{
    /// <summary>Считает очки партии.</summary>
    /// <param name="board">Позиция на момент подсчёта.</param>
    /// <param name="komi">Коми, которое прибавляется белым.</param>
    /// <returns>Очки чёрных и белых.</returns>
    public static Score Calculate(Board board, Komi komi)
    {
        ArgumentNullException.ThrowIfNull(board);

        var area = Breakdown(board);

        return new Score(area.Black, area.White + komi.Value);
    }

    /// <summary>Разбирает площадь доски: камни и территория каждого цвета, нейтральные точки.</summary>
    /// <param name="board">Позиция на момент подсчёта.</param>
    /// <returns>Разбор площади.</returns>
    /// <remarks>
    /// Очки и разбор считаются из одного источника — владения точками, поэтому панель партии
    /// и счёт партии не могут разойтись. Камни попадают в разбор как камни, а не как территория:
    /// интерфейсу важно показать их отдельно.
    /// </remarks>
    public static AreaBreakdown Breakdown(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var ownership = Ownership(board);
        var blackOwned = 0;
        var whiteOwned = 0;

        foreach (var owner in ownership)
        {
            if (owner == StoneColor.Black)
            {
                blackOwned++;
            }
            else if (owner == StoneColor.White)
            {
                whiteOwned++;
            }
        }

        var blackStones = board.OccupiedPoints(StoneColor.Black).Count();
        var whiteStones = board.OccupiedPoints(StoneColor.White).Count();

        return new AreaBreakdown(
            blackStones,
            blackOwned - blackStones,
            whiteStones,
            whiteOwned - whiteStones,
            board.Size.Area - blackOwned - whiteOwned);
    }

    /// <summary>Определяет владельца каждой точки доски.</summary>
    /// <param name="board">Позиция на момент подсчёта.</param>
    /// <returns>
    /// Владелец каждой точки в порядке обхода доски: камень — своему цвету, окружённая пустая
    /// точка — цвету окружения, нейтральная — <see cref="StoneColor.Empty"/>.
    /// </returns>
    /// <remarks>
    /// Нужна не только подсчёту очков, но и кодированию позиции для нейросети (v2):
    /// там территория подаётся отдельными плоскостями.
    /// </remarks>
    public static IReadOnlyList<StoneColor> Ownership(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var ownership = new StoneColor[board.Size.Area];
        Array.Fill(ownership, StoneColor.Empty);

        HashSet<Point> visited = [];

        foreach (var point in board.AllPoints())
        {
            var color = board.At(point);

            if (color != StoneColor.Empty)
            {
                ownership[(point.Y * board.Size.Value) + point.X] = color;
                continue;
            }

            // Пустая точка, уже разобранная вместе со своей областью, свой цвет не меняет.
            if (visited.Contains(point))
            {
                continue;
            }

            var region = FillRegion(board, point, visited);
            var owner = FindOwner(board, region);

            if (owner is null)
            {
                continue;
            }

            foreach (var owned in region)
            {
                ownership[(owned.Y * board.Size.Value) + owned.X] = owner;
            }
        }

        return ownership;
    }

    /// <summary>Собирает связную по стороне пустую область.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="start">Пустая точка, с которой начинается обход.</param>
    /// <param name="visited">Уже посещённые пустые точки: пополняется, чтобы область не обходилась дважды.</param>
    /// <returns>Точки области в порядке обхода.</returns>
    private static List<Point> FillRegion(Board board, Point start, HashSet<Point> visited)
    {
        List<Point> region = [];
        Queue<Point> frontier = new();

        visited.Add(start);
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            region.Add(current);

            foreach (var neighbor in current.Neighbors(board.Size))
            {
                if (board.At(neighbor) == StoneColor.Empty && visited.Add(neighbor))
                {
                    frontier.Enqueue(neighbor);
                }
            }
        }

        return region;
    }

    /// <summary>Определяет владельца пустой области по камням на её границе.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="region">Пустая область.</param>
    /// <returns>Цвет владельца или <c>null</c>, если область нейтральная.</returns>
    /// <remarks>
    /// Область принадлежит цвету, только если все соседние с ней камни одного цвета.
    /// Пустая доска не граничит ни с чем и потому нейтральна целиком.
    /// </remarks>
    private static StoneColor? FindOwner(Board board, List<Point> region)
    {
        StoneColor? owner = null;

        foreach (var point in region)
        {
            foreach (var neighbor in point.Neighbors(board.Size))
            {
                var color = board.At(neighbor);

                if (color == StoneColor.Empty)
                {
                    continue;
                }

                if (owner is null)
                {
                    owner = color;
                    continue;
                }

                if (owner != color)
                {
                    return null;
                }
            }
        }

        return owner;
    }
}