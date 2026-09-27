namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Выбор хода по эвристикам: уровни 25 и 20 кю.</summary>
/// <remarks>
/// Порядок приоритетов: съесть группу противника в атари, спасти свою группу в атари,
/// занять угол в начале партии, иначе — случайный легальный ход. Доля случайности
/// (<paramref name="randomnessPercent"/>) задаёт силу уровня: чем она больше, тем чаще
/// селектор игнорирует эвристики и ходит наугад.
/// Своего <see cref="Random"/> класс не создаёт (<c>AGENTS_GO.md</c>, п. 7).
/// </remarks>
public sealed class HeuristicMoveSelector : IMoveSelector
{
    /// <summary>Шкала процентов: доля случайности задаётся в процентах.</summary>
    private const int PercentScale = 100;

    /// <summary>Сколько камней на доске считается началом партии для занятия углов.</summary>
    private const int OpeningStoneLimit = 4;

    /// <summary>С какой стороны доски углы занимают четвёртой линией, а не третьей.</summary>
    private const int LargeBoardValue = 19;

    private readonly Random _random;
    private readonly int _randomnessPercent;
    private readonly IMoveFilter _filter;

    /// <summary>Создаёт селектор.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <param name="randomnessPercent">Доля случайных ходов в процентах: 0 — только эвристики, 100 — чистый случай.</param>
    /// <param name="tacticalGuard">Отсекать ли случайные ходы, подставляющие свои группы под захват.</param>
    /// <exception cref="ArgumentOutOfRangeException">Доля случайности вне диапазона 0…100.</exception>
    public HeuristicMoveSelector(Random random, int randomnessPercent = 0, bool tacticalGuard = true)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(randomnessPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(randomnessPercent, PercentScale);

        _random = random;
        _randomnessPercent = randomnessPercent;
        _filter = MoveFilters.Guarded(tacticalGuard);
    }

    /// <inheritdoc />
    public string Name => $"Эвристический ход (случайность {_randomnessPercent}%)";

    /// <inheritdoc />
    public Move SelectMove(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return SelectMove(state.Board, state.ToMove);
    }

    /// <summary>Выбирает ход по позиции и цвету, минуя партию.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <returns>Легальный ход или пас, если легальных ходов нет.</returns>
    public Move SelectMove(Board board, StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        var legalMoves = LegalMoves.For(board, color);

        // Пас разрешён всегда: если ходить некуда, игрок пропускает ход.
        if (legalMoves.Count == 0)
        {
            return Move.Pass(color);
        }

        // Слабые уровни чаще ходят наугад — это и есть их слабость.
        if (_random.Next(PercentScale) < _randomnessPercent)
        {
            return RandomMove(board, legalMoves);
        }

        return FindHeuristicMove(board, color, legalMoves) ?? RandomMove(board, legalMoves);
    }

    /// <summary>Выбирает случайный легальный ход.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="legalMoves">Легальные ходы цвета.</param>
    /// <returns>Легальный ход.</returns>
    /// <remarks>
    /// «Наугад» не значит «в самоатари»: при включённом предохранителе случайный ход берётся
    /// из тактически безопасных. Именно случайная ветка подставляла группы кю-уровней (T-037).
    /// </remarks>
    private Move RandomMove(Board board, IReadOnlyList<Move> legalMoves)
    {
        var allowed = _filter.Apply(board, legalMoves);
        var pool = allowed.EverythingAllowed ? legalMoves : allowed.Moves;

        return pool[_random.Next(pool.Count)];
    }

    /// <summary>Выбирает ход по эвристикам.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="legalMoves">Легальные ходы цвета.</param>
    /// <returns>Ход эвристики или <c>null</c>, если ни одна эвристика не сработала.</returns>
    private static Move? FindHeuristicMove(Board board, StoneColor color, IReadOnlyList<Move> legalMoves)
    {
        var capture = AtariHeuristics.FindCapturingMove(board, color);

        return capture
            ?? AtariHeuristics.FindRescueMove(board, color)
            ?? FindOpeningMove(board, legalMoves);
    }

    /// <summary>Ищет ход в свободный угол в начале партии.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="legalMoves">Легальные ходы цвета.</param>
    /// <returns>Ход в угловую точку или <c>null</c>, если партия уже не в начале.</returns>
    private static Move? FindOpeningMove(Board board, IReadOnlyList<Move> legalMoves)
    {
        if (CountStones(board) > OpeningStoneLimit)
        {
            return null;
        }

        var line = board.Size.Value >= LargeBoardValue ? 3 : 2;
        var far = board.Size.Value - 1 - line;

        // Углы обходим по часовой стрелке: так выбор одинаков при одном и том же состоянии доски.
        Point[] corners =
        [
            new((byte)line, (byte)line),
            new((byte)far, (byte)line),
            new((byte)far, (byte)far),
            new((byte)line, (byte)far)
        ];

        foreach (var corner in corners)
        {
            var move = FindMoveAt(legalMoves, corner);

            if (move is not null)
            {
                return move;
            }
        }

        return null;
    }

    /// <summary>Ищет среди легальных ходов ход в указанную точку.</summary>
    /// <param name="legalMoves">Легальные ходы.</param>
    /// <param name="point">Нужная точка.</param>
    /// <returns>Ход в точку или <c>null</c>, если такой ход нелегален.</returns>
    private static Move? FindMoveAt(IReadOnlyList<Move> legalMoves, Point point)
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

    /// <summary>Считает камни на доске.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns>Число занятых точек.</returns>
    private static int CountStones(Board board) => board.Size.Area - board.EmptyPoints().Count();
}
