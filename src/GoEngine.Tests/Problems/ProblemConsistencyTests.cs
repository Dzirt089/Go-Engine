using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Согласованность задач: у доказанных целей — с перебором, у решения источника — с источником.</summary>
/// <remarks>
/// <para>
/// Библиотека состоит из задач <see cref="ProblemGoal.Reference"/>: их исход движком не доказан
/// (перебор показал, что ни снятие, ни два глаза, ни ко не форсируются), поэтому проверяется форма —
/// легальность, чередование, непустое дерево, источник с формулировкой и то, что линия источника
/// проходится целиком, а посторонний ход отклоняется. Обещать большего нельзя.
/// </para>
/// <para>
/// Проверки доказанных целей не ослаблены: они выполняются на образцах
/// (<see cref="EngineFixtures"/>) тем же независимым перебором <see cref="ProblemOracle"/>.
/// </para>
/// </remarks>
public sealed class ProblemConsistencyTests
{
    /// <summary>Образцы, у которых каждый выигрышный первый ход требует не меньше трёх полуходов.</summary>
    private static readonly string[] MultiMove = ["capture-two-liberties", "bent-four"];

    /// <summary>Образцы, у которых набор выигрышных ходов устойчив к окну и горизонту.</summary>
    private static readonly string[] Robust = ["capture-two-liberties", "two-eyes", "bent-four"];

    /// <summary>Идентификаторы задач библиотеки.</summary>
    /// <returns>Данные теории по каждой задаче.</returns>
    public static TheoryData<string> LibraryIds()
    {
        TheoryData<string> data = [];

        foreach (var problem in ProblemLibrary.All)
        {
            data.Add(problem.Id);
        }

        return data;
    }

    /// <summary>Имена образцов с доказанными целями.</summary>
    /// <returns>Данные теории по каждому образцу.</returns>
    public static TheoryData<string> FixtureNames() =>
    [
        "capture-in-corner",
        "capture-two-liberties",
        "nakade",
        "two-eyes",
        "bent-four",
    ];

    [Theory]
    [MemberData(nameof(LibraryIds))]
    public void РешениеИсточника_ФормаЗадачиВерна(string id)
    {
        var problem = ProblemLibrary.ById(id);

        Assert.NotNull(problem);
        Assert.Equal(ProblemGoal.Reference, problem.Goal);
        Assert.False(string.IsNullOrWhiteSpace(problem.Source), $"{id}: не указан источник решения");
        Assert.False(string.IsNullOrWhiteSpace(problem.Description), $"{id}: нет формулировки для игрока");
        Assert.False(problem.Solution.IsLeaf, $"{id}: дерево решения пусто — задача решалась бы без хода");

        var report = ProblemChecker.Check(problem);

        Assert.True(report.IsValid, $"{id}: {string.Join("; ", report.Issues)}");
    }

    [Theory]
    [MemberData(nameof(LibraryIds))]
    public void РешениеИсточника_ЛинияПроходитсяЦеликом(string id)
    {
        var problem = ProblemLibrary.ById(id)!;
        var session = new ProblemSession(problem);
        var node = problem.Solution;
        var moves = 0;

        while (!node.IsLeaf)
        {
            moves++;

            var verdict = session.Play(node.Moves[0].Move);

            if (verdict == ProblemVerdict.Solved)
            {
                break;
            }

            Assert.Equal(ProblemVerdict.Correct, verdict);

            node = node.Moves[0].Next;
            node = node.IsLeaf ? node : node.Moves[0].Next;
        }

        Assert.Equal(ProblemState.Solved, session.State);
        Assert.True(moves > 0, $"{id}: линия источника пуста");
    }

    [Theory]
    [MemberData(nameof(LibraryIds))]
    public void РешениеИсточника_ПринимаетсяРовноХодИсточника(string id)
    {
        var problem = ProblemLibrary.ById(id)!;
        var node = problem.Solution;

        // Линия источника — одна ветвь: в каждом узле ровно один принимаемый ход. Другие
        // продолжения приложение отклонит, и это записано в PROBLEMS.md как ограничение вида.
        while (!node.IsLeaf)
        {
            Assert.Single(node.Moves);
            node = node.Moves[0].Next;
        }
    }

    [Theory]
    [MemberData(nameof(LibraryIds))]
    public void РешениеИсточника_ПостороннийХодОтклонён(string id)
    {
        var problem = ProblemLibrary.ById(id)!;
        var session = new ProblemSession(problem);
        var before = Fingerprint(session.Board);
        var far = new Point((byte)(problem.Size.Value - 2), 1);

        var verdict = session.Play(Move.Play(far, problem.SolverColor));

        Assert.Equal(ProblemVerdict.Wrong, verdict);
        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Equal(before, Fingerprint(session.Board));
        Assert.Equal(0, session.SolverMoveCount);
    }

    [Theory]
    [MemberData(nameof(LibraryIds))]
    public void РешениеИсточника_СписокНеОбещаетПроверкуДвижком(string id)
    {
        var problem = ProblemLibrary.ById(id)!;

        // Формулировка задачи берётся с источника и не должна утверждать машинную проверку:
        // иначе игрок решит, что исход доказан, а это не так.
        Assert.DoesNotContain("Проверена на горизонте", problem.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("принимаемый набор", problem.Description, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Образец_ПринимаемыйНабор_РавенВыигрывающимХодам(string name)
    {
        var problem = EngineFixtures.Get(name);
        var issues = ConsistencyIssues(problem);

        Assert.True(issues.Count == 0, $"{name}: {string.Join("; ", issues)}");
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Образец_КаждыйЛист_ПодтверждёнЦелью(string name)
    {
        var problem = EngineFixtures.Get(name);
        var oracle = new ProblemOracle(problem);
        List<string> issues = [];

        WalkLeaves(problem, oracle, Board(problem), problem.Solution, 0, issues);

        Assert.True(issues.Count == 0, $"{name}: {string.Join("; ", issues)}");
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Образец_Подсказка_СамыйБыстрыйВыигрывающийХод(string name)
    {
        var problem = EngineFixtures.Get(name);
        var oracle = new ProblemOracle(problem);
        var board = Board(problem);
        var accepted = problem.AcceptedFirstMoves;

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
    [MemberData(nameof(FixtureNames))]
    public void Образец_ГоризонтЗаявлен(string name)
    {
        var problem = EngineFixtures.Get(name);

        Assert.True(problem.CheckDepth > 0, $"{name}: не задан заявленный горизонт (GX)");
        Assert.True(problem.WindowRadius > 0, $"{name}: не задан радиус окна (GW)");
    }

    [Fact]
    public void Образцы_Многоходовые_КаждыйВыигрышныйПервыйХод_НеКорочеТрёхПолуходов()
    {
        foreach (var name in MultiMove)
        {
            var problem = EngineFixtures.Get(name);
            var oracle = new ProblemOracle(problem);
            var board = Board(problem);

            Assert.True(problem.AcceptedFirstMoves.Count > 0, $"{name}: нет принимаемых первых ходов");

            foreach (var move in problem.AcceptedFirstMoves)
            {
                Assert.True(
                    Cost(problem, oracle, board, move) >= 3,
                    $"{name}: ход {move.Point.X},{move.Point.Y} выигрывает быстрее трёх полуходов");
            }
        }
    }

    [Fact]
    public void Образцы_Устойчивы_НаОбъявленномОкне_ИГоризонтеПлюсДва()
    {
        foreach (var name in Robust)
        {
            var problem = EngineFixtures.Get(name);
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
                    $"{name}: на радиусе {radius} и горизонте {depth} выигрышных {winning.Count}, принято {accepted.Count}");
            }
        }
    }

    [Fact]
    public void Ход_ВыполняющийЦель_Принимается_ВнеДерева()
    {
        // Дерево содержит только медленный выигрыш: снимающего хода в нём нет.
        var source = EngineFixtures.Get("capture-in-corner");
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
    public void Образец_МногоходовойЖизни_ОсталсяДляПроверкиЦелиLive()
    {
        var life = EngineFixtures.Get("bent-four");

        Assert.Equal(ProblemGoal.Live, life.Goal);
        Assert.Equal(3, life.CheckDepth);
    }

    /// <summary>Отпечаток позиции: сравнение «до и после» без разбора доски по точкам.</summary>
    /// <param name="board">Доска.</param>
    /// <returns>Строка отпечатка.</returns>
    private static string Fingerprint(Board board)
    {
        var text = new System.Text.StringBuilder();

        foreach (var point in board.AllPoints())
        {
            text.Append(board.At(point).Id);
        }

        return text.ToString();
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
    /// <param name="problem">Задача.</param>
    /// <param name="oracle">Независимый перебор.</param>
    /// <param name="board">Позиция.</param>
    /// <param name="move">Ход.</param>
    /// <returns>Число полуходов или <see cref="int.MaxValue"/>, если цель недостижима.</returns>
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
