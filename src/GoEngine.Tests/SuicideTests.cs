using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты самоубийства и проверки легальности хода — <c>GO_RULES.md</c>, п. 3, 5 и 13.3–13.4.</summary>
public sealed class SuicideTests
{
    [Fact]
    public void Ход_Самоубийство_Отклонён()
    {
        var board = SuicideAtCorner();

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black)));
    }

    [Fact]
    public void Ход_Самоубийство_В_Углу_Отклонён()
    {
        // GO_RULES.md, 13.3: белые заняли все дамэ будущей чёрной группы в углу.
        var board = SuicideAtCorner();

        Assert.False(board.IsLegal(Move.Play(new Point(0, 0), StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void Ход_Самоубийство_С_Захватом_Разрешён()
    {
        // GO_RULES.md, 13.4: ход снимает белую группу, хотя своя осталась бы без дамэ.
        var board = SuicideWithCapture();

        var next = board.ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black));

        Assert.Equal(StoneColor.Black, next.At(new Point(0, 0)));
    }

    [Fact]
    public void Ход_Захват_Без_Самоубийства_Разрешён()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White));

        var next = board.ApplyMove(Move.Play(new Point(0, 1), StoneColor.White));

        Assert.Equal(StoneColor.Empty, next.At(new Point(0, 0)));
    }

    [Fact]
    public void IsLegal_Не_Бросает_На_Самоубийстве()
    {
        var board = SuicideAtCorner();

        var result = board.IsLegal(Move.Play(new Point(0, 0), StoneColor.Black));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void IsLegal_Занятая_Точка_Отклонён()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.False(board.IsLegal(Move.Play(new Point(4, 4), StoneColor.White)).IsSuccess);
    }

    [Fact]
    public void IsLegal_Pass_Всегда_Разрешён()
    {
        var board = SuicideAtCorner();

        Assert.True(board.IsLegal(Move.Pass(StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void IsLegal_Resign_Всегда_Разрешён()
    {
        var board = SuicideAtCorner();

        Assert.True(board.IsLegal(Move.Resign(StoneColor.White)).IsSuccess);
    }

    [Fact]
    public void IsLegal_Легальный_Ход_Разрешён()
    {
        var board = new Board(BoardSize.Size9);

        Assert.True(board.IsLegal(Move.Play(new Point(4, 4), StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void IsLegal_Вне_Доски_Отклонён()
    {
        var board = new Board(BoardSize.Size9);

        Assert.False(board.IsLegal(Move.Play(new Point(9, 0), StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void IsLegal_Без_Хода_Отклонён()
    {
        var board = new Board(BoardSize.Size9);

        Assert.False(board.IsLegal(Move.None).IsSuccess);
    }

    [Fact]
    public void IsLegal_Отказ_Содержит_Причину()
    {
        var board = SuicideAtCorner();

        Assert.Contains("Самоубийственный ход", board.IsLegal(Move.Play(new Point(0, 0), StoneColor.Black)).Error);
    }

    [Fact]
    public void MakeMove_Самоубийство_Бросает()
    {
        var board = SuicideAtCorner();

        Assert.Throws<DomainException>(() => board.MakeMove(Move.Play(new Point(0, 0), StoneColor.Black)));
    }

    [Fact]
    public void MakeMove_Самоубийство_Не_Меняет_Доску()
    {
        var board = SuicideAtCorner();

        Assert.Throws<DomainException>(() => board.MakeMove(Move.Play(new Point(0, 0), StoneColor.Black)));
        Assert.Equal(StoneColor.Black, board.At(new Point(1, 0)));
    }

    [Fact]
    public void ApplyMove_Самоубийство_Не_Меняет_Исходную_Доску()
    {
        var board = SuicideAtCorner();

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black)));
        Assert.Equal(StoneColor.Empty, board.At(new Point(0, 0)));
    }

    [Fact]
    public void IsLegal_Не_Требует_Истории_Ходов()
    {
        // Проверка легальности ничего не записывает в доску и не зависит от прошлых ходов.
        var board = SuicideAtCorner();

        var first = board.IsLegal(Move.Play(new Point(4, 4), StoneColor.Black));
        var second = board.IsLegal(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(first.IsSuccess, second.IsSuccess);
    }

    /// <summary>Строит позицию, где чёрный ход в (0,0) — самоубийство.</summary>
    /// <returns>Доска 9×9 с белыми камнями вокруг угла.</returns>
    private static Board SuicideAtCorner() =>
        new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 2), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

    /// <summary>Строит позицию, где чёрный ход в (0,0) снимает белую группу, хотя своя остаётся без дамэ.</summary>
    /// <returns>Доска 9×9 с белой группой в углу и единственным дамэ (0,0).</returns>
    private static Board SuicideWithCapture() =>
        new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 2), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 2), StoneColor.Black));
}
