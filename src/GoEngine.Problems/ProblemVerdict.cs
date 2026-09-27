namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Вердикт хода игрока в задаче.</summary>
public sealed class ProblemVerdict : Enumeration
{
    /// <summary>Ход принят, решение продолжается.</summary>
    public static ProblemVerdict Correct { get; } = new(1, nameof(Correct), "ход принят");

    /// <summary>Ход не из дерева решения: задача не решена, позиция не менялась.</summary>
    public static ProblemVerdict Wrong { get; } = new(2, nameof(Wrong), "ход не решает задачу");

    /// <summary>Задача решена: цель достигнута.</summary>
    public static ProblemVerdict Solved { get; } = new(3, nameof(Solved), "задача решена");

    /// <summary>Задача повреждена: дерево требует нелегального хода.</summary>
    /// <remarks>Такой вердикт означает ошибку в данных задачи, а не ошибку игрока.</remarks>
    public static ProblemVerdict Failed { get; } = new(4, nameof(Failed), "задача повреждена");

    private ProblemVerdict(int id, string name, string descriptions)
        : base(id, name, descriptions)
    {
    }

    /// <summary>Все вердикты.</summary>
    public static IReadOnlyList<ProblemVerdict> All { get; } = [Correct, Wrong, Solved, Failed];
}
