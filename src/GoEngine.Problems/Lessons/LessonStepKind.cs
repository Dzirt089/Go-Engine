namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Вид шага урока.</summary>
/// <remarks>
/// Урок состоит из чередующихся шагов: объяснение игрок читает, задание — выполняет на доске.
/// Вид шага решает, чего ждёт сессия: на объяснении ход не принимается, на задании — принимается
/// только тот, что ведёт к цели урока.
/// </remarks>
public sealed class LessonStepKind : Enumeration
{
    /// <summary>Объяснение: игрок читает текст и смотрит на позицию.</summary>
    public static LessonStepKind Explain { get; } = new(1, nameof(Explain), "объяснение");

    /// <summary>Задание: игрок делает ход на доске.</summary>
    public static LessonStepKind Task { get; } = new(2, nameof(Task), "задание");

    private LessonStepKind(int id, string name, string descriptions)
        : base(id, name, descriptions)
    {
    }
}
