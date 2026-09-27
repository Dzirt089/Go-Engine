using GoEngine.AI;
using GoEngine.AI.Onnx;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Временный замер силы MCTS с сетью на 19×19. Удаляется после записи в DECISIONS.md.</summary>
public sealed class NeuralStrengthProbe
{
    /// <summary>Сколько партий играется в матче.</summary>
    private const int Games = 4;

    /// <summary>Лимит ходов партии: полная партия двух MCTS на 19×19 идёт десятки минут.</summary>
    private const int MoveLimit = 200;

    /// <summary>Сколько времени сеть думает над ходом.</summary>
    private const int MillisecondsPerMove = 500;

    [ModelFact]
    public void Замерить_Силу_Сети_Против_Kyu20() => Match(DifficultyLevel.Kyu20, "Kyu20");

    [ModelFact]
    public void Замерить_Силу_Сети_Против_Kyu10() => Match(DifficultyLevel.Kyu10, "Kyu10");

    private static void Match(DifficultyLevel level, string name)
    {
        var loaded = OnnxEvaluator.Load(ModelFile.FullPath);
        Assert.True(loaded.IsSuccess, $"модель не загрузилась: {loaded.Error ?? "без причины"}");

        using var evaluator = loaded.Value!;
        var wins = 0;
        var report = new List<string>();

        for (var number = 0; number < Games; number++)
        {
            var seed = StrengthBenchmark.Seed + number;
            var neuralColor = number % 2 == 0 ? StoneColor.Black : StoneColor.White;

            IMoveSelector neural = new MctsMoveSelector(
                new Random(seed),
                new PlayoutPolicy(),
                new MctsConfig(TimeSpan.FromMilliseconds(MillisecondsPerMove)),
                evaluator: evaluator);

            var opponent = AiFactory.Create(level, new Random(seed));

            var game = new SelfPlayHarness().PlayGame(
                neuralColor == StoneColor.Black ? neural : opponent,
                neuralColor == StoneColor.Black ? opponent : neural,
                BoardSize.Size19,
                Komi.For19x19,
                seed,
                MoveLimit);

            var won = game.Result.Winner == neuralColor;

            if (won)
            {
                wins++;
            }

            report.Add($"#{number + 1} сеть {(neuralColor == StoneColor.Black ? "чёрные" : "белые")}: победил {game.Result.Winner}, ходов {game.Moves.Count}, статус {game.Result.Status}, счёт {game.Result.Score}");
        }

        Assert.Fail($"ИТОГ против {name}: сеть {wins} из {Games} | {string.Join(" | ", report)}");
    }
}