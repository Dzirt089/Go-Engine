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
        var original = ProblemLibrary.ById("cg-001")!;
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
