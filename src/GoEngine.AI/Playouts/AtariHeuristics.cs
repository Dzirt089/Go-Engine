namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Эвристики атари, общие для селекторов и playout-политики.</summary>
/// <remarks>
/// Кандидаты берутся из групп на доске, а не перебором всех ходов: группы в атари находятся
/// одним обходом (<see cref="GroupTracker.AllGroups"/>), после чего проверяется легальность
/// одного хода. Для playout'ов это решающая экономия: перебор всех ходов на каждом ходу
/// партии стоил бы сотни проверок правил.
/// </remarks>
internal static class AtariHeuristics
{
    /// <summary>Ищет ход, снимающий группу противника в атари.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <returns>Легальный ход с наибольшим захватом или <c>null</c>, если захватов нет.</returns>
    internal static Move? FindCapturingMove(Board board, StoneColor color)
    {
        // Чем больше группа, тем ценнее захват: ищем самую крупную группу в атари за один проход,
        // без сортировки — эта эвристика вызывается на каждом ходу playout'а.
        Group? target = null;

        foreach (var group in GroupTracker.AllGroups(board, color.Opponent()))
        {
            if (group.IsInAtari && (target is null || group.Stones.Count > target.Value.Stones.Count))
            {
                target = group;
            }
        }

        if (target is null)
        {
            return null;
        }

        var move = Move.Play(target.Value.Liberties.First(), color);

        return board.IsLegal(move).IsSuccess ? move : null;
    }

    /// <summary>Проверяет, остаётся ли своя группа после хода без дамэ или в атари.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="move">Ход, легальность которого уже проверена.</param>
    /// <returns><c>true</c>, если после хода у своей группы меньше двух дамэ.</returns>
    /// <remarks>
    /// Случайные playout'ы чаще всего губят свои группы тем, что сами закрывают их дамэ,
    /// в том числе заполняют собственный глаз. Проверка считается без копии доски: дамэ будущей
    /// группы — пустые соседи точки плюс дамэ соседних своих групп, кроме самой точки.
    /// Захват противника в расчёт не берётся: ходы-захваты разбираются эвристиками атари раньше.
    /// </remarks>
    internal static bool LeavesOwnGroupInAtari(Board board, Move move)
    {
        var color = move.Color;
        HashSet<Point> liberties = [];
        HashSet<Point> visited = [];

        foreach (var neighbor in move.Point.Neighbors(board.Size))
        {
            var neighborColor = board.At(neighbor);

            if (neighborColor == StoneColor.Empty)
            {
                liberties.Add(neighbor);
                continue;
            }

            if (neighborColor != color || !visited.Add(neighbor))
            {
                continue;
            }

            var group = GroupTracker.FindGroup(board, neighbor);
            visited.UnionWith(group.Stones);
            liberties.UnionWith(group.Liberties);
        }

        liberties.Remove(move.Point);

        return liberties.Count <= 1;
    }

    /// <summary>Ищет ход, спасающий свою группу в атари.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <returns>Легальный ход в последнее дамэ своей группы или <c>null</c>, если таких групп нет.</returns>
    internal static Move? FindRescueMove(Board board, StoneColor color)
    {
        foreach (var group in GroupTracker.AllGroups(board, color))
        {
            if (!group.IsInAtari)
            {
                continue;
            }

            var move = Move.Play(group.Liberties.First(), color);

            if (board.IsLegal(move).IsSuccess)
            {
                return move;
            }
        }

        return null;
    }
}
