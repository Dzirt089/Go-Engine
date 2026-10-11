namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Чем играет уровень сложности.</summary>
/// <remarks>
/// <para>
/// Видов два: поиск по дереву (<see cref="Mcts"/>) и поиск с оценкой сети (<see cref="Neural"/>).
/// Случайных ходов и эвристик в лестнице больше нет: уровни без поиска уступали соседним настолько,
/// что слабые ступени выглядели «тупыми», и после D-056 все они играют поиском. Селекторы
/// <c>RandomMoveSelector</c> и <c>HeuristicMoveSelector</c> удалены вместе с этими ветвями — их
/// держали только тесты (D-088).
/// </para>
/// <para>Идентификаторы: <c>Mcts</c> = 2, <c>Neural</c> = 3.</para>
/// </remarks>
public sealed class SelectorKind : Enumeration
{
    private const int MctsId = 2;
    private const int NeuralId = 3;

    private SelectorKind(int id, string name) : base(id, name)
    {
    }

    /// <summary>Поиск по дереву Монте-Карло.</summary>
    public static SelectorKind Mcts { get; } = new(MctsId, nameof(Mcts));

    /// <summary>Поиск с оценкой позиции нейросетью.</summary>
    public static SelectorKind Neural { get; } = new(NeuralId, nameof(Neural));
}
