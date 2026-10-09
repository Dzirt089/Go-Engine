namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Вердикт хода игрока в шаге обучения: в уроке или в разборе партии.</summary>
/// <remarks>
/// Один тип на оба вида материала: смысл вердиктов одинаков (ход не требовался, верный, неверный,
/// материал пройден), и разводить два одинаковых перечисления значило бы дублировать правило.
/// </remarks>
public sealed class LessonVerdict : Enumeration
{
    /// <summary>Ход не рассматривался: шаг объяснения или урок уже пройден.</summary>
    public static LessonVerdict None { get; } = new(1, nameof(None), "ход не требуется");

    /// <summary>Ход верный: задание шага выполнено.</summary>
    public static LessonVerdict Correct { get; } = new(2, nameof(Correct), "ход верный");

    /// <summary>Ход не тот, которого ждёт материал: позиция не менялась.</summary>
    public static LessonVerdict Wrong { get; } = new(3, nameof(Wrong), "ход не подходит");

    /// <summary>Материал пройден: шагов (ходов) больше нет.</summary>
    public static LessonVerdict Completed { get; } = new(4, nameof(Completed), "урок пройден");

    private LessonVerdict(int id, string name, string descriptions)
        : base(id, name, descriptions)
    {
    }
}
