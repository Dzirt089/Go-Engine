namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Узел дерева решения: множество принимаемых ходов стороны, которая ходит в позиции.</summary>
/// <remarks>
/// Дерево чередуется: корень — ход решающего, его продолжения — ответы соперника, затем снова
/// ход решающего. Узел без принимаемых ходов — лист: цель задачи достигнута
/// (<see cref="ProblemNode.Leaf"/>).
/// </remarks>
public sealed class ProblemNode
{
    /// <summary>Лист дерева: ходов больше нет, цель достигнута.</summary>
    public static ProblemNode Leaf { get; } = new([]);

    /// <summary>Создаёт узел с принимаемыми ходами.</summary>
    /// <param name="moves">Принимаемые ходы; пустой список — лист.</param>
    public ProblemNode(IReadOnlyList<ProblemMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        Moves = moves;
    }

    /// <summary>Принимаемые ходы в этой позиции.</summary>
    public IReadOnlyList<ProblemMove> Moves { get; }

    /// <summary>Узел является листом: цель достигнута, ходов нет.</summary>
    public bool IsLeaf => Moves.Count == 0;

    /// <summary>Создаёт узел из одного принимаемого хода.</summary>
    /// <param name="move">Ход.</param>
    /// <param name="next">Продолжение после хода.</param>
    /// <returns>Узел с одним ходом.</returns>
    public static ProblemNode One(Move move, ProblemNode next) => new([new ProblemMove(move, next)]);
}
