namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Тактический предохранитель: не даёт движку подставлять свои группы под захват.</summary>
/// <remarks>
/// Жалоба пользователя: уровни кю «подставляются под съедение сразу, тактику не прорабатывают».
/// Причина измерима: в случайном доигрывании подставленная группа чаще выживает, чем гибнет,
/// поэтому оценка такого хода завышена (на 19×19 три секунды на ход — это всего ≈30 playout'ов).
/// Предохранитель — дешёвая проверка корневых ходов без сети: она отсекает самоатари и ничего
/// не обещает про глубину, но убирает самые грубые потери.
/// </remarks>
public static class TacticalGuard
{
    /// <summary>Проверяет, безопасен ли ход тактически.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="move">Ход; предполагается, что он уже признан легальным.</param>
    /// <returns><c>true</c>, если ход не подставляет свою группу под захват.</returns>
    /// <remarks>
    /// Ход считается оправданным, если он снимает камни противника, если своя соседняя группа
    /// уже была в атари (вынужденная защита или продление) или если это пас. Во всех остальных
    /// случаях ход, оставляющий своей группе одно дамэ, отклоняется.
    /// </remarks>
    public static bool IsSafeMove(Board board, Move move)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (move.IsNone || move.Type != MoveType.Play)
        {
            return true;
        }

        // Проверка идёт по копии без истории партии: Board.PlaceStone дописывает позицию
        // в PositionHistory, поэтому ApplyMove на доске с историей делал бы проверяемый ход
        // «уже встречавшимся» и движок начинал бы отдавать ходы, запрещённые суперко (T-037).
        var rules = board.WithoutHistory();

        // Легальность — не дело предохранителя: нелегальный ход отсекает тот, кто его выбрал.
        // Проверка нужна лишь потому, что ApplyMove на самоубийственном ходе бросает исключение.
        if (!rules.IsLegal(move).IsSuccess)
        {
            return true;
        }

        // Захват оправдывает ход: противник теряет камни, даже если наша группа похудела.
        if (rules.ApplyMove(move).CapturedStones.Count > 0)
        {
            return true;
        }

        // Своя группа уже в атари: это защита или продление, отказываться от неё нельзя.
        foreach (var neighbor in move.Point.Neighbors(board.Size))
        {
            if (board.At(neighbor) == move.Color && GroupTracker.FindGroup(board, neighbor).IsInAtari)
            {
                return true;
            }
        }

        return !AtariHeuristics.LeavesOwnGroupInAtari(rules, move);
    }

    /// <summary>Оставляет из ходов только тактически безопасные.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="moves">Ходы-кандидаты.</param>
    /// <returns>Безопасные ходы; пустой список, если таких нет.</returns>
    /// <exception cref="ArgumentNullException">Позиция или список ходов не заданы.</exception>
    public static IReadOnlyList<Move> SafeMoves(Board board, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(moves);

        List<Move> safe = [];

        foreach (var move in moves)
        {
            if (IsSafeMove(board, move))
            {
                safe.Add(move);
            }
        }

        return safe.AsReadOnly();
    }
}
