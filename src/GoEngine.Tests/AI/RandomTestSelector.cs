using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Селектор случайных ходов для тестов: в игре такого уровня больше нет.</summary>
/// <remarks>
/// <para>
/// Раньше случайные ходы были боевым селектором (<c>RandomMoveSelector</c>), и его держали ради
/// проверок: быстрый соперник, который ничего не умеет, нужен тестам самоигрыши и политик.
/// После D-088 боевой селектор удалён — уровней без поиска в лестнице нет, — а проверкам оставлен
/// этот двойник: тестовая опора не обязана жить в библиотеке движка.
/// </para>
/// <para>Ходы берутся из легальных: правила спрашиваются у доски, а не у предполагаемого соперника.</para>
/// </remarks>
internal sealed class RandomTestSelector : IMoveSelector
{
    private readonly Random _random;

    /// <summary>Создаёт селектор случайных ходов.</summary>
    /// <param name="random">Источник случайности: в тестах — с фиксированным seed.</param>
    public RandomTestSelector(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        _random = random;
    }

    /// <inheritdoc />
    public string Name => "случайные ходы (тестовый)";

    /// <summary>Выбирает случайный легальный ход в позиции доски.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, за который играем.</param>
    /// <returns>Легальный ход или пас, если ходов нет.</returns>
    public Move SelectMove(Board board, StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(board);

        List<Move> legal = [];

        for (var y = 0; y < board.Size.Value; y++)
        {
            for (var x = 0; x < board.Size.Value; x++)
            {
                var move = Move.Play(new Point((byte)x, (byte)y), color);

                if (board.IsLegal(move).IsSuccess)
                {
                    legal.Add(move);
                }
            }
        }

        return legal.Count == 0 ? Move.Pass(color) : legal[_random.Next(legal.Count)];
    }

    /// <summary>Выбирает случайный легальный ход: та же подпись, что у боевых селекторов.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, за который играем.</param>
    /// <param name="komi">Коми партии: случайному выбору не нужен, но подпись обязана совпадать.</param>
    /// <param name="history">Ходы партии: тоже не нужны.</param>
    /// <returns>Легальный ход или пас, если ходов нет.</returns>
    public Move SelectMove(Board board, StoneColor color, Komi komi, IReadOnlyList<Move>? history = null)
    {
        _ = komi;
        _ = history;

        return SelectMove(board, color);
    }

    /// <inheritdoc />
    public Move SelectMove(GameState game)
    {
        ArgumentNullException.ThrowIfNull(game);

        List<Move> legal = [];

        for (var y = 0; y < game.Board.Size.Value; y++)
        {
            for (var x = 0; x < game.Board.Size.Value; x++)
            {
                var move = Move.Play(new Point((byte)x, (byte)y), game.ToMove);

                if (game.Board.IsLegal(move).IsSuccess)
                {
                    legal.Add(move);
                }
            }
        }

        return legal.Count == 0 ? Move.Pass(game.ToMove) : legal[_random.Next(legal.Count)];
    }
}

/// <summary>Селекторы для проверок: поиск с предельно малым бюджетом и случайным выбором.</summary>
/// <remarks>
/// Нужны там, где раньше стоял удалённый эвристический селектор (D-088): проверкам важен не поиск,
/// а то, что ход проходит через политики движка — конец партии и пленных. Случайность 100 %
/// означает «выбирай из разрешённого наугад», а предохранитель включается флагом.
/// </remarks>
internal static class TestSelectors
{
    /// <summary>Доля случайности, при которой выбор полностью случаен.</summary>
    private const int FullRandomness = 100;

    /// <summary>Создаёт поиск с одной итерацией и полностью случайным выбором хода.</summary>
    /// <param name="seed">Зерно случайности.</param>
    /// <param name="guard">Включён ли тактический предохранитель.</param>
    /// <returns>Селектор для проверок политик.</returns>
    public static MctsMoveSelector GuardedRandom(int seed, bool guard) =>
        new MctsMoveSelector(
            new Random(seed),
            new PlayoutPolicy(),
            new MctsConfig(1, MctsConfig.DefaultUcb1C) { TacticalGuard = guard },
            FullRandomness);
}
