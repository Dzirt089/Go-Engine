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
    private readonly Problem? _problem;
    private readonly Board? _board;
    private readonly ProblemGoal _goal;
    private readonly StoneColor _solverColor;
    private readonly IReadOnlyList<Point> _targetPoints;
    private readonly Point _target;
    private readonly int _maxNodes;

    /// <summary>Создаёт перебор для задачи.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="maxNodes">Предел числа перебираемых позиций.</param>
    public ProblemSolver(Problem problem, int maxNodes = 200_000)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);

        _problem = problem;
        _goal = problem.Goal;
        _solverColor = problem.SolverColor;
        _targetPoints = problem.TargetPoints;
        _target = problem.Target;
        _maxNodes = maxNodes;
    }

    /// <summary>Создаёт перебор прямо по позиции: нужен для проверки жизни внутри цели «мертва».</summary>
    /// <param name="board">Позиция; история партии не нужна и не учитывается.</param>
    /// <param name="goal">Цель перебора: <see cref="ProblemGoal.Live"/> или <see cref="ProblemGoal.Capture"/>.</param>
    /// <param name="solverColor">Цвет, который добивается цели.</param>
    /// <param name="target">Точка целевой группы.</param>
    /// <param name="targetPoints">Камни целевой группы в этой позиции.</param>
    /// <param name="maxNodes">Предел числа перебираемых позиций.</param>
    /// <remarks>
    /// Второго перебора в проекте нет: этим конструктором цель «мертва» считается теми же
    /// правилами и тем же минимаксом, только роли сторон меняются местами.
    /// </remarks>
    public ProblemSolver(
        Board board,
        ProblemGoal goal,
        StoneColor solverColor,
        Point target,
        IReadOnlyList<Point> targetPoints,
        int maxNodes = 200_000)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(solverColor);
        ArgumentNullException.ThrowIfNull(targetPoints);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);

        _problem = null;
        _board = board;
        _goal = goal;
        _solverColor = solverColor;
        _targetPoints = targetPoints;
        _target = target;
        _maxNodes = maxNodes;
    }

    /// <summary>Ищет выигрывающие первые ходы решающего.</summary>
    /// <param name="depth">Глубина перебора в полуходах.</param>
    /// <returns>Итог перебора.</returns>
    /// <remarks>
    /// Для цели <see cref="ProblemGoal.Dead"/> приговор выносится вложенной проверкой
    /// <see cref="ProblemGoalCheck.ForcedCaptureDetailed"/>, и её перебор входит в <c>Nodes</c>:
    /// без этой суммы отчёт показывал «0 узлов» — как будто приговор вынесен без вычислений.
    /// Если суммарный перебор упирается в предел узлов, <c>LimitReached</c> это показывает, и тогда
    /// приговора нет: неполный перебор не объявляет группу мёртвой.
    /// </remarks>
    public SolutionSearch Search(int depth = 5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        Board? start = _board;

        if (start is null)
        {
            var built = ProblemSetup.Build(_problem!.Size, _problem.Stones);

            if (!built.IsSuccess)
            {
                return new SolutionSearch(false, [], 0, depth, false);
            }

            start = built.Value;
        }

        var root = start!;
        var nodes = 0;
        var limited = false;
        List<Move> winning = [];

        if (_goal == ProblemGoal.Dead)
        {
            // «Мертва» — свойство позиции после хода убивающего: дальше защищающийся играет первым
            // и пытается построить два глаза в пределах заявленной границы задачи.
            var band = _problem is { CheckDepth: > 0 } dead ? dead.CheckDepth : depth;

            foreach (var move in Candidates(root, _solverColor))
            {
                // Ход убивает, если после него защищающийся (он ходит первым) уже не может уйти
                // от захвата в пределах заявленной границы. Критерий тот же, что и в приговоре.
                // Работа вложенного перебора обязана попадать в отчёт: иначе «0 узлов» читается
                // как приговор без вычислений, хотя внутри ForcedCapture идёт настоящий поиск.
                var probe = ProblemGoalCheck.ForcedCaptureDetailed(
                    Apply(root, move), _problem!, band, _solverColor.Opponent());

                nodes += probe.Nodes;
                limited |= probe.LimitReached;

                if (probe.Forced)
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

        foreach (var move in Candidates(root, _solverColor))
        {
            var after = Apply(root, move);

            if (Wins(after, _solverColor.Opponent(), depth - 1, ref nodes, ref limited))
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

    /// <summary>Может ли защищающийся форсировать два глаза за оставшуюся глубину.</summary>
    /// <param name="board">Позиция; защищающийся ходит первым.</param>
    /// <param name="depth">Заявленная граница в полуходах.</param>
    /// <param name="nodes">Счётчик перебранных позиций (общий с внешним перебором).</param>
    /// <param name="limited">Признак упора в предел узлов: доказательства нет.</param>
    /// <returns><c>true</c>, если защита успевает (или перебор неполон).</returns>
    /// <remarks>
    /// Перебор неполный (упёрся в предел узлов) — ответ «успевает»: так задача никогда не будет
    /// объявлена мёртвой по недоказанному перебору. Обратная ошибка безопаснее для задач.
    /// </remarks>
    private bool DefenderCanForceEyes(Board board, int depth, ref int nodes, ref bool limited)
    {
        if (depth <= 0)
        {
            return true;
        }

        var target = board.At(_target);

        if (target == StoneColor.Empty)
        {
            // Целевой группы на доске нет: жить нечем.
            return false;
        }

        var group = _problem is null
            ? GroupTracker.FindGroup(board, _target).Stones
            : _problem.TargetPoints;

        var solver = new ProblemSolver(board, ProblemGoal.Live, target, _target, group, _maxNodes);

        var result = solver.Search(depth);

        nodes += result.Nodes;

        if (result.LimitReached)
        {
            limited = true;

            return true;
        }

        return result.SolverWins;
    }

    private bool Wins(Board board, StoneColor side, int depth, ref int nodes, ref bool limited)
    {
        if (GoalReached(board))
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
            return side != _solverColor || GoalReached(board);
        }

        if (side == _solverColor)
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

    /// <summary>Достигнута ли цель перебора в позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns><c>true</c>, если цель достигнута.</returns>
    /// <remarks>
    /// Для задачи спрашиваем её же проверку цели; для перебора по позиции (проверка жизни) цель —
    /// два настоящих глаза у группы в целевой точке.
    /// </remarks>
    private bool GoalReached(Board board)
    {
        if (_problem is not null)
        {
            return ProblemGoalCheck.IsAchieved(_problem, board);
        }

        if (_goal == ProblemGoal.Live)
        {
            return board.At(_target) == _solverColor
                && ProblemGoalCheck.CountRealEyes(board, _solverColor, GroupTracker.FindGroup(board, _target).Stones) >= 2;
        }

        return _targetPoints.All(point => board.At(point) == StoneColor.Empty);
    }

    /// <summary>Кандидаты перебора: объявленное окно вокруг камней целевой группы.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="side">Чья очередь.</param>
    /// <returns>Легальные ходы-кандидаты.</returns>
    /// <remarks>
    /// <para>
    /// Окно — все точки на шахматном расстоянии не больше объявленного радиуса
    /// (<see cref="Problem.DefaultWindowRadius"/> по умолчанию) от <b>любого камня целевой группы</b>,
    /// плюс все дамэ группы. Радиус считается от камней группы, а не от одной точки цели: узкое окно
    /// давало ложное «победы нет», а окно вокруг одной точки — расхождение наборов.
    /// </para>
    /// <para>
    /// Окно — ограничение нашего перебора, а не правило игры: ход, который по правилам сразу выполняет
    /// цель, принимается и вне окна (<see cref="ProblemSession"/>).
    /// </para>
    /// </remarks>
    private List<Move> Candidates(Board board, StoneColor side)
    {
        var radius = _problem?.WindowRadius ?? Problem.DefaultWindowRadius;

        HashSet<Point> points = [];
        HashSet<Point> stones = board.At(_target) == StoneColor.Empty
            ? [.. _targetPoints]
            : [.. GroupTracker.FindGroup(board, _target).Stones];

        foreach (var stone in stones)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var x = stone.X + dx;
                    var y = stone.Y + dy;

                    if (x >= 0 && y >= 0 && x < board.Size.Value && y < board.Size.Value)
                    {
                        points.Add(new Point((byte)x, (byte)y));
                    }
                }
            }
        }

        // Все дамэ целевой группы входят в окно даже при маленьком радиусе.
        foreach (var stone in stones)
        {
            foreach (var neighbour in stone.Neighbors(board.Size))
            {
                if (board.At(neighbour) == StoneColor.Empty)
                {
                    points.Add(neighbour);
                }
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
