namespace GoEngine.Core;

/// <summary>Позиция на доске.</summary>
/// <remarks>
/// Публичный API иммутабелен: <see cref="ApplyMove"/> возвращает новую доску и не меняет текущую.
/// Внутреннее представление — плоский массив <see cref="StoneColor"/> длиной <c>size * size</c>,
/// наружу он не отдаётся никогда. Для MCTS есть <see cref="Clone"/> и <see cref="MakeMove"/>:
/// быстрая копия и мутация на месте.
/// Снятие групп (T-002), проверка самоубийства (T-003) и ко (T-005) здесь не реализуются:
/// <see cref="ApplyMove"/> только ставит камень.
/// </remarks>
public sealed class Board
{
    private readonly StoneColor[] _stones;

    /// <summary>Создаёт пустую доску указанного размера.</summary>
    /// <param name="size">Размер доски.</param>
    public Board(BoardSize size)
    {
        Size = size;
        _stones = new StoneColor[size.Area];
        Array.Fill(_stones, StoneColor.Empty);
    }

    /// <summary>Создаёт доску поверх готового массива камней. Вызывается только из <see cref="Clone"/>.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="stones">Уже заполненный массив камней, который переходит во владение доски.</param>
    private Board(BoardSize size, StoneColor[] stones)
    {
        Size = size;
        _stones = stones;
    }

    /// <summary>Размер доски.</summary>
    public BoardSize Size { get; }

    /// <summary>Возвращает цвет в точке.</summary>
    /// <param name="point">Точка доски.</param>
    /// <returns><see cref="StoneColor.Empty"/>, если в точке нет камня.</returns>
    /// <exception cref="DomainException">Точка находится вне доски.</exception>
    public StoneColor At(Point point)
    {
        EnsureOnBoard(point);

        return _stones[IndexOf(point)];
    }

    /// <summary>Проверяет, что точка на доске и пуста.</summary>
    /// <param name="point">Точка доски.</param>
    /// <returns><c>true</c>, если в точке нет камня.</returns>
    /// <exception cref="DomainException">Точка находится вне доски.</exception>
    public bool IsEmpty(Point point) => At(point) == StoneColor.Empty;

    /// <summary>Ставит камень и возвращает новую доску.</summary>
    /// <param name="move">Ход. Пас и сдача позицию не меняют.</param>
    /// <returns>Новая доска; исходная доска не меняется.</returns>
    /// <exception cref="DomainException">Ход не задан, точка вне доски или уже занята.</exception>
    public Board ApplyMove(Move move)
    {
        if (move.IsNone)
        {
            throw new DomainException("Ход не задан.");
        }

        var next = Clone();

        // Пас и сдача позицию не меняют, поэтому копия возвращается без изменений.
        if (move.Type == MoveType.Play)
        {
            next.MakeMove(move);
        }

        return next;
    }

    /// <summary>Создаёт независимую копию доски.</summary>
    /// <returns>Копия, изменения которой не затрагивают исходную доску.</returns>
    public Board Clone() => new(Size, (StoneColor[])_stones.Clone());

    /// <summary>Ставит камень прямо в этой доске: мутация на месте для MCTS.</summary>
    /// <param name="move">Ход типа <see cref="MoveType.Play"/> на пустую точку.</param>
    /// <exception cref="DomainException">Ход не задан, не является <c>Play</c>, точка вне доски или занята.</exception>
    public void MakeMove(Move move)
    {
        if (move.IsNone)
        {
            throw new DomainException("Ход не задан.");
        }

        if (move.Type != MoveType.Play)
        {
            throw new DomainException($"MakeMove принимает только ход Play, получен {move.Type.Name}.");
        }

        EnsureOnBoard(move.Point);

        var index = IndexOf(move.Point);

        if (_stones[index] != StoneColor.Empty)
        {
            throw new DomainException($"Точка {move.Point} уже занята.");
        }

        _stones[index] = move.Color;
    }

    /// <summary>Перечисляет все точки доски слева направо, сверху вниз.</summary>
    /// <returns>Все точки доски, включая занятые.</returns>
    public IEnumerable<Point> AllPoints()
    {
        for (var y = 0; y < Size.Value; y++)
        {
            for (var x = 0; x < Size.Value; x++)
            {
                yield return new Point((byte)x, (byte)y);
            }
        }
    }

    /// <summary>Перечисляет пустые точки доски.</summary>
    /// <returns>Точки, в которых нет камней.</returns>
    public IEnumerable<Point> EmptyPoints()
    {
        foreach (var point in AllPoints())
        {
            if (_stones[IndexOf(point)] == StoneColor.Empty)
            {
                yield return point;
            }
        }
    }

    /// <summary>Перечисляет точки, занятые камнями указанного цвета.</summary>
    /// <param name="color">Цвет камней: <see cref="StoneColor.Black"/> или <see cref="StoneColor.White"/>.</param>
    /// <returns>Точки с камнями указанного цвета.</returns>
    /// <exception cref="DomainException">Передан <see cref="StoneColor.Empty"/>.</exception>
    public IEnumerable<Point> OccupiedPoints(StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(color);

        if (color == StoneColor.Empty)
        {
            throw new DomainException("Пустой цвет не занимает точки доски: для пустых точек есть EmptyPoints.");
        }

        return EnumerateOccupiedPoints(color);
    }

    /// <summary>Перечисляет точки указанного цвета.</summary>
    /// <remarks>Вынесено из <see cref="OccupiedPoints"/>, чтобы проверка входа выполнялась сразу, а не при перечислении.</remarks>
    /// <param name="color">Цвет камней, уже проверенный вызывающим кодом.</param>
    /// <returns>Точки с камнями указанного цвета.</returns>
    private IEnumerable<Point> EnumerateOccupiedPoints(StoneColor color)
    {
        foreach (var point in AllPoints())
        {
            if (_stones[IndexOf(point)] == color)
            {
                yield return point;
            }
        }
    }

    /// <summary>Вычисляет индекс точки в плоском массиве.</summary>
    /// <param name="point">Точка, уже проверенная на принадлежность доске.</param>
    /// <returns>Индекс в массиве камней.</returns>
    private int IndexOf(Point point) => (point.Y * Size.Value) + point.X;

    /// <summary>Проверяет, что точка лежит на доске.</summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <exception cref="DomainException">Точка находится вне доски.</exception>
    private void EnsureOnBoard(Point point)
    {
        if (!point.IsOnBoard(Size))
        {
            throw new DomainException($"Точка {point} находится вне доски {Size}.");
        }
    }
}
