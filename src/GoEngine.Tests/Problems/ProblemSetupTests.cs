using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Сборка позиции задачи легальными ходами.</summary>
public sealed class ProblemSetupTests
{
    [Fact]
    public void Сборка_СовпадаетСоСхемой_ПоКаждойТочке()
    {
        var stones = new[]
        {
            ProblemStone.White(0, 0), ProblemStone.White(1, 0),
            ProblemStone.Black(0, 1), ProblemStone.Black(1, 1)
        };

        var built = ProblemSetup.Build(BoardSize.Size9, stones);

        Assert.True(built.IsSuccess, built.Error);
        Assert.Null(built.Value!.History);

        // Сравнение по каждой точке доски: камень, цвет и пустота должны совпасть со схемой.
        Assert.True(ProblemSetup.Verify(built.Value!, stones).IsSuccess);
        Assert.Equal(StoneColor.White, built.Value!.At(new Point(0, 0)));
        Assert.Equal(StoneColor.Black, built.Value!.At(new Point(1, 1)));
        Assert.Equal(StoneColor.Empty, built.Value!.At(new Point(2, 0)));
    }

    [Fact]
    public void Позиция_БезЛегальногоПорядка_Отвергается()
    {
        // Белый камень окружён со всех сторон: последний чёрный камень снял бы его, а снятие
        // при расстановке запрещено — значит легального порядка нет.
        var stones = new[]
        {
            ProblemStone.White(1, 1), ProblemStone.Black(0, 1), ProblemStone.Black(2, 1),
            ProblemStone.Black(1, 0), ProblemStone.Black(1, 2)
        };

        var built = ProblemSetup.Build(BoardSize.Size9, stones);

        Assert.False(built.IsSuccess);
        Assert.Contains("расставить", built.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Дубликат_Точки_Отвергается()
    {
        var stones = new[] { ProblemStone.Black(1, 1), ProblemStone.White(1, 1) };

        var built = ProblemSetup.Build(BoardSize.Size9, stones);

        Assert.False(built.IsSuccess);
        Assert.Contains("дважды", built.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Камень_ВнеДоски_Отвергается()
    {
        var built = ProblemSetup.Build(BoardSize.Size9, [ProblemStone.Black(9, 0)]);

        Assert.False(built.IsSuccess);
        Assert.Contains("вне доски", built.Error, StringComparison.Ordinal);
    }
}
