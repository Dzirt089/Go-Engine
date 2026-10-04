using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Цель «мертва»: граница проверки, подтверждение листьев захватом и порядок сложности.</summary>
/// <remarks>
/// Приговор по цели <see cref="ProblemGoal.Dead"/> выносится фактом уровня правил — атакующий
/// форсирует захват группы в пределах заявленной границы (<see cref="Problem.CheckDepth"/>).
/// Глазные эвристики в приговоре не участвуют: они дают достаточное условие жизни, а не необходимое.
/// </remarks>
public sealed class DeadGoalTests
{
    /// <summary>Образцы с целью «мертва»: в библиотеке таких задач больше нет.</summary>
    /// <remarks>
    /// Библиотеку заменили задачи из источника, у которых машинного критерия исхода нет.
    /// Проверки цели «мертва» остались в полной силе и выполняются на образцах
    /// (<see cref="EngineFixtures.DeadNames"/>) — тех самых позициях с теми же горизонтами.
    /// </remarks>
    private static IEnumerable<Problem> DeadProblems => EngineFixtures.DeadNames.Select(EngineFixtures.Get);

    [Fact]
    public void Образцы_СодержатМногоходовыеЗадачи_СЦельюМертва()
    {
        var dead = DeadProblems.ToList();

        // Дерево обрезается на приговоре, поэтому длина файла у «мертва» равна одному ходу,
        // а многоходовость измеряется заявленным горизонтом (и снятием под лучшей игрой).
        Assert.True(dead.Count >= 4, $"задач с целью dead: {dead.Count}");
        Assert.True(
            dead.Count(problem => problem.CheckDepth >= 3) >= 4,
            "ожидалось не меньше четырёх задач с горизонтом от трёх полуходов");
    }

    [Fact]
    public void ПереборDead_СчитаетРаботуВложенныхПоисков()
    {
        // Регрессия: внешний счётчик не суммировал вложенные переборы, и отчёт показывал «0 узлов» —
        // это читалось как приговор без вычислений, хотя внутри ForcedCapture идёт настоящий поиск.
        foreach (var problem in DeadProblems)
        {
            var search = new ProblemSolver(problem).Search(problem.CheckDepth);

            Assert.True(search.SolverWins, $"{problem.Id}: перебор не нашёл победы");
            Assert.True(search.Nodes > 0, $"{problem.Id}: перебор отчитался нулём узлов");
            Assert.False(search.LimitReached, $"{problem.Id}: перебор упёрся в предел узлов");
        }
    }

    [Fact]
    public void Каждая_ЗадачаDead_ОбъявляетГраницуПроверки()
    {
        foreach (var problem in DeadProblems)
        {
            Assert.True(problem.CheckDepth > 0, $"{problem.Id}: не задана граница проверки (GX)");
        }
    }

    [Fact]
    public void Каждая_ЗадачаDead_ФорсируетЗахватВНачале()
    {
        foreach (var problem in DeadProblems)
        {
            var board = Board(problem);

            // Атакующий начинает — захват обязан форсироваться в пределах заявленной границы.
            Assert.True(
                ProblemGoalCheck.ForcedCapture(board, problem, problem.CheckDepth),
                $"{problem.Id}: захват не форсируется за {problem.CheckDepth} полуходов");
        }
    }

    [Fact]
    public void Каждая_ЗадачаDead_Нетривиальна_ЗащитаСпасаетсяПервымХодом()
    {
        foreach (var problem in DeadProblems)
        {
            var board = Board(problem);

            // Если группа мертва даже при ходе защиты, задача решалась бы без хода убивающего.
            Assert.False(
                ProblemGoalCheck.ForcedCapture(board, problem, problem.CheckDepth, problem.TargetColor),
                $"{problem.Id}: группа мертва уже при ходе защиты — задача тривиальна");
        }
    }

    [Fact]
    public void Каждый_ЛистDead_ПодтверждёнФорсированнымЗахватом()
    {
        foreach (var problem in DeadProblems)
        {
            List<string> broken = [];
            Walk(problem, problem.Solution, Board(problem), problem.SolverColor, 1, broken);

            Assert.True(broken.Count == 0, $"{problem.Id}: {string.Join("; ", broken)}");
        }
    }

    [Fact]
    public void Сложность_УпорядоченаПоГлубине()
    {
        var depthToRank = DeadProblems
            .GroupBy(problem => LongestLine(problem.Solution))
            .OrderBy(group => group.Key)
            .Select(group => (Depth: group.Key, Rank: group.Min(problem => problem.Rank)))
            .ToList();

        for (var index = 1; index < depthToRank.Count; index++)
        {
            Assert.True(
                depthToRank[index].Rank <= depthToRank[index - 1].Rank,
                $"линия {depthToRank[index].Depth} ({depthToRank[index].Rank} кю) не сложнее линии {depthToRank[index - 1].Depth} ({depthToRank[index - 1].Rank} кю)");
        }
    }

    private static void Walk(Problem problem, ProblemNode node, Board board, StoneColor toMove, int depth, List<string> issues)
    {
        if (node.IsLeaf)
        {
            if (!ProblemGoalCheck.ForcedCapture(board, problem, problem.CheckDepth))
            {
                issues.Add($"лист на глубине {depth} (очередь {toMove.Name}) не подтверждён форсированным захватом");
            }

            return;
        }

        foreach (var move in node.Moves)
        {
            var legality = board.IsLegal(move.Move);

            if (!legality.IsSuccess)
            {
                issues.Add($"ход {move.Move.Point.X},{move.Move.Point.Y} на глубине {depth} нелегален: {legality.Error}");

                continue;
            }

            Walk(problem, move.Next, board.ApplyMove(move.Move), toMove.Opponent(), depth + 1, issues);
        }
    }

    private static Board Board(Problem problem)
    {
        var built = ProblemSetup.Build(problem.Size, problem.Stones);

        Assert.True(built.IsSuccess, $"{problem.Id}: позиция не собирается — {built.Error}");

        return built.Value!;
    }

    private static int LongestLine(ProblemNode node)
    {
        var best = 0;

        foreach (var move in node.Moves)
        {
            best = Math.Max(best, 1 + LongestLine(move.Next));
        }

        return best;
    }
}
