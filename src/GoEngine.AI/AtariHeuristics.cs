namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Эвристики атари, общие для селекторов и playout-политики.</summary>
/// <remarks>
/// Захват считается без копирования доски: группа противника снимается тогда и только тогда,
/// когда её единственное дамэ — точка хода. Вынесено из <see cref="HeuristicMoveSelector"/>
/// в отдельный класс, чтобы политика playout'ов не дублировала те же правила.
/// </remarks>
internal static class AtariHeuristics
{
    /// <summary>Ищет ход, снимающий больше всего камней противника.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="legalMoves">Легальные ходы цвета.</param>
    /// <returns>Ход с наибольшим захватом или <c>null</c>, если захватов нет.</returns>
    internal static Move? FindCaptureMove(Board board, StoneColor color, IReadOnlyList<Move> legalMoves)
    {
        Move? best = null;
        var bestCaptured = 0;

        foreach (var move in legalMoves)
        {
            var captured = CountCapturedStones(board, move.Point, color);

            if (captured > bestCaptured)
            {
                bestCaptured = captured;
                best = move;
            }
        }

        return best;
    }

    /// <summary>Ищет ход, спасающий свою группу в атари.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="legalMoves">Легальные ходы цвета.</param>
    /// <returns>Ход в последнее дамэ своей группы или <c>null</c>, если таких групп нет.</returns>
    internal static Move? FindRescueMove(Board board, StoneColor color, IReadOnlyList<Move> legalMoves)
    {
        foreach (var group in GroupTracker.AllGroups(board, color))
        {
            if (!group.IsInAtari)
            {
                continue;
            }

            var rescue = FindMoveAt(legalMoves, group.Liberties.First());

            if (rescue is not null)
            {
                return rescue;
            }
        }

        return null;
    }

    /// <summary>Ищет среди легальных ходов ход в указанную точку.</summary>
    /// <param name="legalMoves">Легальные ходы.</param>
    /// <param name="point">Нужная точка.</param>
    /// <returns>Ход в точку или <c>null</c>, если такой ход нелегален.</returns>
    internal static Move? FindMoveAt(IReadOnlyList<Move> legalMoves, Point point)
    {
        foreach (var move in legalMoves)
        {
            if (move.Point == point)
            {
                return move;
            }
        }

        return null;
    }

    /// <summary>Считает, сколько камней противника снимет ход в точку.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="color">Цвет хода.</param>
    /// <returns>Число камней, у которых этот ход — последнее дамэ; 0, если снятий нет.</returns>
    private static int CountCapturedStones(Board board, Point point, StoneColor color)
    {
        var opponent = color.Opponent();
        var captured = 0;
        HashSet<Point> visited = [];

        foreach (var neighbor in point.Neighbors(board.Size))
        {
            if (board.At(neighbor) != opponent || !visited.Add(neighbor))
            {
                continue;
            }

            var group = GroupTracker.FindGroup(board, neighbor);
            visited.UnionWith(group.Stones);

            if (group.Liberties.Count == 1 && group.Liberties.Contains(point))
            {
                captured += group.Stones.Count;
            }
        }

        return captured;
    }
}
