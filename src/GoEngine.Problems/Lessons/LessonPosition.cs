namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Позиция шага урока: доска, камни, чей ход и что подсветить.</summary>
/// <remarks>
/// Позиция задаётся расстановкой, а не ходами, и собирается легально (<see cref="ProblemSetup"/>):
/// у доски нет публичного способа поставить камень в обход правил — то же решение, что и в задачах.
/// Пустая расстановка разрешена: первый шаг урока о правилах показывает чистую доску.
/// </remarks>
public sealed class LessonPosition
{
    /// <summary>Создаёт позицию шага.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="stones">Камни расстановки; пусто — чистая доска.</param>
    /// <param name="toMove">Чей ход в позиции.</param>
    /// <param name="highlight">Точки, которые надо подсветить игроку; пусто — подсвечивать нечего.</param>
    /// <exception cref="DomainException">Нарушен инвариант позиции.</exception>
    public LessonPosition(
        BoardSize size,
        IReadOnlyList<ProblemStone> stones,
        StoneColor toMove,
        IReadOnlyList<Point> highlight)
    {
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(stones);
        ArgumentNullException.ThrowIfNull(toMove);
        ArgumentNullException.ThrowIfNull(highlight);

        if (toMove == StoneColor.Empty)
        {
            throw new DomainException("В позиции урока не указано, чей ход.");
        }

        foreach (var stone in stones)
        {
            if (stone.Color == StoneColor.Empty)
            {
                throw new DomainException($"В расстановке урока есть пустая точка {stone.Point}.");
            }

            if (!stone.Point.IsOnBoard(size))
            {
                throw new DomainException($"Камень {stone.Point} находится вне доски {size}.");
            }
        }

        foreach (var point in highlight)
        {
            if (!point.IsOnBoard(size))
            {
                throw new DomainException($"Подсвеченная точка {point} находится вне доски {size}.");
            }
        }

        Size = size;
        Stones = stones;
        ToMove = toMove;
        Highlight = highlight;
    }

    /// <summary>Размер доски.</summary>
    public BoardSize Size { get; }

    /// <summary>Камни расстановки.</summary>
    public IReadOnlyList<ProblemStone> Stones { get; }

    /// <summary>Чей ход.</summary>
    public StoneColor ToMove { get; }

    /// <summary>Точки, подсвеченные игроку: на них смотрит объяснение.</summary>
    public IReadOnlyList<Point> Highlight { get; }
}
