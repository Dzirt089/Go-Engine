using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты партии — <c>GO_RULES.md</c>, п. 7–10.</summary>
public sealed class GameStateTests
{
    [Fact]
    public void NewGame_Начинает_С_Чёрных()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        Assert.Equal(StoneColor.Black, game.ToMove);
    }

    [Fact]
    public void NewGame_Начинает_Без_Ходов()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        Assert.Equal(0, game.MoveNumber);
    }

    [Fact]
    public void NewGame_Партия_Идёт()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void NewGame_Лимит_По_Умолчанию()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        Assert.Equal(2 * BoardSize.Size9.Area, game.MoveLimit);
    }

    [Fact]
    public void NewGame_Заданный_Лимит_Ходов()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9, moveLimit: 10);

        Assert.Equal(10, game.MoveLimit);
    }

    [Fact]
    public void NewGame_Отрицательный_Лимит_Бросает()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameState.NewGame(BoardSize.Size9, Komi.For9x9, moveLimit: -1));
    }

    [Fact]
    public void NewGame_Начальная_Позиция_В_Истории()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        Assert.True(game.History.Contains(game.Board));
    }

    [Fact]
    public void Play_Меняет_Цвет()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(StoneColor.White, game.ToMove);
    }

    [Fact]
    public void Play_Увеличивает_Номер_Хода()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(1, game.MoveNumber);
    }

    [Fact]
    public void Play_Ставит_Камень_На_Доску()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(StoneColor.Black, game.Board.At(new Point(4, 4)));
    }

    [Fact]
    public void Play_Возвращает_Итог_Хода()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        var result = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Play_Нелегальный_Ход_Возвращает_Ошибку()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        var result = game.Play(Move.Play(new Point(4, 4), StoneColor.White));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Play_Нелегальный_Ход_Не_Меняет_Партию()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.White));

        Assert.Equal(StoneColor.White, game.ToMove);
    }

    [Fact]
    public void Play_Чужого_Цвета_Отклонён()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        var result = game.Play(Move.Play(new Point(4, 4), StoneColor.White));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Play_Без_Хода_Отклонён()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        Assert.False(game.Play(Move.None).IsSuccess);
    }

    [Fact]
    public void Play_Самоубийство_Отклонён_Без_Исключения()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        var result = game.Play(Move.Play(new Point(0, 0), StoneColor.Black));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Pass_Один_Не_Завершает()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        _ = game.Play(Move.Pass(StoneColor.Black));

        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void Pass_Два_Завершает_По_Правилу_Двух_Пасов()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Pass(StoneColor.Black));

        _ = game.Play(Move.Pass(StoneColor.White));

        Assert.Equal(GameStatus.FinishedByTwoPasses, game.Status);
    }

    [Fact]
    public void Pass_После_Хода_Не_Завершает()
    {
        // Два паса завершают партию только подряд: после хода одиночный пас её не заканчивает.
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(5, 5), StoneColor.White));

        _ = game.Play(Move.Pass(StoneColor.Black));

        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void Pass_Не_Меняет_Доску()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        _ = game.Play(Move.Pass(StoneColor.Black));

        Assert.Empty(game.Board.OccupiedPoints(StoneColor.Black));
    }

    [Fact]
    public void Resign_Завершает_Победой_Соперника()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        _ = game.Play(Move.Resign(StoneColor.Black));

        Assert.Equal(StoneColor.White, game.Finish().Value.Winner);
    }

    [Fact]
    public void Resign_Завершает_Партию()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        _ = game.Play(Move.Resign(StoneColor.White));

        Assert.Equal(GameStatus.FinishedByResign, game.Status);
    }

    [Fact]
    public void Resign_Без_Счёта()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Resign(StoneColor.Black));

        Assert.Null(game.Finish().Value.Score);
    }

    [Fact]
    public void MoveLimit_Завершает_Партию()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9, moveLimit: 2);

        _ = game.Play(Move.Play(new Point(1, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(2, 2), StoneColor.White));

        Assert.Equal(GameStatus.FinishedByMoveLimit, game.Status);
    }

    [Fact]
    public void MoveLimit_Раньше_Лимита_Партия_Идёт()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9, moveLimit: 3);

        _ = game.Play(Move.Play(new Point(1, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(2, 2), StoneColor.White));

        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void Play_После_Завершения_Отклонён()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Resign(StoneColor.Black));

        Assert.False(game.Play(Move.Play(new Point(4, 4), StoneColor.White)).IsSuccess);
    }

    [Fact]
    public void Finish_Возвращает_Корректный_Score()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Pass(StoneColor.Black));
        _ = game.Play(Move.Pass(StoneColor.White));

        var result = game.Finish();

        Assert.Equal(Scorer.Calculate(game.Board, Komi.For9x9), result.Value.Score);
    }

    [Fact]
    public void Finish_Возвращает_Победителя()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Pass(StoneColor.Black));
        _ = game.Play(Move.Pass(StoneColor.White));

        Assert.Equal(StoneColor.White, game.Finish().Value.Winner);
    }

    [Fact]
    public void Finish_Завершает_Идущую_Партию()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        _ = game.Finish();

        Assert.Equal(GameStatus.FinishedByTwoPasses, game.Status);
    }

    [Fact]
    public void Finish_После_Сдачи_Сохраняет_Причину()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        _ = game.Play(Move.Resign(StoneColor.White));

        Assert.Equal("Соперник сдался.", game.Finish().Value.Reason);
    }

    [Fact]
    public void Moves_Содержит_Все_Ходы()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(1, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(2, 2), StoneColor.White));
        _ = game.Play(Move.Pass(StoneColor.Black));

        Assert.Equal(3, game.Moves.Count);
    }

    [Fact]
    public void Moves_Хранит_Порядок_Ходов()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(1, 1), StoneColor.Black));

        Assert.Equal(Move.Play(new Point(1, 1), StoneColor.Black), game.Moves[0]);
    }

    [Fact]
    public void Play_Суперко_Отклоняет_Повторение()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        // Белые снимают камень, чёрные не могут сразу вернуть позицию.
        _ = game.Play(Move.Play(new Point(3, 4), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(5, 4), StoneColor.White));
        _ = game.Play(Move.Play(new Point(4, 3), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.White));
        _ = game.Play(Move.Play(new Point(4, 5), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.White));

        var capture = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.False(capture.IsSuccess);
    }
}
