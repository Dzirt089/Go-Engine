namespace GoEngine.AI;

using GoEngine.AI.Mcts;
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
    /// <returns>Селектор, играющий на уровне.</returns>
    /// <exception cref="ArgumentNullException">Уровень или источник случайности не задан.</exception>
    /// <exception cref="AiException">У уровня неизвестный вид селектора.</exception>
    public static IMoveSelector Create(DifficultyLevel level, Random random)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(random);

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
            var config = new MctsConfig(level.PlayoutBudget, level.Ucb1C);

            return new MctsMoveSelector(random, policy, config, level.RandomnessPercent);
        }

        throw new AiException($"Уровень {level.Name} ссылается на неизвестный вид селектора {level.Kind.Name}.");
    }
}
