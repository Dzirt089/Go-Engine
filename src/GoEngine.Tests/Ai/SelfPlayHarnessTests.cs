using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты партии AI против AI — <c>GO_RULES.md</c>, п. 7–9.</summary>
public sealed class SelfPlayHarnessTests
{
    private const int Seed = 20260926;

    [Fact]
    public void SelfPlay_9x9_Партия_Завершается()
    {
        var result = PlayRandomGame();

        Assert.True(result.Result.Status.IsFinished);
    }

    [Fact]
    public void SelfPlay_Все_Ходы_Легальны()
    {
        var result = PlayRandomGame();

        Assert.All(Replay(result.Moves), played => Assert.True(played.IsSuccess));
    }

    [Fact]
    public void SelfPlay_Детерминирован_При_Фиксированном_Seed()
    {
        var first = PlayRandomGame();
        var second = PlayRandomGame();

        Assert.Equal(first.Moves, second.Moves);
    }

    [Fact]
    public void SelfPlay_Возвращает_Финальную_Доску()
    {
        // Камни по ходу партии снимаются, поэтому финальная доска — это позиция после всех ходов.
        var result = PlayRandomGame();
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        foreach (var move in result.Moves)
        {
            _ = game.Play(move);
        }

        Assert.Equal(PositionHash.From(game.Board), PositionHash.From(result.FinalBoard));
    }

    [Fact]
    public void SelfPlay_Считает_Счёт_По_Китайским_Правилам()
    {
        var result = PlayRandomGame();

        Assert.Equal(Scorer.Calculate(result.FinalBoard, Komi.For9x9), result.Result.Score);
    }

    [Fact]
    public void SelfPlay_Партия_Не_Длиннее_Лимита()
    {
        var result = PlayRandomGame();

        Assert.True(result.Moves.Count <= BoardSize.Size9.DefaultMoveLimit);
    }

    [Fact]
    public void SelfPlay_Ходы_Чередуют_Цвета()
    {
        var result = PlayRandomGame();
        var colors = result.Moves.Select(move => move.Color).ToList();

        Assert.All(colors.Where((_, index) => index % 2 == 0), color => Assert.Equal(StoneColor.Black, color));
    }

    [Fact]
    public void SelfPlay_Пустой_Селектор_Бросает()
    {
        var harness = new SelfPlayHarness();

        Assert.Throws<ArgumentNullException>(() =>
            harness.PlayGame(null!, new RandomMoveSelector(new Random(Seed)), BoardSize.Size9, Komi.For9x9, Seed));
    }

    [Fact]
    public void SelfPlay_Отрицательный_Seed_Бросает()
    {
        var harness = new SelfPlayHarness();
        var selector = new RandomMoveSelector(new Random(Seed));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            harness.PlayGame(selector, selector, BoardSize.Size9, Komi.For9x9, -1));
    }

    /// <summary>Играет партию двух случайных селекторов с фиксированным зерном.</summary>
    /// <returns>Итог партии.</returns>
    private static SelfPlayResult PlayRandomGame() =>
        new SelfPlayHarness().PlayGame(
            new RandomMoveSelector(new Random(Seed)),
            new RandomMoveSelector(new Random(Seed)),
            BoardSize.Size9,
            Komi.For9x9,
            Seed);

    /// <summary>Повторяет партию на новой партии и возвращает результаты всех ходов.</summary>
    /// <param name="moves">Записанные ходы партии.</param>
    /// <returns>Результаты применения каждого хода.</returns>
    private static List<Result<MoveResult>> Replay(IReadOnlyList<Move> moves)
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        List<Result<MoveResult>> results = [];

        foreach (var move in moves)
        {
            results.Add(game.Play(move));
        }

        return results;
    }
}
