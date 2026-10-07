namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Вердикт хода игрока в уроке.</summary>
public sealed class LessonVerdict : Enumeration
{
    /// <summary>Ход не рассматривался: шаг объяснения или урок уже пройден.</summary>
    public static LessonVerdict None { get; } = new(1, nameof(None), "ход не требуется");

    /// <summary>Ход верный: задание шага выполнено.</summary>
    public static LessonVerdict Correct { get; } = new(2, nameof(Correct), "ход верный");

    /// <summary>Ход не тот, которого ждёт урок: позиция не менялась.</summary>
    public static LessonVerdict Wrong { get; } = new(3, nameof(Wrong), "ход не подходит");

    /// <summary>Урок пройден: шагов больше нет.</summary>
    public static LessonVerdict Completed { get; } = new(4, nameof(Completed), "урок пройден");

    private LessonVerdict(int id, string name, string descriptions)
        : base(id, name, descriptions)
    {
    }
}
