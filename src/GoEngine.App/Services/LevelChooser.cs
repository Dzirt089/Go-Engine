using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Какие уровни AI предлагать для выбранной доски и что делать с выбором, ставшим недоступным.</summary>
/// <remarks>
/// Разделение зон ответственности — <c>DECISIONS.md</c>, D-038: уровни Дан играют только
/// с загруженной сетью и только на досках 19×19 и 13×13, потому что сеть обучалась на 19×19.
/// Логика вынесена из диалога настроек: у окна её не проверить тестами, а здесь — можно.
/// </remarks>
public static class LevelChooser
{
    /// <summary>Уровни кю: доступны на любой доске и без сети.</summary>
    private static readonly DifficultyLevel[] KyuLevels =
    [
        DifficultyLevel.Kyu30,
        DifficultyLevel.Kyu25,
        DifficultyLevel.Kyu20,
        DifficultyLevel.Kyu15,
        DifficultyLevel.Kyu10,
        DifficultyLevel.Kyu8,
        DifficultyLevel.Kyu5
    ];

    /// <summary>Уровень, на который сбрасывается выбор, ставший недоступным.</summary>
    public static DifficultyLevel Fallback => DifficultyLevel.Kyu10;

    /// <summary>Перечисляет уровни, доступные для доски.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="networkAvailable">Загружена ли модель нейросети.</param>
    /// <returns>Уровни в порядке списка выбора.</returns>
    public static IReadOnlyList<DifficultyLevel> Available(BoardSize size, bool networkAvailable)
    {
        ArgumentNullException.ThrowIfNull(size);

        // Уровни Дан скрываются на 9×9 (оценка сети там неправдоподобна) и без модели.
        return size == BoardSize.Size9 || !networkAvailable
            ? KyuLevels
            : [.. KyuLevels, DifficultyLevel.Dan5];
    }

    /// <summary>Проверяет, доступен ли уровень для доски.</summary>
    /// <param name="level">Уровень.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="networkAvailable">Загружена ли модель нейросети.</param>
    /// <returns><c>true</c>, если уровень можно предложить игроку.</returns>
    public static bool IsAvailable(DifficultyLevel level, BoardSize size, bool networkAvailable)
    {
        ArgumentNullException.ThrowIfNull(level);

        return Available(size, networkAvailable).Contains(level);
    }

    /// <summary>Возвращает уровень, если он доступен, иначе уровень сброса.</summary>
    /// <param name="selected">Выбранный уровень.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="networkAvailable">Загружена ли модель нейросети.</param>
    /// <returns>Уровень, который можно использовать для этой доски.</returns>
    public static DifficultyLevel Resolve(DifficultyLevel selected, BoardSize size, bool networkAvailable) =>
        IsAvailable(selected, size, networkAvailable) ? selected : Fallback;

    /// <summary>Собирает подпись уровня для списка выбора.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>Например, «10 кю» или «5 дан».</returns>
    public static string Label(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        return level.RankKyu < 0 ? $"{-level.RankKyu} дан" : $"{level.RankKyu} кю";
    }
}
