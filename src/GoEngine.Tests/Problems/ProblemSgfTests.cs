using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Разбор и запись файла задачи (SGF с вариантами).</summary>
public sealed class ProblemSgfTests
{
    private const string Simple = "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Тестовая задача]GC[Условие]GE[capture]GD[25]PL[B]GT[ai]AB[ah]AB[bh]AW[ai]AW[bi];B[ci])";

    private const string WithVariation = "(;SZ[9]GN[С вариантами]GE[live]PL[B]GD[20]GT[ai]AB[ai]AB[bh]AW[ah];B[bi](;W[ci];B[di])(;W[di]))";

    [Fact]
    public void Разбор_ЧитаетПоляИДерево()
    {
        var parsed = ProblemSgf.Parse(Simple, "test-001");

        Assert.True(parsed.IsSuccess, parsed.Error);

        var problem = parsed.Value!;

        Assert.Equal("test-001", problem.Id);
        Assert.Equal("Тестовая задача", problem.Name);
        Assert.Equal(BoardSize.Size9, problem.Size);
        Assert.Equal(ProblemGoal.Capture, problem.Goal);
        Assert.Equal(25, problem.Rank);
        Assert.Equal(StoneColor.Black, problem.SolverColor);
        Assert.Equal(new Point(0, 0), problem.Target);
        Assert.Equal(4, problem.Stones.Count);
        Assert.Equal(new Point(2, 0), problem.Hint!.Value.Point);
    }

    /// <summary>Задача, у которой список значений разорван переводом строки — как в SGF сайта.</summary>
    private const string WithMultilineValues =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]\n"
        + "GN[Список значений в несколько строк]\n"
        + "GC[Ход чёрных. Проверка разбора списка значений.]\n"
        + "GE[reference]GD[20]SO[проверка, задача 1]PL[B]GT[dd]\n"
        + "AB[dd][ed]\n[fd]\n"
        + "AW[ee]\n[fe]\n"
        + ";B[da]\n;W[ca])";

    [Fact]
    public void Разбор_ТерпитПереводСтроки_ВнутриСпискаЗначений()
    {
        // Регрессия: в SGF сайта список значений бывает разорван переводом строки. Разбор
        // останавливался на нём и терял остаток узла вместе с ходами: задача получалась неполной
        // молча — это хуже отказа, потому что игрок увидел бы чужую позицию.
        var parsed = ProblemSgf.Parse(WithMultilineValues, "test-lines");

        Assert.True(parsed.IsSuccess, parsed.Error);

        var problem = parsed.Value!;

        Assert.Equal(5, problem.Stones.Count);
        Assert.Contains(problem.Stones, stone => stone.Point == new Point(3, 5) && stone.Color == StoneColor.Black);
        Assert.Contains(problem.Stones, stone => stone.Point == new Point(4, 5) && stone.Color == StoneColor.Black);
        Assert.Contains(problem.Stones, stone => stone.Point == new Point(5, 5) && stone.Color == StoneColor.Black);
        Assert.Contains(problem.Stones, stone => stone.Point == new Point(4, 4) && stone.Color == StoneColor.White);
        Assert.Contains(problem.Stones, stone => stone.Point == new Point(5, 4) && stone.Color == StoneColor.White);

        // Строки после разорванного списка тоже обязаны читаться: и свойства, и ходы.
        Assert.Equal("проверка, задача 1", problem.Source);
        Assert.Equal("Ход чёрных. Проверка разбора списка значений.", problem.Description);
        Assert.Equal(new Point(3, 8), problem.Hint!.Value.Point);
        Assert.Equal(new Point(2, 8), problem.Solution.Moves[0].Next.Moves[0].Move.Point);
    }

    [Theory]
    [InlineData("e\nc")]
    [InlineData("e\r\nc")]
    [InlineData("zz")]
    public void Разбор_НеразобраннаяКоординатаКамня_ДаётОтказ(string broken)
    {
        // Регрессия: неразобранная координата в AB/AW пропускалась молча — задача разбиралась
        // успешно, но камней в ней было меньше, чем в файле. Это класс «молча неправильно»:
        // позиция выглядит собранной и отличается от задуманной. Перевод строки внутри значения
        // (AB[e↵c]) — частный случай того же пропуска.
        var text = "(;SZ[9]GN[Разрыв значения камня]GC[Ход чёрных.]GE[reference]SO[проверка, задача 1]"
            + "PL[B]GT[dd]AB[dd][ed]AB[" + broken + "]AW[ee];B[da])";

        var parsed = ProblemSgf.Parse(text, "test-broken-stone");

        Assert.False(parsed.IsSuccess);
        Assert.Contains("AB", parsed.Error!, StringComparison.Ordinal);
        Assert.Contains("не разобрана", parsed.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void Разбор_ПереводСтрокиВнутриЗначенияКамня_НеТеряетКамень()
    {
        // Проверка того же места с другой стороны: если значение разорвано переводом строки,
        // разбор обязан отказать, а не вернуть задачу без одного камня.
        const string broken = "(;SZ[9]GN[Разрыв]GC[Ход чёрных.]GE[reference]SO[проверка, задача 1]"
            + "PL[B]GT[dd]AB[dd]AB[e\nc]AW[ee];B[da])";

        var parsed = ProblemSgf.Parse(broken, "test-broken-stone-2");

        Assert.False(parsed.IsSuccess);
        Assert.Contains("AB", parsed.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void Разбор_ЧитаетВарианты()
    {
        var parsed = ProblemSgf.Parse(WithVariation, "test-002");

        Assert.True(parsed.IsSuccess, parsed.Error);

        var problem = parsed.Value!;

        Assert.Single(problem.Solution.Moves);

        var reply = problem.Solution.Moves[0].Next;

        Assert.Equal(2, reply.Moves.Count);
        Assert.All(reply.Moves, move => Assert.Equal(StoneColor.White, move.Move.Color));
    }

    [Theory]
    [InlineData("SZ[9]")]
    [InlineData("(;SZ[9]GN[Без цели]PL[B]AB[ai];B[bi])")]
    [InlineData("(;SZ[9]GN[Без хода]GE[capture]PL[B]AB[ai]AW[ai])")]
    [InlineData("(;SZ[7]GN[Плохой размер]GE[capture]PL[B]GT[ai]AB[ai];B[bi])")]
    [InlineData("(;SZ[9]GN[Точка вне доски]GE[capture]PL[B]GT[zz]AB[ai];B[bi])")]
    public void Разбор_БитыхФайлов_ДаётОтказ(string text)
    {
        var parsed = ProblemSgf.Parse(text);

        Assert.False(parsed.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(parsed.Error));
    }

    [Fact]
    public void Запись_ИЧтение_ДаютТуЖеЗадачу()
    {
        var original = ProblemLibrary.ById("ts-008")!;
        var text = ProblemSgf.Write(original);
        var parsed = ProblemSgf.Parse(text, original.Id);

        Assert.True(parsed.IsSuccess, parsed.Error);

        var restored = parsed.Value!;

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Size, restored.Size);
        Assert.Equal(original.Goal, restored.Goal);
        Assert.Equal(original.Rank, restored.Rank);
        Assert.Equal(original.SolverColor, restored.SolverColor);
        Assert.Equal(original.Target, restored.Target);
        Assert.Equal(original.Stones.Count, restored.Stones.Count);
        Assert.Equal(original.Hint!.Value.Point, restored.Hint!.Value.Point);
        Assert.Equal(original.Komi.Value, restored.Komi.Value);

        // Источник решения обязан переживать запись и чтение: без него задача «по решению
        // источника» перестанет разбираться вовсе.
        Assert.Equal(original.Source, restored.Source);
    }

    [Fact]
    public void РешениеИсточника_БезИсточника_ДаётОтказ()
    {
        // Формулировка есть, источника нет: такую задачу игрок увидит как проверенную движком,
        // а это неправда, поэтому разбор обязан отказать.
        var parsed = ProblemSgf.Parse(
            "(;SZ[9]GN[Без источника]GC[Ход чёрных.]GE[reference]PL[B]GT[ai]AB[ah]AB[bh]AW[ai];B[bi])",
            "test-ref");

        Assert.False(parsed.IsSuccess);
        Assert.Contains("SO", parsed.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void РешениеИсточника_СИсточником_Читается()
    {
        var parsed = ProblemSgf.Parse(
            "(;SZ[9]GN[С источником]GC[Ход чёрных.]GE[reference]SO[пример.сайт, задача 1]PL[B]GT[ai]AB[ah]AB[bh]AW[ai];B[bi])",
            "test-ref-2");

        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(ProblemGoal.Reference, parsed.Value!.Goal);
        Assert.Equal("пример.сайт, задача 1", parsed.Value!.Source);
    }

    [Fact]
    public void Пас_ЧитаетсяКакПас()
    {
        var parsed = ProblemSgf.Parse("(;SZ[9]GN[С пасом]GE[capture]PL[B]GT[ai]AB[ah]AW[ai];B[ci](;W[];B[bi]))", "test-003");

        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(MoveType.Pass, parsed.Value!.Solution.Moves[0].Next.Moves[0].Move.Type);
    }

    [Fact]
    public void Внешний_Каталог_Читается()
    {
        var directory = Path.Combine(Path.GetTempPath(), "goengine-problems-" + Guid.NewGuid().ToString("N"));

        try
        {
            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "my-001.sgf"), Simple);

            var loaded = ProblemLibrary.LoadFrom(directory);

            Assert.True(loaded.IsSuccess, loaded.Error);
            Assert.Single(loaded.Value!);
            Assert.Equal("my-001", loaded.Value![0].Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Отсутствующий_Каталог_ДаётОтказ()
    {
        var loaded = ProblemLibrary.LoadFrom(Path.Combine(Path.GetTempPath(), "goengine-missing-" + Guid.NewGuid().ToString("N")));

        Assert.False(loaded.IsSuccess);
        Assert.Contains("не найден", loaded.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void БитыйФайл_ВКаталоге_ДаётОтказ()
    {
        var directory = Path.Combine(Path.GetTempPath(), "goengine-problems-" + Guid.NewGuid().ToString("N"));

        try
        {
            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "bad.sgf"), "(;SZ[9]GN[Без цели]PL[B]AB[ai];B[bi])");

            var loaded = ProblemLibrary.LoadFrom(directory);

            Assert.False(loaded.IsSuccess);
            Assert.Contains("bad.sgf", loaded.Error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
