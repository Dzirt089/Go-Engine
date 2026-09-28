using GoEngine.AI;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Независимый перебор для проверки задач: своё окно кандидатов и свой минимакс.</summary>
/// <remarks>
/// <para>
/// Написан заново и <see cref="ProblemSolver"/> не использует: иначе тест подтверждал бы ту же
/// реализацию, что и проверяемый код (тот же принцип, что у отдельной приёмки в <c>tools/</c>).
/// </para>
/// <para>
/// Цель задаётся предикатом позиции, поэтому и захват, и два глаза, и «мертва» считаются одним
/// перебором. Окно кандидатов настраивается радиусом: правило согласованности проверяется
/// на радиусе библиотеки (2) и на расширенном (3), чтобы набор не зависел от ширины окна.
/// </para>
/// </remarks>
internal sealed class ProblemOracle
{
    private readonly Dictionary<(ulong Hash, int Depth, int ToMove), bool> _memo = [];

    /// <summary>Создаёт перебор по задаче.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="radius">Радиус окна кандидатов вокруг целевой группы.</param>
    internal ProblemOracle(Problem problem, int radius = Problem.DefaultWindowRadius)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        Problem = problem;
        Radius = radius;
        Kind = problem.Goal == ProblemGoal.Live ? GoalKind.Live : GoalKind.Capture;
        Dead = problem.Goal == ProblemGoal.Dead;
        SolverColor = problem.SolverColor;
    }

    /// <summary>Задача, которую проверяют.</summary>
    internal Problem Problem { get; }

    /// <summary>Радиус окна кандидатов.</summary>
    internal int Radius { get; }

    private GoalKind Kind { get; }

    private bool Dead { get; }

    /// <summary>Цвет, который добивается цели.</summary>
    internal StoneColor SolverColor { get; }

    /// <summary>Бюджет перебора: больше — дольше, но полнее.</summary>
    internal long NodeBudget { get; init; } = 400_000;

    /// <summary>Перебор упёрся в бюджет: результат неполон.</summary>
    internal bool Exhausted { get; private set; }

    /// <summary>Снята ли целевая группа.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns><c>true</c>, если все камни целевой группы исчезли с доски.</returns>
    internal bool CaptureDone(Board board)
    {
        foreach (var point in Problem.TargetPoints)
        {
            if (board.At(point) != StoneColor.Empty)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Два настоящих глаза у целевой группы.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns><c>true</c>, если группа на доске и у неё два глаза по <see cref="EyeDetector"/>.</returns>
    internal bool LiveDone(Board board)
    {
        if (board.At(Problem.Target) == StoneColor.Empty)
        {
            return false;
        }

        var color = board.At(Problem.Target);
        List<Point> group = [];

        foreach (var point in Problem.TargetPoints)
        {
            if (board.At(point) == color)
            {
                group.Add(point);
            }
        }

        if (group.Count == 0)
        {
            return false;
        }

        var eyes = 0;

        foreach (var point in board.EmptyPoints())
        {
            if (IsAdjacentTo(group, point) && EyeDetector.IsEye(board, point, color))
            {
                eyes++;
            }
        }

        return eyes >= 2;
    }

    /// <summary>Достигнута ли цель задачи в позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns><c>true</c>, если цель выполнена.</returns>
    internal bool GoalDone(Board board) => Kind == GoalKind.Capture ? CaptureDone(board) : LiveDone(board);

    /// <summary>Целевая группа ещё на доске.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns><c>true</c>, если в целевой точке стоит камень цвета цели.</returns>
    internal bool GroupOnBoard(Board board) => board.At(Problem.Target) == Problem.TargetColor;

    /// <summary>Мертва ли группа: она на доске, и атакующий форсирует снятие за глубину.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="depth">Глубина в полуходах; защищающийся ходит первым.</param>
    /// <returns><c>true</c>, если снятие форсировано.</returns>
    internal bool DeadForced(Board board, int depth) =>
        GroupOnBoard(board) && Wins(board, Problem.TargetColor, depth);

    /// <summary>Форсирует ли решающий цель за глубину.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="toMove">Чья очередь хода.</param>
    /// <param name="depth">Глубина в полуходах.</param>
    /// <returns><c>true</c>, если цель достигается при лучшей защите.</returns>
    /// <remarks>Бюджет узлов считается заново на каждый вызов; таблица позиций сохраняется между вызовами.</remarks>
    internal bool Wins(Board board, StoneColor toMove, int depth)
    {
        Nodes = 0;
        Exhausted = false;

        return Play(board, toMove, depth);
    }

    private bool Play(Board board, StoneColor toMove, int depth)
    {
        if (GoalDone(board))
        {
            return true;
        }

        if (depth <= 0)
        {
            return false;
        }

        if (Nodes++ > NodeBudget)
        {
            Exhausted = true;

            return false;
        }

        var key = (PositionHash.From(board).Hash, depth, toMove.Id);

        if (_memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var moves = WindowMoves(board, toMove);
        bool result;

        if (moves.Count == 0)
        {
            // Ходить нечем — пас: очередь переходит сопернику, глубина уменьшается.
            result = Play(board, toMove.Opponent(), depth - 1);
        }
        else if (toMove == SolverColor)
        {
            result = false;

            foreach (var move in moves)
            {
                if (Play(board.ApplyMove(move), toMove.Opponent(), depth - 1))
                {
                    result = true;

                    break;
                }
            }
        }
        else
        {
            result = true;

            foreach (var move in moves)
            {
                if (!Play(board.ApplyMove(move), toMove.Opponent(), depth - 1))
                {
                    result = false;

                    break;
                }
            }
        }

        _memo[key] = result;

        return result;
    }

    /// <summary>Сколько позиций перебрано.</summary>
    internal long Nodes { get; private set; }

    /// <summary>Выигрывающие первые ходы стороны: цель достигается после каждого из них.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="side">Сторона, которая ходит.</param>
    /// <param name="depth">Заявленный горизонт в полуходах.</param>
    /// <returns>Ходы в каноническом порядке обхода доски.</returns>
    internal IReadOnlyList<Move> WinningMoves(Board board, StoneColor side, int depth)
    {
        List<Move> winning = [];

        foreach (var move in WindowMoves(board, side))
        {
            var after = board.ApplyMove(move);

            if (Dead)
            {
                if (DeadForced(after, depth - 1))
                {
                    winning.Add(move);
                }

                continue;
            }

            if (GoalDone(after) || Wins(after, side.Opponent(), depth - 1))
            {
                winning.Add(move);
            }
        }

        return Canonical(winning);
    }

    /// <summary>Минимальное число полуходов до цели при лучшей игре обеих сторон.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="side">Сторона, которая ходит первой.</param>
    /// <param name="limit">Предел поиска.</param>
    /// <returns>Число полуходов или 0, если за предел цель не достижима.</returns>
    internal int MinHalfMoves(Board board, StoneColor side, int limit)
    {
        for (var depth = 1; depth <= limit; depth++)
        {
            if (Wins(board, side, depth))
            {
                return depth;
            }

            if (Exhausted)
            {
                return 0;
            }
        }

        return 0;
    }

    /// <summary>Легальные ходы-кандидаты в окне вокруг целевой группы.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="side">Сторона, которая ходит.</param>
    /// <returns>Ходы в каноническом порядке обхода доски.</returns>
    internal IReadOnlyList<Move> WindowMoves(Board board, StoneColor side)
    {
        List<Move> moves = [];

        foreach (var point in WindowPoints(board))
        {
            if (!board.IsEmpty(point))
            {
                continue;
            }

            var move = Move.Play(point, side);

            if (board.IsLegal(move).IsSuccess)
            {
                moves.Add(move);
            }
        }

        return Canonical(moves);
    }

    /// <summary>Точки окна вокруг текущей целевой группы.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns>Точки окна в каноническом порядке обхода доски.</returns>
    internal IReadOnlyList<Point> WindowPoints(Board board)
    {
        List<Point> stones = [];

        if (board.At(Problem.Target) != StoneColor.Empty)
        {
            foreach (var stone in GroupTracker.FindGroup(board, Problem.Target).Stones)
            {
                stones.Add(stone);
            }
        }

        if (stones.Count == 0)
        {
            stones.AddRange(Problem.TargetPoints);
        }

        var size = board.Size.Value;
        HashSet<Point> points = [];

        if (Radius * 2 + 1 >= size)
        {
            foreach (var point in board.AllPoints())
            {
                points.Add(point);
            }
        }
        else
        {
            foreach (var stone in stones)
            {
                for (var dy = -Radius; dy <= Radius; dy++)
                {
                    for (var dx = -Radius; dx <= Radius; dx++)
                    {
                        var x = stone.X + dx;
                        var y = stone.Y + dy;

                        if (x >= 0 && y >= 0 && x < size && y < size)
                        {
                            points.Add(new Point((byte)x, (byte)y));
                        }
                    }
                }
            }
        }

        return Canonical(points);
    }

    private static IReadOnlyList<Move> Canonical(IEnumerable<Move> moves) =>
        [.. moves.OrderBy(move => move.Point.Y).ThenBy(move => move.Point.X)];

    private static IReadOnlyList<Point> Canonical(IEnumerable<Point> points) =>
        [.. points.OrderBy(point => point.Y).ThenBy(point => point.X)];

    private static bool IsAdjacentTo(IReadOnlyList<Point> group, Point point)
    {
        foreach (var stone in group)
        {
            if (Math.Abs(stone.X - point.X) + Math.Abs(stone.Y - point.Y) == 1)
            {
                return true;
            }
        }

        return false;
    }

    private enum GoalKind
    {
        /// <summary>Снятие целевой группы.</summary>
        Capture,

        /// <summary>Два настоящих глаза у целевой группы.</summary>
        Live,
    }
}
