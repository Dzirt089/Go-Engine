namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Чем играет уровень сложности.</summary>
/// <remarks>Идентификаторы: <c>Random</c> = 0, <c>Heuristic</c> = 1, <c>Mcts</c> = 2, <c>Neural</c> = 3.</remarks>
public sealed class SelectorKind : Enumeration
{
    private const int RandomId = 0;
    private const int HeuristicId = 1;
    private const int MctsId = 2;
    private const int NeuralId = 3;

    private SelectorKind(int id, string name) : base(id, name)
    {
    }

    /// <summary>Случайный выбор хода без эвристик.</summary>
    public static SelectorKind Random { get; } = new(RandomId, nameof(Random));

    /// <summary>Выбор хода по эвристикам атари и углов.</summary>
    public static SelectorKind Heuristic { get; } = new(HeuristicId, nameof(Heuristic));

    /// <summary>Поиск по дереву Монте-Карло.</summary>
    public static SelectorKind Mcts { get; } = new(MctsId, nameof(Mcts));

    /// <summary>Поиск с оценкой позиции нейросетью.</summary>
    /// <remarks>Работает только на больших досках: сеть обучалась на 19×19 (D-038).</remarks>
    public static SelectorKind Neural { get; } = new(NeuralId, nameof(Neural));
}
