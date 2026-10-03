namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Отбор корневых ходов: правила движка применяются вместе и в одном порядке.</summary>
/// <remarks>
/// Селекторы спрашивают одно место, чтобы порядок правил не разъезжался: сначала
/// <see cref="EndgamePolicy"/> (не доедать свою территорию), затем <see cref="PrisonerPolicy"/>
/// (не кормить пленных). Пустой список означает пас — так же, как у каждого правила по отдельности.
/// </remarks>
public static class MovePolicy
{
    /// <summary>Отбирает ходы всеми правилами движка.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="moves">Ходы-кандидаты; предполагаются легальными.</param>
    /// <param name="opponentPassed">Соперник только что пропустил ход.</param>
    /// <returns>Разрешённые ходы; пустой список означает пас.</returns>
    /// <exception cref="ArgumentNullException">Позиция, цвет или список ходов не заданы.</exception>
    public static MoveFilterResult Apply(Board board, StoneColor color, IReadOnlyList<Move> moves, bool opponentPassed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(moves);

        var endgame = EndgamePolicy.Instance.Apply(board, color, moves, opponentPassed);

        if (!endgame.EverythingAllowed && endgame.Moves.Count == 0)
        {
            return endgame;
        }

        var pool = endgame.EverythingAllowed ? moves : endgame.Moves;
        var prisoners = PrisonerPolicy.Instance.Apply(board, color, pool);

        if (!prisoners.EverythingAllowed && prisoners.Moves.Count == 0)
        {
            return new MoveFilterResult([], EverythingAllowed: false);
        }

        return prisoners.EverythingAllowed ? endgame : prisoners;
    }
}
