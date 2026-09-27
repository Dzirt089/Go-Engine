namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Оценка позиции нейросетью: одна оценка на итерацию поиска.</summary>
/// <remarks>
/// Одна оценка даёт и исход позиции (её получает путь до корня), и вероятности ходов (их получает
/// новый ребёнок): сеть вызывается один раз на итерацию. Порядок разбора ходов задаёт политика
/// сети — первым пробуется ход, который она считает вероятным (T-033).
/// </remarks>
public sealed class NeuralEvaluation : IMctsEvaluation
{
    private readonly IPositionEvaluator _evaluator;

    /// <summary>Создаёт оценку сетью.</summary>
    /// <param name="evaluator">Оценка позиции нейросетью.</param>
    /// <exception cref="ArgumentNullException">Оценка не задана.</exception>
    public NeuralEvaluation(IPositionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        _evaluator = evaluator;
    }

    /// <inheritdoc />
    public bool GivesPriors => true;

    /// <inheritdoc />
    public void Evaluate(MctsTree tree, MctsNode node, IReadOnlyList<Move> history, Komi komi)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(node);

        var evaluation = _evaluator.Evaluate(node.Board, node.ToMove, komi, PathMoves(node, history));

        if (!node.IsFullyExpanded)
        {
            var move = BestUntriedMove(node, evaluation);
            _ = tree.Expand(node, move, PriorOf(evaluation, move, node.Board.Size));
        }

        tree.Backpropagate(node, evaluation.WinProbability);
    }

    /// <summary>Выбирает неразобранный ход с наибольшей вероятностью по мнению сети.</summary>
    /// <param name="node">Узел с неразобранными ходами.</param>
    /// <param name="evaluation">Оценка позиции узла.</param>
    /// <returns>Ход, который сеть считает самым вероятным из оставшихся.</returns>
    private static Move BestUntriedMove(MctsNode node, PositionEvaluation evaluation)
    {
        var size = node.Board.Size;
        var best = node.UntriedMoves[0];
        var bestPrior = PriorOf(evaluation, best, size);

        foreach (var move in node.UntriedMoves)
        {
            var prior = PriorOf(evaluation, move, size);

            if (prior > bestPrior)
            {
                best = move;
                bestPrior = prior;
            }
        }

        return best;
    }

    /// <summary>Берёт вероятность хода из оценки сети.</summary>
    /// <param name="evaluation">Оценка позиции.</param>
    /// <param name="move">Ход.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Вероятность хода от 0 до 1.</returns>
    private static double PriorOf(PositionEvaluation evaluation, Move move, BoardSize size) =>
        move.Type == MoveType.Pass
            ? evaluation.ProbabilityOf(evaluation.PassIndex)
            : evaluation.ProbabilityOf((move.Point.Y * size.Value) + move.Point.X);

    /// <summary>Собирает ходы от начала партии до узла.</summary>
    /// <param name="node">Узел дерева.</param>
    /// <param name="history">Ходы партии до корня дерева.</param>
    /// <returns>Ходы по порядку: сначала партия, затем путь по дереву.</returns>
    private static IReadOnlyList<Move> PathMoves(MctsNode node, IReadOnlyList<Move> history)
    {
        var path = new List<Move>();

        for (var current = node; current?.Parent is not null; current = current.Parent)
        {
            path.Add(current.Move);
        }

        if (path.Count == 0)
        {
            return history;
        }

        path.Reverse();
        path.InsertRange(0, history);

        return path;
    }
}
