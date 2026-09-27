namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Тактический предохранитель как фильтр ходов: единственное место его правил.</summary>
/// <remarks>
/// Жалоба пользователя: уровни кю «подставляются под съедение сразу, тактику не прорабатывают».
/// Причина измерима: в случайном доигрывании подставленная группа чаще выживает, чем гибнет,
/// поэтому оценка такого хода завышена (на 19×19 три секунды на ход — это всего ≈30 playout'ов).
/// Фильтр — дешёвая проверка ходов без сети: он отсекает подстановки и ничего не обещает про
/// глубину, но убирает самые грубые потери (T-037, D-046).
/// </remarks>
public sealed class TacticalMoveFilter : IMoveFilter
{
    /// <summary>Единственный экземпляр: фильтр не хранит состояния.</summary>
    public static TacticalMoveFilter Instance { get; } = new();

    private TacticalMoveFilter()
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Если безопасных ходов не осталось, играть разрешено любой: вынужденный пас хуже подстановки.
    /// Это то же правило, что и в отборе корневых ходов, и живёт оно теперь в одном месте.
    /// </remarks>
    public MoveFilterResult Apply(Board board, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(moves);

        var safe = TacticalGuard.SafeMoves(board, moves);

        return safe.Count > 0
            ? new MoveFilterResult(safe, EverythingAllowed: false)
            : new MoveFilterResult(moves, EverythingAllowed: true);
    }
}
