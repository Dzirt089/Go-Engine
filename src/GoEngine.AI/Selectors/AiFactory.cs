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
        IPositionEvaluator? evaluator = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(random);

        if (level.NeedsNetwork)
        {
            return CreateNeural(level, random, boardSize, evaluator);
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
    /// <returns>Селектор MCTS с оценкой сети.</returns>
    /// <exception cref="DomainException">Доска 9×9, размер не указан или оценка не передана.</exception>
    private static IMoveSelector CreateNeural(
        DifficultyLevel level,
        Random random,
        BoardSize? boardSize,
        IPositionEvaluator? evaluator)
    {
        if (boardSize is null)
        {
            throw new DomainException($"Уровень {level.Name} играет с сетью: нужен размер доски партии (D-038).");
        }

        if (boardSize == BoardSize.Size9)
        {
            throw new DomainException($"Уровень {level.Name} не играет на доске 9×9: сеть обучалась на 19×19, её оценка там неправдоподобна (D-038). Выберите 13×13 или 19×19 либо уровень кю.");
        }

        if (evaluator is null)
        {
            throw new DomainException($"Уровню {level.Name} нужна загруженная модель нейросети.");
        }

        var config = level.TimeBudget is { } time
            ? new MctsConfig(time, level.Ucb1C)
            : new MctsConfig(level.PlayoutBudget, level.Ucb1C);

        return new MctsMoveSelector(
            random,
            new PlayoutPolicy(level.PlayoutConfig),
            config,
            level.RandomnessPercent,
            evaluator: evaluator);
    }
}