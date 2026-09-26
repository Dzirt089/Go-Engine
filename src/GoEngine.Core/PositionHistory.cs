namespace GoEngine.Core;

/// <summary>История позиций партии: нужна позиционному суперко (<c>GO_RULES.md</c>, п. 6).</summary>
/// <remarks>
/// История изменяема только своими методами: <see cref="Hashes"/> отдаёт снимок, поэтому
/// добавить или удалить позицию в обход <see cref="Add"/> нельзя. Хранится хеш, а не сама доска:
/// для суперко важно лишь то, встречалась ли позиция раньше.
/// </remarks>
public sealed class PositionHistory
{
    private readonly HashSet<PositionHash> _hashes = [];

    /// <summary>Число позиций в истории.</summary>
    public int Count => _hashes.Count;

    /// <summary>Снимок хешей истории.</summary>
    /// <remarks>
    /// Возвращается копия: коллекция создаётся заново при каждом обращении, поэтому изменение
    /// снимка не меняет историю. В горячих путях (MCTS) снимок брать не нужно — есть
    /// <see cref="Contains(Board)"/>.
    /// </remarks>
    public IReadOnlyCollection<PositionHash> Hashes => _hashes.ToArray();

    /// <summary>Добавляет позицию доски в историю.</summary>
    /// <param name="board">Позиция.</param>
    /// <exception cref="DomainException">В позиции встретился цвет, не являющийся камнем.</exception>
    public void Add(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        _hashes.Add(PositionHash.From(board));
    }

    /// <summary>Проверяет, встречалась ли позиция доски в партии.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns><c>true</c>, если такая позиция уже была.</returns>
    /// <exception cref="DomainException">В позиции встретился цвет, не являющийся камнем.</exception>
    public bool Contains(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        return _hashes.Contains(PositionHash.From(board));
    }

    /// <summary>Проверяет, встречался ли такой хеш позиции.</summary>
    /// <param name="hash">Хеш позиции.</param>
    /// <returns><c>true</c>, если позиция с таким хешем уже была.</returns>
    public bool Contains(PositionHash hash) => _hashes.Contains(hash);
}
