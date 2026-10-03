using GoEngine.Core;

namespace GoEngine.AI;

/// <summary>Случайный выбор хода: уровень 30 кю.</summary>
/// <remarks>
/// Селектор собирает все легальные ходы и выбирает один из них. Случайность берётся только
/// из инжектированного <see cref="Random"/> — своего генератора класс не создаёт
/// (<c>AGENTS_GO.md</c>, п. 7). При фиксированном seed выбор детерминирован.
/// </remarks>
public sealed class RandomMoveSelector : IMoveSelector
{
    private readonly Random _random;

    /// <summary>Создаёт селектор.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    public RandomMoveSelector(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        _random = random;
    }

    /// <inheritdoc />
    public string Name => "Случайный ход (30 кю)";

    /// <inheritdoc />
    public Move SelectMove(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return SelectMove(state.Board, state.ToMove);
    }

    /// <summary>Выбирает ход по позиции и цвету, минуя партию.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <returns>Легальный ход или пас, если легальных ходов нет или все они убыточны.</returns>
    /// <remarks>
    /// Перегрузка нужна анализу и MCTS: там позиция есть, а партии может и не быть.
    /// Ходы, которые заведомо только кормят соперника, отсекает <see cref="PrisonerPolicy"/>:
    /// уровень остаётся случайным, но перестаёт отдавать камни в свои мёртвые группы
    /// (жалоба 2026-10-03). Если полезных ходов не осталось, уровень пасует.
    /// </remarks>
    public Move SelectMove(Board board, StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        var legalMoves = LegalMoves.For(board, color);

        // Пас разрешён всегда (GO_RULES.md, п. 3): если ходить некуда, игрок пропускает ход.
        if (legalMoves.Count == 0)
        {
            return Move.Pass(color);
        }

        var allowed = PrisonerPolicy.Instance.Apply(board, color, legalMoves);
        var pool = allowed.EverythingAllowed ? legalMoves : allowed.Moves;

        return pool.Count == 0 ? Move.Pass(color) : pool[_random.Next(pool.Count)];
    }
}
