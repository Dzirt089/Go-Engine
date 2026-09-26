namespace GoEngine.Core;

/// <summary>Поиск групп камней и их дамэ.</summary>
/// <remarks>
/// Класс статический: состояние группы полностью определяется доской, кэшировать нечего.
/// Обход — только по стороне (<c>GO_RULES.md</c>, п. 4), диагонали не учитываются.
/// </remarks>
public static class GroupTracker
{
    /// <summary>Находит группу, к которой принадлежит точка.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка, занятая камнем.</param>
    /// <returns>Камни группы того же цвета, соединённые по стороне, и все дамэ группы.</returns>
    /// <exception cref="DomainException">Точка вне доски или в точке нет камня.</exception>
    public static Group FindGroup(Board board, Point point)
    {
        ArgumentNullException.ThrowIfNull(board);

        // At проверяет принадлежность доске: вне доски группа не определена.
        var color = board.At(point);

        if (color == StoneColor.Empty)
        {
            throw new DomainException($"В точке {point} нет камня: группу ищут только от камня.");
        }

        List<Point> stones = [];
        HashSet<Point> liberties = [];
        HashSet<Point> visited = [point];
        Queue<Point> frontier = new();

        frontier.Enqueue(point);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            stones.Add(current);

            foreach (var neighbor in current.Neighbors(board.Size))
            {
                var neighborColor = board.At(neighbor);

                if (neighborColor == StoneColor.Empty)
                {
                    liberties.Add(neighbor);
                    continue;
                }

                // Камни другого цвета — граница группы: в обход не попадают.
                if (neighborColor != color || !visited.Add(neighbor))
                {
                    continue;
                }

                frontier.Enqueue(neighbor);
            }
        }

        return new Group(color, stones.AsReadOnly(), liberties);
    }

    /// <summary>Считает дамэ группы заново, по её камням.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="group">Группа, для которой нужны дамэ.</param>
    /// <returns>Пустые точки, соседние с камнями группы по стороне.</returns>
    /// <exception cref="DomainException">Группа не задана.</exception>
    public static IReadOnlySet<Point> FindLiberties(Board board, Group group)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(group.Stones);
        ArgumentNullException.ThrowIfNull(group.Color);

        HashSet<Point> liberties = [];

        foreach (var stone in group.Stones)
        {
            foreach (var neighbor in stone.Neighbors(board.Size))
            {
                if (board.At(neighbor) == StoneColor.Empty)
                {
                    liberties.Add(neighbor);
                }
            }
        }

        return liberties;
    }

    /// <summary>Перечисляет все группы указанного цвета.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет камней: <see cref="StoneColor.Black"/> или <see cref="StoneColor.White"/>.</param>
    /// <returns>Группы цвета в порядке обхода доски; у каждой свои камни и дамэ.</returns>
    /// <exception cref="DomainException">Передан <see cref="StoneColor.Empty"/>.</exception>
    public static IReadOnlyList<Group> AllGroups(Board board, StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        if (color == StoneColor.Empty)
        {
            throw new DomainException("Пустой цвет не образует групп: укажите Black или White.");
        }

        List<Group> groups = [];
        HashSet<Point> visited = [];

        foreach (var point in board.OccupiedPoints(color))
        {
            if (!visited.Add(point))
            {
                continue;
            }

            var group = FindGroup(board, point);
            visited.UnionWith(group.Stones);
            groups.Add(group);
        }

        return groups.AsReadOnly();
    }
}
