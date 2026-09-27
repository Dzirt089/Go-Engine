namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Состояние решения задачи.</summary>
public sealed class ProblemState : Enumeration
{
    /// <summary>Решение продолжается.</summary>
    public static ProblemState InProgress { get; } = new(1, nameof(InProgress), "решение продолжается");

    /// <summary>Задача решена.</summary>
    public static ProblemState Solved { get; } = new(2, nameof(Solved), "задача решена");

    private ProblemState(int id, string name, string descriptions)
        : base(id, name, descriptions)
    {
    }
}
