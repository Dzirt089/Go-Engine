namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Правило пленных: движок не играет ходы, которые заведомо только кормят соперника.</summary>
/// <remarks>
/// <para>
/// Жалоба пользователя 2026-10-03: соперник (AI) ставил камни к своим доказанно мёртвым группам
/// и в плотную территорию чёрных, увеличивая счёт пленных у противника. Ход в свою мёртвую группу
/// её не спасает — перебор <see cref="Endgame.ProposeDead(Board)"/> доказал, что группа снимается
/// даже когда защищающийся ходит первым, — а каждый лишний камень уходит в пленные.
/// </para>
/// <para>
/// Правило узкое и опирается на доказательство, а не на догадку. Отбрасываются только ходы,
/// которые ставят камень в точку, примыкающую к своей доказанно мёртвой группе, или в территорию
/// соперника на доске без доказанно мёртвых камней (<see cref="Scorer.Ownership"/>). Ход, который
/// что-то даёт — снимает камни, спасает живую группу из атари или прижимает чужую до одного дамэ, —
/// играется в любом случае: иначе движок отказывался бы от защиты своих групп и от убийства чужих
/// в чужом владении.
/// </para>
/// <para>
/// Правило включается только там, где перебор уже что-то доказал, — в позиции с доказанно мёртвой
/// группой. В открытой позиции оно молчит: «владение» из подсчёта очков там означает, что первый
/// камень забирает всю пустую доску, и любой ход выглядел бы убыточным.
/// </para>
/// <para>
/// Если правило отбросило все ходы, движок пасует: пас законен и ничего не кормит
/// (<c>GO_RULES.md</c>, п. 3), а партия завершается двумя пасами (п. 8).
/// </para>
/// <para>
/// Границы честные: перебор мёртвых локальный (до двух дамэ, горизонт шесть полуходов, без ко
/// и дальних угроз, D-068), поэтому правило молчит там, где смерть группы не доказана. Ошибка
/// в эту сторону безопасна: движок просто продолжает играть как раньше.
/// </para>
/// </remarks>
public sealed class PrisonerPolicy
{
    /// <summary>Правило пленных — одно на все селекторы: состояния у него нет.</summary>
    public static PrisonerPolicy Instance { get; } = new();

    private PrisonerPolicy()
    {
    }

    /// <summary>Отбирает ходы, которые не кормят пленных.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="moves">Ходы-кандидаты; предполагаются легальными.</param>
    /// <returns>
    /// Разрешённые ходы. Пустой список при <c>EverythingAllowed = false</c> означает пас: правило
    /// запретило все ходы, и селектор обязан пропустить ход, а не выбирать меньшее зло.
    /// </returns>
    /// <exception cref="ArgumentNullException">Позиция, цвет или список ходов не заданы.</exception>
    public MoveFilterResult Apply(Board board, StoneColor color, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(moves);

        if (moves.Count == 0)
        {
            return new MoveFilterResult(moves, EverythingAllowed: true);
        }

        // Мёртвых считает тот же перебор, что и подсчёт очков: второго доказательства в проекте нет.
        var dead = Endgame.ProposeDead(board);
        HashSet<Point> doomed = [.. dead];

        if (doomed.Count == 0)
        {
            // Правило молчит, пока перебор ничего не доказал. Это не осторожность, а необходимость:
            // в открытой позиции «владение» из подсчёта очков означает, что первый же камень
            // забирает всю пустую доску, и правило объявило бы убыточным любой ход — движок пасовал бы
            // в начале партии (проверено: 40 разрешённых ходов из 40 становились тремя).
            // Заодно правило не трогает обычную середину партии: там доказанно мёртвых групп нет.
            return new MoveFilterResult(moves, EverythingAllowed: true);
        }

        HashSet<Point> ownDead = [.. dead.Where(point => board.At(point) == color)];

        // Территория соперника считается по доске БЕЗ мёртвых: точки снятых групп — его владение,
        // и ход туда такой же убыток, как ход рядом со своей мёртвой группой.
        var cleared = Endgame.ClearedBoard(board, dead);
        var ownership = Scorer.Ownership(cleared);
        var side = board.Size.Value;

        List<Move> kept = [];

        foreach (var move in moves)
        {
            if (Allowed(board, color, move, ownDead, doomed, ownership, side))
            {
                kept.Add(move);
            }
        }

        return kept.Count == moves.Count
            ? new MoveFilterResult(moves, EverythingAllowed: true)
            : new MoveFilterResult(kept.AsReadOnly(), EverythingAllowed: false);
    }

    /// <summary>Решает, разрешает ли правило пленных этот ход.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="move">Проверяемый ход.</param>
    /// <param name="ownDead">Камни своих доказанно мёртвых групп.</param>
    /// <param name="doomed">Камни всех доказанно мёртвых групп: по ним видно, кого не спасают.</param>
    /// <param name="ownership">Владение точками на доске без мёртвых камней.</param>
    /// <param name="side">Сторона доски: по ней считается индекс точки.</param>
    /// <returns><c>true</c>, если ход разрешён.</returns>
    /// <remarks>
    /// Обычный ход — тот, что не примыкает к своей мёртвой группе и не лежит в чужой территории, —
    /// правило не трогает вовсе: в середине партии оно не меняет ничего.
    /// </remarks>
    private static bool Allowed(
        Board board,
        StoneColor color,
        Move move,
        HashSet<Point> ownDead,
        HashSet<Point> doomed,
        IReadOnlyList<StoneColor> ownership,
        int side)
    {
        // Пас кандидатом не бывает, но правило его не касается: пас ничего не кормит.
        if (move.IsNone || move.Type != MoveType.Play)
        {
            return true;
        }

        var point = move.Point;
        var inOpponentArea = ownership[(point.Y * side) + point.X] == color.Opponent();
        var nearOwnDead = ownDead.Count > 0 && point.Neighbors(board.Size).Any(ownDead.Contains);

        if (!inOpponentArea && !nearOwnDead)
        {
            return true;
        }

        // Угроза чужой группе — довод только для чужой территории. Рядом со своей мёртвой группой
        // она ничего не спасает: группа уже доказанно мертва, а прижатие соперника в его же владении
        // не мешает ему снять её и записать камень в пленные (это и есть жалоба 2026-10-03).
        return GivesSomething(board, move, doomed, allowThreat: inOpponentArea && !nearOwnDead);
    }

    /// <summary>Проверяет, даёт ли ход что-то кроме потери камня.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="move">Проверяемый ход.</param>
    /// <param name="doomed">Камни всех доказанно мёртвых групп.</param>
    /// <param name="allowThreat">Считать ли угрозу чужой группе доводом за ход.</param>
    /// <returns><c>true</c>, если ход снимает камни, спасает живую группу или прижимает чужую.</returns>
    /// <remarks>
    /// Спасение доказанно мёртвой группы сюда не входит: перебор доказал, что её не спасти,
    /// и продление такой группы — это ровно то кормление, на которое жаловался игрок.
    /// </remarks>
    private static bool GivesSomething(Board board, Move move, HashSet<Point> doomed, bool allowThreat)
    {
        var after = board.WithoutHistory().ApplyMove(move);

        if (after.CapturedStones.Count > 0)
        {
            return true;
        }

        foreach (var neighbor in move.Point.Neighbors(board.Size))
        {
            if (board.At(neighbor) == move.Color
                && !doomed.Contains(neighbor)
                && GroupTracker.FindGroup(board, neighbor).IsInAtari)
            {
                return true;
            }

            var enemy = after.At(neighbor);

            if (allowThreat
                && enemy != StoneColor.Empty
                && enemy != move.Color
                && GroupTracker.FindGroup(after, neighbor).IsInAtari)
            {
                return true;
            }
        }

        return false;
    }
}
