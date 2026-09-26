using GoEngine.AI;
using GoEngine.AI.Mcts;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Стенд замера силы AI: матчи, цумэго и пропускная способность playout'ов.</summary>
/// <remarks>
/// Протокол замера — <c>DECISIONS.md</c> D-017: полные партии 9×9 с чередованием цветов,
/// фиксированные seed'ы, относительное сравнение версий на одном уровне.
/// Стенд один на всю серию T-019a…T-019e, чтобы замеры были сопоставимы.
/// </remarks>
internal static class StrengthBenchmark
{
    /// <summary>Сколько партий играется в матче.</summary>
    internal const int Games = 10;

    /// <summary>Базовое зерно серии замеров.</summary>
    internal const int Seed = 20260926;

    /// <summary>Сколько миллисекунд отводится на замер пропускной способности.</summary>
    internal const int ThroughputMilliseconds = 500;

    /// <summary>Играет матч между двумя версиями селектора, чередуя цвета.</summary>
    /// <param name="first">Испытуемая версия: фабрика селектора по зерну.</param>
    /// <param name="second">Версия-соперник.</param>
    /// <returns>Число партий, выигранных испытуемой версией.</returns>
    internal static int PlayMatch(Func<Random, IMoveSelector> first, Func<Random, IMoveSelector> second)
    {
        var wins = 0;

        for (var number = 0; number < Games; number++)
        {
            var seed = Seed + number;
            var firstColor = number % 2 == 0 ? StoneColor.Black : StoneColor.White;

            var game = new SelfPlayHarness().PlayGame(
                firstColor == StoneColor.Black ? first(new Random(seed)) : second(new Random(seed)),
                firstColor == StoneColor.Black ? second(new Random(seed)) : first(new Random(seed)),
                BoardSize.Size9,
                Komi.For9x9,
                seed);

            if (game.Result.Winner == firstColor)
            {
                wins++;
            }
        }

        return wins;
    }

    /// <summary>Сколько playout'ов отводится на разбор одной позиции цумэго.</summary>
    internal const int TsumegoPlayouts = 10;

    /// <summary>Считает, сколько цумэго решает политика.</summary>
    /// <param name="policy">Политика playout'ов испытуемой версии.</param>
    /// <returns>Число решённых позиций из <see cref="TsumegoPositions"/>.</returns>
    /// <remarks>
    /// Бюджет разбора — <see cref="TsumegoPlayouts"/>: на шести playout'ах исход позиции
    /// с двумя кандидатами почти случаен, и замер перестаёт что-либо показывать.
    /// </remarks>
    internal static int SolveTsumego(PlayoutPolicy policy)
    {
        var solved = 0;

        foreach (var (rows, color, expected) in TsumegoPositions.All)
        {
            var board = TsumegoBuilder.Build(rows);
            var config = new MctsConfig(TsumegoPlayouts, MctsConfig.DefaultUcb1C);
            var selector = new MctsMoveSelector(new Random(Seed), policy, config);

            if (selector.SelectMove(board, color, Komi.For9x9).Point == expected)
            {
                solved++;
            }
        }

        return solved;
    }

    /// <summary>Считает пропускную способность playout'ов.</summary>
    /// <returns>Сколько playout'ов движок делает за секунду на позиции середины партии.</returns>
    internal static double PlayoutsPerSecond()
    {
        var config = new MctsConfig(TimeSpan.FromMilliseconds(ThroughputMilliseconds));
        var selector = new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), config);
        var root = selector.Search(TestPositions.BlockInAtari(), StoneColor.Black, Komi.For9x9);

        return root.Visits * (1000.0 / ThroughputMilliseconds);
    }
}
