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
    /// <summary>Уровни кю: доступны на любой доске, играют сетью, когда для доски есть модель.</summary>
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

    /// <summary>Собирает подпись уровня без контекста доски: ранг, движок и бюджет 9×9.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>Например, «5 дан · нейросеть · 44 итерации на 9×9».</returns>
    /// <remarks>
    /// Ранги номинальные — силу внешним соперником не мерили, поэтому подпись всегда называет
    /// движок и бюджет: по ним видно, чем уровень играет на самом деле. Бюджет с сетью зависит
    /// от доски, поэтому точную подпись для партии даёт <see cref="Describe"/> (D-054), а здесь
    /// указана доска 9×9 — так подпись не врёт, а оговаривает доску.
    /// </remarks>
    public static string Label(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        return $"{Rank(level)} · {Engine(level)} · {Budget(level)}";
    }

    /// <summary>Собирает подпись уровня для выбранной доски: фактический движок и точный бюджет.</summary>
    /// <param name="level">Уровень.</param>
    /// <param name="size">Размер доски партии.</param>
    /// <param name="modelAvailableForSize">Есть ли модель для этого размера.</param>
    /// <returns>Например, «5 дан · нейросеть 19×19 · 25 итераций».</returns>
    public static string Describe(DifficultyLevel level, BoardSize size, bool modelAvailableForSize)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(size);

        if (level.NeedsNetwork)
        {
            return modelAvailableForSize
                ? $"{Rank(level)} · нейросеть {BoardSizes.Label(size)} · {Iterations(level, size)}"
                : $"{Rank(level)} · нейросеть недоступна";
        }

        // Уровни кю играют сетью, когда модель для доски есть, и MCTS — когда её нет: подпись
        // обязана называть фактический движок, а не тот, который предпочли бы (D-045).
        if (level.NeuralBudget is not null)
        {
            return modelAvailableForSize
                ? $"{Rank(level)} · MCTS с сетью {BoardSizes.Label(size)} · {Iterations(level, size)}"
                : $"{Rank(level)} · MCTS без сети · {Budget(level)}";
        }

        return Label(level);
    }

    /// <summary>Сколько итераций поиска делает уровень на этой доске.</summary>
    /// <param name="level">Уровень.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Например, «25 итераций».</returns>
    private static string Iterations(DifficultyLevel level, BoardSize size) =>
        Iterations(level.NeuralBudget!.Value.For(size));

    /// <summary>Число итераций словами: «1 итерация», «3 итерации», «25 итераций».</summary>
    /// <param name="count">Число итераций.</param>
    /// <returns>Подпись с правильным падежом.</returns>
    private static string Iterations(int count) => $"{count} {Plural(count, "итерация", "итерации", "итераций")}";

    /// <summary>Число playout'ов словами: «1 playout», «3 playout'а», «25 playout'ов».</summary>
    /// <param name="count">Число playout'ов.</param>
    /// <returns>Подпись с правильным падежом.</returns>
    private static string Playouts(int count) => $"{count} {Plural(count, "playout", "playout'а", "playout'ов")}";

    /// <summary>Выбирает форму слова по числу: 1 — «итерация», 2–4 — «итерации», иначе «итераций».</summary>
    /// <param name="count">Число.</param>
    /// <param name="one">Форма для одного.</param>
    /// <param name="few">Форма для двух–четырёх.</param>
    /// <param name="many">Форма для остальных.</param>
    /// <returns>Нужная форма слова.</returns>
    private static string Plural(int count, string one, string few, string many)
    {
        if (count % 100 is >= 11 and <= 14)
        {
            return many;
        }

        return (count % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many
        };
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
    /// <returns>«нейросеть», «MCTS с сетью», «MCTS без сети», «эвристики» или «случайные ходы».</returns>
    /// <remarks>
    /// Ветви про эвристики и случайные ходы остаются ради совместимости: уровней без поиска
    /// в лестнице больше нет (D-054), но сами селекторы живы и используются тестами и замерами.
    /// </remarks>
    public static string Engine(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        if (level.NeedsNetwork)
        {
            return "нейросеть";
        }

        if (level.NeuralBudget is not null)
        {
            return "MCTS с сетью";
        }

        if (level.Kind == SelectorKind.Random)
        {
            return "случайные ходы";
        }

        return level.Kind == SelectorKind.Heuristic ? "эвристики" : "MCTS без сети";
    }

    /// <summary>Бюджет уровня без сети словами.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>«4 с/ход», «48 playout'ов» или «44 итерации на 9×9» у уровней только с сетью.</returns>
    /// <remarks>
    /// Это бюджет пути без сети: он остаётся у уровней кю, которые играют сетью лишь тогда,
    /// когда для доски нашлась модель. У ступеней, которым сеть нужна всегда, доски нет —
    /// поэтому показывается бюджет 9×9, а точный даёт <see cref="Describe"/>.
    /// </remarks>
    public static string Budget(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        if (level.TimeBudget is { } time)
        {
            return $"{time.TotalSeconds:0.#} с/ход";
        }

        if (level.PlayoutBudget > 0)
        {
            return Playouts(level.PlayoutBudget);
        }

        return level.NeuralBudget is { } neural ? $"{Iterations(neural.For9x9)} на 9×9" : "без поиска";
    }
}
