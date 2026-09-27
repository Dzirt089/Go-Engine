using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Библиотека задач: доказательства корректности каждой задачи.</summary>
public sealed class ProblemLibraryTests
{
    public static TheoryData<string> Ids()
    {
        TheoryData<string> data = [];

        foreach (var problem in ProblemLibrary.All)
        {
            data.Add(problem.Id);
        }

        return data;
    }

    [Fact]
    public void Библиотека_НеМеньшеДвенадцатиЗадач()
    {
        Assert.True(ProblemLibrary.Count >= 12, $"задач в библиотеке: {ProblemLibrary.Count}");
    }

    [Fact]
    public void Идентификаторы_Уникальны()
    {
        var ids = ProblemLibrary.All.Select(problem => problem.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Библиотека_ПокрываетЦелиИРазмеры()
    {
        Assert.Contains(ProblemLibrary.All, problem => problem.Goal == ProblemGoal.Capture);
        Assert.Contains(ProblemLibrary.All, problem => problem.Goal == ProblemGoal.Live);
        Assert.Contains(ProblemLibrary.All, problem => problem.Size.Value == 13);
        Assert.Contains(ProblemLibrary.All, problem => problem.Size.Value == 19);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Каждая_Задача_ПроходитПроверку(string id)
    {
        var problem = ProblemLibrary.ById(id);

        Assert.NotNull(problem);

        var report = ProblemChecker.Check(problem);

        Assert.True(report.IsValid, $"{id}: {string.Join("; ", report.Issues)}");
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Каждая_Задача_РешаетсяОднимХодом_БезВторогоРешения(string id)
    {
        var problem = ProblemLibrary.ById(id);

        Assert.NotNull(problem);

        // Независимый перебор: победа должна быть, и ровно один первый ход должен её давать.
        var search = new ProblemSolver(problem).Search(depth: 1);

        Assert.False(search.LimitReached);
        Assert.True(search.SolverWins, $"{id}: перебор не нашёл победы");
        Assert.Single(search.WinningMoves);
        Assert.Equal(problem.Hint!.Value.Point, search.WinningMoves[0].Point);
    }
}
