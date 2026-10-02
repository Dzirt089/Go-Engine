using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Согласование подсчёта: помеченные мёртвые камни и подтверждённый итог партии.</summary>
/// <remarks>
/// <para>
/// Партия, завершённая двумя пасами, сама не заканчивается: стороны согласуют мёртвые группы
/// (<c>GO_RULES.md</c>, пп. 7–9). Здесь живёт ровно это согласование — пометки, предложение
/// перебора и подтверждённый итог. Панель партии показывает его состояние строками и разметкой,
/// а пометок не хранит: счёт, территория и пленные читают одно место, и разойтись им нечем.
/// </para>
/// <para>
/// Пометки относятся к конкретной позиции, а не к партии вообще: любое изменение позиции
/// (ход, отмена, возврат, загрузка, новая партия) начинает согласование заново — это делает
/// <see cref="Sync"/>. Мёртвые камни не снимаются с доски молча: их помечает перебор
/// <see cref="Endgame.ProposeDead"/> или игрок кликом.
/// </para>
/// <para>
/// Итог по согласованным мёртвым считает <c>Core</c>, но пленных и коми знает партия, поэтому
/// расчёт приходит сюда делегатом. Служба не тащит в себя <see cref="GameState"/>: ей довольно
/// доски, состояния партии и умения посчитать итог по списку мёртвых.
/// </para>
/// </remarks>
public sealed class ScoringAgreement
{
    /// <summary>Считает итог партии по согласованным мёртвым камням.</summary>
    private readonly Func<IReadOnlyCollection<Point>, FinalScore> _finalize;

    /// <summary>Помеченные мёртвыми камни: набор, потому что спрашивают «помечена ли эта точка».</summary>
    private readonly HashSet<Point> _dead = [];

    /// <summary>Помеченные камни в порядке обхода доски: список для вида, а не для поиска.</summary>
    private IReadOnlyList<Point> _deadPoints = [];

    /// <summary>Сколько раз менялись пометки мёртвых: по этому счётчику вид перерисовывает доску.</summary>
    private int _deadRevision;

    /// <summary>Доска, для которой посчитано предложение мёртвых: ключ кэша «одно предложение на позицию».</summary>
    /// <remarks>
    /// Кэш нужен из-за цены вопроса: <see cref="Endgame.ProposeDead"/> перебирает до 20 000 позиций
    /// на группу. Счёт, территория и пленные читаются на каждое изменение свойства и на каждую
    /// перерисовку, поэтому перебор в них недопустим: предложение считается один раз, там же, где
    /// меняется позиция, и дальше только переиспользуется.
    /// </remarks>
    private Board? _proposalBoard;

    /// <summary>Состояние партии, для которого посчитано предложение мёртвых.</summary>
    /// <remarks>
    /// Пара «доска + состояние» — ключ кэша: предложение зависит и от того, завершена ли партия
    /// двумя пасами. Отмена хода возвращает ту же самую доску (экземпляр сохраняется), но партия
    /// снова идёт — и по одному лишь экземпляру доски кэш вернул бы предложение для завершённой
    /// партии, то есть пометки мёртвых остались бы на играющей доске (регрессия поймана при
    /// рефакторинге: прежний код сверял статус на каждом вызове, а не только доску).
    /// </remarks>
    private GameStatus? _proposalStatus;

    /// <summary>Предложение мёртвых для доски <see cref="_proposalBoard"/>.</summary>
    private IReadOnlyList<Point> _proposal = [];

    /// <summary>Сколько раз перебор предлагал мёртвые группы за партию.</summary>
    private int _deadProposalCount;

    /// <summary>Подтверждённый итог партии или <c>null</c>, пока подсчёт не подтверждён.</summary>
    private FinalScore? _finalScore;

    /// <summary>Позиция согласования: доска, к которой относятся пометки.</summary>
    private Board _board;

    /// <summary>Состояние партии на момент согласования.</summary>
    private GameStatus _status;

    /// <summary>Создаёт согласование для позиции партии.</summary>
    /// <param name="board">Текущая позиция.</param>
    /// <param name="status">Состояние партии: согласование идёт только после двух пасов.</param>
    /// <param name="finalize">Как посчитать итог по согласованным мёртвым камням.</param>
    /// <exception cref="ArgumentNullException">Позиция, состояние или расчёт не заданы.</exception>
    /// <remarks>
    /// Позиция передаётся сразу и тут же синхронизируется: согласования без позиции не бывает,
    /// а предложение перебора нужно уже на первом чтении счёта. Поэтому отдельного «пустого»
    /// состояния у службы нет — есть позиция, для которой она отвечает.
    /// </remarks>
    public ScoringAgreement(Board board, GameStatus status, Func<IReadOnlyCollection<Point>, FinalScore> finalize)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(finalize);

        _finalize = finalize;
        _board = board;
        _status = status;

        // Синхронизация — единственный путь, которым считаются пометки и предложение: второй
        // такой же расчёт в конструкторе разошёлся бы с ним при первой же правке.
        Sync(board, status);
    }

    /// <summary>Пометки мёртвых изменились.</summary>
    /// <remarks>
    /// Событие поднимает только правка пометок кликом. Смену позиции (<see cref="Sync"/>) вызывающий
    /// сообщает виду сам: он и так меняет всю панель партии, и второе сообщение об одном и том же
    /// заставило бы вид перерисоваться дважды.
    /// </remarks>
    public event EventHandler? Changed;

    /// <summary>Помеченные мёртвыми камни: точка означает всю свою группу.</summary>
    /// <remarks>Набор, а не список: по нему спрашивают «помечена ли точка», а не «что за чем идёт».</remarks>
    public IReadOnlyCollection<Point> DeadStones => _dead;

    /// <summary>Помеченные мёртвыми камни в порядке обхода доски.</summary>
    public IReadOnlyList<Point> DeadPoints => _deadPoints;

    /// <summary>Сколько раз менялись пометки мёртвых.</summary>
    /// <remarks>
    /// Вид перерисовывает доску по этому счётчику: список <see cref="DeadPoints"/> может остаться
    /// тем же объектом, и подписка на него одного изменения не заметит.
    /// </remarks>
    public int DeadRevision => _deadRevision;

    /// <summary>Сколько раз перебор предлагал мёртвые группы за партию.</summary>
    /// <remarks>
    /// Открыт ради проверок: предложение мёртвых — самый дорогой перебор в подсчёте
    /// (<see cref="Endgame.ProposeDead"/>, до 20 000 позиций на группу), и он обязан считаться
    /// один раз на позицию. Тест читает счёт, территорию и пленных десятки раз и требует, чтобы
    /// счётчик не вырос, а проверочный режим печатает его рядом с разбором.
    /// </remarks>
    public int DeadProposalCount => _deadProposalCount;

    /// <summary>Идёт согласование мёртвых: партия завершена двумя пасами, подсчёт не подтверждён.</summary>
    /// <remarks>
    /// В это время ходы не принимаются, а клик по доске помечает группу мёртвой. Пока игрок
    /// не подтвердил подсчёт, доска остаётся как была, а счёт — предварительным. Окончание
    /// по лимиту ходов и сдача согласования не требуют.
    /// </remarks>
    public bool IsCounting => _status == GameStatus.FinishedByTwoPasses && _finalScore is null;

    /// <summary>Подсчёт можно подтвердить: идёт согласование мёртвых.</summary>
    /// <remarks>
    /// Доступно сразу, как только партия завершилась двумя пасами: ничего дополнительно включать
    /// не нужно. Согласование обязательно по правилам (D-064), но приглашение к нему — не ошибка
    /// и не «недосчитанный» экран: счёт, территория и пленные уже показаны и верны.
    /// </remarks>
    public bool CanConfirmScore => IsCounting;

    /// <summary>Итог по текущей позиции: подтверждённый или предварительный.</summary>
    /// <remarks>
    /// Считает <c>Core</c> одной функцией от доски, пометок, пленных партии и системы подсчёта.
    /// Отдельного «предварительного» подсчёта нет: иначе он разошёлся бы с итогом.
    /// </remarks>
    public FinalScore CurrentScore => _finalScore ?? _finalize(_dead);

    /// <summary>Доска подсчёта: без помеченных мёртвых камней.</summary>
    /// <remarks>
    /// Территория и владение считаются по ней, а не по игровой доске: мёртвый камень внутри
    /// чужого владения не должен отменять территорию соперника. Кэша нет намеренно — пометки
    /// меняются кликами, и сбрасывать кэш пришлось бы в каждом месте, где меняется партия.
    /// </remarks>
    public Board ScoringBoard => _dead.Count == 0 ? _board : Endgame.ClearedBoard(_board, _dead);

    /// <summary>Приводит согласование в соответствие с позицией партии.</summary>
    /// <param name="board">Текущая позиция.</param>
    /// <param name="status">Состояние партии.</param>
    /// <exception cref="ArgumentNullException">Позиция или состояние не заданы.</exception>
    /// <remarks>
    /// Пометки живут ровно столько, сколько живёт позиция: любой ход, отмена, возврат, загрузка
    /// и новая партия их обнуляют — мёртвая группа одной позиции ничего не значит в другой.
    /// Как только партия завершается двумя пасами, перебор предлагает то, что доказано
    /// (<see cref="Endgame.ProposeDead"/>): игрок видит готовое предложение и правит его кликами.
    /// Подтверждённый итог при этом сбрасывается — он относился к прежней позиции.
    /// </remarks>
    public void Sync(Board board, GameStatus status)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(status);

        _board = board;
        _status = status;
        _finalScore = null;

        var proposed = ProposedDead(board, status);

        if (_dead.SetEquals(proposed))
        {
            return;
        }

        _dead.Clear();
        _dead.UnionWith(proposed);
        _deadRevision++;
        RefreshDeadPoints();
    }

    /// <summary>Помечает или снимает пометку группы, которой принадлежит точка.</summary>
    /// <param name="point">Точка на доске: клик игрока по камню.</param>
    /// <returns><c>true</c>, если пометки изменились.</returns>
    /// <remarks>
    /// Помечается вся группа: камень живёт и умирает вместе с ней, частично мёртвой группы не
    /// бывает. Повторный клик снимает пометку — до подтверждения игрок может передумать.
    /// Вне согласования, по пустой точке и по точке вне доски метод ничего не делает и возвращает
    /// <c>false</c>: виду не нужно самому проверять состояние партии.
    /// </remarks>
    public bool ToggleDeadAt(Point point)
    {
        if (!IsCounting || !point.IsOnBoard(_board.Size) || _board.At(point) == StoneColor.Empty)
        {
            return false;
        }

        var stones = GroupTracker.FindGroup(_board, point).Stones;

        if (_dead.Contains(point))
        {
            foreach (var stone in stones)
            {
                _ = _dead.Remove(stone);
            }
        }
        else
        {
            foreach (var stone in stones)
            {
                _ = _dead.Add(stone);
            }
        }

        _deadRevision++;
        RefreshDeadPoints();
        Notify();

        return true;
    }

    /// <summary>Подтверждает подсчёт: мёртвые снимаются, становятся пленными и входят в итог.</summary>
    /// <returns><c>true</c>, если подсчёт подтверждён этим вызовом.</returns>
    /// <remarks>
    /// До подтверждения счёт предварительный, после — окончательный. Повторный вызов ничего
    /// не делает: подтверждать нечего. Вернуться к согласованию можно отменой хода — она снова
    /// открывает партию и сбрасывает пометки.
    /// </remarks>
    public bool ConfirmScore()
    {
        if (!IsCounting)
        {
            return false;
        }

        _finalScore = _finalize(_dead);

        return true;
    }

    /// <summary>Переносит подтверждённый итог на другую систему подсчёта.</summary>
    /// <param name="rule">Система, по которой называется победитель.</param>
    /// <exception cref="ArgumentNullException">Система подсчёта не задана.</exception>
    /// <remarks>
    /// Величины в подтверждённом итоге уже посчитаны: смена системы выбирает, по какой из двух
    /// называть победителя и строку счёта. Пока подсчёт не подтверждён, менять нечего — итог
    /// считается по текущей системе, и поправка относилась бы к прошлому выбору.
    /// </remarks>
    public void ApplyRule(ScoringRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (_finalScore is { } final)
        {
            _finalScore = final with { Rule = rule };
        }
    }

    /// <summary>Предложение мёртвых для позиции: считается один раз и переиспользуется.</summary>
    /// <param name="board">Позиция, для которой предлагаются мёртвые группы.</param>
    /// <param name="status">Состояние партии.</param>
    /// <returns>Камни предложенных групп; пустой список, пока партия не завершена двумя пасами.</returns>
    /// <remarks>
    /// <para>
    /// Перебор предлагает только доказанные группы и ничего не утверждает о сэки и глазах:
    /// ложное «мёртвая» испортило бы счёт молча, поэтому предложение осторожное.
    /// </para>
    /// <para>
    /// Кэш держится на самой доске: доска неизменяема, и её экземпляр меняется ровно тогда, когда
    /// меняется позиция (ход, отмена, возврат, загрузка, новая партия). Поэтому геттеры счёта,
    /// территории и пленных читают готовое предложение, а не запускают перебор заново: считать
    /// его на каждое чтение свойства значило бы перебирать десятки тысяч позиций на каждую
    /// перерисовку панели.
    /// </para>
    /// </remarks>
    private IReadOnlyList<Point> ProposedDead(Board board, GameStatus status)
    {
        if (ReferenceEquals(_proposalBoard, board) && _proposalStatus == status)
        {
            return _proposal;
        }

        _proposalBoard = board;
        _proposalStatus = status;
        _proposal = status == GameStatus.FinishedByTwoPasses ? Endgame.ProposeDead(board) : [];

        if (status == GameStatus.FinishedByTwoPasses)
        {
            _deadProposalCount++;
        }

        return _proposal;
    }

    /// <summary>Обновляет список помеченных камней для вида: порядок — как у обхода доски.</summary>
    private void RefreshDeadPoints() =>
        _deadPoints = [.. _dead.OrderBy(static point => point.Y).ThenBy(static point => point.X)];

    /// <summary>Сообщает об изменении пометок мёртвых.</summary>
    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
