using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты снятия камней — <c>GO_RULES.md</c>, п. 4 и 13.1–13.2.</summary>
public sealed class CaptureTests
{
    [Fact]
    public void Захват_Одиночного_Камня()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(3, 4), StoneColor.White))
            .ApplyMove(Move.Play(new Point(5, 4), StoneColor.White))
            .ApplyMove(Move.Play(new Point(4, 3), StoneColor.White))
            .ApplyMove(Move.Play(new Point(4, 5), StoneColor.White));

        Assert.Equal(StoneColor.Empty, board.At(new Point(4, 4)));
    }

    [Fact]
    public void Захват_Группы_Из_Двух_Камней()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.White));

        Point[] capturedGroup = [new(0, 0), new(1, 0)];

        Assert.All(capturedGroup, point => Assert.Equal(StoneColor.Empty, board.At(point)));
    }

    [Fact]
    public void Захват_Не_Происходит_Если_Есть_Дамэ()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.Equal(StoneColor.Black, board.At(new Point(1, 1)));
    }

    [Fact]
    public void Захват_В_Углу()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White));

        Assert.Equal(StoneColor.Empty, board.At(new Point(0, 0)));
    }

    [Fact]
    public void Захват_На_Краю()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(4, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(3, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(5, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(4, 1), StoneColor.White));

        Assert.Equal(StoneColor.Empty, board.At(new Point(4, 0)));
    }

    [Fact]
    public void Захват_Двух_Групп_Одним_Ходом()
    {
        // Обе чёрные группы лишаются последней дамэ (2,1) одним ходом белых.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(3, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(4, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(3, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 2), StoneColor.White))
            .ApplyMove(Move.Play(new Point(3, 2), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Point[] capturedGroups = [new(1, 1), new(3, 1)];

        Assert.All(capturedGroups, point => Assert.Equal(StoneColor.Empty, board.At(point)));
    }

    [Fact]
    public void Захват_Белой_Группы_Чёрными()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black));

        Point[] capturedGroup = [new(0, 0), new(1, 0)];

        Assert.All(capturedGroup, point => Assert.Equal(StoneColor.Empty, board.At(point)));
    }

    [Fact]
    public void CapturedStones_Пусто_Если_Ничего_Не_Снято()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Empty(board.CapturedStones);
    }

    [Fact]
    public void CapturedStones_Содержит_Снятые()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(3, 4), StoneColor.White))
            .ApplyMove(Move.Play(new Point(5, 4), StoneColor.White))
            .ApplyMove(Move.Play(new Point(4, 3), StoneColor.White))
            .ApplyMove(Move.Play(new Point(4, 5), StoneColor.White));

        Point[] expected = [new(4, 4)];

        Assert.Equal(expected, board.CapturedStones);
    }

    [Fact]
    public void CapturedStones_Содержит_Всю_Снятую_Группу()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.White));

        Assert.Equal(2, board.CapturedStones.Count);
    }

    [Fact]
    public void CapturedStones_Относится_Только_К_Последнему_Ходу()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White));

        var next = board.ApplyMove(Move.Play(new Point(8, 8), StoneColor.Black));

        Assert.Empty(next.CapturedStones);
    }

    [Fact]
    public void CapturedStones_Пас_Пусто()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White));

        Assert.Empty(board.ApplyMove(Move.Pass(StoneColor.Black)).CapturedStones);
    }

    [Fact]
    public void CapturedStones_Исходная_Доска_Не_Меняется()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White));

        _ = board.ApplyMove(Move.Play(new Point(0, 1), StoneColor.White));

        Assert.Equal(StoneColor.Black, board.At(new Point(0, 0)));
    }

    [Fact]
    public void MakeMove_Записывает_Снятые_Камни()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White));

        board.MakeMove(Move.Play(new Point(0, 1), StoneColor.White));

        Point[] expected = [new(0, 0)];

        Assert.Equal(expected, board.CapturedStones);
    }

    [Fact]
    public void CaptureResult_None_Без_Снятых_Камней()
    {
        Assert.False(CaptureResult.None.HasCaptures);
    }

    [Fact]
    public void CaptureResult_Со_Снятыми_Камнями_Имеет_Захваты()
    {
        Assert.True(new CaptureResult([new Point(0, 0)]).HasCaptures);
    }
}
