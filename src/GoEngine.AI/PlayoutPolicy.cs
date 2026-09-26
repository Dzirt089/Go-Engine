namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Политика игры в playout'ах MCTS: атари, ходы рядом с камнями, иначе случайный ход.</summary>
/// <remarks>
/// Порядок приоритетов: с вероятностью <see cref="PlayoutConfig.AtariProbability"/> — съесть
/// группу противника в атари, затем спасти свою группу в атари; с вероятностью
/// <see cref="PlayoutConfig.NeighborProbability"/> — ход рядом с существующими камнями;
/// иначе — случайный легальный ход. MCTS внутри playout'а не вызывается: playout должен
/// оставаться дешёвым (<c>DECISIONS.md</c>, D-003).
/// Случайность приходит снаружи: политика своего <see cref="Random"/> не создаёт
/// (<c>AGENTS_GO.md</c>, п. 7).
/// </remarks>
public sealed class PlayoutPolicy
{
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

        var legalMoves = LegalMoves.For(board, color);

        // Пас разрешён всегда: если ходить некуда, playout пропускает ход.
        if (legalMoves.Count == 0)
        {
            return Move.Pass(color);
        }

        if (random.NextDouble() < _config.AtariProbability)
        {
            var atariMove = AtariHeuristics.FindCaptureMove(board, color, legalMoves)
                ?? AtariHeuristics.FindRescueMove(board, color, legalMoves);

            if (atariMove is not null)
            {
                return atariMove.Value;
            }
        }

        if (random.NextDouble() < _config.NeighborProbability)
        {
            var neighborMove = FindNeighborMove(board, legalMoves, random);

            if (neighborMove is not null)
            {
                return neighborMove.Value;
            }
        }

        return legalMoves[random.Next(legalMoves.Count)];
    }

    /// <summary>Выбирает случайный легальный ход рядом с уже стоящими камнями.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="legalMoves">Легальные ходы.</param>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Ход рядом с камнем или <c>null</c>, если доска пуста.</returns>
    private static Move? FindNeighborMove(Board board, IReadOnlyList<Move> legalMoves, Random random)
    {
        List<Move> neighborMoves = [];

        foreach (var move in legalMoves)
        {
            if (HasNeighborStone(board, move.Point))
            {
                neighborMoves.Add(move);
            }
        }

        return neighborMoves.Count == 0 ? null : neighborMoves[random.Next(neighborMoves.Count)];
    }

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
