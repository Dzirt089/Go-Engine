namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Политика игры в playout'ах MCTS: атари, ходы рядом с камнями, иначе случайный ход.</summary>
/// <remarks>
/// Порядок приоритетов: с вероятностью <see cref="PlayoutConfig.AtariProbability"/> — съесть
/// группу противника в атари, затем спасти свою группу в атари; с вероятностью
/// <see cref="PlayoutConfig.NeighborProbability"/> — ход рядом с существующими камнями;
/// иначе — случайный легальный ход. Ходы выбираются пробами случайных точек, а не перебором
/// всех ходов: в середине партии почти любая пустая точка легальна, и полный перебор
/// стоил бы сотни проверок правил на каждый ход playout'а.
/// MCTS внутри playout'а не вызывается (<c>DECISIONS.md</c>, D-003), своего
/// <see cref="Random"/> политика не создаёт (<c>AGENTS_GO.md</c>, п. 7).
/// </remarks>
public sealed class PlayoutPolicy
{
    /// <summary>Сколько случайных точек пробуется, прежде чем перебирать ходы целиком.</summary>
    private const int RandomAttempts = 20;

    private readonly PlayoutConfig _config;

    /// <summary>Создаёт политику с настройками по умолчанию.</summary>
    public PlayoutPolicy() : this(PlayoutConfig.Default)
    {
    }

    /// <summary>Создаёт политику с заданными настройками.</summary>
    /// <param name="config">Вероятности эвристик.</param>
    /// <exception cref="ArgumentOutOfRangeException">Вероятность вне диапазона 0…1.</exception>
    public PlayoutPolicy(PlayoutConfig config)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(config.AtariProbability);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(config.AtariProbability, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(config.NeighborProbability);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(config.NeighborProbability, 1);

        _config = config;
    }

    /// <summary>Выбирает ход playout'а.</summary>
    /// <param name="state">Партия, в которой идёт playout.</param>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <returns>Легальный ход или пас, если ходить некуда.</returns>
    public Move SelectMove(GameState state, Random random)
    {
        ArgumentNullException.ThrowIfNull(state);

        return SelectMove(state.Board, state.ToMove, random);
    }

    /// <summary>Выбирает ход playout'а по позиции и цвету, минуя партию.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Легальный ход или пас, если ходить некуда.</returns>
    public Move SelectMove(Board board, StoneColor color, Random random)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(random);

        if (random.NextDouble() < _config.AtariProbability)
        {
            var atariMove = AtariHeuristics.FindCapturingMove(board, color)
                ?? AtariHeuristics.FindRescueMove(board, color);

            if (atariMove is not null)
            {
                return atariMove.Value;
            }
        }

        if (random.NextDouble() < _config.NeighborProbability)
        {
            var neighborMove = FindRandomMove(board, color, random, point => HasNeighborStone(board, point) && IsSafe(board, color, point));

            if (neighborMove is not null)
            {
                return neighborMove.Value;
            }
        }

        // Если остались только самоубийственные и атарийные ходы, playout пропускает ход:
        // так он заканчивается на устоявшейся позиции, а не на доске, забитой камнями до отказа.
        // Пас разрешён всегда (GO_RULES.md, п. 3).
        return FindRandomMove(board, color, random, point => IsSafe(board, color, point)) ?? Move.Pass(color);
    }

    /// <summary>Ищет случайный ход, подходящий под условие.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="random">Источник случайности.</param>
    /// <param name="fits">Условие, которому должна удовлетворять точка хода.</param>
    /// <returns>Легальный ход или <c>null</c>, если подходящих ходов нет.</returns>
    /// <remarks>
    /// Сначала пробуется несколько случайных точек: этого почти всегда достаточно. Если пробы
    /// не удались (доска почти заполнена), ходы перебираются целиком — так пас выдаётся только
    /// тогда, когда легальных ходов действительно нет.
    /// </remarks>
    private static Move? FindRandomMove(Board board, StoneColor color, Random random, Func<Point, bool> fits)
    {
        for (var attempt = 0; attempt < RandomAttempts; attempt++)
        {
            var index = random.Next(board.Size.Area);
            var point = new Point((byte)(index % board.Size.Value), (byte)(index / board.Size.Value));

            if (fits(point) && IsLegal(board, color, point))
            {
                return Move.Play(point, color);
            }
        }

        foreach (var point in board.EmptyPoints())
        {
            if (fits(point) && IsLegal(board, color, point))
            {
                return Move.Play(point, color);
            }
        }

        return null;
    }

    /// <summary>Проверяет, что ход в точку легален.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="point">Точка хода.</param>
    /// <returns><c>true</c>, если правила ход разрешают.</returns>
    private static bool IsLegal(Board board, StoneColor color, Point point) =>
        board.IsEmpty(point) && board.IsLegal(Move.Play(point, color)).IsSuccess;

    /// <summary>Проверяет, что ход не губит свою группу.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="point">Точка хода.</param>
    /// <returns><c>true</c>, если после хода у своей группы останется больше одного дамэ.</returns>
    /// <remarks>
    /// Без этой проверки случайный playout сам закрывает дамэ своих групп и теряет их:
    /// партия заканчивается тем, что обе стороны убивают собственные живые группы.
    /// </remarks>
    private static bool IsSafe(Board board, StoneColor color, Point point) =>
        board.IsEmpty(point)
        && !AtariHeuristics.LeavesOwnGroupInAtari(board, Move.Play(point, color));

    /// <summary>Проверяет, стоит ли рядом с точкой камень.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка хода.</param>
    /// <returns><c>true</c>, если хотя бы один сосед по стороне занят.</returns>
    private static bool HasNeighborStone(Board board, Point point)
    {
        foreach (var neighbor in point.Neighbors(board.Size))
        {
            if (board.At(neighbor) != StoneColor.Empty)
            {
                return true;
            }
        }

        return false;
    }
}
