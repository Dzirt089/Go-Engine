using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты дерева MCTS — <c>DECISIONS.md</c> D-003.</summary>
public sealed class MctsTreeTests
{
    private const double Ucb1C = 1.41;

    [Fact]
    public void Ucb1_Не_Посещённый_Бесконечность()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);

        Assert.True(double.IsPositiveInfinity(child.Ucb1(Ucb1C)));
    }

    [Fact]
    public void Ucb1_Посещённый_Узел_Конечен()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);
        tree.Backpropagate(child, StoneColor.Black);

        Assert.False(double.IsPositiveInfinity(child.Ucb1(Ucb1C)));
    }

    [Fact]
    public void Ucb1_Победа_Повышает_Оценку()
    {
        var winner = ExpandedChild(StoneColor.Black);
        var loser = ExpandedChild(StoneColor.White);

        Assert.True(winner.Ucb1(Ucb1C) > loser.Ucb1(Ucb1C));
    }

    [Fact]
    public void Select_Возвращает_Лист()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        FullyExpandRoot(tree);

        Assert.NotSame(tree.Root, tree.Select());
    }

    [Fact]
    public void Select_Возвращает_Узел_С_Неразобранными_Ходами()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        FullyExpandRoot(tree);

        Assert.False(tree.Select().IsFullyExpanded);
    }

    [Fact]
    public void Select_На_Свежем_Дереве_Возвращает_Корень()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);

        Assert.Same(tree.Root, tree.Select());
    }

    [Fact]
    public void Expand_Добавляет_Ребёнка()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);

        var child = tree.Expand(tree.Root);

        Assert.Contains(child, tree.Root.Children);
    }

    [Fact]
    public void Expand_Уменьшает_Неразобранные_Ходы()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var before = tree.Root.UntriedMoves.Count;

        _ = tree.Expand(tree.Root);

        Assert.Equal(before - 1, tree.Root.UntriedMoves.Count);
    }

    [Fact]
    public void Expand_Ставит_Ход_На_Доску_Ребёнка()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var move = tree.Root.UntriedMoves[^1];

        var child = tree.Expand(tree.Root);

        Assert.Equal(StoneColor.Black, child.Board.At(move.Point));
    }

    [Fact]
    public void Expand_Меняет_Цвет_Хода()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);

        var child = tree.Expand(tree.Root);

        Assert.Equal(StoneColor.White, child.ToMove);
    }

    [Fact]
    public void Expand_Полностью_Разобранный_Узел_Бросает()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        FullyExpandRoot(tree);

        Assert.Throws<AiException>(() => tree.Expand(tree.Root));
    }

    [Fact]
    public void Backpropagate_Обновляет_Визиты_И_Победы()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);

        tree.Backpropagate(child, StoneColor.Black);

        Assert.Equal(1, child.Visits);
    }

    [Fact]
    public void Backpropagate_Считает_Победу_Сделавшему_Ход()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);

        tree.Backpropagate(child, StoneColor.Black);

        Assert.Equal(1, child.Wins);
    }

    [Fact]
    public void Backpropagate_Проигрыш_Не_Даёт_Побед()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);

        tree.Backpropagate(child, StoneColor.White);

        Assert.Equal(0, child.Wins);
    }

    [Fact]
    public void Backpropagate_Обновляет_Всю_Цепочку()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);
        var grandChild = tree.Expand(child);

        tree.Backpropagate(grandChild, StoneColor.White);

        Assert.Equal(1, tree.Root.Visits);
    }

    [Fact]
    public void MctsTree_Корень_Без_Истории_Партии()
    {
        // Анализ не должен пополнять историю настоящей партии: иначе суперко начнёт
        // запрещать ходы из-за вариантов, которые в партии не игрались.
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        var tree = new MctsTree(game.Board, game.ToMove);

        Assert.Null(tree.Root.Board.History);
    }

    [Fact]
    public void MctsTree_История_Партии_Не_Меняется_После_Анализа()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var tree = new MctsTree(game.Board, game.ToMove);
        var child = tree.Expand(tree.Root);
        tree.Backpropagate(child, StoneColor.Black);

        Assert.Equal(1, game.History.Count);
    }

    [Fact]
    public void MctsTree_Корень_Ходит_Заданным_Цветом()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.White);

        Assert.Equal(StoneColor.White, tree.Root.ToMove);
    }

    /// <summary>Строит дерево с одним разобранным ходом и передаёт ему результат.</summary>
    /// <param name="winner">Победитель, которого нужно передать наверх.</param>
    /// <returns>Ребёнок корня с одной партией в статистике.</returns>
    private static MctsNode ExpandedChild(StoneColor winner)
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);
        tree.Backpropagate(child, winner);

        return child;
    }

    /// <summary>Разбирает все ходы корня.</summary>
    /// <param name="tree">Дерево, у которого расширяется корень.</param>
    private static void FullyExpandRoot(MctsTree tree)
    {
        while (!tree.Root.IsFullyExpanded)
        {
            _ = tree.Expand(tree.Root);
        }
    }
}
