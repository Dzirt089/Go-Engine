namespace GoEngine.AI;

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
    private readonly double _raveK;
    private readonly bool _usePriors;

    /// <summary>Создаёт дерево с корнем в текущей позиции партии.</summary>
    /// <param name="board">Позиция, из которой ищется ход.</param>
    /// <param name="toMove">Цвет, который ходит из этой позиции.</param>
    /// <param name="ucb1C">Коэффициент исследования UCB1.</param>
    /// <param name="raveK">Постоянная RAVE; 0 отключает поправку RAVE.</param>
    /// <param name="usePriors">Учитывать ли приоритеты ходов из политики сети (PUCT вместо UCB1).</param>
    public MctsTree(
        Board board,
        StoneColor toMove,
        double ucb1C = MctsConfig.DefaultUcb1C,
        double raveK = MctsConfig.DefaultRaveK,
        bool usePriors = false)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(toMove);

        var rootBoard = board.WithoutHistory();
        Root = new MctsNode(Move.None, null, rootBoard, toMove, LegalMoves.For(rootBoard, toMove));
        _ucb1C = ucb1C;
        _raveK = raveK;
        _usePriors = usePriors;
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
        RequireUntriedMoves(node);

        return Add(node, node.TakeUntriedMove());
    }

    /// <summary>Добавляет к узлу ребёнка с указанным ходом.</summary>
    /// <param name="node">Узел с неразобранными ходами.</param>
    /// <param name="move">Ход из числа неразобранных.</param>
    /// <param name="prior">Вероятность хода по мнению сети: приоритет исследования.</param>
    /// <returns>Новый ребёнок.</returns>
    /// <remarks>Порядок разбора ходов задаёт вызывающий: при игре с сетью — её политика.</remarks>
    /// <exception cref="AiException">Хода нет среди неразобранных.</exception>
    public MctsNode Expand(MctsNode node, Move move, double prior = 0)
    {
        ArgumentNullException.ThrowIfNull(node);
        RequireUntriedMoves(node);

        var child = Add(node, node.TakeUntriedMove(move));
        child.Prior = prior;

        return child;
    }

    /// <summary>Проверяет, что у узла остались неразобранные ходы.</summary>
    /// <param name="node">Узел с неразобранными ходами.</param>
    /// <exception cref="AiException">У узла не осталось неразобранных ходов.</exception>
    private static void RequireUntriedMoves(MctsNode node)
    {
        if (node.IsFullyExpanded)
        {
            throw new AiException("Узел разобран полностью: расширять нечем.");
        }
    }

    /// <summary>Добавляет к узлу ребёнка по уже взятому ходу.</summary>
    /// <param name="node">Родительский узел.</param>
    /// <param name="move">Ход, уже убранный из неразобранных.</param>
    /// <returns>Новый ребёнок.</returns>
    private static MctsNode Add(MctsNode node, Move move)
    {
        var board = node.Board.ApplyMove(move);
        var toMove = node.ToMove.Opponent();
        var child = new MctsNode(move, node, board, toMove, LegalMoves.For(board, toMove));

        node.Children.Add(child);

        return child;
    }

    /// <summary>Передаёт оценку позиции от узла к корню.</summary>
    /// <param name="node">Узел, чья позиция оценена.</param>
    /// <param name="winForToMove">Шансы на победу цвета, который ходит из этой позиции, от 0 до 1.</param>
    /// <remarks>
    /// Вверх по пути вероятность разворачивается: узлу нужно мнение о том, кто сделал его ход,
    /// а оценка сети дана с точки зрения того, кто ходит сейчас.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Вероятность вне диапазона 0…1.</exception>
    public void Backpropagate(MctsNode node, double winForToMove)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentOutOfRangeException.ThrowIfNegative(winForToMove);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(winForToMove, 1.0);

        var leafToMove = node.ToMove;

        for (var current = node; current is not null; current = current.Parent)
        {
            current.RegisterResult(current.Move.Color == leafToMove ? winForToMove : 1.0 - winForToMove);
        }
    }

    /// <summary>Передаёт результат партии от узла к корню.</summary>
    /// <param name="node">Узел, с которого началась партия.</param>
    /// <param name="winner">Победитель партии.</param>
    public void Backpropagate(MctsNode node, StoneColor winner) =>
        Backpropagate(node, winner, []);

    /// <summary>Передаёт результат партии с обновлением статистики RAVE.</summary>
    /// <param name="node">Узел, с которого началась симуляция.</param>
    /// <param name="winner">Победитель партии.</param>
    /// <param name="playedMoves">Ходы симуляции: по ним обновляется статистика «ход сыграл бы и здесь».</param>
    /// <remarks>
    /// Обычные посещения обновляются только по пути спуска, а RAVE — у всех детей этого пути,
    /// чей ход встретился в симуляции: так оценка хода набирает статистику быстрее.
    /// </remarks>
    public void Backpropagate(MctsNode node, StoneColor winner, IReadOnlyCollection<Move> playedMoves)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(winner);
        ArgumentNullException.ThrowIfNull(playedMoves);

        HashSet<Move> played = [.. playedMoves];

        for (var current = node; current is not null; current = current.Parent)
        {
            current.RegisterResult(winner);

            if (_raveK <= 0)
            {
                continue;
            }

            foreach (var child in current.Children)
            {
                if (played.Contains(child.Move))
                {
                    child.RegisterRaveResult(winner);
                }
            }
        }
    }

    /// <summary>Считает оценку ребёнка: PUCT с сетью, иначе UCB1 или UCB1 с поправкой RAVE.</summary>
    /// <param name="node">Узел-ребёнок.</param>
    /// <returns>Оценка для выбора.</returns>
    private double Score(MctsNode node) => _usePriors ? node.Puct(_ucb1C) : node.Ucb1Rave(_ucb1C, _raveK);

    /// <summary>Выбирает ребёнка с наибольшей оценкой UCB1.</summary>
    /// <param name="node">Узел, у которого есть дети.</param>
    /// <returns>Лучший ребёнок; при равных оценках — первый.</returns>
    private MctsNode BestChild(MctsNode node)
    {
        var best = node.Children[0];

        foreach (var child in node.Children)
        {
            if (Score(child) > Score(best))
            {
                best = child;
            }
        }

        return best;
    }
}