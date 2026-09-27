namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Итог перебора решений задачи.</summary>
/// <param name="SolverWins">У решающего есть выигрывающая линия в пределах глубины.</param>
/// <param name="WinningMoves">Все первые ходы решающего, которые выигрывают в пределах глубины.</param>
/// <param name="Nodes">Сколько позиций перебрано.</param>
/// <param name="Depth">Глубина перебора в полуходах.</param>
/// <param name="LimitReached">Перебор упёрся в предел узлов: результат неполный.</param>
public readonly record struct SolutionSearch(
    bool SolverWins,
    IReadOnlyList<Move> WinningMoves,
    int Nodes,
    int Depth,
    bool LimitReached);

/// <summary>Независимый перебор решений (минимакс) по правилам движка.</summary>
/// <remarks>
/// Нужен, чтобы поймать <b>второе</b> решение, которого нет в дереве задачи: дерево может быть
/// неполным, и тогда задача неверна. Перебор ограничен глубиной и списком кандидатов — точек рядом
/// с целевой группой, поэтому он честно сообщает границы: <see cref="SolutionSearch.Depth"/> и
/// <see cref="SolutionSearch.LimitReached"/>. Пас входит в перебор: если защищающийся не находит
/// хода, линия всё равно продолжается.
/// </remarks>
public sealed class ProblemSolver
{
    private readonly Problem _problem;
    private readonly int _maxNodes;

    /// <summary>Создаёт перебор для задачи.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="maxNodes">Предел числа перебираемых позиций.</param>
    public ProblemSolver(Problem problem, int maxNodes = 200_000)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);

        _problem = problem;
        _maxNodes = maxNodes;
    }

    /// <summary>Ищет выигрывающие первые ходы решающего.</summary>
    /// <param name="depth">Глубина перебора в полуходах.</param>
    /// <returns>Итог перебора.</returns>
    public SolutionSearch Search(int depth = 5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        var built = ProblemSetup.Build(_problem.Size, _problem.Stones);

        if (!built.IsSuccess)
        {
            return new SolutionSearch(false, [], 0, depth, false);
        }

        var nodes = 0;
        var limited = false;
        List<Move> winning = [];

        foreach (var move in Candidates(built.Value!, _problem.SolverColor))
        {
            var after = Apply(built.Value!, move);

            if (Wins(after, _problem.SolverColor.Opponent(), depth - 1, ref nodes, ref limited))
            {
                winning.Add(move);
            }

            if (limited)
            {
                break;
            }
        }

        return new SolutionSearch(winning.Count > 0, winning.AsReadOnly(), nodes, depth, limited);
    }

    private bool Wins(Board board, StoneColor side, int depth, ref int nodes, ref bool limited)
    {
        if (ProblemGoalCheck.IsAchieved(_problem, board))
        {
            return true;
        }

        if (depth <= 0)
        {
            return false;
        }

        if (++nodes > _maxNodes)
        {
            limited = true;

            return false;
        }

        var moves = Candidates(board, side);

        if (moves.Count == 0)
        {
            // Ходов нет — сторона пасует: для защищающегося это значит, что он не мешает.
            return side != _problem.SolverColor || ProblemGoalCheck.IsAchieved(_problem, board);
        }

        if (side == _problem.SolverColor)
        {
            foreach (var move in moves)
            {
                if (Wins(Apply(board, move), side.Opponent(), depth - 1, ref nodes, ref limited))
                {
                    return true;
                }

                if (limited)
                {
                    return false;
                }
            }

            return false;
        }

        foreach (var move in moves)
        {
            if (!Wins(Apply(board, move), side.Opponent(), depth - 1, ref nodes, ref limited))
            {
                return false;
            }

            if (limited)
            {
                return false;
            }
        }

        return true;
    }

    private List<Move> Candidates(Board board, StoneColor side)
    {
        HashSet<Point> points = [];

        foreach (var target in _problem.TargetPoints)
        {
            if (board.At(target) != StoneColor.Empty)
            {
                points.Add(target);
            }

            foreach (var neighbour in target.Neighbors(board.Size))
            {
                points.Add(neighbour);
            }
        }

        List<Move> moves = [];

        foreach (var point in points)
        {
            if (board.At(point) != StoneColor.Empty)
            {
                continue;
            }

            var move = Move.Play(point, side);

            if (board.IsLegal(move).IsSuccess)
            {
                moves.Add(move);
            }
        }

        return moves;
    }

    private static Board Apply(Board board, Move move) =>
        move.Type == MoveType.Play ? board.ApplyMove(move) : board;
}
