using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты политики playout'ов — <c>AGENTS_GO.md</c>, п. 7 и <c>DECISIONS.md</c> D-003.</summary>
public sealed class PlayoutPolicyTests
{
    private const int Seed = 20260926;

    [Fact]
    public void Playout_Всегда_Легальный_Ход()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var policy = new PlayoutPolicy();

        var move = policy.SelectMove(game, new Random(Seed));

        Assert.True(game.Board.IsLegal(move).IsSuccess);
    }

    [Fact]
    public void Playout_Съедает_Атари_Если_Возможно()
    {
        var game = AtariToCapture();
        var policy = new PlayoutPolicy(new PlayoutConfig(1.0, 0.0));

        var move = policy.SelectMove(game, new Random(Seed));

        Assert.Equal(new Point(1, 2), move.Point);
    }

    [Fact]
    public void Playout_Спасает_Атари_Если_Возможно()
    {
        var game = AtariToRescue();
        var policy = new PlayoutPolicy(new PlayoutConfig(1.0, 0.0));

        var move = policy.SelectMove(game, new Random(Seed));

        Assert.Equal(new Point(1, 2), move.Point);
    }

    [Fact]
    public void Playout_Детерминирован_При_Фиксированном_Seed()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        var policy = new PlayoutPolicy();

        var first = policy.SelectMove(game, new Random(Seed));
        var second = policy.SelectMove(game, new Random(Seed));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Playout_Пас_Когда_Ходов_Нет()
    {
        var policy = new PlayoutPolicy();

        var move = policy.SelectMove(TestPositions.BoardWithoutLegalMoves(), StoneColor.Black, new Random(Seed));

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Playout_Соседний_Ход_Рядом_С_Камнем()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        // Эвристики атари выключены, ход рядом с камнями обязателен.
        var policy = new PlayoutPolicy(new PlayoutConfig(0.0, 1.0));

        var move = policy.SelectMove(game, new Random(Seed));

        Assert.Contains(move.Point, Move.Play(new Point(4, 4), StoneColor.Black).Point.Neighbors(BoardSize.Size9));
    }

    [Fact]
    public void Playout_Без_Соседей_Ходит_Случайно()
    {
        // На пустой доске соседей нет: политика обязана вернуть легальный ход, а не упасть.
        var policy = new PlayoutPolicy(new PlayoutConfig(0.0, 1.0));

        var move = policy.SelectMove(new Board(BoardSize.Size9), StoneColor.Black, new Random(Seed));

        Assert.Equal(MoveType.Play, move.Type);
    }

    [Fact]
    public void Playout_Config_По_Умолчанию()
    {
        Assert.Equal(new PlayoutConfig(0.9, 0.5), PlayoutConfig.Default);
    }

    [Fact]
    public void Playout_Config_Вероятность_Больше_Единицы_Бросает()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayoutPolicy(new PlayoutConfig(1.5, 0.5)));
    }

    [Fact]
    public void Playout_Config_Отрицательная_Вероятность_Бросает()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayoutPolicy(new PlayoutConfig(0.5, -0.1)));
    }

    [Fact]
    public void Playout_Все_Ходы_Партии_Легальны()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var policy = new PlayoutPolicy();
        var random = new Random(Seed);
        List<Result<MoveResult>> played = [];

        for (var number = 0; number < 60 && game.Status == GameStatus.InProgress; number++)
        {
            played.Add(game.Play(policy.SelectMove(game, random)));
        }

        Assert.All(played, result => Assert.True(result.IsSuccess));
    }

    /// <summary>Строит позицию, где белый камень в атари и чёрные снимают его ходом в (1,2).</summary>
    /// <returns>Партия, в которой ход чёрных.</returns>
    private static GameState AtariToCapture()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(1, 1), StoneColor.White));
        _ = game.Play(Move.Play(new Point(2, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(5, 5), StoneColor.White));
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(5, 6), StoneColor.White));

        return game;
    }

    /// <summary>Строит позицию, где чёрная группа в атари и чёрные спасают её ходом в (1,2).</summary>
    /// <returns>Партия, в которой ход чёрных.</returns>
    private static GameState AtariToRescue()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(1, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.White));
        _ = game.Play(Move.Play(new Point(5, 5), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(2, 1), StoneColor.White));
        _ = game.Play(Move.Play(new Point(5, 6), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.White));

        return game;
    }
}
