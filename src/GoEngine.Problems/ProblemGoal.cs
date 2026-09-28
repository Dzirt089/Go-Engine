namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Цель задачи на жизнь и смерть.</summary>
/// <remarks>
/// Цель определяет, что считается успехом, и как это проверяется по правилам движка
/// (<see cref="ProblemChecker"/>): <see cref="Capture"/> — камни целевой группы исчезли с доски,
/// <see cref="Live"/> — у целевой группы есть два настоящих глаза, <see cref="Dead"/> — атакующий
/// форсирует захват группы в пределах заявленной границы проверки.
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

    /// <summary>Убить целевую группу, не снимая её: защищающийся не может построить два глаза.</summary>
    /// <remarks>
    /// Отличие от <see cref="Capture"/>: камни цели остаются на доске, важен результат — группа мертва.
    /// Проверяется перебором: защищающийся играет первым и не может форсировать два глаза за
    /// заявленную границу <see cref="Problem.CheckDepth"/>; граница ограничена, и это записано в задаче.
    /// </remarks>
    public static ProblemGoal Dead { get; } = new(3, nameof(Dead), "убить группу, не снимая: двух глаз у неё быть не может");

    /// <summary>Все цели задач.</summary>
    public static IReadOnlyList<ProblemGoal> All { get; } = [Capture, Live, Dead];
}
