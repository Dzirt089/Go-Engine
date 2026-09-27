namespace GoEngine.Core;

/// <summary>Позиция на доске.</summary>
/// <remarks>
/// Публичный API иммутабелен: <see cref="ApplyMove"/> возвращает новую доску и не меняет текущую.
/// Внутреннее представление — плоский массив <see cref="StoneColor"/> длиной <c>size * size</c>,
/// наружу он не отдаётся никогда. Для MCTS есть <see cref="Clone"/> и <see cref="MakeMove"/>:
/// быстрая копия и мутация на месте.
/// Ход снимает группы противника без дамэ (<c>GO_RULES.md</c>, п. 4) и запоминает снятые камни
/// в <see cref="CapturedStones"/>. Самоубийственный ход отклоняется (<c>GO_RULES.md</c>, п. 5),
/// позиция, повторяющая уже встречавшуюся, — тоже (<c>GO_RULES.md</c>, п. 6). Успешный ход
/// пополняет <see cref="History"/> позицией после хода.
/// <see cref="ApplyMove"/> и <see cref="MakeMove"/> сообщают об отказе исключением
/// <see cref="DomainException"/> — так же, как о ходе в занятую точку; для проверки без исключений
/// есть <see cref="IsLegal"/>.
/// Сама доска отвечает только за позицию и её изменение: правила постановки камня (самоубийство
/// и суперко) вынесены в <c>MoveLegality</c>, снятие групп — в <c>CaptureResolver</c>, поиск групп
/// и дамэ — в <see cref="GroupTracker"/>.
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

    /// <summary>Возвращает копию доски без истории позиций.</summary>
    /// <returns>Новая доска с той же позицией и <see cref="History"/> равным <c>null</c>.</returns>
    /// <remarks>
    /// Нужна анализу — MCTS, playout, разбор вариантов: его ходы не должны пополнять
    /// историю настоящей партии, иначе суперко начнёт запрещать ходы из-за вариантов,
    /// которые в партии не игрались.
    /// </remarks>
    public Board WithoutHistory() => new(Size, (StoneColor[])_stones.Clone(), null);

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
        EnsurePlayable(move);

        var legality = MoveLegality.CheckPlay(this, move);

        if (!legality.IsSuccess)
        {
            throw new DomainException(legality.Error!);
        }

        PlaceStone(move);
    }

    /// <summary>Строит позицию после хода, не меняя эту доску и историю партии.</summary>
    /// <param name="move">Ход типа <see cref="MoveType.Play"/> на пустую точку.</param>
    /// <returns>Копия доски без истории, в которой ход уже сделан.</returns>
    /// <exception cref="DomainException">Ход не задан, не является <c>Play</c>, точка вне доски или занята.</exception>
    /// <remarks>
    /// Нужен проверке ко: чтобы сравнить позицию с историей, ход надо «примерить».
    /// Правила (самоубийство, суперко) здесь не проверяются — это дело <see cref="IsLegal"/>.
    /// Копия создаётся без истории, иначе предпросмотр пополнял бы историю партии.
    /// </remarks>
    internal Board PreviewMove(Move move)
    {
        EnsurePlayable(move);

        var preview = new Board(Size, (StoneColor[])_stones.Clone(), null);
        preview.PlaceStone(move);

        return preview;
    }

    /// <summary>Проверяет, что ход можно поставить на доску: тип, границы, пустая точка.</summary>
    /// <param name="move">Проверяемый ход.</param>
    /// <exception cref="DomainException">Ход не задан, не является <c>Play</c>, точка вне доски или занята.</exception>
    private void EnsurePlayable(Move move)
    {
        if (move.IsNone)
        {
            throw new DomainException("Ход не задан.");
        }

        if (move.Type != MoveType.Play)
        {
            throw new DomainException($"Ход должен быть типа Play, получен {move.Type.Name}.");
        }

        EnsureOnBoard(move.Point);

        if (_stones[IndexOf(move.Point)] != StoneColor.Empty)
        {
            throw new DomainException($"Точка {move.Point} уже занята.");
        }
    }

    /// <summary>Проверяет, разрешён ли ход, не меняя доску.</summary>
    /// <param name="move">Проверяемый ход.</param>
    /// <returns>Успех, если ход легален; иначе причина отказа на русском языке.</returns>
    /// <remarks>
    /// Пас и сдача разрешены всегда (<c>GO_RULES.md</c>, п. 3). Метод не бросает исключений:
    /// ожидаемый отказ — это <see cref="Result{T}"/>, а не <see cref="DomainException"/>.
    /// Проверяются самоубийство (<c>GO_RULES.md</c>, п. 5) и позиционное суперко (п. 6);
    /// если у доски нет истории, ограничение суперко не действует.
    /// </remarks>
    public Result<Unit> IsLegal(Move move)
    {
        if (move.IsNone)
        {
            return Result<Unit>.Fail("Ход не задан.");
        }

        return move.Type == MoveType.Play
            ? MoveLegality.CheckPlay(this, move)
            : Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Ставит проверенный камень и снимает группы противника без дамэ.</summary>
    /// <param name="move">Ход типа <see cref="MoveType.Play"/>, уже проверенный <see cref="IsLegal"/>.</param>
    private void PlaceStone(Move move)
    {
        _stones[IndexOf(move.Point)] = move.Color;

        // Результат относится только к последнему ходу: предыдущий список снятых камней не накапливается.
        _lastCapture = CaptureResolver.Capture(this, move.Point, move.Color);

        // В историю попадает позиция после хода: именно её сравнивает суперко.
        History?.Add(this);
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
    /// <remarks>Внутренний доступ: им пользуются правила хода, чтобы не копировать позицию.</remarks>
    internal int IndexOf(Point point) => (point.Y * Size.Value) + point.X;

    /// <summary>Возвращает цвет камня по индексу массива.</summary>
    /// <param name="index">Индекс, полученный из <see cref="IndexOf"/>.</param>
    /// <returns>Цвет камня в точке или <see cref="StoneColor.Empty"/>.</returns>
    internal StoneColor StoneAt(int index) => _stones[index];

    /// <summary>Ставит камень по индексу массива.</summary>
    /// <param name="index">Индекс, полученный из <see cref="IndexOf"/>.</param>
    /// <param name="color">Новый цвет точки.</param>
    /// <remarks>Мутация на месте: вызывающий отвечает за правила и целостность групп.</remarks>
    internal void SetStone(int index, StoneColor color) => _stones[index] = color;

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
