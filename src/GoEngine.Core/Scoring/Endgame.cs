namespace GoEngine.Core;

/// <summary>Конец партии: предложение мёртвых групп, снятие мёртвых камней и итоговый подсчёт.</summary>
/// <remarks>
/// <para>
/// После двух пасов игроки согласуют мёртвые группы (<c>GO_RULES.md</c>, п. 7–8 и 9). Ядро не
/// ведёт переговоры: оно предлагает только то, что доказано ограниченным перебором
/// (<see cref="ProposeDead"/>), а окончательный список принимает от вызывающего кода
/// (<see cref="Finalize(Board, IReadOnlyCollection{Point}, Komi, int, int)"/>). Поэтому интерфейс показывает предложение, игрок правит его кликами,
/// и только подтверждённый список попадает в счёт.
/// </para>
/// <para>
/// Снятый мёртвый камень не исчезает бесследно: он уходит в пленные соперника
/// (<see cref="FinalScore.BlackPrisoners"/>, <see cref="FinalScore.WhitePrisoners"/>) и потому
/// виден и в японской величине, и в площади — его точка становится территорией снявшего.
/// </para>
/// <para>
/// Территория и владение считаются по доске <b>без</b> мёртвых камней
/// (<see cref="ClearedBoard"/>): иначе мёртвый камень внутри чужого владения отменял бы
/// территорию соперника и приносил очки своему цвету, которых тот не заслужил.
/// </para>
/// </remarks>
public static class Endgame
{
    /// <summary>Сколько дамэ может быть у группы, чтобы её вообще проверяли перебором на смерть.</summary>
    /// <remarks>
    /// Группа с тремя и более дамэ в пределах горизонта не доказуемо мертва, а неполный перебор
    /// не имеет права называть её мёртвой. Ограничение — граница предложения, а не правила:
    /// игрок вправе снять любую группу вручную, и <see cref="Finalize(Board, IReadOnlyCollection{Point}, Komi, int, int)"/> примет его список.
    /// </remarks>
    public const int CandidateLiberties = 2;

    /// <summary>Горизонт поиска форсированного захвата в полуходах.</summary>
    /// <remarks>Шесть полуходов — это три хода каждой стороны: хватает на гонку за одно-два дамэ.</remarks>
    public const int HorizonHalfMoves = 6;

    /// <summary>Предел перебранных позиций на одну группу.</summary>
    /// <remarks>Упор в предел означает «не доказано»: группа остаётся на доске.</remarks>
    public const int MaxNodesPerGroup = 20_000;

    /// <summary>Предлагает мёртвые группы: те, что снимаются форсированно.</summary>
    /// <param name="board">Позиция на момент согласования.</param>
    /// <returns>
    /// Камни предложенных групп в порядке обхода доски; пустой список, если ничего не доказано.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Группа предлагается мёртвой, только если перебор доказал форсированный захват: защищающийся
    /// ходит первым, и ни один его ход не спасает группу в пределах
    /// <see cref="HorizonHalfMoves"/> полуходов и <see cref="MaxNodesPerGroup"/> позиций.
    /// Рассматриваются группы не больше чем с <see cref="CandidateLiberties"/> дамэ — у остальных
    /// захват за горизонт не доказать.
    /// </para>
    /// <para>
    /// Границы честные: перебор локальный (дамэ группы и снятие прижатых групп), без ко и без
    /// дальних угроз. Мёртвая группа, которая умирает через ко или через угрозу вдали, предложена
    /// не будет — её помечает игрок. Наоборот не бывает: предложенная группа действительно
    /// снимается при любом ответе защиты.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Point> ProposeDead(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        // Перебор идёт по доске без истории: примерочные ходы не должны пополнять историю суперко
        // настоящей партии — иначе запрет за повторение позиции запретил бы игроку законный ход.
        var analysis = board.WithoutHistory();
        List<Point> dead = [];

        foreach (var color in PlayerColors)
        {
            foreach (var group in GroupTracker.AllGroups(analysis, color))
            {
                if (group.Liberties.Count > CandidateLiberties)
                {
                    continue;
                }

                if (DeadGroupSearch.IsForcedCapture(analysis, group, HorizonHalfMoves, MaxNodesPerGroup))
                {
                    dead.AddRange(group.Stones);
                }
            }
        }

        // Порядок — как у обхода доски: по нему интерфейс рисует пометки, а тесты сравнивают списки.
        dead.Sort(static (left, right) =>
            left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X));

        return dead.AsReadOnly();
    }

    /// <summary>Возвращает доску без мёртвых камней: по ней считаются территория и владение.</summary>
    /// <param name="board">Позиция на момент согласования.</param>
    /// <param name="dead">Помеченные мёртвыми камни: точка означает всю свою группу.</param>
    /// <returns>Доска того же размера без мёртвых камней и без истории партии.</returns>
    /// <exception cref="DomainException">Точка вне доски или в точке нет камня.</exception>
    /// <remarks>
    /// <para>
    /// Переданная точка разворачивается в целую группу: камень живёт и умирает вместе с ней,
    /// частично снятой группы не бывает. Поэтому клик по одному камню снимает всю группу, и
    /// интерфейсу не нужно повторять это правило.
    /// </para>
    /// <para>
    /// История партии к результату не привязывается: доска подсчёта — не продолжение партии,
    /// и ходить по ней никто не будет.
    /// </para>
    /// </remarks>
    public static Board ClearedBoard(Board board, IReadOnlyCollection<Point> dead)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(dead);

        var removed = ExpandToGroups(board, dead);
        var cleared = new Board(board.Size);

        foreach (var point in board.AllPoints())
        {
            var color = board.At(point);

            if (color != StoneColor.Empty && !removed.Contains(point))
            {
                cleared.SetStone(cleared.IndexOf(point), color);
            }
        }

        return cleared;
    }

    /// <summary>Считает итог партии по стандартному для доски коми.</summary>
    /// <param name="board">Позиция на момент подсчёта.</param>
    /// <param name="dead">Помеченные мёртвыми камни.</param>
    /// <returns>Итог без пленных, снятых по ходам: коми берётся по размеру доски.</returns>
    /// <remarks>Перегрузка для подсчёта позиции без партии: пленных за партию у неё нет.</remarks>
    public static FinalScore Finalize(Board board, IReadOnlyCollection<Point> dead)
    {
        ArgumentNullException.ThrowIfNull(board);

        return Finalize(board, dead, Komi.For(board.Size));
    }

    /// <summary>Считает итог партии с коми партии.</summary>
    /// <param name="board">Позиция на момент подсчёта.</param>
    /// <param name="dead">Помеченные мёртвыми камни.</param>
    /// <param name="komi">Коми партии.</param>
    /// <returns>Итог без пленных, снятых по ходам.</returns>
    public static FinalScore Finalize(Board board, IReadOnlyCollection<Point> dead, Komi komi) =>
        Finalize(board, dead, komi, 0, 0);

    /// <summary>Считает окончательный итог: мёртвые снимаются и становятся пленными.</summary>
    /// <param name="board">Позиция на момент подсчёта.</param>
    /// <param name="dead">Помеченные мёртвыми камни: точка означает всю свою группу.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="blackPrisoners">Камни, снятые чёрными по ходам партии.</param>
    /// <param name="whitePrisoners">Камни, снятые белыми по ходам партии.</param>
    /// <returns>Итог с обеими величинами: по площади и по территории с пленными.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Число пленных отрицательное.</exception>
    /// <exception cref="DomainException">Точка вне доски или в точке нет камня.</exception>
    /// <remarks>
    /// <para>
    /// Пленные считаются ровно один раз: снятые по ходам приходят параметрами, снятые сейчас —
    /// из списка мёртвых. Метод — чистая функция: повторный вызов с тем же списком даёт тот же
    /// итог, накопления «второй раз» не бывает.
    /// </para>
    /// <para>
    /// Камни, снятые по ходам, не вычитаются из камней на доске: доска и так их не содержит.
    /// Их учитывает только японская величина — этим две системы и отличаются.
    /// </para>
    /// <para>
    /// Победитель называется по японской системе — основной (<see cref="ScoringRule.Japanese"/>).
    /// Другую систему задаёт перегрузка с <see cref="ScoringRule"/>.
    /// </para>
    /// </remarks>
    public static FinalScore Finalize(
        Board board,
        IReadOnlyCollection<Point> dead,
        Komi komi,
        int blackPrisoners,
        int whitePrisoners) =>
        Finalize(board, dead, komi, blackPrisoners, whitePrisoners, ScoringRule.Japanese);

    /// <summary>Считает окончательный итог по выбранной системе подсчёта.</summary>
    /// <param name="board">Позиция на момент подсчёта.</param>
    /// <param name="dead">Помеченные мёртвыми камни: точка означает всю свою группу.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="blackPrisoners">Камни, снятые чёрными по ходам партии.</param>
    /// <param name="whitePrisoners">Камни, снятые белыми по ходам партии.</param>
    /// <param name="rule">Система подсчёта: по ней называются победитель и строка итога.</param>
    /// <returns>Итог с обеими величинами: по площади и по территории с пленными.</returns>
    /// <exception cref="ArgumentNullException">Система подсчёта не задана.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Число пленных отрицательное.</exception>
    /// <exception cref="DomainException">Точка вне доски или в точке нет камня.</exception>
    /// <remarks>
    /// Обе величины считаются всегда: система выбирает лишь ту, по которой называется победитель
    /// (<see cref="FinalScore.Winner"/>). Так переключатель систем не пересчитывает счёт заново
    /// и не может разойтись с показанным разбором.
    /// </remarks>
    public static FinalScore Finalize(
        Board board,
        IReadOnlyCollection<Point> dead,
        Komi komi,
        int blackPrisoners,
        int whitePrisoners,
        ScoringRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(dead);
        ArgumentOutOfRangeException.ThrowIfNegative(blackPrisoners);
        ArgumentOutOfRangeException.ThrowIfNegative(whitePrisoners);

        var removed = ExpandToGroups(board, dead);
        var removedBlack = removed.Count(point => board.At(point) == StoneColor.Black);
        var removedWhite = removed.Count - removedBlack;

        var area = Scorer.Breakdown(ClearedBoard(board, removed));

        return new FinalScore(
            area.BlackStones,
            area.BlackTerritory,
            area.WhiteStones,
            area.WhiteTerritory,
            area.Neutral,
            blackPrisoners + removedWhite,
            whitePrisoners + removedBlack,
            komi)
        {
            Rule = rule
        };
    }

    /// <summary>Разворачивает точки в целые группы и проверяет, что помечены камни.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="points">Помеченные точки.</param>
    /// <returns>Камни групп, к которым принадлежат помеченные точки.</returns>
    /// <exception cref="DomainException">Точка вне доски или в точке нет камня.</exception>
    private static HashSet<Point> ExpandToGroups(Board board, IReadOnlyCollection<Point> points)
    {
        HashSet<Point> expanded = [];

        foreach (var point in points)
        {
            if (!point.IsOnBoard(board.Size))
            {
                throw new DomainException($"Точка {point} находится вне доски {board.Size}.");
            }

            if (board.At(point) == StoneColor.Empty)
            {
                throw new DomainException($"В точке {point} нет камня: мёртвым помечают только занятую точку.");
            }

            expanded.UnionWith(GroupTracker.FindGroup(board, point).Stones);
        }

        return expanded;
    }

    /// <summary>Цвета игроков: пустой цвет групп не образует и в переборе не участвует.</summary>
    private static readonly StoneColor[] PlayerColors = [StoneColor.Black, StoneColor.White];
}
