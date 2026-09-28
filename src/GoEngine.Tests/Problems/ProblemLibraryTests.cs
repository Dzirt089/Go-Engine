using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Библиотека задач: состав, идентификаторы и проверка движком.</summary>
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
        Assert.Contains(ProblemLibrary.All, problem => problem.Goal == ProblemGoal.Dead);
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
    public void ПринимаемыеПервыеХоды_СовпадаютСПервымиХодамиДерева(string id)
    {
        var problem = ProblemLibrary.ById(id);

        Assert.NotNull(problem);

        var tree = problem.Solution.Moves.Select(move => move.Move.Point).ToList();
        var accepted = problem.AcceptedFirstMoves.Select(move => move.Point).ToList();

        Assert.Equal(tree, accepted);
        Assert.Equal(accepted.Count, problem.AcceptedFirstMoveCount);
        Assert.Equal(problem.Hint!.Value.Point, accepted[0]);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Описание_Задачи_ОбъявляетГоризонтИЛист(string id)
    {
        var problem = ProblemLibrary.ById(id);

        Assert.NotNull(problem);
        Assert.Contains("Проверена на горизонте", problem.Description, StringComparison.Ordinal);
        Assert.Contains("принимаемый набор", problem.Description, StringComparison.Ordinal);
    }
}
