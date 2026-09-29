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
    public void Библиотека_НачинаетсяДесятьюЗадачамиСайта()
    {
        // Первые десять задач — задачи пользователя с сайта (ts-001…ts-010), все вида
        // «по решению источника»: машинного критерия исхода у них нет.
        //
        // Число задач в проверке не фиксируется: набор растёт вместе с файлами Problems/*.sgf
        // (решение D-062 — библиотека читает все встроенные файлы), а жёсткая десятка ломала бы
        // сборку на каждой новой задаче и ничего не говорила о содержимом библиотеки.
        var expected = Enumerable.Range(1, 10).Select(number => $"ts-{number:000}").ToArray();

        Assert.True(ProblemLibrary.Count >= expected.Length, "в библиотеке меньше десяти задач сайта");
        Assert.Equal(expected, ProblemLibrary.All.Take(expected.Length).Select(problem => problem.Id).ToArray());
        Assert.All(ProblemLibrary.All.Take(expected.Length), problem => Assert.Equal(ProblemGoal.Reference, problem.Goal));
    }

    [Fact]
    public void Библиотека_ЧитаетВсеВстроенныеФайлыЗадач()
    {
        // Задача — это файл Problems/<id>.sgf, вшитый ресурсом; отдельного списка задач в коде нет.
        // Проверка держит это свойство: новая задача (ts-011 и дальше) попадает в режим задач
        // без правок кода, а забытый файл или фильтр по имени видны сразу.
        var resources = typeof(ProblemLibrary).Assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sgf", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(resources);
        Assert.Equal(resources.Count, ProblemLibrary.Count);

        foreach (var resource in resources)
        {
            // Имя ресурса — «<пространство имён>.Problems.<id>.sgf», поэтому идентификатор задачи
            // берётся из предпоследнего отрезка имени: так же его читает и сама библиотека.
            var id = resource.Split('.')[^2];

            Assert.Contains(ProblemLibrary.All, problem => string.Equals(problem.Id, id, StringComparison.Ordinal));
        }
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
        // Задачи перенесены на меньшую доску: большинство 9×9, три задачи (ts-004, ts-027, ts-030) — 13×13.
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
