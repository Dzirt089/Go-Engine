namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Прохождение задачи: позиция, очередь хода, вердикты и отмена.</summary>
/// <remarks>
/// Игрок ходит только за <see cref="Problem.SolverColor"/>. Ответы соперника берутся из дерева
/// решения детерминированно: применяется первый принимаемый ход узла. Состояние всегда
/// восстанавливается повторным проигрыванием ходов игрока от начальной позиции — поэтому отмена
/// и перезапуск не могут разойтись с деревом.
/// </remarks>
public sealed class ProblemSession
{
    private readonly Problem _problem;
    private readonly Board _initial;
    private readonly List<Move> _solverMoves = [];

    /// <summary>Создаёт сессию решения задачи.</summary>
    /// <param name="problem">Задача.</param>
    /// <exception cref="DomainException">Позиция задачи не собирается легальными ходами.</exception>
    public ProblemSession(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var built = ProblemSetup.Build(problem.Size, problem.Stones);

        if (!built.IsSuccess)
        {
            throw new DomainException($"Задача {problem.Id}: позиция не собирается — {built.Error}");
        }

        _problem = problem;
        _initial = built.Value!;
        Board = _initial;
        ToMove = problem.SolverColor;
        State = ProblemState.InProgress;
    }

    /// <summary>Задача, которую решают.</summary>
    public Problem Problem => _problem;

    /// <summary>Текущая позиция.</summary>
    public Board Board { get; private set; }

    /// <summary>Чей ход в текущей позиции.</summary>
    public StoneColor ToMove { get; private set; }

    /// <summary>Состояние решения.</summary>
    public ProblemState State { get; private set; }

    /// <summary>Сколько ходов сделал игрок.</summary>
    public int SolverMoveCount => _solverMoves.Count;

    /// <summary>Можно ли отменить ход.</summary>
    public bool CanBack => _solverMoves.Count > 0;

    /// <summary>Подсказка: первый принимаемый ход игрока в текущей позиции.</summary>
    public Move? Hint
    {
        get
        {
            var node = Replay();

            return node.Moves.Count > 0 ? node.Moves[0].Move : null;
        }
    }

    /// <summary>Принимаемые ходы игрока в текущей позиции.</summary>
    public IReadOnlyList<Move> AcceptedMoves
    {
        get
        {
            var node = Replay();
            List<Move> moves = [];

            foreach (var move in node.Moves)
            {
                moves.Add(move.Move);
            }

            return moves.AsReadOnly();
        }
    }

    /// <summary>Играет ход игрока.</summary>
    /// <param name="move">Ход.</param>
    /// <returns>Вердикт: принят, решён, неверен или задача повреждена.</returns>
    public ProblemVerdict Play(Move move)
    {
        ArgumentNullException.ThrowIfNull(move);

        var node = Replay();

        if (State == ProblemState.Solved || node.IsLeaf)
        {
            State = ProblemState.Solved;

            return ProblemVerdict.Solved;
        }

        var accepted = Find(node, move);

        if (accepted is null)
        {
            return ProblemVerdict.Wrong;
        }

        if (!TryApply(Board, move, out var board))
        {
            return ProblemVerdict.Failed;
        }

        _solverMoves.Add(move);
        Board = board;

        var next = accepted.Value.Next;

        if (next.IsLeaf)
        {
            State = ProblemState.Solved;
            ToMove = _problem.SolverColor.Opponent();

            return ProblemVerdict.Solved;
        }

        // Ответ соперника берётся из дерева: первый принимаемый ход узла.
        var reply = next.Moves[0].Move;

        if (!TryApply(Board, reply, out var afterReply))
        {
            return ProblemVerdict.Failed;
        }

        Board = afterReply;
        ToMove = _problem.SolverColor;

        var after = next.Moves[0].Next;

        if (after.IsLeaf)
        {
            State = ProblemState.Solved;

            return ProblemVerdict.Solved;
        }

        return ProblemVerdict.Correct;
    }

    /// <summary>Играет ход игрока в точку.</summary>
    /// <param name="point">Точка доски.</param>
    /// <returns>Вердикт хода.</returns>
    public ProblemVerdict Play(Point point) => Play(Move.Play(point, ToMove));

    /// <summary>Отменяет последний ход игрока вместе с ответом соперника.</summary>
    /// <returns><c>true</c>, если было что отменять.</returns>
    public bool Back()
    {
        if (_solverMoves.Count == 0)
        {
            return false;
        }

        _solverMoves.RemoveAt(_solverMoves.Count - 1);
        _ = Replay();
        State = ProblemState.InProgress;

        return true;
    }

    /// <summary>Возвращает задачу в начальную позицию.</summary>
    public void Reset()
    {
        _solverMoves.Clear();
        _ = Replay();
        State = ProblemState.InProgress;
    }

    /// <summary>Восстанавливает позицию и возвращает узел дерева для текущего хода игрока.</summary>
    private ProblemNode Replay()
    {
        var board = _initial;
        var node = _problem.Solution;

        foreach (var move in _solverMoves)
        {
            var accepted = Find(node, move);

            if (accepted is null)
            {
                Board = _initial;

                return ProblemNode.Leaf;
            }

            if (TryApply(board, move, out var afterMove))
            {
                board = afterMove;
            }

            node = accepted.Value.Next;

            if (node.IsLeaf)
            {
                break;
            }

            var reply = node.Moves[0];

            if (TryApply(board, reply.Move, out var afterReply))
            {
                board = afterReply;
            }

            node = reply.Next;
        }

        Board = board;
        ToMove = node.IsLeaf ? _problem.SolverColor.Opponent() : _problem.SolverColor;

        return node;
    }

    private static ProblemMove? Find(ProblemNode node, Move move)
    {
        foreach (var accepted in node.Moves)
        {
            if (accepted.Move.Type != move.Type)
            {
                continue;
            }

            if (move.Type != MoveType.Play || accepted.Move.Point == move.Point)
            {
                return accepted;
            }
        }

        return null;
    }

    private static bool TryApply(Board board, Move move, out Board after)
    {
        after = board;

        if (move.Type != MoveType.Play)
        {
            return true;
        }

        if (!board.IsLegal(move).IsSuccess)
        {
            return false;
        }

        after = board.ApplyMove(move);

        return true;
    }
}
