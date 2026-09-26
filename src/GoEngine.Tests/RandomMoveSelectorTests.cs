using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты случайного селектора — <c>AGENTS_GO.md</c>, п. 7 и <c>GO_RULES.md</c>, п. 3.</summary>
public sealed class RandomMoveSelectorTests
{
    private const int Seed = 20260926;

    [Fact]
    public void Random_Возвращает_Легальный_Ход()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var selector = new RandomMoveSelector(new Random(Seed));

        var move = selector.SelectMove(game);

        Assert.True(game.Board.IsLegal(move).IsSuccess);
    }

    [Fact]
    public void Random_Не_Ходит_В_Занятую_Точку()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var selector = new RandomMoveSelector(new Random(Seed));
        List<Result<MoveResult>> results = [];

        for (var i = 0; i < 40 && game.Status == GameStatus.InProgress; i++)
        {
            results.Add(game.Play(selector.SelectMove(game)));
        }

        // Партия отклоняет ход в занятую точку, самоубийство и нарушение суперко:
        // принятыми оказались все ходы селектора.
        Assert.All(results, result => Assert.True(result.IsSuccess));
    }

    [Fact]
    public void Random_При_Отсутствии_Ходов_Пас()
    {
        // Белые заняли всю доску, кроме (4,4) и (4,6): у белой группы два дамэ,
        // поэтому чёрный ход в любую из пустых точек — самоубийство без захвата.
        var selector = new RandomMoveSelector(new Random(Seed));

        var move = selector.SelectMove(BoardWithoutLegalMoves(), StoneColor.Black);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Random_При_Отсутствии_Ходов_Пас_За_Свой_Цвет()
    {
        var selector = new RandomMoveSelector(new Random(Seed));

        var move = selector.SelectMove(BoardWithoutLegalMoves(), StoneColor.White);

        Assert.Equal(StoneColor.White, move.Color);
    }

    [Fact]
    public void Random_Детерминирован_При_Фиксированном_Seed()
    {
        var first = new RandomMoveSelector(new Random(Seed))
            .SelectMove(GameState.NewGame(BoardSize.Size9, Komi.For9x9));
        var second = new RandomMoveSelector(new Random(Seed))
            .SelectMove(GameState.NewGame(BoardSize.Size9, Komi.For9x9));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Random_Возвращает_Ход_Своего_Цвета()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));
        var selector = new RandomMoveSelector(new Random(Seed));

        var move = selector.SelectMove(game);

        Assert.Equal(StoneColor.White, move.Color);
    }

    [Fact]
    public void Random_Пас_При_Полной_Доске_Легален()
    {
        var selector = new RandomMoveSelector(new Random(Seed));

        var move = selector.SelectMove(BoardWithoutLegalMoves(), StoneColor.Black);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Random_Имя_Селектора_Заполнено()
    {
        Assert.False(string.IsNullOrWhiteSpace(new RandomMoveSelector(new Random(Seed)).Name));
    }

    /// <summary>Строит позицию, в которой у чёрных нет ни одного легального хода.</summary>
    /// <returns>Доска 9×9, полностью занятая белыми камнями, кроме (4,4) и (4,6).</returns>
    /// <remarks>
    /// Две пустые точки не соседние, поэтому белая группа сохраняет второе дамэ: чёрный ход
    /// в любую из них не снимает белых и оставляет чёрный камень без дамэ — самоубийство.
    /// </remarks>
    private static Board BoardWithoutLegalMoves()
    {
        var board = new Board(BoardSize.Size9);
        var first = new Point(4, 4);
        var second = new Point(4, 6);

        foreach (var point in board.AllPoints())
        {
            if (point != first && point != second)
            {
                board = board.ApplyMove(Move.Play(point, StoneColor.White));
            }
        }

        return board;
    }
}
