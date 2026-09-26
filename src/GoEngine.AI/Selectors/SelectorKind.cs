namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Чем играет уровень сложности.</summary>
/// <remarks>Идентификаторы: <c>Random</c> = 0, <c>Heuristic</c> = 1, <c>Mcts</c> = 2.</remarks>
public sealed class SelectorKind : Enumeration
{
    private const int RandomId = 0;
    private const int HeuristicId = 1;
    private const int MctsId = 2;

    private SelectorKind(int id, string name) : base(id, name)
    {
    }

    /// <summary>Случайный выбор хода без эвристик.</summary>
    public static SelectorKind Random { get; } = new(RandomId, nameof(Random));

    /// <summary>Выбор хода по эвристикам атари и углов.</summary>
    public static SelectorKind Heuristic { get; } = new(HeuristicId, nameof(Heuristic));

    /// <summary>Поиск по дереву Монте-Карло.</summary>
    public static SelectorKind Mcts { get; } = new(MctsId, nameof(Mcts));
}
