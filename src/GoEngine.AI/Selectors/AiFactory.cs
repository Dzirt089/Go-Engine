namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Единая точка входа: селектор по уровню сложности.</summary>
/// <remarks>
/// Вид селектора задаётся <see cref="DifficultyLevel.Kind"/>: случайный, эвристический или MCTS.
/// Магических строк нет — только элементы перечислений (<c>AGENTS.md</c>, п. 11).
/// </remarks>
public static class AiFactory
{
    /// <summary>Создаёт селектор для уровня.</summary>
    /// <param name="level">Уровень сложности.</param>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <param name="boardSize">Размер доски партии: уровни с сетью играют только на больших.</param>
    /// <param name="evaluator">Оценка позиции нейросетью: нужна уровням с сетью.</param>
    /// <returns>Селектор, играющий на уровне.</returns>
    /// <exception cref="ArgumentNullException">Уровень или источник случайности не задан.</exception>
    /// <exception cref="AiException">У уровня неизвестный вид селектора.</exception>
    /// <exception cref="DomainException">Уровню нужна сеть, но доска 9×9 или оценка не передана.</exception>
    /// <remarks>
    /// Разделение зон ответственности по размеру доски — <c>DECISIONS.md</c>, D-038: сеть
    /// обучалась на 19×19, на 9×9 её оценка неправдоподобна, поэтому уровни Дан там запрещены,
    /// а уровни Кю работают на любой доске.
    /// </remarks>
    public static IMoveSelector Create(
        DifficultyLevel level,
        Random random,
        BoardSize? boardSize = null,
        IPositionEvaluator? evaluator = null,
        string? modelsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(random);

        if (level.NeedsNetwork)
        {
            return CreateNeural(level, random, boardSize, evaluator, modelsDirectory);
        }

        if (level.Kind == SelectorKind.Random)
        {
            return new RandomMoveSelector(random);
        }

        if (level.Kind == SelectorKind.Heuristic)
        {
            return new HeuristicMoveSelector(random, level.RandomnessPercent);
        }

        if (level.Kind == SelectorKind.Mcts)
        {
            var policy = new PlayoutPolicy(level.PlayoutConfig);

            // Уровни кю играют сетью, когда для размера доски есть модель: случайные доигрывания
            // при малом бюджете не видят тактику и подставляют группы (жалоба пользователя),
            // а сеть даёт и приоритеты, и оценку. Нет модели — MCTS по playout'ам или по времени.
            // Бюджет с сетью — в итерациях, а не во времени: так партия детерминирована, а время
            // хода подстраивается под доску (D-054).
            if (level.NeuralBudget is { } neuralIterations
                && evaluator is not null
                && modelsDirectory is not null
                && boardSize is not null
                && evaluator.LoadModelForBoardSize(boardSize.Value.Value, modelsDirectory))
            {
                return new MctsMoveSelector(
                    random,
                    policy,
                    new MctsConfig(neuralIterations.For(boardSize.Value), level.Ucb1C),
                    level.RandomnessPercent,
                    evaluator: evaluator);
            }

            var config = level.TimeBudget is { } time
                ? new MctsConfig(time, level.Ucb1C)
                : new MctsConfig(level.PlayoutBudget, level.Ucb1C);

            return new MctsMoveSelector(random, policy, config, level.RandomnessPercent);
        }

        throw new AiException($"Уровень {level.Name} ссылается на неизвестный вид селектора {level.Kind.Name}.");
    }

    /// <summary>Создаёт селектор уровня, который играет с нейросетью.</summary>
    /// <param name="level">Уровень сложности.</param>
    /// <param name="random">Источник случайности.</param>
    /// <param name="boardSize">Размер доски партии.</param>
    /// <param name="evaluator">Оценка позиции нейросетью.</param>
    /// <param name="modelsDirectory">Каталог с файлами моделей.</param>
    /// <returns>Селектор MCTS с оценкой сети.</returns>
    /// <exception cref="DomainException">Размер доски или каталог не указаны, оценка не передана
    /// либо для размера нет модели.</exception>
    /// <remarks>
    /// Какую модель брать для какого размера, решает таблица профилей (D-039): основная сеть
    /// годится для 13×13 и 19×19, для 9×9 нужна специализированная. Нет модели под размер —
    /// уровень с сетью недоступен, и это ожидаемый отказ, а не ошибка программиста (D-038).
    /// </remarks>
    private static IMoveSelector CreateNeural(
        DifficultyLevel level,
        Random random,
        BoardSize? boardSize,
        IPositionEvaluator? evaluator,
        string? modelsDirectory)
    {
        if (boardSize is null)
        {
            throw new DomainException($"Уровень {level.Name} играет с сетью: нужен размер доски партии (D-038).");
        }

        if (evaluator is null)
        {
            throw new DomainException($"Уровню {level.Name} нужна загруженная модель нейросети.");
        }

        if (modelsDirectory is null)
        {
            throw new DomainException($"Уровню {level.Name} нужен каталог моделей нейросети.");
        }

        var size = boardSize.Value;

        if (!evaluator.LoadModelForBoardSize(size.Value, modelsDirectory))
        {
            throw new DomainException($"Нет модели нейросети для доски {size}: выберите другую доску или уровень кю (D-039).");
        }

        // Ступени с сетью задают бюджет в итерациях: один вызов сети на итерацию, поэтому время
        // хода зависит от доски, а сила — нет (D-054).
        var iterations = level.NeuralBudget
            ?? throw new DomainException($"У уровня {level.Name} не задан бюджет итераций с сетью.");

        return new MctsMoveSelector(
            random,
            new PlayoutPolicy(level.PlayoutConfig),
            new MctsConfig(iterations.For(size), level.Ucb1C),
            level.RandomnessPercent,
            evaluator: evaluator);
    }
}