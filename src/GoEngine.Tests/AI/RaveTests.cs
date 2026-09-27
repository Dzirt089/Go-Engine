using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты RAVE — <c>DECISIONS.md</c> D-020.</summary>
public sealed class RaveTests
{
    private const int Seed = 20260926;

    [Fact]
    public void Rave_β_Стремится_К_1_При_Малых_N()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);

        Assert.Equal(1, child.RaveBeta(MctsConfig.DefaultRaveK));
    }

    [Fact]
    public void Rave_β_Стремится_К_0_При_Больших_N()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);

        for (var number = 0; number < 10000; number++)
        {
            tree.Backpropagate(child, StoneColor.Black);
        }

        Assert.True(child.RaveBeta(MctsConfig.DefaultRaveK) < 0.2);
    }

    [Fact]
    public void Rave_Visits_Обновляются_При_Backpropagate()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);

        tree.Backpropagate(child, StoneColor.Black, [child.Move]);

        Assert.Equal(1, child.RaveVisits);
    }

    [Fact]
    public void Rave_Не_Обновляются_Для_Ходов_Вне_Симуляции()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);

        tree.Backpropagate(child, StoneColor.Black, []);

        Assert.Equal(0, child.RaveVisits);
    }

    [Fact]
    public void Rave_Не_Ломает_Обычный_UCB1_При_Нулевой_Постоянной()
    {
        // Постоянная RAVE равна нулю — поправка выключена, оценка совпадает с обычным UCB1.
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);
        tree.Backpropagate(child, StoneColor.Black, [child.Move]);

        Assert.Equal(child.Ucb1(MctsConfig.DefaultUcb1C), child.Ucb1Rave(MctsConfig.DefaultUcb1C, 0));
    }

    [Fact]
    public void Rave_Влияет_На_Выбор_Хода()
    {
        // Узел проиграл свою партию, но ход встречался в выигранных симуляциях:
        // RAVE поднимает оценку, а выбор ребёнка идёт именно по ней.
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);
        tree.Backpropagate(child, StoneColor.White);
        tree.Backpropagate(child, StoneColor.Black, [child.Move]);

        Assert.True(child.Ucb1Rave(MctsConfig.DefaultUcb1C, MctsConfig.DefaultRaveK) > child.Ucb1(MctsConfig.DefaultUcb1C));
    }

    [Fact]
    public void Rave_Детерминирован_При_Seed()
    {
        var config = new MctsConfig(6, MctsConfig.DefaultUcb1C) { RaveK = MctsConfig.DefaultRaveK };
        var first = new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), config)
            .SelectMove(TestPositions.CapturingRace(), StoneColor.Black, Komi.For9x9);
        var second = new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), config)
            .SelectMove(TestPositions.CapturingRace(), StoneColor.Black, Komi.For9x9);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Rave_Без_Поправки_Даёт_Тот_Же_Ход()
    {
        var withoutRave = new MctsConfig(6, MctsConfig.DefaultUcb1C) { RaveK = 0 };
        var first = new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), withoutRave)
            .SelectMove(TestPositions.CapturingRace(), StoneColor.Black, Komi.For9x9);
        var second = new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), withoutRave)
            .SelectMove(TestPositions.CapturingRace(), StoneColor.Black, Komi.For9x9);

        Assert.Equal(first, second);
    }

    [Fact]
    public void MctsConfig_Постоянная_Rave_По_Умолчанию()
    {
        Assert.Equal(MctsConfig.DefaultRaveK, MctsConfig.Default.RaveK);
    }

    [Fact]
    public void Rave_Ходы_Симуляции_Учитываются_У_Детей()
    {
        // Ход, сыгранный в симуляции из другого узла, получает RAVE-статистику у общего родителя.
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var first = tree.Expand(tree.Root);
        var second = tree.Expand(tree.Root);

        tree.Backpropagate(first, StoneColor.Black, [first.Move, second.Move]);

        Assert.Equal(1, second.RaveVisits);
    }
}
