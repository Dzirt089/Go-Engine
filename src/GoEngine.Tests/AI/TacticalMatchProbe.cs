using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Временный матч «до против после»: MCTS без предохранителя против MCTS с ним. Удаляется.</summary>
public sealed class TacticalMatchProbe
{
    [Fact]
    public void Матч_До_Против_После()
    {
        var report = new List<string>();

        foreach (var size in new[] { BoardSize.Size9, BoardSize.Size13 })
        {
            var playouts = size == BoardSize.Size9 ? 50 : 30;
            var limit = size == BoardSize.Size9 ? 150 : 200;
            var komi = size == BoardSize.Size9 ? Komi.For9x9 : Komi.For13x13;
            var wins = 0;

            for (var number = 0; number < 4; number++)
            {
                var seed = 20260926 + number;
                var guardColor = number % 2 == 0 ? StoneColor.Black : StoneColor.White;

                var guarded = new MctsMoveSelector(
                    new Random(seed),
                    new PlayoutPolicy(),
                    new MctsConfig(playouts, MctsConfig.DefaultUcb1C) { TacticalGuard = true });

                var plain = new MctsMoveSelector(
                    new Random(seed),
                    new PlayoutPolicy(),
                    new MctsConfig(playouts, MctsConfig.DefaultUcb1C) { TacticalGuard = false });

                var game = new SelfPlayHarness().PlayGame(
                    guardColor == StoneColor.Black ? guarded : plain,
                    guardColor == StoneColor.Black ? plain : guarded,
                    size,
                    komi,
                    seed,
                    limit);

                if (game.Result.Winner == guardColor)
                {
                    wins++;
                }

                var board = game.FinalBoard;
                var black = board.AllPoints().Count(point => board.At(point) == StoneColor.Black);
                var white = board.AllPoints().Count(point => board.At(point) == StoneColor.White);

                report.Add($"{size} #{number + 1} предохранитель {(guardColor == StoneColor.Black ? "чёрные" : "белые")}: победил {game.Result.Winner}, счёт {game.Result.Score}, камней {black}ч/{white}б, ходов {game.Moves.Count}");
            }

            report.Add($"{size}: предохранитель выиграл {wins} из 4");
        }

        Assert.Fail(string.Join(" | ", report));
    }
}
