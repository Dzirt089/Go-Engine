using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Правило согласованности: принимаемый набор в каждом узле равен выигрывающим ходам.</summary>
/// <remarks>
/// Проверка независимая: выигрышные ходы считает <see cref="ProblemOracle"/>, а не
/// <see cref="ProblemSolver"/>. Расхождение — падение теста, а не предупреждение: неполный набор
/// даёт игроку вердикт «неверно» за правильный ход, а это ложный отказ.
/// </remarks>
public sealed class ProblemConsistencyTests
{
    /// <summary>Задачи, у которых каждый выигрышный первый ход требует не меньше трёх полуходов.</summary>
    private static readonly string[] MultiMoveIds = ["nk-001", "nk-002", "nk-003", "cg-010", "ti-001", "sb-001", "lf-005"];

    /// <summary>Задачи, для которых проверяется устойчивость на радиусе 3 и горизонте +2.</summary>
    private static readonly string[] RobustIds = ["cg-010", "ti-001", "sb-001", "lf-001", "lf-002", "lf-003", "lf-004", "lf-005"];

    public static TheoryData<string> Ids()
    {
        TheoryData<string> data = [];

        foreach (var problem in ProblemLibrary.All)
        {
            data.Add(problem.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void ПринимаемыйНабор_ВКаждомУзле_РавенВыигрывающимХодам(string id)
    {
        var problem = ProblemLibrary.ById(id);
        var issues = ConsistencyIssues(problem!);

        Assert.True(issues.Count == 0, $"{id}: {string.Join("; ", issues)}");
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Каждый_Лист_ПодтверждёнЦелью(string id)
    {
        var problem = ProblemLibrary.ById(id);
        var oracle = new ProblemOracle(problem!);
        List<string> issues = [];
        WalkLeaves(problem!, oracle, Board(problem!), problem!.Solution, 0, issues);

        Assert.True(issues.Count == 0, $"{id}: {string.Join("; ", issues)}");
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Подсказка_СамыйБыстрыйВыигрывающийХод(string id)
    {
        var problem = ProblemLibrary.ById(id);
        var oracle = new ProblemOracle(problem!);
        var board = Board(problem!);
        var accepted = problem!.AcceptedFirstMoves;

        Assert.NotEmpty(accepted);

        var fastest = accepted
            .Select((move, index) => (Move: move, Index: index, Cost: Cost(problem, oracle, board, move)))
            .OrderBy(entry => entry.Cost)
            .ThenBy(entry => entry.Move.Point.Y)
            .ThenBy(entry => entry.Move.Point.X)
            .First();

        Assert.Equal(0, fastest.Index);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Задача_НеСлабая_И_Горизонт_Задан(string id)
    {
        var problem = ProblemLibrary.ById(id);

        Assert.False(problem!.IsWeak, $"{id}: принимаемых первых ходов {problem.AcceptedFirstMoveCount}");
        Assert.True(problem.CheckDepth > 0, $"{id}: не задан заявленный горизонт (GX)");
    }

    [Fact]
    public void Многоходовые_КаждыйВыигрышныйПервыйХод_НеКорочеТрёхПолуходов()
    {
        foreach (var id in MultiMoveIds)
        {
            var problem = ProblemLibrary.ById(id);

            Assert.NotNull(problem);

            var oracle = new ProblemOracle(problem);
            var board = Board(problem);

            foreach (var move in problem.AcceptedFirstMoves)
            {
                Assert.True(
                    Cost(problem, oracle, board, move) >= 3,
                    $"{id}: ход {move.Point.X},{move.Point.Y} выигрывает быстрее трёх полуходов");
            }
        }
    }

    [Fact]
    public void Устойчивость_НаОбъявленномОкне_ИГоризонтеПлюсДва()
    {
        foreach (var id in RobustIds)
        {
            var problem = ProblemLibrary.ById(id);

            Assert.NotNull(problem);

            var board = Board(problem);
            var accepted = problem.AcceptedFirstMoves;
            var horizon = problem.CheckDepth;
            var window = problem.WindowRadius;

            // Объявленное окно, расширенное окно и горизонт +2 обязаны давать тот же набор.
            foreach (var (radius, depth) in new[] { (window, horizon), (window + 2, horizon), (window, horizon + 2) })
            {
                var oracle = new ProblemOracle(problem, radius);
                var winning = oracle.WinningMoves(board, problem.SolverColor, depth);

                Assert.True(
                    Same(winning, accepted),
                    $"{id}: на радиусе {radius} и горизонте {depth} выигрышных {winning.Count}, принято {accepted.Count}");
            }
        }
    }

    [Fact]
    public void Ход_ВыполняющийЦель_Принимается_ВнеДерева()
    {
        // Дерево содержит только медленный выигрыш: снимающего хода в нём нет.
        var source = ProblemLibrary.ById("cg-001");

        Assert.NotNull(source);

        var slow = new ProblemNode([new ProblemMove(Move.Play(new Point(3, 0), StoneColor.Black), ProblemNode.Leaf)]);
        var problem = new Problem(
            source.Id,
            source.Name,
            source.Size,
            source.Komi,
            source.SolverColor,
            source.Goal,
            source.Stones,
            source.Target,
            source.Rank,
            source.Description,
            slow,
            source.CheckDepth,
            source.WindowRadius);
        var session = new ProblemSession(problem);

        // ci (2,0) снимает целевую группу, но принимаемого набора дерева в нём нет.
        Assert.Equal(ProblemVerdict.Solved, session.Play(new Point(2, 0)));
        Assert.Equal(ProblemState.Solved, session.State);

        // Откат возвращает задачу в решение.
        Assert.True(session.Back());
        Assert.Equal(ProblemState.InProgress, session.State);

        // Ход вне дерева, который цель не выполняет, по-прежнему «неверно».
        Assert.Equal(ProblemVerdict.Wrong, session.Play(new Point(8, 8)));
    }

    [Fact]
    public void Библиотека_СодержитМногоходовуюЖизнь()
    {
        var life = ProblemLibrary.ById("lf-005");

        Assert.NotNull(life);
        Assert.Equal(ProblemGoal.Live, life.Goal);
        Assert.Contains("lf-005", MultiMoveIds, StringComparer.Ordinal);
    }

    /// <summary>Задача многоходовая: каждый выигрышный первый ход не короче трёх полуходов до цели.</summary>
    /// <param name="problem">Задача.</param>
    /// <returns><c>true</c>, если задача многоходовая.</returns>
    private static bool IsMultiMove(Problem problem)
    {
        var oracle = new ProblemOracle(problem);
        var board = Board(problem);

        return problem.AcceptedFirstMoves.All(move => Cost(problem, oracle, board, move) >= 3);
    }

    private static List<string> ConsistencyIssues(Problem problem)
    {
        var oracle = new ProblemOracle(problem);
        var board = Board(problem);
        List<string> issues = [];

        Walk(problem, oracle, board, problem.Solution, problem.SolverColor, 0, issues);

        return issues;
    }

    private static void Walk(
        Problem problem,
        ProblemOracle oracle,
        Board board,
        ProblemNode node,
        StoneColor side,
        int ply,
        List<string> issues)
    {
        if (node.IsLeaf)
        {
            LeafIssues(problem, oracle, board, ply, issues);

            return;
        }

        var remaining = problem.CheckDepth - ply;

        if (side == problem.SolverColor)
        {
            var expected = oracle.WinningMoves(board, side, Math.Max(1, remaining));
            var accepted = Points(node);

            if (!Same(expected, accepted))
            {
                issues.Add(
                    $"узел {ply}: принято [{DescribePoints(accepted)}], выигрышных [{DescribeMoves(expected)}]");
            }
        }
        else
        {
            foreach (var move in node.Moves)
            {
                if (move.Move.Color != side)
                {
                    issues.Add($"узел {ply}: ход за {move.Move.Color.Name}, а ходит {side.Name}");
                }

                // Ответ защиты сделан: дальше ходит решающий и обязан по-прежнему форсировать цель.
                var after = board.ApplyMove(move.Move);
                var keeps = oracle.Wins(after, problem.SolverColor, Math.Max(1, remaining - 1));

                if (!keeps)
                {
                    issues.Add($"узел {ply}: после ответа {move.Move.Point.X},{move.Move.Point.Y} победа не форсируется");
                }
            }
        }

        foreach (var move in node.Moves)
        {
            Walk(problem, oracle, board.ApplyMove(move.Move), move.Next, side.Opponent(), ply + 1, issues);
        }
    }

    private static void WalkLeaves(Problem problem, ProblemOracle oracle, Board board, ProblemNode node, int ply, List<string> issues)
    {
        if (node.IsLeaf)
        {
            LeafIssues(problem, oracle, board, ply, issues);

            return;
        }

        foreach (var move in node.Moves)
        {
            WalkLeaves(problem, oracle, board.ApplyMove(move.Move), move.Next, ply + 1, issues);
        }
    }

    private static void LeafIssues(Problem problem, ProblemOracle oracle, Board board, int ply, List<string> issues)
    {
        if (problem.Goal == ProblemGoal.Dead)
        {
            if (!oracle.GroupOnBoard(board))
            {
                issues.Add($"лист {ply}: целевая группа снята — это не цель dead");
            }

            if (!oracle.DeadForced(board, problem.CheckDepth))
            {
                issues.Add($"лист {ply}: захват не форсирован за {problem.CheckDepth} полуходов");
            }

            // Перекрёстная сверка с проверкой библиотеки: приговор обязан совпасть.
            if (!ProblemGoalCheck.TargetPresent(board, problem)
                || !ProblemGoalCheck.ForcedCapture(board, problem, problem.CheckDepth, problem.TargetColor))
            {
                issues.Add($"лист {ply}: проверка библиотеки не подтверждает лист цели dead");
            }

            return;
        }

        if (!oracle.GoalDone(board))
        {
            issues.Add($"лист {ply}: цель {problem.Goal.Name} не достигнута");
        }

        if (!ProblemGoalCheck.IsAchieved(problem, board, problem.CheckDepth))
        {
            issues.Add($"лист {ply}: проверка библиотеки не подтверждает лист");
        }
    }

    /// <summary>Число полуходов до цели, если сыграть этот ход: 1 — цель достигается сразу.</summary>
    private static int Cost(Problem problem, ProblemOracle oracle, Board board, Move move)
    {
        var after = board.ApplyMove(move);

        if (oracle.GoalDone(after))
        {
            return 1;
        }

        var rest = oracle.MinHalfMoves(after, problem.SolverColor.Opponent(), problem.CheckDepth + 2);

        return rest == 0 ? int.MaxValue : rest + 1;
    }

    private static bool Same(IReadOnlyList<Move> left, IReadOnlyList<Point> right) =>
        left.Count == right.Count && left.All(move => right.Contains(move.Point));

    private static bool Same(IReadOnlyList<Move> left, IReadOnlyList<Move> right) =>
        left.Count == right.Count && left.All(move => right.Any(other => other.Point == move.Point));

    private static List<Point> Points(ProblemNode node)
    {
        List<Point> points = [];

        foreach (var move in node.Moves)
        {
            points.Add(move.Move.Point);
        }

        return points;
    }

    private static string DescribePoints(IEnumerable<Point> points) =>
        string.Join(" ", points.Select(point => $"{point.X},{point.Y}"));

    private static string DescribeMoves(IEnumerable<Move> moves) =>
        string.Join(" ", moves.Select(move => $"{move.Point.X},{move.Point.Y}"));

    private static Board Board(Problem problem)
    {
        var built = ProblemSetup.Build(problem.Size, problem.Stones);

        Assert.True(built.IsSuccess, $"{problem.Id}: позиция не собирается — {built.Error}");

        return built.Value!;
    }
}
