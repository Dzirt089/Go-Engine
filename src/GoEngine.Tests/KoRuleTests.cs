using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты позиционного суперко — <c>GO_RULES.md</c>, п. 6 и 13.5–13.6.</summary>
public sealed class KoRuleTests
{
    [Fact]
    public void Ко_Немедленный_Обратный_Захват_Запрещён()
    {
        // GO_RULES.md, 13.5: белые снимают камень в ко, чёрные не могут отбить его сразу.
        var board = TestPositions.KoShape(new PositionHistory())
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.Play(new Point(2, 2), StoneColor.Black)));
    }

    [Fact]
    public void Ко_Через_Ход_Разрешён()
    {
        // После хода в другом месте повторения позиции нет, отбить ко можно.
        var board = TestPositions.KoShape(new PositionHistory())
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(7, 7), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(7, 8), StoneColor.White));

        var recapture = board.ApplyMove(Move.Play(new Point(2, 2), StoneColor.Black));

        Assert.Equal(StoneColor.Empty, recapture.At(new Point(2, 1)));
    }

    [Fact]
    public void Суперко_Повторение_Через_Два_Хода_Запрещено()
    {
        // GO_RULES.md, 13.6: возврат в ко повторяет позицию, бывшую в партии два хода назад.
        var board = TestPositions.KoShape(new PositionHistory())
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        var result = board.IsLegal(Move.Play(new Point(2, 2), StoneColor.Black));

        Assert.Contains("суперко", result.Error);
    }

    [Fact]
    public void Суперко_Без_Истории_Разрешает()
    {
        var board = TestPositions.KoShape(null)
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.True(board.IsLegal(Move.Play(new Point(2, 2), StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void IsLegal_Суперко_Возвращает_Result()
    {
        var board = TestPositions.KoShape(new PositionHistory())
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.False(board.IsLegal(Move.Play(new Point(2, 2), StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void Суперко_Позиция_После_Хода_Попадает_В_Историю()
    {
        var history = new PositionHistory();
        var board = new Board(BoardSize.Size9).WithHistory(history);

        var next = board.ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.True(history.Contains(next));
    }

    [Fact]
    public void Суперко_MakeMove_Пополняет_Историю()
    {
        var history = new PositionHistory();
        var board = new Board(BoardSize.Size9).WithHistory(history);

        board.MakeMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.True(history.Contains(board));
    }

    [Fact]
    public void Суперко_Пас_Разрешён()
    {
        var board = TestPositions.KoShape(new PositionHistory())
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.True(board.IsLegal(Move.Pass(StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void Суперко_Сдача_Разрешена()
    {
        var board = TestPositions.KoShape(new PositionHistory())
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.True(board.IsLegal(Move.Resign(StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void Суперко_Новая_Позиция_Разрешена()
    {
        var board = TestPositions.KoShape(new PositionHistory())
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.True(board.IsLegal(Move.Play(new Point(7, 7), StoneColor.Black)).IsSuccess);
    }

    [Fact]
    public void KoRule_Повторение_Позиции_Из_Истории_Запрещено()
    {
        // Позиция с чёрным камнем уже встречалась в партии: повторный ход запрещён.
        var history = new PositionHistory();
        history.Add(new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black)));
        var board = new Board(BoardSize.Size9).WithHistory(history);

        Assert.True(KoRule.ViolatesSuperko(board, Move.Play(new Point(4, 4), StoneColor.Black)));
    }

    [Fact]
    public void KoRule_Без_Истории_Возвращает_Ложь()
    {
        Assert.False(KoRule.ViolatesSuperko(new Board(BoardSize.Size9), Move.Play(new Point(4, 4), StoneColor.Black)));
    }

    [Fact]
    public void KoRule_Пас_Не_Нарушает_Суперко()
    {
        var history = new PositionHistory();
        var board = new Board(BoardSize.Size9).WithHistory(history);
        history.Add(board);

        Assert.False(KoRule.ViolatesSuperko(board, Move.Pass(StoneColor.Black)));
    }

    [Fact]
    public void KoRule_Новая_Позиция_Не_Нарушает_Суперко()
    {
        var board = TestPositions.KoShape(new PositionHistory());

        Assert.False(KoRule.ViolatesSuperko(board, Move.Play(new Point(7, 7), StoneColor.Black)));
    }

    [Fact]
    public void Ко_Захваченный_Камень_Снимается_До_Проверки_Суперко()
    {
        // Ход белых в ко легален: он снимает камень, а такая позиция ещё не встречалась.
        var board = TestPositions.KoShape(new PositionHistory());

        Assert.True(board.IsLegal(Move.Play(new Point(2, 1), StoneColor.White)).IsSuccess);
    }

}
