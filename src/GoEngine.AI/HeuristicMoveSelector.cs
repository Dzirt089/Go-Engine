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

    /// <summary>Создаёт селектор.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <param name="randomnessPercent">Доля случайных ходов в процентах: 0 — только эвристики, 100 — чистый случай.</param>
    /// <exception cref="ArgumentOutOfRangeException">Доля случайности вне диапазона 0…100.</exception>
    public HeuristicMoveSelector(Random random, int randomnessPercent = 0)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(randomnessPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(randomnessPercent, PercentScale);

        _random = random;
        _randomnessPercent = randomnessPercent;
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
            return legalMoves[_random.Next(legalMoves.Count)];
        }

        return FindHeuristicMove(board, color, legalMoves) ?? legalMoves[_random.Next(legalMoves.Count)];
    }

    /// <summary>Выбирает ход по эвристикам.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="legalMoves">Легальные ходы цвета.</param>
    /// <returns>Ход эвристики или <c>null</c>, если ни одна эвристика не сработала.</returns>
    private static Move? FindHeuristicMove(Board board, StoneColor color, IReadOnlyList<Move> legalMoves)
    {
        var capture = FindCaptureMove(board, color, legalMoves);

        return capture ?? FindRescueMove(board, color, legalMoves) ?? FindOpeningMove(board, legalMoves);
    }

    /// <summary>Ищет ход, снимающий больше всего камней противника.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="legalMoves">Легальные ходы цвета.</param>
    /// <returns>Ход с наибольшим захватом или <c>null</c>, если захватов нет.</returns>
    private static Move? FindCaptureMove(Board board, StoneColor color, IReadOnlyList<Move> legalMoves)
    {
        Move? best = null;
        var bestCaptured = 0;

        foreach (var move in legalMoves)
        {
            var captured = CountCapturedStones(board, move.Point, color);

            if (captured > bestCaptured)
            {
                bestCaptured = captured;
                best = move;
            }
        }

        return best;
    }

    /// <summary>Ищет ход, спасающий свою группу в атари.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="legalMoves">Легальные ходы цвета.</param>
    /// <returns>Ход в последнее дамэ своей группы или <c>null</c>, если таких групп нет.</returns>
    private static Move? FindRescueMove(Board board, StoneColor color, IReadOnlyList<Move> legalMoves)
    {
        foreach (var group in GroupTracker.AllGroups(board, color))
        {
            if (!group.IsInAtari)
            {
                continue;
            }

            var rescue = FindMoveAt(legalMoves, group.Liberties.First());

            if (rescue is not null)
            {
                return rescue;
            }
        }

        return null;
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

    /// <summary>Считает, сколько камней противника снимет ход в точку.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="color">Цвет хода.</param>
    /// <returns>Число камней, у которых этот ход — последнее дамэ; 0, если снятий нет.</returns>
    /// <remarks>
    /// Доска при этом не копируется: группа противника снимается тогда и только тогда,
    /// когда её единственное дамэ — точка хода.
    /// </remarks>
    private static int CountCapturedStones(Board board, Point point, StoneColor color)
    {
        var opponent = color.Opponent();
        var captured = 0;
        HashSet<Point> visited = [];

        foreach (var neighbor in point.Neighbors(board.Size))
        {
            if (board.At(neighbor) != opponent || !visited.Add(neighbor))
            {
                continue;
            }

            var group = GroupTracker.FindGroup(board, neighbor);
            visited.UnionWith(group.Stones);

            if (group.Liberties.Count == 1 && group.Liberties.Contains(point))
            {
                captured += group.Stones.Count;
            }
        }

        return captured;
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
