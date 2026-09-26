namespace GoEngine.AI.Mcts;

using GoEngine.Core;

/// <summary>Дерево MCTS: спуск, расширение и передача результата наверх.</summary>
/// <remarks>
/// Дерево работает на копии доски без истории партии (<see cref="Board.WithoutHistory"/>):
/// ходы, найденные в анализе, не должны пополнять историю настоящей партии.
/// Спуск детерминирован: при равных оценках выбирается первый ребёнок.
/// </remarks>
public sealed class MctsTree
{
    private readonly double _ucb1C;

    /// <summary>Создаёт дерево с корнем в текущей позиции партии.</summary>
    /// <param name="board">Позиция, из которой ищется ход.</param>
    /// <param name="toMove">Цвет, который ходит из этой позиции.</param>
    /// <param name="ucb1C">Коэффициент исследования UCB1.</param>
    public MctsTree(Board board, StoneColor toMove, double ucb1C = MctsConfig.DefaultUcb1C)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(toMove);

        var rootBoard = board.WithoutHistory();
        Root = new MctsNode(Move.None, null, rootBoard, toMove, LegalMoves.For(rootBoard, toMove));
        _ucb1C = ucb1C;
    }

    /// <summary>Корень дерева — позиция, для которой ищется ход.</summary>
    public MctsNode Root { get; }

    /// <summary>Спускается от корня к узлу, который нужно расширить или оценить.</summary>
    /// <returns>Первый узел, у которого остались неразобранные ходы, или лист.</returns>
    public MctsNode Select()
    {
        var node = Root;

        while (node.IsFullyExpanded && node.Children.Count > 0)
        {
            node = BestChild(node);
        }

        return node;
    }

    /// <summary>Добавляет к узлу одного ребёнка.</summary>
    /// <param name="node">Узел с неразобранными ходами.</param>
    /// <returns>Новый ребёнок.</returns>
    /// <exception cref="AiException">У узла не осталось неразобранных ходов.</exception>
    public MctsNode Expand(MctsNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.IsFullyExpanded)
        {
            throw new AiException("Узел разобран полностью: расширять нечем.");
        }

        var move = node.TakeUntriedMove();
        var board = node.Board.ApplyMove(move);
        var toMove = node.ToMove.Opponent();
        var child = new MctsNode(move, node, board, toMove, LegalMoves.For(board, toMove));

        node.Children.Add(child);

        return child;
    }

    /// <summary>Передаёт результат партии от узла к корню.</summary>
    /// <param name="node">Узел, с которого началась партия.</param>
    /// <param name="winner">Победитель партии.</param>
    public void Backpropagate(MctsNode node, StoneColor winner)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(winner);

        for (var current = node; current is not null; current = current.Parent)
        {
            current.RegisterResult(winner);
        }
    }

    /// <summary>Выбирает ребёнка с наибольшей оценкой UCB1.</summary>
    /// <param name="node">Узел, у которого есть дети.</param>
    /// <returns>Лучший ребёнок; при равных оценках — первый.</returns>
    private MctsNode BestChild(MctsNode node)
    {
        var best = node.Children[0];

        foreach (var child in node.Children)
        {
            if (child.Ucb1(_ucb1C) > best.Ucb1(_ucb1C))
            {
                best = child;
            }
        }

        return best;
    }
}
