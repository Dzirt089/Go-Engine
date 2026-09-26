namespace GoEngine.AI.Mcts;

using GoEngine.Core;

/// <summary>Узел дерева MCTS: позиция, ход в неё и статистика посещений.</summary>
/// <remarks>
/// Узел хранит позицию после своего хода и ещё не разобранные ходы из неё, поэтому дерево
/// не пересчитывает правила при каждом спуске. <see cref="Wins"/> считается с точки зрения
/// игрока, сделавшего <see cref="Move"/>: это позволяет выбирать лучший ход сравнением
/// оценок детей одного родителя.
/// </remarks>
public sealed class MctsNode
{
    private readonly List<Move> _untriedMoves;

    /// <summary>Создаёт узел.</summary>
    /// <param name="move">Ход, которым пришли в узел; у корня — <see cref="Move.None"/>.</param>
    /// <param name="parent">Родительский узел или <c>null</c> у корня.</param>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="toMove">Цвет, который ходит из этой позиции.</param>
    /// <param name="untriedMoves">Ещё не разобранные ходы из этой позиции.</param>
    /// <exception cref="ArgumentNullException">Позиция, цвет или список ходов не заданы.</exception>
    public MctsNode(Move move, MctsNode? parent, Board board, StoneColor toMove, IEnumerable<Move> untriedMoves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(toMove);
        ArgumentNullException.ThrowIfNull(untriedMoves);

        Move = move;
        Parent = parent;
        Board = board;
        ToMove = toMove;
        _untriedMoves = [.. untriedMoves];
    }

    /// <summary>Ход, которым пришли в узел.</summary>
    public Move Move { get; }

    /// <summary>Родительский узел или <c>null</c> у корня.</summary>
    public MctsNode? Parent { get; }

    /// <summary>Позиция после <see cref="Move"/>.</summary>
    public Board Board { get; }

    /// <summary>Цвет, который ходит из этой позиции.</summary>
    public StoneColor ToMove { get; }

    /// <summary>Дети узла — позиции после ходов из этой.</summary>
    public List<MctsNode> Children { get; } = [];

    /// <summary>Сколько раз узел участвовал в playout'ах.</summary>
    public int Visits { get; private set; }

    /// <summary>Сколько playout'ов из узла выиграл игрок, сделавший <see cref="Move"/>.</summary>
    public double Wins { get; private set; }

    /// <summary>Ходы, которые из этой позиции ещё не пробовали.</summary>
    public IReadOnlyList<Move> UntriedMoves => _untriedMoves.AsReadOnly();

    /// <summary>Проверяет, что все ходы из позиции уже попробованы.</summary>
    public bool IsFullyExpanded => _untriedMoves.Count == 0;

    /// <summary>Считает оценку UCB1 — баланс выигрышей и исследования.</summary>
    /// <param name="c">Коэффициент исследования: больше — шире поиск.</param>
    /// <returns>Оценка узла; у непосещённого узла — бесконечность, поэтому он выбирается первым.</returns>
    public double Ucb1(double c)
    {
        // Непосещённый узел обязан быть выбран раньше посещённых, иначе часть ходов никогда не проверится.
        if (Visits == 0 || Parent is null || Parent.Visits == 0)
        {
            return double.PositiveInfinity;
        }

        return (Wins / Visits) + (c * Math.Sqrt(Math.Log(Parent.Visits) / Visits));
    }

    /// <summary>Забирает очередной неразобранный ход.</summary>
    /// <returns>Последний из неразобранных ходов.</returns>
    /// <remarks>Ход берётся с конца списка: удаление из конца не сдвигает остальные элементы.</remarks>
    internal Move TakeUntriedMove()
    {
        var last = _untriedMoves.Count - 1;
        var move = _untriedMoves[last];
        _untriedMoves.RemoveAt(last);

        return move;
    }

    /// <summary>Учитывает результат партии.</summary>
    /// <param name="winner">Победитель партии.</param>
    internal void RegisterResult(StoneColor winner)
    {
        Visits++;

        if (Move.Color == winner)
        {
            Wins += 1;
        }
    }
}
