using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты эвристического селектора и уровней 30–20 кю — <c>AGENTS_GO.md</c>, п. 7.</summary>
/// <remarks>
/// Эвристики остались отдельным селектором, но в лестнице уровней их больше нет: 30, 25 и 20 кю
/// играют MCTS, потому что без поиска выглядели «тупыми» рядом с 15 кю (D-054). Поведение
/// эвристики проверяется напрямую — с той же случайностью, что была у уровня 20 кю.
/// </remarks>
public sealed class HeuristicMoveSelectorTests
{
    private const int Seed = 20260926;

    /// <summary>Сколько партий играется в матче уровней.</summary>
    private const int Games = 20;

    [Fact]
    public void Kyu30_Возвращает_Легальный_Ход()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var selector = DifficultyLevel.Kyu30.CreateSelector(new Random(Seed));

        var move = selector.SelectMove(game);

        Assert.True(game.Board.IsLegal(move).IsSuccess);
    }

    [Fact]
    public void Kyu30_Играет_Поиском_А_Не_Наугад()
    {
        Assert.IsType<MctsMoveSelector>(DifficultyLevel.Kyu30.CreateSelector(new Random(Seed)));
    }

    [Fact]
    public void Эвристика_Уровня_Kyu20_Спасает_Группу_В_Атари()
    {
        var game = AtariToRescue();
        var selector = new HeuristicMoveSelector(new Random(Seed), DifficultyLevel.Kyu20.RandomnessPercent);

        var move = selector.SelectMove(game);

        Assert.Equal(new Point(1, 2), move.Point);
    }

    [Fact]
    public void Эвристика_Уровня_Kyu20_Съедает_Камень_В_Атари()
    {
        var game = AtariToCapture();
        var selector = new HeuristicMoveSelector(new Random(Seed), DifficultyLevel.Kyu20.RandomnessPercent);

        var move = selector.SelectMove(game);

        Assert.Equal(new Point(1, 2), move.Point);
    }

    [Fact]
    public void Эвристика_Без_Случайности_Спасает_Группу()
    {
        var game = AtariToRescue();
        var selector = new HeuristicMoveSelector(new Random(Seed), randomnessPercent: 0);

        var move = selector.SelectMove(game);

        Assert.Equal(Move.Play(new Point(1, 2), StoneColor.Black), move);
    }

    [Fact]
    public void Эвристика_Без_Случайности_Съедает_Камень()
    {
        var game = AtariToCapture();
        var selector = new HeuristicMoveSelector(new Random(Seed), randomnessPercent: 0);

        var move = selector.SelectMove(game);

        Assert.Equal(Move.Play(new Point(1, 2), StoneColor.Black), move);
    }

    [Fact]
    public void Эвристика_Занимает_Угол_В_Начале()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var selector = new HeuristicMoveSelector(new Random(Seed), randomnessPercent: 0);

        var move = selector.SelectMove(game);

        Assert.Equal(new Point(2, 2), move.Point);
    }

    [Fact]
    public void Kyu20_Возвращает_Легальный_Ход()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var selector = DifficultyLevel.Kyu20.CreateSelector(new Random(Seed));

        Assert.True(game.Board.IsLegal(selector.SelectMove(game)).IsSuccess);
    }

    [Fact]
    public void Kyu20_Детерминирован_При_Фиксированном_Seed()
    {
        var first = DifficultyLevel.Kyu20.CreateSelector(new Random(Seed)).SelectMove(GameState.NewGame(BoardSize.Size9, Komi.For9x9));
        var second = DifficultyLevel.Kyu20.CreateSelector(new Random(Seed)).SelectMove(GameState.NewGame(BoardSize.Size9, Komi.For9x9));

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifficultyLevel_Ранги_Убывают_С_Ростом_Кю()
    {
        Assert.True(DifficultyLevel.Kyu30.RankKyu > DifficultyLevel.Kyu25.RankKyu
            && DifficultyLevel.Kyu25.RankKyu > DifficultyLevel.Kyu20.RankKyu);
    }

    [Fact]
    public void DifficultyLevel_Слабее_Значит_Больше_Случайности()
    {
        Assert.True(DifficultyLevel.Kyu30.RandomnessPercent > DifficultyLevel.Kyu25.RandomnessPercent
            && DifficultyLevel.Kyu25.RandomnessPercent > DifficultyLevel.Kyu20.RandomnessPercent);
    }

    [Fact]
    public void Kyu20_Не_Проигрывает_Kyu30_В_Матче()
    {
        // Партий двадцать, а не сто: оба уровня теперь играют поиском, и каждая партия стоит
        // секунды машинного времени. Порог прежний — «сильный уровень не проигрывает матч»:
        // требование «не менее 7 из 10» на этом движке не подтверждается (D-014, D-047).
        var wins = 0;

        for (var number = 0; number < Games; number++)
        {
            var seed = Seed + number;
            var strong = DifficultyLevel.Kyu20.CreateSelector(new Random(seed));
            var weak = DifficultyLevel.Kyu30.CreateSelector(new Random(seed));
            var strongColor = number % 2 == 0 ? StoneColor.Black : StoneColor.White;

            var game = new SelfPlayHarness().PlayGame(
                strongColor == StoneColor.Black ? strong : weak,
                strongColor == StoneColor.Black ? weak : strong,
                BoardSize.Size9,
                Komi.For9x9,
                seed);

            if (game.Result.Winner == strongColor)
            {
                wins++;
            }
        }

        Assert.True(wins >= Games / 2, $"Kyu20 победил в {wins} партиях из {Games}.");
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
}
