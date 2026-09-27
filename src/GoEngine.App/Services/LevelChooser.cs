using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Какие уровни AI предлагать для выбранной доски и что делать с выбором, ставшим недоступным.</summary>
/// <remarks>
/// Уровни Дан показываются только тогда, когда для выбранной доски есть модель
/// (<c>DECISIONS.md</c>, D-039): основная сеть годится для 13×13 и 19×19, для 9×9 нужна
/// специализированная. Что именно доступно, решает голова — она знает каталог и профили,
/// а сюда передаётся готовый ответ «есть ли модель для этого размера».
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
    /// <param name="modelAvailableForSize">Нашлась ли модель для этого размера доски.</param>
    /// <returns>Уровни в порядке списка выбора.</returns>
    public static IReadOnlyList<DifficultyLevel> Available(BoardSize size, bool modelAvailableForSize)
    {
        ArgumentNullException.ThrowIfNull(size);

        // Уровни с нейросетью скрываются, если для этой доски нет модели: без неё уровень
        // не создать. Список идёт по возрастанию силы: кю, затем 1 кю с сетью и ступени Дан.
        return modelAvailableForSize
            ? [.. KyuLevels, DifficultyLevel.Kyu1, DifficultyLevel.Dan1, DifficultyLevel.Dan5]
            : KyuLevels;
    }

    /// <summary>Проверяет, доступен ли уровень для доски.</summary>
    /// <param name="level">Уровень.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="modelAvailableForSize">Нашлась ли модель для этого размера доски.</param>
    /// <returns><c>true</c>, если уровень можно предложить игроку.</returns>
    public static bool IsAvailable(DifficultyLevel level, BoardSize size, bool modelAvailableForSize)
    {
        ArgumentNullException.ThrowIfNull(level);

        return Available(size, modelAvailableForSize).Contains(level);
    }

    /// <summary>Возвращает уровень, если он доступен, иначе уровень сброса.</summary>
    /// <param name="selected">Выбранный уровень.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="modelAvailableForSize">Нашлась ли модель для этого размера доски.</param>
    /// <returns>Уровень, который можно использовать для этой доски.</returns>
    public static DifficultyLevel Resolve(DifficultyLevel selected, BoardSize size, bool modelAvailableForSize) =>
        IsAvailable(selected, size, modelAvailableForSize) ? selected : Fallback;

    /// <summary>Собирает подпись уровня: ранг, движок и бюджет.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>Например, «5 дан · нейросеть · 4 с/ход» или «20 кю · эвристики · без поиска».</returns>
    /// <remarks>
    /// Ранги номинальные — силу внешним соперником не мерили, поэтому подпись всегда называет
    /// движок и бюджет: по ним видно, чем уровень играет на самом деле.
    /// </remarks>
    public static string Label(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        return $"{Rank(level)} · {Engine(level)} · {Budget(level)}";
    }

    /// <summary>Собирает подпись уровня для панели партии: с размером доски, если играет сеть.</summary>
    /// <param name="level">Уровень.</param>
    /// <param name="size">Размер доски партии.</param>
    /// <param name="modelAvailableForSize">Есть ли модель для этого размера.</param>
    /// <returns>Например, «5 дан · нейросеть 9×9 · 4 с/ход».</returns>
    public static string Describe(DifficultyLevel level, BoardSize size, bool modelAvailableForSize)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(size);

        if (!level.NeedsNetwork)
        {
            return Label(level);
        }

        return modelAvailableForSize
            ? $"{Rank(level)} · нейросеть {size.Value}×{size.Value} · {Budget(level)}"
            : $"{Rank(level)} · нейросеть недоступна";
    }

    /// <summary>Ранг уровня словами.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>Например, «10 кю» или «5 дан».</returns>
    public static string Rank(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        return level.RankKyu < 0 ? $"{-level.RankKyu} дан" : $"{level.RankKyu} кю";
    }

    /// <summary>Чем играет уровень.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>«нейросеть», «MCTS без сети», «эвристики» или «случайные ходы».</returns>
    public static string Engine(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        if (level.NeedsNetwork)
        {
            return "нейросеть";
        }

        if (level.Kind == SelectorKind.Random)
        {
            return "случайные ходы";
        }

        return level.Kind == SelectorKind.Heuristic ? "эвристики" : "MCTS без сети";
    }

    /// <summary>Бюджет уровня словами.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>«4 с/ход», «6 playout'ов» или «без поиска».</returns>
    public static string Budget(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        if (level.TimeBudget is { } time)
        {
            return $"{time.TotalSeconds:0.#} с/ход";
        }

        return level.PlayoutBudget > 0 ? $"{level.PlayoutBudget} playout'ов" : "без поиска";
    }
}
