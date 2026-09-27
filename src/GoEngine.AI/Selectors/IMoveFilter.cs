namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Отбор ходов, которые движок считает допустимыми в позиции.</summary>
/// <remarks>
/// Селекторы не знают правил отбора: они спрашивают фильтр и играют то, что он разрешил.
/// Тактический предохранитель — одна из реализаций (D-046, D-051).
/// </remarks>
public interface IMoveFilter
{
    /// <summary>Отбирает допустимые ходы из предложенных.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="moves">Ходы-кандидаты.</param>
    /// <returns>Допустимые ходы и признак, что ограничений нет.</returns>
    /// <exception cref="ArgumentNullException">Позиция или список ходов не заданы.</exception>
    MoveFilterResult Apply(Board board, IReadOnlyList<Move> moves);
}

/// <summary>Что вернул фильтр ходов: какие ходы допустимы.</summary>
/// <param name="Moves">Допустимые ходы в исходном порядке.</param>
/// <param name="EverythingAllowed">Ограничений нет: допустим любой ход, список не сузился.</param>
/// <remarks>
/// Признак <paramref name="EverythingAllowed"/> нужен, когда фильтр не нашёл ни одного допустимого
/// хода: тогда играть разрешено любой ход, и селектору незачем сужать список кандидатов.
/// </remarks>
public readonly record struct MoveFilterResult(IReadOnlyList<Move> Moves, bool EverythingAllowed);
