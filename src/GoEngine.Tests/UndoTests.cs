using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты отмены и возврата ходов — T-026.</summary>
public sealed class UndoTests
{
    [Fact]
    public void Undo_Возвращает_Позицию_До_Хода()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        _ = game.Undo();

        Assert.Equal(StoneColor.Empty, game.Board.At(new Point(4, 4)));
    }

    [Fact]
    public void Undo_Возвращает_Очередь_Хода()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        _ = game.Undo();

        Assert.Equal(StoneColor.Black, game.ToMove);
    }

    [Fact]
    public void Undo_Уменьшает_Номер_Хода()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        _ = game.Undo();

        Assert.Equal(0, game.MoveNumber);
    }

    [Fact]
    public void Undo_Убирает_Ход_Из_Списка()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(2, 2), StoneColor.White));

        _ = game.Undo();

        Assert.Single(game.Moves);
    }

    [Fact]
    public void Redo_Повторяет_Отменённый_Ход()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        _ = game.Undo();

        _ = game.Redo();

        Assert.Equal(StoneColor.Black, game.Board.At(new Point(4, 4)));
    }

    [Fact]
    public void Undo_Восстанавливает_Статус_Партии()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Pass(StoneColor.Black));
        _ = game.Play(Move.Pass(StoneColor.White));

        _ = game.Undo();

        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void Undo_После_Паса_Возвращает_Ход()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Pass(StoneColor.Black));

        _ = game.Undo();

        Assert.Equal(StoneColor.Black, game.ToMove);
    }

    [Fact]
    public void Undo_Без_Ходов_Отказ()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        Assert.False(game.Undo().IsSuccess);
    }

    [Fact]
    public void Redo_Без_Отмены_Отказ()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        Assert.False(game.Redo().IsSuccess);
    }

    [Fact]
    public void Undo_Очищает_Возврат_После_Нового_Хода()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        _ = game.Undo();
        _ = game.Play(Move.Play(new Point(2, 2), StoneColor.Black));

        Assert.False(game.CanRedo);
    }

    [Fact]
    public void Undo_Возвращает_Историю_Суперко()
    {
        // Без отката истории повторный ход той же точкой нарушил бы суперко.
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        _ = game.Undo();

        var again = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.True(again.IsSuccess);
    }

    [Fact]
    public void Undo_Возвращает_Захваченные_Камни()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.White));

        _ = game.Undo();

        Assert.Equal(StoneColor.Black, game.Board.At(new Point(0, 0)));
    }

    [Fact]
    public void CanUndo_Становится_Истиной_После_Хода()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.True(game.CanUndo);
    }

    [Fact]
    public void Undo_Двух_Ходов_Подряд()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(2, 2), StoneColor.White));

        _ = game.Undo();
        _ = game.Undo();

        Assert.Empty(game.Moves);
    }
}
