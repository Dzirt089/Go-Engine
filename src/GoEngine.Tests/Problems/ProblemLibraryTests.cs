using GoEngine.Core;
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
    public void Библиотека_СодержитДесятьЗадачПоРешениюИсточника()
    {
        // Библиотека заменена задачами пользователя с сайта: их десять, и все они вида
        // «по решению источника» — машинного критерия исхода у них нет.
        Assert.Equal(10, ProblemLibrary.Count);
        Assert.All(ProblemLibrary.All, problem => Assert.Equal(ProblemGoal.Reference, problem.Goal));

        var expected = Enumerable.Range(1, 10).Select(number => $"ts-{number:000}").ToArray();

        Assert.Equal(expected, ProblemLibrary.All.Select(problem => problem.Id).ToArray());
    }

    [Fact]
    public void Идентификаторы_Уникальны()
    {
        var ids = ProblemLibrary.All.Select(problem => problem.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Библиотека_ПокрываетРазмерыДосок()
    {
        // Задачи перенесены на меньшую доску: девять из них 9×9, одна (с камнем за кропом) — 13×13.
        Assert.Contains(ProblemLibrary.All, problem => problem.Size.Value == 9);
        Assert.Contains(ProblemLibrary.All, problem => problem.Size.Value == 13);
        Assert.Contains(ProblemLibrary.All, problem => problem.SolverColor == StoneColor.Black);
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
    public void Каждая_Задача_ОбъявляетИсточник(string id)
    {
        var problem = ProblemLibrary.ById(id);

        Assert.NotNull(problem);
        Assert.Contains("goproblems.ru", problem.Source, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(problem.Description), $"{id}: нет формулировки");
    }
}
