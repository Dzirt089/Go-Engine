namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Цель задачи на жизнь и смерть.</summary>
/// <remarks>
/// Цель определяет, что считается успехом, и как это проверяется по правилам движка
/// (<see cref="ProblemChecker"/>): <see cref="Capture"/> — камни целевой группы исчезли с доски,
/// <see cref="Live"/> — у целевой группы есть два настоящих глаза.
/// </remarks>
public sealed class ProblemGoal : Enumeration
{
    /// <summary>Убить целевую группу: в решённой позиции её камней на доске нет.</summary>
    public static ProblemGoal Capture { get; } = new(1, nameof(Capture), "убить группу");

    /// <summary>Обеспечить жизнь: у целевой группы должно быть два настоящих глаза.</summary>
    public static ProblemGoal Live { get; } = new(2, nameof(Live), "обеспечить жизнь группы");

    private ProblemGoal(int id, string name, string descriptions)
        : base(id, name, descriptions)
    {
    }

    /// <summary>Все цели задач.</summary>
    public static IReadOnlyList<ProblemGoal> All { get; } = [Capture, Live];
}
