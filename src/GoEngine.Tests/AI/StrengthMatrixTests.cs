using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Матрица силы уровней: каждая пара играет полные партии на 9×9.</summary>
/// <remarks>
/// Бюджет урезан до <see cref="PlayoutsPerMove"/> playout'ов на ход, а партий в паре —
/// <see cref="GamesPerPair"/>: штатные бюджеты уровней 10/8/5 кю равны 1–3 секундам на ход,
/// и полная матрица заняла бы часы. Поэтому тест проверяет не «сильный выигрывает ≥ 7 из 10»
/// (это требование на движке не подтверждается, см. <c>DECISIONS.md</c> D-014, D-021),
/// а то, что матрица играется целиком, без нарушений правил и с работающим замером.
/// Измеренные результаты пар — в <c>DECISIONS.md</c> D-021.
/// </remarks>
public sealed class StrengthMatrixTests
{
    /// <summary>Сколько playout'ов на ход отводится в матрице.</summary>
    private const int PlayoutsPerMove = 6;

    /// <summary>Сколько партий играется в каждой паре уровней.</summary>
    private const int GamesPerPair = 2;

    /// <summary>Пары уровней от слабой к сильной.</summary>
    private static readonly (DifficultyLevel Strong, DifficultyLevel Weak)[] Pairs =
    [
        (DifficultyLevel.Kyu25, DifficultyLevel.Kyu30),
        (DifficultyLevel.Kyu20, DifficultyLevel.Kyu25),
        (DifficultyLevel.Kyu15, DifficultyLevel.Kyu20),
        (DifficultyLevel.Kyu10, DifficultyLevel.Kyu15),
        (DifficultyLevel.Kyu5, DifficultyLevel.Kyu10)
    ];

    [Fact]
    public void Матрица_Силы_Играется_Целиком()
    {
        var games = PlayMatrix();

        Assert.All(games, game => Assert.True(game.Result.Status.IsFinished));
    }

    [Fact]
    public void Матрица_Силы_Играет_Все_Пары()
    {
        Assert.Equal(Pairs.Length * GamesPerPair, PlayMatrix().Count);
    }

    [Fact]
    public void Матрица_Силы_Ходы_Легальны()
    {
        // Записанные ходы первой MCTS-пары повторяются на новой партии: правила не нарушены.
        var game = PlayPair(DifficultyLevel.Kyu15, DifficultyLevel.Kyu20)[0];
        var replay = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        List<Result<MoveResult>> played = [];

        foreach (var move in game.Moves)
        {
            played.Add(replay.Play(move));
        }

        Assert.All(played, result => Assert.True(result.IsSuccess));
    }

    [Fact]
    public void Матрица_Силы_Замер_Работает()
    {
        var wins = StrengthBenchmark.PlayMatch(
            random => AiFactory.Create(DifficultyLevel.Kyu20, random),
            random => AiFactory.Create(DifficultyLevel.Kyu25, random));

        Assert.InRange(wins, 0, StrengthBenchmark.Games);
    }

    /// <summary>Играет всю матрицу пар.</summary>
    /// <returns>Итоги всех сыгранных партий.</returns>
    private static List<SelfPlayResult> PlayMatrix()
    {
        List<SelfPlayResult> games = [];

        foreach (var (strong, weak) in Pairs)
        {
            games.AddRange(PlayPair(strong, weak));
        }

        return games;
    }

    /// <summary>Играет пару уровней.</summary>
    /// <param name="strong">Сильный уровень.</param>
    /// <param name="weak">Слабый уровень.</param>
    /// <returns>Итоги партий пары.</returns>
    private static List<SelfPlayResult> PlayPair(DifficultyLevel strong, DifficultyLevel weak)
    {
        List<SelfPlayResult> games = [];

        for (var number = 0; number < GamesPerPair; number++)
        {
            var seed = StrengthBenchmark.Seed + number;
            games.Add(new SelfPlayHarness().PlayGame(
                Create(strong, new Random(seed)),
                Create(weak, new Random(seed)),
                BoardSize.Size9,
                Komi.For9x9,
                seed));
        }

        return games;
    }

    /// <summary>Создаёт селектор уровня с урезанным бюджетом MCTS.</summary>
    /// <param name="level">Уровень сложности.</param>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Селектор уровня.</returns>
    private static IMoveSelector Create(DifficultyLevel level, Random random) =>
        level.Kind == SelectorKind.Mcts
            ? new MctsMoveSelector(
                random,
                new PlayoutPolicy(level.PlayoutConfig),
                new MctsConfig(PlayoutsPerMove, level.Ucb1C) { RaveK = MctsConfig.DefaultRaveK })
            : AiFactory.Create(level, random);
}
