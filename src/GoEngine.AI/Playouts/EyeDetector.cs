namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Распознавание настоящих глаз: точки, которые нельзя заполнять в playout'ах.</summary>
/// <remarks>
/// Точка — глаз цвета <c>C</c>, если все соседи по стороне заняты камнями <c>C</c> (край доски
/// соседом не считается, он граница), а из диагональных соседей камнями <c>C</c> занято
/// не меньше нормы: в углу — единственная диагональ, на краю — обе, в центре — три из четырёх.
/// Диагональная проверка обязательна: без неё ложные глаза (2 диагонали из 4) считались бы
/// глазами, и playout убивал бы свои живые группы.
/// </remarks>
public static class EyeDetector
{
    /// <summary>Сколько диагоналей должно быть занято у точки в углу.</summary>
    private const int CornerDiagonals = 1;

    /// <summary>Сколько диагоналей должно быть занято у точки на краю.</summary>
    private const int EdgeDiagonals = 2;

    /// <summary>Сколько диагоналей должно быть занято у точки в центре.</summary>
    private const int CenterDiagonals = 3;

    /// <summary>Проверяет, является ли точка настоящим глазом указанного цвета.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Проверяемая точка.</param>
    /// <param name="color">Цвет, для которого проверяется глаз.</param>
    /// <returns><c>true</c>, если точка — глаз цвета <paramref name="color"/>.</returns>
    /// <exception cref="DomainException">Точка находится вне доски.</exception>
    public static bool IsEye(Board board, Point point, StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        // At проверяет принадлежность доске: вне доски глаз не определён.
        if (board.At(point) != StoneColor.Empty)
        {
            return false;
        }

        foreach (var neighbor in point.Neighbors(board.Size))
        {
            if (board.At(neighbor) != color)
            {
                return false;
            }
        }

        var required = RequiredDiagonals(board.Size, point);
        var mine = 0;

        foreach (var diagonal in Diagonals(board.Size, point))
        {
            if (board.At(diagonal) == color)
            {
                mine++;
            }
        }

        return mine >= required;
    }

    /// <summary>Считает, сколько диагоналей должны быть заняты у точки.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="point">Точка.</param>
    /// <returns>1 для угла, 2 для края, 3 для центра.</returns>
    private static int RequiredDiagonals(BoardSize size, Point point)
    {
        var onVerticalEdge = point.X == 0 || point.X == size.Value - 1;
        var onHorizontalEdge = point.Y == 0 || point.Y == size.Value - 1;

        if (onVerticalEdge && onHorizontalEdge)
        {
            return CornerDiagonals;
        }

        return onVerticalEdge || onHorizontalEdge ? EdgeDiagonals : CenterDiagonals;
    }

    /// <summary>Перечисляет диагональных соседей точки, лежащих на доске.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="point">Точка.</param>
    /// <returns>От одной до четырёх диагональных точек.</returns>
    private static IEnumerable<Point> Diagonals(BoardSize size, Point point)
    {
        for (var dx = -1; dx <= 1; dx += 2)
        {
            for (var dy = -1; dy <= 1; dy += 2)
            {
                var x = point.X + dx;
                var y = point.Y + dy;

                if (x >= 0 && x < size.Value && y >= 0 && y < size.Value)
                {
                    yield return new Point((byte)x, (byte)y);
                }
            }
        }
    }
}
