namespace GoEngine.Core;

/// <summary>Снятие групп противника, оставшихся без дамэ.</summary>
/// <remarks>
/// Порядок из <c>GO_RULES.md</c>, п. 4: сначала ставится камень, затем снимаются группы
/// противника без дамэ. Перебираются только соседи хода: дамэ может потерять лишь группа,
/// соседняя с новой точкой, поэтому полный обход всех групп доски не нужен. Группа могла
/// лишиться последней дамэ одновременно с нескольких сторон, поэтому соседи отмечаются
/// в <c>visited</c>.
/// </remarks>
internal static class CaptureResolver
{
    /// <summary>Снимает группы противника без дамэ, соседние с поставленным камнем.</summary>
    /// <param name="board">Доска, на которой камень уже стоит: метод меняет её камни на месте.</param>
    /// <param name="point">Точка, в которую только что поставлен камень.</param>
    /// <param name="color">Цвет поставленного камня.</param>
    /// <returns>Снятые камни в порядке обхода групп или пустой результат.</returns>
    internal static CaptureResult Capture(Board board, Point point, StoneColor color)
    {
        var opponent = color.Opponent();
        List<Point> captured = [];
        HashSet<Point> visited = [];

        foreach (var neighbor in point.Neighbors(board.Size))
        {
            if (board.StoneAt(board.IndexOf(neighbor)) != opponent || !visited.Add(neighbor))
            {
                continue;
            }

            var group = GroupTracker.FindGroup(board, neighbor);
            visited.UnionWith(group.Stones);

            if (!group.IsCaptured)
            {
                continue;
            }

            foreach (var stone in group.Stones)
            {
                board.SetStone(board.IndexOf(stone), StoneColor.Empty);
                captured.Add(stone);
            }
        }

        return new CaptureResult(captured.AsReadOnly());
    }
}
