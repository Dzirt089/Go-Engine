using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Тесты второго основания accept: ход, выполняющий цель по правилам, принимается всегда.</summary>
/// <remarks>
/// Дерево решения ограничено объявленным окном и горизонтом, поэтому правильный ход может в него
/// не попасть. Отклонять такой ход нельзя: игрок получал бы «неверно» за ход, который убивает
/// группу. Порядок проверки в <see cref="ProblemSession.Play"/>: сначала дерево, потом правила.
/// </remarks>
public sealed class ProblemSessionGoalAcceptTests
{
    /// <summary>Белый камень в (1,1) с единственной дамэ (1,2): чёрный ход в (1,2) его снимает.</summary>
    private static Problem CaptureInOneMove()
    {
        List<ProblemStone> stones =
        [
            ProblemStone.White(1, 1),
            ProblemStone.Black(0, 1),
            ProblemStone.Black(1, 0),
            ProblemStone.Black(2, 1),
        ];

        // В дереве стоит заведомо другой ход: цель выполняется не им, а снятием.
        var leaf = ProblemNode.Leaf;
        var afterWrong = new ProblemNode([ProblemMove.Play(new Point(5, 5), StoneColor.White, leaf)]);
        var root = new ProblemNode([ProblemMove.Play(new Point(4, 4), StoneColor.Black, afterWrong)]);

        return new Problem(
            "goal-accept-001",
            "Снять камень вне дерева",
            BoardSize.Size9,
            new Komi(0),
            StoneColor.Black,
            ProblemGoal.Capture,
            stones,
            new Point(1, 1),
            30,
            "Чёрные снимают белый камень",
            root,
            checkDepth: 1);
    }

    [Fact]
    public void Ход_Снимающий_Группу_Вне_Дерева_Даёт_Решено()
    {
        var session = new ProblemSession(CaptureInOneMove());

        var verdict = session.Play(new Point(1, 2));

        Assert.Equal(ProblemVerdict.Solved, verdict);
        Assert.Equal(ProblemState.Solved, session.State);
        Assert.Equal(StoneColor.Empty, session.Board.At(new Point(1, 1)));
    }

    [Fact]
    public void Ход_Вне_Дерева_И_Без_Цели_Даёт_Неверно()
    {
        var session = new ProblemSession(CaptureInOneMove());
        var before = session.Board;

        var verdict = session.Play(new Point(8, 8));

        Assert.Equal(ProblemVerdict.Wrong, verdict);
        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Same(before, session.Board);
        Assert.Equal(0, session.SolverMoveCount);
    }
}
