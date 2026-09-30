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
/// <para>
/// Подписи уровней собраны для игрока, а не для разработчика: ранг и одно понятное слово
/// о движке. Аббревиатуры и числа итераций из подписей убраны по жалобе пользователя:
/// «Соперник 25 кю · MCTS с сетью 13×13 · 2 итерации» ничего не объясняет и переносится
/// на две строки. Честность при этом не теряется: ранги номинальные (D-043), поэтому силу
/// подпись не обещает, зато называет движок фактический — тот, который действительно сядет
/// играть (D-048).
/// </para>
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

    /// <summary>Собирает подпись уровня без контекста доски: ранг и понятное имя движка.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>Например, «5 дан · нейросеть» или «10 кю · нейросеть или перебор».</returns>
    /// <remarks>
    /// Доски здесь нет, поэтому у уровней кю названы оба возможных движка: без контекста подпись
    /// не имеет права обещать сеть, которой может и не оказаться (D-048). Там, где доска известна
    /// (список уровней, панель партии), подпись собирает <see cref="Describe"/> — и называет один
    /// движок, который сядет играть.
    /// </remarks>
    public static string Label(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        return $"{Rank(level)} · {Engine(level)}";
    }

    /// <summary>Собирает подпись уровня для выбранной доски: ранг и движок, который сядет играть.</summary>
    /// <param name="level">Уровень.</param>
    /// <param name="size">Размер доски партии.</param>
    /// <param name="modelAvailableForSize">Есть ли модель для этого размера.</param>
    /// <returns>Например, «5 дан · нейросеть 13×13» или «20 кю · перебор».</returns>
    /// <remarks>
    /// Одна короткая строка: ранг, движок и — у сети — доска, под которую обучена модель (D-039).
    /// Прежняя подпись («MCTS с сетью 13×13 · 2 итерации») называла сокращение и служебное число
    /// и переносилась на две строки в узкой панели; игроку это ничего не объясняло. Бюджет поиска
    /// из подписи убран и по другой причине: силу ступени он не измеряет (D-043), поэтому для
    /// служебных нужд остался отдельный <see cref="Budget"/>. Движок назван фактический,
    /// а не предпочтительный: уровни кю играют сетью, когда модель для доски есть, и перебором,
    /// когда её нет (D-045, D-048).
    /// </remarks>
    public static string Describe(DifficultyLevel level, BoardSize size, bool modelAvailableForSize)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(size);

        return $"{Rank(level)} · {GameEngine(level, size, modelAvailableForSize)}";
    }

    /// <summary>Называет движок, который действительно сядет играть на этой доске.</summary>
    /// <param name="level">Уровень.</param>
    /// <param name="size">Размер доски партии.</param>
    /// <param name="modelAvailableForSize">Нашлась ли модель для этого размера.</param>
    /// <returns>«нейросеть 13×13», «перебор», «нейросеть недоступна» или имя движка уровня.</returns>
    /// <remarks>
    /// Ступеням с сетью модель нужна всегда: без неё уровень не создать, и подпись говорит это
    /// прямо. Уровни кю играют сетью лишь там, где модель нашлась, и перебором — где нет (D-045):
    /// подпись обязана называть фактический движок (D-048).
    /// </remarks>
    private static string GameEngine(DifficultyLevel level, BoardSize size, bool modelAvailableForSize)
    {
        if (level.NeedsNetwork)
        {
            return modelAvailableForSize ? Neural(size) : "нейросеть недоступна";
        }

        if (level.NeuralBudget is null)
        {
            return Engine(level);
        }

        return modelAvailableForSize ? Neural(size) : "перебор";
    }

    /// <summary>Имя сетевого движка вместе с доской: модель обучена под конкретный размер (D-039).</summary>
    /// <param name="size">Размер доски партии.</param>
    /// <returns>Например, «нейросеть 13×13».</returns>
    private static string Neural(BoardSize size) => $"нейросеть {BoardSizes.Label(size)}";

    /// <summary>Число пробных партий словами: «1 доигрывание», «3 доигрывания», «25 доигрываний».</summary>
    /// <param name="count">Сколько партий доигрывает поиск на ход.</param>
    /// <returns>Подпись с правильным падежом.</returns>
    /// <remarks>
    /// Имя метода английское, как везде в коде движка, а текст — русский: заимствованное «playout»
    /// в подписи не попадает, потому что игроку оно ничего не говорит.
    /// </remarks>
    private static string PlayedOutGames(int count) =>
        $"{count} {Plural(count, "доигрывание", "доигрывания", "доигрываний")}";

    /// <summary>Выбирает форму слова по числу: 1 — «доигрывание», 2–4 — «доигрывания», иначе «доигрываний».</summary>
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

    /// <summary>Чем играет уровень — понятными словами, без аббревиатур.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>«нейросеть», «нейросеть или перебор», «перебор», «эвристика» или «случайные ходы».</returns>
    /// <remarks>
    /// Уровни кю играют сетью только там, где для доски нашлась модель, а признака модели в этой
    /// подписи нет: поэтому у них названы оба движка. Обещать один из них значило бы повторить
    /// дефект D-048, когда подпись называла не тот движок, который садился играть. Ветви про
    /// эвристику и случайные ходы остаются ради совместимости: уровней без поиска в лестнице
    /// больше нет (D-056), но сами селекторы живы и используются тестами и замерами.
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
            return "нейросеть или перебор";
        }

        if (level.Kind == SelectorKind.Random)
        {
            return "случайные ходы";
        }

        return level.Kind == SelectorKind.Heuristic ? "эвристика" : "перебор";
    }

    /// <summary>Чем ограничен поиск уровня: служебная подпись понятными словами.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>«2 с/ход», «12 доигрываний» или «по сети».</returns>
    /// <remarks>
    /// В интерфейс эта подпись не попадает: игроку число оценок или доигрываний ничего не говорит,
    /// а силу ступени оно не измеряет (D-043), поэтому в подписях уровня (<see cref="Label"/>,
    /// <see cref="Describe"/>) стоит только ранг и движок. Заимствованное «playout» заменено русским
    /// «доигрывание»: это и есть пробная партия, сыгранная до конца. У ступеней, которым сеть нужна
    /// всегда, бюджет — число её оценок на ход, но без доски оно бессмысленно, поэтому подпись
    /// называет только источник ограничения.
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
            return PlayedOutGames(level.PlayoutBudget);
        }

        return level.NeuralBudget is not null ? "по сети" : "без поиска";
    }
}
