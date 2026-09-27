using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Подставная оценка позиции: тестам не нужна модель.</summary>
/// <remarks>
/// Была описана дважды — в тестах модели представления и в тестах отката на уровни кю. Вынесена
/// в одно место: подставная оценка должна меняться в одном месте, а не в каждом наборе.
/// </remarks>
internal sealed class FakeEvaluator : IPositionEvaluator
{
    /// <inheritdoc />
    public PositionEvaluation Evaluate(Board board, StoneColor toMove, Komi komi, IReadOnlyList<Move> moves)
    {
        var area = board.Size.Area;
        var policy = new double[area + 1];
        policy[0] = 1.0;

        return new PositionEvaluation(policy, 0.5, area);
    }

    /// <inheritdoc />
    public bool LoadModelForBoardSize(int boardSize, string modelsDirectory) => true;
}
