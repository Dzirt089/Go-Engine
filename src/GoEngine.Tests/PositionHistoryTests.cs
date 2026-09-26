using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты хеша позиции и истории партии — <c>GO_RULES.md</c>, п. 6.</summary>
public sealed class PositionHistoryTests
{
    [Fact]
    public void PositionHash_Детерминирован()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(PositionHash.From(board), PositionHash.From(board));
    }

    [Fact]
    public void PositionHash_Одинаковые_Позиции_Одинаковые_Хеши()
    {
        var first = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(2, 3), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(5, 5), StoneColor.White));

        // Та же расстановка получена другим порядком ходов.
        var second = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(5, 5), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 3), StoneColor.Black));

        Assert.Equal(PositionHash.From(first), PositionHash.From(second));
    }

    [Fact]
    public void PositionHash_Разные_Позиции_Разные_Хеши()
    {
        var first = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(2, 3), StoneColor.Black));
        var second = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(3, 3), StoneColor.Black));

        Assert.NotEqual(PositionHash.From(first), PositionHash.From(second));
    }

    [Fact]
    public void PositionHash_Цвет_Входит_В_Хеш()
    {
        var black = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(2, 3), StoneColor.Black));
        var white = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(2, 3), StoneColor.White));

        Assert.NotEqual(PositionHash.From(black), PositionHash.From(white));
    }

    [Fact]
    public void PositionHash_Пустая_Доска_Не_Зависит_От_Размера()
    {
        // Пустая позиция не содержит камней, поэтому хеш у неё нулевой на любом размере.
        Assert.Equal(PositionHash.From(new Board(BoardSize.Size9)), PositionHash.From(new Board(BoardSize.Size19)));
    }

    [Fact]
    public void PositionHash_Пас_Не_Меняет_Хеш()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        var afterPass = board.ApplyMove(Move.Pass(StoneColor.White));

        Assert.Equal(PositionHash.From(board), PositionHash.From(afterPass));
    }

    [Fact]
    public void History_Содержит_Добавленное()
    {
        var history = new PositionHistory();
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        history.Add(board);

        Assert.True(history.Contains(board));
    }

    [Fact]
    public void History_Не_Содержит_Не_Добавленное()
    {
        var history = new PositionHistory();
        history.Add(new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black)));

        var other = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(3, 3), StoneColor.Black));

        Assert.False(history.Contains(other));
    }

    [Fact]
    public void History_Count_Корректен()
    {
        var history = new PositionHistory();
        history.Add(new Board(BoardSize.Size9));
        history.Add(new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black)));
        history.Add(new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black)));

        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void History_Повторное_Добавление_Не_Растёт()
    {
        var history = new PositionHistory();
        var board = new Board(BoardSize.Size9);

        history.Add(board);
        history.Add(board);

        Assert.Equal(1, history.Count);
    }

    [Fact]
    public void History_Hashes_Возвращает_Снимок()
    {
        var history = new PositionHistory();
        history.Add(new Board(BoardSize.Size9));
        history.Add(new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black)));

        Assert.Equal(2, history.Hashes.Count);
    }

    [Fact]
    public void History_Содержит_Хеш_Напрямую()
    {
        var history = new PositionHistory();
        var board = new Board(BoardSize.Size9);

        history.Add(board);

        Assert.True(history.Contains(PositionHash.From(board)));
    }

    [Fact]
    public void Board_WithHistory_Привязывает_Историю()
    {
        var history = new PositionHistory();

        Assert.Same(history, new Board(BoardSize.Size9).WithHistory(history).History);
    }

    [Fact]
    public void Board_Без_Истории_Возвращает_Ноль()
    {
        Assert.Null(new Board(BoardSize.Size9).History);
    }

    [Fact]
    public void Board_Clone_Сохраняет_Историю()
    {
        var board = new Board(BoardSize.Size9).WithHistory(new PositionHistory());

        Assert.Same(board.History, board.Clone().History);
    }

    [Fact]
    public void Board_ApplyMove_Сохраняет_Историю()
    {
        var board = new Board(BoardSize.Size9).WithHistory(new PositionHistory());

        Assert.Same(board.History, board.ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black)).History);
    }
}
