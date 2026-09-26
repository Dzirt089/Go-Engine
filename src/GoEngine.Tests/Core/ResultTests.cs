using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты результата операции — <c>AGENTS.md</c>, п. 4.</summary>
public sealed class ResultTests
{
    [Fact]
    public void Result_Ok_IsSuccess()
    {
        Assert.True(Result.Ok().IsSuccess);
    }

    [Fact]
    public void Result_Fail_HasError()
    {
        Assert.Equal("Ход в ко запрещён.", Result.Fail("Ход в ко запрещён.").Error);
    }

    [Fact]
    public void Result_Fail_Без_Причины_Бросает()
    {
        Assert.Throws<ArgumentException>(() => Result.Fail(" "));
    }

    [Fact]
    public void ResultT_Ok_HasValue()
    {
        Assert.Equal(7, Result<int>.Ok(7).Value);
    }

    [Fact]
    public void ResultT_Ok_IsSuccess()
    {
        Assert.True(Result<int>.Ok(7).IsSuccess);
    }

    [Fact]
    public void ResultT_Fail_NoValue()
    {
        Assert.Equal(0, Result<int>.Fail("Нет значения.").Value);
    }

    [Fact]
    public void ResultT_Fail_HasError()
    {
        Assert.False(Result<int>.Fail("Нет значения.").IsSuccess);
    }

    [Fact]
    public void Result_Ok_Без_Ошибки()
    {
        Assert.Null(Result.Ok().Error);
    }

    [Fact]
    public void Result_IsSuccess_Ложь_При_Отказе()
    {
        Assert.False(Result.Fail("Отказ.").IsSuccess);
    }

    [Fact]
    public void ResultT_Ok_Без_Ошибки()
    {
        Assert.Null(Result<int>.Ok(7).Error);
    }

    [Fact]
    public void ResultT_Fail_Без_Причины_Бросает()
    {
        Assert.Throws<ArgumentException>(() => Result<int>.Fail(""));
    }

    [Fact]
    public void ResultT_Unit_Ok()
    {
        Assert.True(Result<Unit>.Ok(Unit.Value).IsSuccess);
    }

    [Fact]
    public void Board_IsLegal_Возвращает_Result_Unit()
    {
        var result = new Board(BoardSize.Size9).IsLegal(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(Unit.Value, result.Value);
    }

    [Fact]
    public void GameState_Play_Возвращает_Result_MoveResult()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var move = Move.Play(new Point(4, 4), StoneColor.Black);

        Assert.Equal(move, game.Play(move).Value.Move);
    }

    [Fact]
    public void GameState_Finish_Возвращает_Result_GameResult()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        Assert.True(game.Finish().IsSuccess);
    }
}
