using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты самоубийства и проверки легальности хода — <c>GO_RULES.md</c>, п. 3, 5 и 13.3–13.4.</summary>
public sealed class SuicideTests
{
    [Fact]
    public void Ход_Самоубийство_Отклонён()
    {
        var board = TestPositions.SuicideAtCorner();

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black)));
    }

    [Fact]
    public void Ход_Самоубийство_В_Углу_Отклонён()
    {
        // GO_RULES.md, 13.3: белые заняли все дамэ будущей чёрной группы в углу.
        var board = TestPositions.SuicideAtCorner();

        Assert.False(board.IsLegal(Move.Play(new Point(0, 0), StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void Ход_Самоубийство_С_Захватом_Разрешён()
    {
        // GO_RULES.md, 13.4: ход снимает белую группу, хотя своя осталась бы без дамэ.
        var board = TestPositions.SuicideWithCapture();

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
        var board = TestPositions.SuicideAtCorner();

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
        var board = TestPositions.SuicideAtCorner();

        Assert.True(board.IsLegal(Move.Pass(StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void IsLegal_Resign_Всегда_Разрешён()
    {
        var board = TestPositions.SuicideAtCorner();

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
        var board = TestPositions.SuicideAtCorner();

        Assert.Contains("Самоубийственный ход", board.IsLegal(Move.Play(new Point(0, 0), StoneColor.Black)).Error);
    }

    [Fact]
    public void MakeMove_Самоубийство_Бросает()
    {
        var board = TestPositions.SuicideAtCorner();

        Assert.Throws<DomainException>(() => board.MakeMove(Move.Play(new Point(0, 0), StoneColor.Black)));
    }

    [Fact]
    public void MakeMove_Самоубийство_Не_Меняет_Доску()
    {
        var board = TestPositions.SuicideAtCorner();

        Assert.Throws<DomainException>(() => board.MakeMove(Move.Play(new Point(0, 0), StoneColor.Black)));
        Assert.Equal(StoneColor.Black, board.At(new Point(1, 0)));
    }

    [Fact]
    public void ApplyMove_Самоубийство_Не_Меняет_Исходную_Доску()
    {
        var board = TestPositions.SuicideAtCorner();

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black)));
        Assert.Equal(StoneColor.Empty, board.At(new Point(0, 0)));
    }

    [Fact]
    public void IsLegal_Не_Требует_Истории_Ходов()
    {
        // Проверка легальности ничего не записывает в доску и не зависит от прошлых ходов.
        var board = TestPositions.SuicideAtCorner();

        var first = board.IsLegal(Move.Play(new Point(4, 4), StoneColor.Black));
        var second = board.IsLegal(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(first.IsSuccess, second.IsSuccess);
    }

}
