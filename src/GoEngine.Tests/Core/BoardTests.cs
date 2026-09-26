using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты доски: постановка камня, копирование, перечисление точек.</summary>
public sealed class BoardTests
{
    [Fact]
    public void Board_Новая_Пустая()
    {
        var board = new Board(BoardSize.Size9);

        Assert.All(board.AllPoints(), point => Assert.Equal(StoneColor.Empty, board.At(point)));
    }

    [Fact]
    public void Board_Новая_Имеет_Заданный_Размер()
    {
        Assert.Equal(BoardSize.Size13, new Board(BoardSize.Size13).Size);
    }

    [Fact]
    public void Board_ApplyMove_Ставит_камень()
    {
        var next = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(2, 3), StoneColor.Black));

        Assert.Equal(StoneColor.Black, next.At(new Point(2, 3)));
    }

    [Fact]
    public void Board_ApplyMove_Возвращает_Новую_Доску_Не_Мутирует_Исходную()
    {
        var board = new Board(BoardSize.Size9);

        _ = board.ApplyMove(Move.Play(new Point(2, 3), StoneColor.Black));

        Assert.Equal(StoneColor.Empty, board.At(new Point(2, 3)));
    }

    [Fact]
    public void Board_ApplyMove_Возвращает_Другой_Экземпляр()
    {
        var board = new Board(BoardSize.Size9);

        Assert.NotSame(board, board.ApplyMove(Move.Play(new Point(2, 3), StoneColor.Black)));
    }

    [Fact]
    public void Board_ApplyMove_На_Занятую_Точку_Бросает()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(2, 3), StoneColor.Black));

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.Play(new Point(2, 3), StoneColor.White)));
    }

    [Fact]
    public void Board_ApplyMove_Вне_Доски_Бросает()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.Play(new Point(9, 0), StoneColor.Black)));
    }

    [Fact]
    public void Board_ApplyMove_Без_Хода_Бросает()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.None));
    }

    [Fact]
    public void Board_ApplyMove_Пас_Не_Меняет_Позицию()
    {
        var next = new Board(BoardSize.Size9).ApplyMove(Move.Pass(StoneColor.Black));

        Assert.All(next.AllPoints(), point => Assert.Equal(StoneColor.Empty, next.At(point)));
    }

    [Fact]
    public void Board_MakeMove_Мутирует_На_Месте()
    {
        var board = new Board(BoardSize.Size9);

        board.MakeMove(Move.Play(new Point(1, 1), StoneColor.White));

        Assert.Equal(StoneColor.White, board.At(new Point(1, 1)));
    }

    [Fact]
    public void Board_MakeMove_Пасом_Бросает()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Throws<DomainException>(() => board.MakeMove(Move.Pass(StoneColor.Black)));
    }

    [Fact]
    public void Board_MakeMove_На_Занятую_Точку_Бросает()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(5, 5), StoneColor.Black));

        Assert.Throws<DomainException>(() => board.MakeMove(Move.Play(new Point(5, 5), StoneColor.White)));
    }

    [Fact]
    public void Board_Clone_Независимая_Копия()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        var clone = board.Clone();
        clone.MakeMove(Move.Play(new Point(0, 0), StoneColor.White));

        Assert.Equal(StoneColor.Empty, board.At(new Point(0, 0)));
    }

    [Fact]
    public void Board_Clone_Повторяет_Позицию()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(board.At(new Point(4, 4)), board.Clone().At(new Point(4, 4)));
    }

    [Fact]
    public void Board_EmptyPoints_Корректно()
    {
        var board = new Board(BoardSize.Size9);
        var occupied = new Point(4, 4);

        var next = board.ApplyMove(Move.Play(occupied, StoneColor.Black));

        Assert.Equal(board.AllPoints().Where(point => point != occupied), next.EmptyPoints());
    }

    [Fact]
    public void Board_OccupiedPoints_Корректно()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(3, 3), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(4, 5), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.White));

        Assert.Equal(new Point[] { new(3, 3), new(4, 5) }, board.OccupiedPoints(StoneColor.Black));
    }

    [Fact]
    public void Board_OccupiedPoints_Пустого_Цвета_Бросает()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Throws<DomainException>(() => board.OccupiedPoints(StoneColor.Empty));
    }

    [Fact]
    public void Board_AllPoints_Возвращает_Только_Точки_Доски()
    {
        var board = new Board(BoardSize.Size9);

        Assert.All(board.AllPoints(), point => Assert.True(point.IsOnBoard(BoardSize.Size9)));
    }

    [Fact]
    public void Board_At_Вне_Доски_Бросает()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Throws<DomainException>(() => board.At(new Point(9, 0)));
    }

    [Fact]
    public void Board_IsEmpty_Возвращает_Ложь_Для_Занятой_Точки()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(3, 3), StoneColor.Black));

        Assert.False(board.IsEmpty(new Point(3, 3)));
    }
}
