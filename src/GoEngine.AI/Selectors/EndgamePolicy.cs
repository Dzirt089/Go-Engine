namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Правила конца партии: движок не доедает свою территорию и вовремя пасует.</summary>
/// <remarks>
/// Жалоба пользователя: «когда я решил закончить игру, сказал по кнопке несколько раз пас — противник
/// доел мои камни, а потом начал ходить и стал поедать свои же очки не останавливаясь». Причина:
/// селектор отдаёт ход всегда, когда есть формально легальные ходы, а заполнение собственной
/// территории от полезного хода в оценке не отличается. Правило отсекает такие ходы у корня,
/// и если играть нечего — движок пасует, после чего партия завершается двумя пасами
/// (<c>GO_RULES.md</c>, п. 8).
/// Тактический предохранитель (<see cref="TacticalMoveFilter"/>) этим правилом не отменяется:
/// в середине партии вынужденный ход по-прежнему лучше паса, пас появляется только тогда, когда
/// все оставшиеся ходы заполняют свою же территорию.
/// </remarks>
public sealed class EndgamePolicy
{
    /// <summary>Коми для сравнения очков: сравниваются очки своей стороны, а коми в них не входит.</summary>
    private static readonly Komi ZeroKomi = new(0);

    private EndgamePolicy()
    {
    }

    /// <summary>Правила конца партии — одни на все селекторы: состояния у них нет.</summary>
    public static EndgamePolicy Instance { get; } = new();

    /// <summary>Проверяет, что соперник только что пропустил ход.</summary>
    /// <param name="history">Ходы партии до текущей позиции или <c>null</c>, если истории нет.</param>
    /// <returns><c>true</c>, если последний сделанный ход — пас.</returns>
    public static bool OpponentPassed(IReadOnlyList<Move>? history) =>
        history is { Count: > 0 } && history[^1].Type == MoveType.Pass;

    /// <summary>Отбирает ходы, которые правила конца партии разрешают играть.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="moves">Ходы-кандидаты; предполагаются легальными.</param>
    /// <param name="opponentPassed">Соперник только что пропустил ход.</param>
    /// <returns>
    /// Разрешённые ходы. Пустой список при <c>EverythingAllowed = false</c> означает пас: правило
    /// запретило все ходы, и селектор обязан пропустить ход, а не выбирать меньшее зло.
    /// </returns>
    /// <exception cref="ArgumentNullException">Позиция, цвет или список ходов не заданы.</exception>
    /// <remarks>
    /// Правило паса словами:
    /// 1. Ход, который что-то даёт — снимает камни противника, спасает свою группу из атари или
    ///    прижимает чужую группу до одного дамэ, — играется всегда, даже внутри своей территории.
    /// 2. Ход внутрь своей территории, который ничего не даёт, не играется: очко остаётся своим же,
    ///    и партия от такого хода не меняется — это и есть «доедание своих очков».
    /// 3. Прочие ходы играются, но если соперник только что пропустил ход, играются лишь те,
    ///    что увеличивают очки ходящего по <see cref="Scorer.Calculate"/>.
    /// 4. Если после отбора не осталось ни одного хода — движок пасует.
    /// «Своя территория» — пустая точка, пустая область которой граничит только с камнями ходящего:
    /// это ровно <see cref="Scorer.Ownership"/>, своего подсчёта здесь нет.
    /// Оговорка о почти пустой доске: пока камни есть только у одной стороны, вся пустая область
    /// формально принадлежит ей, поэтому если соперник в этот момент пасует, ходящий тоже пасует.
    /// В обычной партии это недостижимо: после первого ответного камня область становится нейтральной,
    /// и правило перестаёт что-либо отсекать.
    /// </remarks>
    public MoveFilterResult Apply(Board board, StoneColor color, IReadOnlyList<Move> moves, bool opponentPassed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(moves);

        if (moves.Count == 0)
        {
            return new MoveFilterResult(moves, EverythingAllowed: true);
        }

        var ownership = Scorer.Ownership(board);
        var pointsBefore = opponentPassed ? Points(board, color) : 0;
        List<Move> kept = [];

        foreach (var move in moves)
        {
            if (CanPlay(board, color, move, ownership, opponentPassed, pointsBefore))
            {
                kept.Add(move);
            }
        }

        // Пустой список — это пас: правило отсекло всё, что можно было сыграть.
        if (kept.Count == 0)
        {
            return new MoveFilterResult([], EverythingAllowed: false);
        }

        return kept.Count == moves.Count
            ? new MoveFilterResult(moves, EverythingAllowed: true)
            : new MoveFilterResult(kept.AsReadOnly(), EverythingAllowed: false);
    }

    /// <summary>Решает, разрешает ли правило конца партии этот ход.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="move">Проверяемый ход.</param>
    /// <param name="ownership">Владелец каждой точки позиции.</param>
    /// <param name="opponentPassed">Соперник только что пропустил ход.</param>
    /// <param name="pointsBefore">Очки ходящего до хода: нужны при пасе соперника.</param>
    /// <returns><c>true</c>, если ход разрешён.</returns>
    private static bool CanPlay(
        Board board,
        StoneColor color,
        Move move,
        IReadOnlyList<StoneColor> ownership,
        bool opponentPassed,
        double pointsBefore)
    {
        // Пас и сдача позицию не меняют: правило их не касается.
        if (move.IsNone || move.Type != MoveType.Play)
        {
            return true;
        }

        // Ход вне своей территории играется всегда, кроме случая, когда соперник пропустил ход:
        // тогда нужен прирост очков, а его считают по позиции после хода.
        if (ownership[(move.Point.Y * board.Size.Value) + move.Point.X] != color)
        {
            return !opponentPassed || Points(After(board, move), color) > pointsBefore;
        }

        // Своя территория: играется только то, что что-то даёт, — иначе это доедание своих очков.
        return GivesSomething(board, move);
    }

    /// <summary>Проверяет, даёт ли ход что-то кроме заполнения точки.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="move">Проверяемый ход.</param>
    /// <returns><c>true</c>, если ход снимает камни, спасает свою группу или прижимает чужую.</returns>
    /// <remarks>
    /// Исключения важны для защиты: спасение своей группы из атари и ответ на угрозу — это ходы
    /// внутри своей территории, отказ от которых стоил бы группы или очков.
    /// </remarks>
    private static bool GivesSomething(Board board, Move move)
    {
        var after = After(board, move);

        if (after.CapturedStones.Count > 0)
        {
            return true;
        }

        foreach (var neighbor in move.Point.Neighbors(board.Size))
        {
            // Своя группа была в атари: это защита или продление, отказываться от неё нельзя.
            if (board.At(neighbor) == move.Color && GroupTracker.FindGroup(board, neighbor).IsInAtari)
            {
                return true;
            }

            // Чужая группа после хода осталась с одним дамэ: ход — угроза, а не заполнение.
            var enemy = after.At(neighbor);

            if (enemy != StoneColor.Empty && enemy != move.Color && GroupTracker.FindGroup(after, neighbor).IsInAtari)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Возвращает позицию после хода, не трогая историю партии.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="move">Проверяемый ход.</param>
    /// <returns>Позиция после хода.</returns>
    /// <remarks>
    /// Копия берётся без истории: <c>ApplyMove</c> на доске с историей дописывал бы в неё проверяемый
    /// ход и портил суперко — та же причина, по которой так устроен <see cref="TacticalGuard"/> (T-037).
    /// </remarks>
    private static Board After(Board board, Move move) => board.WithoutHistory().ApplyMove(move);

    /// <summary>Считает очки цвета по китайским правилам.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, чьи очки нужны.</param>
    /// <returns>Очки цвета без коми: коми прибавляется белым и в сравнении не участвует.</returns>
    private static double Points(Board board, StoneColor color)
    {
        var score = Scorer.Calculate(board, ZeroKomi);

        return color == StoneColor.Black ? score.Black : score.White;
    }
}
