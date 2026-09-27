namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Фильтры ходов: предохранитель включён, выключен или не нужен.</summary>
/// <remarks>
/// Выбор фильтра — единственное, что осталось у селекторов от предохранителя: сами правила
/// отбора живут в <see cref="TacticalMoveFilter"/> (D-051).
/// </remarks>
public static class MoveFilters
{
    /// <summary>Фильтр без ограничений: допустим любой ход.</summary>
    public static IMoveFilter None { get; } = new NoMoveFilter();

    /// <summary>Возвращает фильтр по признаку «предохранитель включён».</summary>
    /// <param name="enabled">Включён ли тактический предохранитель.</param>
    /// <returns>Тактический фильтр или фильтр без ограничений.</returns>
    public static IMoveFilter Guarded(bool enabled) => enabled ? TacticalMoveFilter.Instance : None;

    /// <summary>Фильтр без ограничений: возвращает ходы как есть.</summary>
    private sealed class NoMoveFilter : IMoveFilter
    {
        /// <inheritdoc />
        public MoveFilterResult Apply(Board board, IReadOnlyList<Move> moves)
        {
            ArgumentNullException.ThrowIfNull(board);
            ArgumentNullException.ThrowIfNull(moves);

            return new MoveFilterResult(moves, EverythingAllowed: true);
        }
    }
}
