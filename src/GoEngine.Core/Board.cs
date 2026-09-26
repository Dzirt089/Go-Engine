namespace GoEngine.Core;

/// <summary>Позиция на доске.</summary>
/// <remarks>
/// Публичный API иммутабелен: <see cref="ApplyMove"/> возвращает новую доску и не меняет текущую.
/// Внутреннее представление — плоский массив <see cref="StoneColor"/> длиной <c>size * size</c>,
/// наружу он не отдаётся никогда. Для MCTS есть <see cref="Clone"/> и <see cref="MakeMove"/>:
/// быстрая копия и мутация на месте.
/// Ход снимает группы противника без дамэ (<c>GO_RULES.md</c>, п. 4) и запоминает снятые камни
/// в <see cref="CapturedStones"/>. Самоубийственный ход отклоняется (<c>GO_RULES.md</c>, п. 5).
/// Проверка ко (T-005) здесь ещё не реализована.
/// <see cref="ApplyMove"/> и <see cref="MakeMove"/> сообщают об отказе исключением
/// <see cref="DomainException"/> — так же, как о ходе в занятую точку; для проверки без исключений
/// есть <see cref="IsLegal"/>.
/// </remarks>
public sealed class Board
{
    private readonly StoneColor[] _stones;
    private CaptureResult _lastCapture = CaptureResult.None;

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
    /// <param name="history">История позиций партии или <c>null</c>, если история не ведётся.</param>
    private Board(BoardSize size, StoneColor[] stones, PositionHistory? history)
    {
        Size = size;
        _stones = stones;
        History = history;
    }

    /// <summary>Размер доски.</summary>
    public BoardSize Size { get; }

    /// <summary>История позиций партии или <c>null</c>, если история не ведётся.</summary>
    /// <remarks>
    /// История одна на партию и передаётся копиям по ссылке: <see cref="Clone"/> и
    /// <see cref="ApplyMove"/> продолжают ту же партию. Позиции добавляет проверка ко (T-005).
    /// </remarks>
    public PositionHistory? History { get; }

    /// <summary>Камни, снятые последним ходом этой доски.</summary>
    /// <remarks>
    /// Внутренний массив наружу не отдаётся: возвращается список только для чтения.
    /// Пас и сдача не снимают ничего, поэтому у свежей доски список пуст.
    /// </remarks>
    public IReadOnlyList<Point> CapturedStones => _lastCapture.CapturedStones;

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
    /// <param name="move">Ход. Пас и сдача позицию не меняют и ничего не снимают.</param>
    /// <returns>Новая доска; исходная доска не меняется.</returns>
    /// <exception cref="DomainException">Ход не задан, точка вне доски или уже занята.</exception>
    /// <remarks>Снятые ходом камни доступны у новой доски в <see cref="CapturedStones"/>.</remarks>
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
    /// <remarks>
    /// История позиций общая с исходной доской: копия продолжает ту же партию. Для анализа
    /// без истории (например, в MCTS) доска создаётся без неё — <see cref="History"/> остаётся <c>null</c>.
    /// </remarks>
    public Board Clone() => new(Size, (StoneColor[])_stones.Clone(), History);

    /// <summary>Привязывает к копии доски историю позиций партии.</summary>
    /// <param name="history">История позиций.</param>
    /// <returns>Новая доска с той же позицией и указанной историей; исходная доска не меняется.</returns>
    public Board WithHistory(PositionHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        return new Board(Size, (StoneColor[])_stones.Clone(), history);
    }

    /// <summary>Ставит камень прямо в этой доске: мутация на месте для MCTS.</summary>
    /// <param name="move">Ход типа <see cref="MoveType.Play"/> на пустую точку.</param>
    /// <exception cref="DomainException">Ход не задан, не является <c>Play</c>, точка вне доски,
    /// занята или ход самоубийственный.</exception>
    /// <remarks>
    /// Порядок из <c>GO_RULES.md</c>, п. 4: сначала ставится камень, затем снимаются группы
    /// противника без дамэ. Снятые камни записываются в <see cref="CapturedStones"/>.
    /// Ход проверяется до мутации, поэтому при <see cref="DomainException"/> доска не меняется.
    /// </remarks>
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

        var legality = CheckPlay(move);

        if (!legality.IsSuccess)
        {
            throw new DomainException(legality.Error!);
        }

        PlaceStone(move);
    }

    /// <summary>Проверяет, разрешён ли ход, не меняя доску.</summary>
    /// <param name="move">Проверяемый ход.</param>
    /// <returns>Успех, если ход легален; иначе причина отказа на русском языке.</returns>
    /// <remarks>
    /// Пас и сдача разрешены всегда (<c>GO_RULES.md</c>, п. 3). Метод не бросает исключений:
    /// ожидаемый отказ — это <see cref="Result{T}"/>, а не <see cref="DomainException"/>.
    /// </remarks>
    public Result<Unit> IsLegal(Move move)
    {
        if (move.IsNone)
        {
            return Result<Unit>.Fail("Ход не задан.");
        }

        return move.Type == MoveType.Play
            ? CheckPlay(move)
            : Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Ставит проверенный камень и снимает группы противника без дамэ.</summary>
    /// <param name="move">Ход типа <see cref="MoveType.Play"/>, уже проверенный <see cref="IsLegal"/>.</param>
    private void PlaceStone(Move move)
    {
        // Результат относится только к последнему ходу: предыдущий список снятых камней не накапливается.
        _lastCapture = CaptureResult.None;
        _stones[IndexOf(move.Point)] = move.Color;

        CaptureAdjacentOpponentGroups(move.Point, move.Color);
    }

    /// <summary>Проверяет ход постановки камня по правилам, не меняя эту доску.</summary>
    /// <param name="move">Ход типа <see cref="MoveType.Play"/>.</param>
    /// <returns>Успех, если ход легален; иначе причина отказа на русском языке.</returns>
    /// <remarks>
    /// Самоубийство проверяется на копии доски: чтобы узнать, останутся ли у своей группы дамэ,
    /// надо сначала снять чужие группы без дамэ, а исходную доску менять нельзя.
    /// </remarks>
    private Result<Unit> CheckPlay(Move move)
    {
        if (!move.Point.IsOnBoard(Size))
        {
            return Result<Unit>.Fail($"Точка {move.Point} находится вне доски {Size}.");
        }

        if (_stones[IndexOf(move.Point)] != StoneColor.Empty)
        {
            return Result<Unit>.Fail($"Точка {move.Point} уже занята.");
        }

        var probe = Clone();
        probe.PlaceStone(move);
        var ownGroup = GroupTracker.FindGroup(probe, move.Point);

        // GO_RULES.md, п. 5: ход разрешён, если у своей группы остались дамэ или если ход снял
        // хотя бы один камень противника. Второе условие и есть исключение для захвата.
        return ownGroup.IsCaptured && !probe._lastCapture.HasCaptures
            ? Result<Unit>.Fail($"Самоубийственный ход: у своей группы в точке {move.Point} не остаётся дамэ.")
            : Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Снимает группы противника без дамэ, соседние с поставленным камнем.</summary>
    /// <param name="point">Точка, в которую только что поставлен камень.</param>
    /// <param name="color">Цвет поставленного камня.</param>
    /// <remarks>
    /// Перебираются только соседи хода: дамэ может потерять лишь группа, соседняя с новой точкой,
    /// поэтому полный обход всех групп доски не нужен. Группа могла лишиться последней дамэ
    /// одновременно с нескольких сторон, поэтому соседи отмечаются в <c>visited</c>.
    /// </remarks>
    private void CaptureAdjacentOpponentGroups(Point point, StoneColor color)
    {
        var opponent = color.Opponent();
        List<Point> captured = [];
        HashSet<Point> visited = [];

        foreach (var neighbor in point.Neighbors(Size))
        {
            if (_stones[IndexOf(neighbor)] != opponent || !visited.Add(neighbor))
            {
                continue;
            }

            var group = GroupTracker.FindGroup(this, neighbor);
            visited.UnionWith(group.Stones);

            if (!group.IsCaptured)
            {
                continue;
            }

            foreach (var stone in group.Stones)
            {
                _stones[IndexOf(stone)] = StoneColor.Empty;
                captured.Add(stone);
            }
        }

        _lastCapture = new CaptureResult(captured.AsReadOnly());
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
