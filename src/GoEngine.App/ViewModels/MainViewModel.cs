using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.Core;
using Microsoft.Extensions.Logging;

namespace GoEngine.App.ViewModels;

/// <summary>Состояние партии для окна: доска, панель статуса и ход AI.</summary>
/// <remarks>
/// Модель представления ничего не рисует и не знает про Avalonia: она показывает данные
/// <see cref="GameState"/> строками и играет за AI. Все правила (легальность хода, завершение
/// партии, подсчёт) выполняет <c>Core</c>, выбор хода — <c>AI</c>.
/// Ход AI считается в фоне. Синхронные <see cref="PlayMove"/> и <see cref="Pass"/> считают его
/// на месте и остаются для тестов, загрузки партии и смены настроек; интерфейс пользуется
/// <see cref="PlayMoveAsync"/> и <see cref="PassAsync"/>. Причина: синхронный поиск блокировал поток
/// интерфейса на время раздумий (на уровне с сетью — больше секунды), поэтому ход игрока появлялся
/// на доске одновременно с ответом соперника, а анимация успевала закончиться до первой отрисовки.
/// Отмена не прерывает уже начатый поиск, но его результат не применяется (см. <see cref="CancelThinking"/>).
/// </remarks>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Random _random;
    private readonly IPositionEvaluator? _evaluator;
    private readonly IReadOnlySet<int> _modelSizes;

    /// <summary>Логгер партии: по нему после сбоя видно, что происходило перед ним.</summary>
    private readonly ILogger _log = AppLog.For<MainViewModel>();
    private GameState _game;
    private AppSettings _settings;
    private bool _showTerritory;

    /// <summary>Часы партии: считают время, пока партия идёт, и замирают на паузе.</summary>
    private readonly GameClock _clock;

    /// <summary>Партия начата: время идёт или стоит на паузе.</summary>
    /// <remarks>
    /// Отдельно от состояния самой партии: свежезапущенное приложение и «Отменить партию» дают
    /// партию, которая ещё не начата, — время в ней не считается. Смена настроек, загрузка SGF
    /// и первый ход игрока, наоборот, означают игру: часы идут с этого мгновения.
    /// </remarks>
    private bool _gameStarted;

    /// <summary>Согласование подсчёта: пометки мёртвых групп и подтверждённый итог партии.</summary>
    /// <remarks>
    /// Пометки относятся к конкретной позиции, а не к партии вообще: любое изменение партии
    /// (ход, отмена, возврат, загрузка) начинает согласование заново — это делает
    /// <see cref="ScoringAgreement.Sync"/>. Мёртвые камни не снимаются с доски молча: их помечает
    /// перебор <see cref="Endgame.ProposeDead"/> или игрок кликом. Пометки, счётчик ревизии,
    /// счётчик предложений и подтверждённый итог живут в службе: панель партии только читает их.
    /// </remarks>
    private readonly ScoringAgreement _scoring;

    /// <summary>Отмена текущего поиска хода соперника: результат отменённого поиска не применяется.</summary>
    private CancellationTokenSource? _aiCancellation;

    /// <summary>Поиск сериализуется: <see cref="Random"/> не потокобезопасен, и двух «раздумий» быть не должно.</summary>
    private readonly object _aiLock = new();

    /// <summary>Поиск хода соперника идёт прямо сейчас.</summary>
    private bool _isThinking;

    /// <summary>Подписи уровней. Список хранится полем намеренно: подмена <c>ItemsSource</c>
    /// на новом экземпляре заставляет список выбора сбросить выбранный индекс и вернуть его
    /// в модель представления — из-за этого партия начиналась не с выбранным уровнем.</summary>
    private IReadOnlyList<string> _levelLabels = [];

    /// <summary>Создаёт модель представления по настройкам.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <param name="random">Источник случайности для AI; в тестах — с фиксированным seed.</param>
    /// <param name="evaluator">Оценка позиции нейросетью для уровней Дан; <c>null</c> — игра без сети.</param>
    /// <param name="modelSizes">Стороны доски, для которых есть модель; <c>null</c> — взять у приложения.</param>
    /// <param name="time">Источник времени для часов партии; <c>null</c> — часы приложения.</param>
    public MainViewModel(
        AppSettings settings,
        Random random,
        IPositionEvaluator? evaluator = null,
        IReadOnlySet<int>? modelSizes = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);

        _settings = settings;
        _random = random;
        _evaluator = evaluator;
        _modelSizes = modelSizes ?? global::GoEngine.App.App.ModelSizes;
        _game = CreateGame(settings);

        // Согласование подсчёта заводится вместе с партией: позиция нужна ему сразу, иначе счёт,
        // территория и пленные не ответили бы до первого хода. Итог по мёртвым считает партия —
        // пленных и коми знает она, а не служба согласования.
        _scoring = new ScoringAgreement(_game.Board, _game.Status, dead => _game.FinalScore(dead, ScoringRule));
        _scoring.Changed += OnDeadChanged;

        // Часы создаются до первого хода соперника: партия ещё не начата, и время не идёт.
        _clock = new GameClock(time);
        _clock.Changed += OnClockChanged;

        RefreshLabels();
        LogNewGame();
        ShowAiMoveIfNeeded();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Текущая позиция.</summary>
    public Board Board => _game.Board;

    /// <summary>Точка последнего хода или <c>null</c>, если ходов ещё не было.</summary>
    public Point? LastMove
    {
        get
        {
            var last = _game.Moves.LastOrDefault(move => move.Type == MoveType.Play);

            return last.IsNone ? null : last.Point;
        }
    }

    /// <summary>Кто ходит: «Чёрные» или «Белые».</summary>
    public string ToMove => StoneColorLabels.Label(_game.ToMove);

    /// <summary>Номер последнего хода.</summary>
    public string MoveNumber => _game.MoveNumber.ToString(CultureInfo.InvariantCulture);

    /// <summary>Коми партии.</summary>
    public string Komi => _game.Komi.ToString();

    /// <summary>Перевес победителя без знака: «7.5» или «0» при ровном счёте.</summary>
    /// <remarks>
    /// Берётся та же величина, что называет победителя (<see cref="FinalScore.TerritoryWinner"/>
    /// или <see cref="FinalScore.AreaWinner"/>): иначе баннер спорил бы со строкой счёта.
    /// </remarks>
    public double Margin
    {
        get
        {
            var score = CurrentScore;

            return ScoringRule == Core.ScoringRule.Chinese ? score.AreaMargin : score.TerritoryMargin;
        }
    }

    /// <summary>Система подсчёта партии: японская (основная) или китайская.</summary>
    /// <remarks>
    /// Смена системы партию не пересоздаёт: она выбирает, по какой из двух посчитанных величин
    /// называется победитель. Обе величины Core считает всегда, поэтому переключение мгновенно
    /// и не может разойтись с разбором в <see cref="ScoreDetail"/>. Экран настроек пишет сюда же:
    /// так выбранная система попадает и в файл настроек, и в текущую партию.
    /// </remarks>
    public Core.ScoringRule ScoringRule
    {
        get => _settings.ToScoringRule();
        set
        {
            var rule = value ?? Core.ScoringRule.Japanese;

            if (rule == _settings.ToScoringRule())
            {
                return;
            }

            _settings = _settings with { ScoringRule = rule.Name };

            // Подтверждённый итог тоже меняет систему: величины в нём уже посчитаны, а победитель
            // и строка зависят от выбора. Без этой строки переключение после подтверждения
            // не изменило бы ничего — счёт остался бы назван по прежней системе.
            _scoring.ApplyRule(rule);

            NotifyAll();
        }
    }

    /// <summary>Справка о подсчёте: системы, из чего складываются очки и какое коми типично в РФ.</summary>
    /// <remarks>
    /// Показывается рядом с выбором системы на экране настроек. Коми правилами не задаётся —
    /// его определяет регламент встречи, поэтому в справке названо типичное турнирное значение,
    /// а не выдуманное правило.
    /// </remarks>
    public string ScoringHint =>
        "Японская система (основная): очки = территория + пленные + камни, снятые как мёртвые, плюс коми белым. "
        + "Китайская система (показывается рядом): очки = свои камни на доске + территория, пленные не считаются. "
        + "Территория в обеих системах считается по доске без мёртвых камней. "
        + "Коми задаёт регламент встречи: в турнирах РФ на 19×19 типично 6,5.";

    /// <summary>Счёт партии строкой по основной системе: предварительный или окончательный.</summary>
    /// <remarks>
    /// Пока идёт согласование мёртвых, счёт предварительный: он считается по доске без помеченных
    /// камней, поэтому пометка сразу видна в числе — игрок понимает, что именно подтверждает.
    /// После <see cref="ConfirmScore"/> строка берётся из подтверждённого итога и не меняется.
    /// </remarks>
    public string Score => CurrentScore.ToString();

    /// <summary>Счёт одной строкой: сначала та система, по которой называется победитель.</summary>
    /// <remarks>
    /// Обе системы показаны рядом, но каждая — одной строкой из трёх чисел: очки сторон и коми.
    /// Подробный разбор («территория + пленные + камни на доске») убран: на панели он занимал
    /// четыре строки и повторял одно и то же дважды, а игроку нужен счёт, а не арифметика
    /// (жалоба 2026-09-30). Полный разбор площади остался в <see cref="ScoreBreakdown"/> —
    /// он показывается по переключателю «Территория».
    /// </remarks>
    public string ScoreDetail
    {
        get
        {
            var current = CurrentScore;

            // Первой идёт основная система: победителя называет именно она.
            var primary = SystemLine(current, ScoringRule);
            var secondary = SystemLine(current, OpponentRule(ScoringRule));

            return $"{primary} · {secondary}";
        }
    }

    /// <summary>Состояние партии словами.</summary>
    public string Status => _game.Status switch
    {
        var status when status == GameStatus.FinishedByTwoPasses => "Завершена двумя пасами",
        var status when status == GameStatus.FinishedByResign => "Завершена сдачей",
        var status when status == GameStatus.FinishedByMoveLimit => "Завершена по лимиту ходов",
        _ => "Идёт"
    };

    /// <summary>Камни, снятые последним ходом.</summary>
    public IReadOnlyList<Point> LastCaptured => _game.Board.CapturedStones;

    /// <summary>Цвет снятых камней: это цвет соперника сделавшего ход.</summary>
    public StoneColor LastCapturedColor => LastCaptured.Count == 0 ? StoneColor.Empty : _game.ToMove;

    /// <summary>Ранг уровня словами: «20 кю» или «5 дан».</summary>
    /// <remarks>
    /// Только ранг: по этой строке проверяют себя режимы приложения (<c>--state</c>, <c>--e2e</c>).
    /// Движок и бюджет называет <see cref="LevelDescription"/>.
    /// </remarks>
    public string Level => LevelChooser.Rank(CurrentLevel);

    /// <summary>Уровень и движок одной строкой: «5 дан · нейросеть 9×9» или «20 кю · перебор».</summary>
    /// <remarks>
    /// Подпись собирает <see cref="LevelChooser.Describe"/>: движок называется фактический —
    /// тот, который сядет играть на этой доске, — а ранги номинальные (D-043, D-048).
    /// Ни аббревиатур, ни служебных чисел в подписи нет: игроку они ничего не объясняют.
    /// Своей копии слов про движок у модели представления нет: раньше здесь жило второе
    /// свойство <c>Engine</c>, и оно единственное во всём интерфейсе продолжало говорить
    /// «MCTS без сети».
    /// </remarks>
    public string LevelDescription =>
        LevelChooser.Describe(CurrentLevel, _settings.ToBoardSize(), HasModelFor(_settings.ToBoardSize()));

    /// <summary>Уровень, которым играет партия прямо сейчас.</summary>
    public DifficultyLevel CurrentLevel => _settings.ToDifficultyLevel();

    /// <summary>Цвет игрока словами.</summary>
    public string PlayerColor => StoneColorLabels.Label(_settings.ToPlayerColor());

    /// <summary>Сколько чёрных камней снято за партию.</summary>
    /// <remarks>
    /// Счёт ведёт партия (<see cref="GameState.WhitePrisoners"/>): снятые чёрные камни — добыча
    /// белых. Своего счётчика у модели представления нет: два счётчика разошлись бы после отмены.
    /// </remarks>
    public int CapturedBlack => _game.WhitePrisoners;

    /// <summary>Сколько белых камней снято за партию.</summary>
    public int CapturedWhite => _game.BlackPrisoners;

    /// <summary>Снятые камни одной строкой: «чёрные 3 : 1 белые».</summary>
    /// <remarks>Порядок тот же, что и в разборе счёта: сначала чёрные, потом белые.</remarks>
    public string Captures => $"чёрные {CapturedBlack} : {CapturedWhite} белые";

    /// <summary>Пленные одной строкой: сколько камней взяла каждая сторона и из чего это сложилось.</summary>
    /// <remarks>
    /// <para>
    /// Пленные называются по тому, кто их взял: «чёрные 5» — пять снятых чёрными камней. Это ровно
    /// та величина, которая входит в японскую систему слагаемым к территории
    /// (<see cref="FinalScore.BlackTerritoryPoints"/>), поэтому по строке игрок проверяет счёт,
    /// а не догадывается, что имелось в виду.
    /// </para>
    /// <para>
    /// Слагаемых два: камни, снятые ходами партии (<see cref="GameState.BlackPrisoners"/>), и камни,
    /// признанные мёртвыми в заключительной позиции (правила вида спорта «го», пп. 1.10 и 1.12).
    /// Второе слагаемое счётчик партии не знает — его снимает подсчёт, а не ход, — поэтому строка
    /// называет разложение в скобках. Без него игрок видел «пленные 0» рядом со счётом, в котором
    /// эти камни уже учтены (жалоба 2026-09-30). Пока пометок нет, слагаемое одно и скобки не нужны.
    /// </para>
    /// </remarks>
    public string PrisonersLine
    {
        get
        {
            var blackPlayed = _game.BlackPrisoners;
            var whitePlayed = _game.WhitePrisoners;
            var dead = _scoring.DeadStones;

            if (dead.Count == 0)
            {
                return $"Пленные: чёрные {blackPlayed} : {whitePlayed} белые";
            }

            // Пометка означает всю группу, поэтому точек с камнями каждого цвета достаточно:
            // список мёртвых всегда развёрнут по группам (ToggleDeadAt и Endgame.ProposeDead).
            var deadBlack = dead.Count(point => _game.Board.At(point) == StoneColor.Black);
            var deadWhite = dead.Count - deadBlack;

            return $"Пленные: чёрные {blackPlayed + deadWhite} : {whitePlayed + deadBlack} белые "
                + $"(в партии {blackPlayed} : {whitePlayed} · мертвыми {deadWhite} : {deadBlack})";
        }
    }

    /// <summary>Итог завершённой партии или пустая строка, пока партия идёт.</summary>
    public string Outcome
    {
        get
        {
            if (_game.Status == GameStatus.InProgress)
            {
                return string.Empty;
            }

            // Сдача и лимит ходов не проходят согласование, и победителя в них называет партия,
            // а не счёт доски: сдавшийся проигрывает независимо от того, что стоит на доске.
            // Раньше победитель брался только из счёта, и игрок, сдавшийся при перевесе,
            // объявлялся победителем (найдено разведкой 2026-10-02).
            if (_game.Status == GameStatus.FinishedByResign || _game.Status == GameStatus.FinishedByMoveLimit)
            {
                var finished = _game.Finish();

                if (finished.IsSuccess && finished.Value.Winner is { } taken)
                {
                    return $"Победили {(taken == StoneColor.Black ? "чёрные" : "белые")}";
                }
            }

            // Два паса: победителя называет та же величина, что стоит в строке счёта, иначе панель
            // спорила бы сама с собой. После подтверждения это зафиксированный итог, до него —
            // текущий счёт по доске без помеченных мёртвых камней.
            var winner = CurrentScore.Winner;

            return winner == StoneColor.Empty ? "Ничья" : $"Победили {(winner == StoneColor.Black ? "чёрные" : "белые")}";
        }
    }

    /// <summary>Показывать ли итог партии: он есть только у завершённой.</summary>
    public bool HasOutcome => Outcome.Length > 0;

    /// <summary>Крупная строка итога для баннера: кто победил и с каким перевесом.</summary>
    /// <remarks>
    /// Итог партии виден и в строке состояния, но её легко не заметить: на настольной версии
    /// строка стоит среди прочих, а на телефоне прячется в шторке. Баннер называет исход
    /// отдельно и заметно (жалоба 2026-09-30).
    /// </remarks>
    public string ResultHeadline => Views.GameStatusLines.ResultHeadline(
        HasOutcome,
        Outcome,
        Margin.ToString(CultureInfo.InvariantCulture),
        ResultTone);

    /// <summary>Строка под заголовком баннера: счёт и оговорка о предварительном подсчёте.</summary>
    public string ResultDetail => Views.GameStatusLines.ResultDetail(IsCounting, Score);

    /// <summary>Итог глазами игрока: выиграл он, проиграл или ничья.</summary>
    /// <remarks>
    /// <para>
    /// Баннер красится по этому признаку: «победили чёрные» ничего не говорит игроку, который
    /// играет белыми. Сравнение идёт с цветом самого игрока, а не с цветом в настройках партии.
    /// </para>
    /// <para>
    /// Пока итог не назван, тон — <see cref="GameTone.None"/>. Раньше здесь всегда стоял цвет,
    /// и доска подсвечивалась рамкой выигрыша или проигрыша с первого хода: на пустой доске
    /// победитель по коми — белые, поэтому играющий чёрными сразу видел рамку проигрыша
    /// (жалоба пользователя 2026-10-02 «подсчёт на доске ведётся некорректно»). Это же правило
    /// действует и во время согласования мёртвых: победитель ещё не подтверждён, и подсвечивать
    /// его нельзя — иначе вид спорил бы с баннером, который в это время молчит.
    /// </para>
    /// </remarks>
    public GameTone ResultTone
    {
        get
        {
            if (_game.Status == GameStatus.InProgress || _scoring.IsCounting)
            {
                return GameTone.None;
            }

            var winner = CurrentScore.Winner;

            if (winner == StoneColor.Empty)
            {
                return GameTone.Draw;
            }

            return winner == _settings.ToPlayerColor() ? GameTone.Win : GameTone.Loss;
        }
    }

    /// <summary>Показывать ли разметку территории, пока партия идёт.</summary>
    /// <remarks>
    /// Завершённая партия показывает территорию независимо от переключателя: по ней видно,
    /// как посчитан счёт (<c>GO_RULES.md</c>, п. 9). Переключатель нужен, чтобы посмотреть
    /// разделение доски, не доводя партию до конца.
    /// </remarks>
    public bool ShowTerritory
    {
        get => _showTerritory;
        set
        {
            if (_showTerritory == value)
            {
                return;
            }

            _showTerritory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasTerritory));
            OnPropertyChanged(nameof(Territory));
            OnPropertyChanged(nameof(ScoreBreakdown));
        }
    }

    /// <summary>Показывается ли разметка территории сейчас.</summary>
    public bool HasTerritory => _showTerritory || _game.Status != GameStatus.InProgress;

    /// <summary>Владение точками для разметки или <c>null</c>, если разметка выключена.</summary>
    /// <remarks>
    /// Считает <see cref="Scorer.Ownership"/> по доске без помеченных мёртвых камней
    /// (<see cref="ScoringBoard"/>): камень — своему цвету, пустая область — цвету окружения,
    /// спорная — нейтральна. Мёртвый камень внутри чужого владения не отменяет территорию
    /// соперника и не приносит очков своему цвету. Второго подсчёта территории в проекте нет.
    /// </remarks>
    public IReadOnlyList<StoneColor>? Territory => HasTerritory ? Scorer.Ownership(ScoringBoard) : null;

    /// <summary>Расшифровка разметки: сколько у сторон камней и территории и сколько нейтральных точек.</summary>
    /// <remarks>
    /// Считает <see cref="Scorer.Breakdown"/>: разбор площади живёт в <c>Core</c> и используется
    /// и подсчётом очков, и панелью партии — второго подсчёта в проекте нет. Показывается только
    /// при включённой разметке территории: это пояснение к картинке, а не постоянная строка.
    /// </remarks>
    public string ScoreBreakdown
    {
        get
        {
            if (!HasTerritory)
            {
                return string.Empty;
            }

            var area = Scorer.Breakdown(ScoringBoard);

            // Компактно и в две строки: панель партии — 320 точек, прежняя формулировка
            // («чёрные 8 (камни 4 + территория 4) · белые 2 (камни 2 + территория 0) · нейтрально 75»)
            // занимала четыре строки и вытесняла действия партии с экрана.
            // Нейтральные точки — это те, что не засчитаны никому: без них сумма не сходится
            // с площадью доски, и игрок принимает нейтраль за потерянную территорию.
            return $"Территория — чёрные {area.BlackTerritory} · белые {area.WhiteTerritory} · нейтрально {area.Neutral}"
                + Environment.NewLine
                + $"Камни — чёрные {area.BlackStones} · белые {area.WhiteStones}";
        }
    }

    /// <summary>Подписи сторон доски для списка выбора.</summary>
    /// <remarks>Список размеров и подписи собирает <see cref="BoardSizes"/> — один источник
    /// для панели партии и экрана настроек (D-051).</remarks>
    public IReadOnlyList<string> SizeLabels => BoardSizes.Labels;

    /// <summary>Выбранная сторона доски: смена начинает новую партию со стандартным коми.</summary>
    /// <remarks>
    /// Смена размера пересоздаёт партию: продолжать её на другой доске нельзя. Коми берётся
    /// стандартное для нового размера, иначе счёт был бы не по правилам (<c>GO_RULES.md</c>, п. 8).
    /// </remarks>
    public int SelectedSizeIndex
    {
        get => BoardSizes.IndexOf(_settings.ToBoardSize());
        set
        {
            if (value < 0 || value >= BoardSizes.All.Count || value == SelectedSizeIndex)
            {
                return;
            }

            var size = BoardSizes.At(value);

            StartWith(size, LevelChooser.Resolve(_settings.ToDifficultyLevel(), size, HasModelFor(size)));
        }
    }

    /// <summary>Уровни, доступные для выбранной доски.</summary>
    /// <remarks>Список собирает <see cref="LevelListView"/>, состав считает <see cref="LevelChooser"/>:
    /// он знает про наличие модели (D-038, D-039).</remarks>
    public IReadOnlyList<DifficultyLevel> LevelOptions =>
        LevelListView.ForGame(_settings.ToBoardSize(), HasModelFor(_settings.ToBoardSize())).Levels;

    /// <summary>Подписи доступных уровней.</summary>
    /// <remarks>
    /// Экземпляр списка меняется только вместе с содержимым: список выбора в интерфейсе подписан
    /// на это свойство, и лишняя подмена сбрасывала бы выбранный уровень.
    /// </remarks>
    public IReadOnlyList<string> LevelLabels => _levelLabels;

    /// <summary>Пересобирает подписи уровней, если их состав изменился.</summary>
    /// <remarks>
    /// Подпись собирает <see cref="LevelChooser.Describe"/> с размером текущей доски: список
    /// выбора обязан называть движок, который действительно сядет играть, а не тот, который
    /// уровень предпочёл бы (D-045).
    /// </remarks>
    private void RefreshLabels()
    {
        var size = _settings.ToBoardSize();
        var labels = LevelListView.ForGame(size, HasModelFor(size)).Labels;

        if (_levelLabels.SequenceEqual(labels, StringComparer.Ordinal))
        {
            return;
        }

        _levelLabels = labels;
        OnPropertyChanged(nameof(LevelLabels));
    }

    /// <summary>Выбранный уровень: смена начинает новую партию.</summary>
    public int SelectedLevelIndex
    {
        get
        {
            var options = LevelOptions;
            var current = _settings.ToDifficultyLevel();

            for (var index = 0; index < options.Count; index++)
            {
                if (options[index] == current)
                {
                    return index;
                }
            }

            return 0;
        }
        set
        {
            var options = LevelOptions;

            if (value < 0 || value >= options.Count)
            {
                return;
            }

            // Сравниваем сами уровни, а не индексы: список мог быть пересобран, и тот же индекс
            // теперь указывает на другой уровень. Совпал с текущим — партию не пересоздаём.
            var level = options[value];

            if (level == CurrentLevel)
            {
                return;
            }

            StartWith(_settings.ToBoardSize(), level);
        }
    }

    /// <summary>Подсказка, почему уровней Дан нет, или <c>null</c>, если они доступны.</summary>
    public string? LevelHint
    {
        get
        {
            var size = _settings.ToBoardSize();

            if (HasModelFor(size))
            {
                return null;
            }

            return _evaluator is null
                ? "Модель нейросети не найдена: играют уровни кю"
                : $"Для доски {BoardSizes.Label(size)} нет модели: играют уровни кю";
        }
    }

    /// <summary>Показывать ли подсказку об уровнях.</summary>
    public bool HasLevelHint => LevelHint is not null;

    /// <summary>Есть ли модель для этой доски.</summary>
    /// <param name="size">Сторона доски.</param>
    /// <returns><c>true</c>, если нейросеть загружена и для размера есть файл модели.</returns>
    /// <remarks>
    /// Открыт видам: экран настроек получает этот ответ вместо собственного чтения статических
    /// <c>App.Evaluator</c>/<c>App.ModelSizes</c>, чтобы наличие модели считалось в одном месте.
    /// </remarks>
    public bool HasModelFor(BoardSize size) => _evaluator is not null && _modelSizes.Contains(size.Value);

    /// <summary>Соперник думает: поиск идёт в фоне, интерфейс в это время живой.</summary>
    /// <remarks>
    /// Пока идёт поиск, ходы игрока не принимаются: иначе игрок сходил бы за соперника.
    /// Результат отменённого поиска не применяется — очередь остаётся за игроком.
    /// </remarks>
    public bool IsThinking => _isThinking;

    /// <summary>Начинает партию с выбранными доской и уровнем.</summary>
    /// <param name="size">Сторона доски.</param>
    /// <param name="level">Уровень AI.</param>
    private void StartWith(BoardSize size, DifficultyLevel level) =>
        ApplySettings(AppSettings.From(
            size,
            level,
            _settings.ToPlayerColor(),
            Core.Komi.For(size),
            _settings.ToScoringRule(),
            _settings.SoundEnabled));

    /// <summary>Играет ход игрока, затем ход AI, если очередь за ним.</summary>
    /// <param name="point">Точка хода.</param>
    /// <returns><c>true</c>, если ход принят.</returns>
    /// <remarks>На паузе ход отклоняется, а доска не меняется: пауза останавливает и время, и игру.</remarks>
    public bool PlayMove(Point point)
    {
        if (!CanPlayMove || _isThinking || !TryPlay(Move.Play(point, _game.ToMove)))
        {
            return false;
        }

        MarkStartedIfNeeded();
        ShowAiMoveIfNeeded();

        return true;
    }

    /// <summary>Играет ход игрока и считает ответ соперника в фоне.</summary>
    /// <param name="point">Точка хода.</param>
    /// <returns><c>true</c>, если ход игрока принят.</returns>
    /// <remarks>
    /// Ход игрока применяется сразу и сразу же сообщается виду, поэтому доска успевает
    /// перерисоваться и показать анимацию, пока соперник думает. Ответ соперника приходит фоном;
    /// если за время поиска партия изменилась (отмена, новая партия, смена режима), устаревший ход
    /// не играется.
    /// </remarks>
    public async Task<bool> PlayMoveAsync(Point point)
    {
        if (!CanPlayMove || _isThinking || !TryPlay(Move.Play(point, _game.ToMove)))
        {
            return false;
        }

        MarkStartedIfNeeded();
        NotifyAll();
        await PlayAiTurnAsync().ConfigureAwait(true);

        return true;
    }

    /// <summary>Передаёт ход: пасует игрок, затем отвечает AI, если очередь за ним.</summary>
    /// <returns><c>true</c>, если пас принят.</returns>
    /// <remarks>
    /// Два паса подряд завершают партию (<c>GO_RULES.md</c>, п. 7): если после паса игрока
    /// пасует и AI, партия заканчивается, а статус и счёт обновляются в <see cref="NotifyAll"/>.
    /// </remarks>
    public bool Pass()
    {
        if (!CanPlayMove || _isThinking || !TryPlay(Move.Pass(_game.ToMove)))
        {
            return false;
        }

        MarkStartedIfNeeded();
        ShowAiMoveIfNeeded();

        return true;
    }

    /// <summary>Передаёт ход и считает ответ соперника в фоне.</summary>
    /// <returns><c>true</c>, если пас игрока принят.</returns>
    public async Task<bool> PassAsync()
    {
        if (!CanPlayMove || _isThinking || !TryPlay(Move.Pass(_game.ToMove)))
        {
            return false;
        }

        MarkStartedIfNeeded();
        NotifyAll();
        await PlayAiTurnAsync().ConfigureAwait(true);

        return true;
    }

    /// <summary>Можно ли отменить ход игрока: отмена обязана вернуть очередь игроку.</summary>
    /// <remarks>Кнопка «Отменить» недоступна, когда отменять ход игрока нечего.</remarks>
    public bool CanUndo => _game.CanUndoTo(_settings.ToPlayerColor());

    /// <summary>Можно ли вернуть отменённый ход игрока.</summary>
    public bool CanRedo => _game.CanRedoTo(_settings.ToPlayerColor());

    /// <summary>Идёт согласование мёртвых групп: партия завершена двумя пасами, подсчёт не подтверждён.</summary>
    /// <remarks>
    /// В это время ходы не принимаются, а клик по доске помечает группу мёртвой. Мёртвые камни
    /// не снимаются молча: пока игрок не подтвердил подсчёт, доска остаётся как была, а счёт —
    /// предварительным. Окончание по лимиту ходов и сдача согласования не требуют. Состояние
    /// ведёт <see cref="ScoringAgreement"/>: панели и тестам оно нужно здесь, под своим именем.
    /// </remarks>
    public bool IsCounting => _scoring.IsCounting;

    /// <summary>Подсчёт можно подтвердить: идёт согласование мёртвых групп.</summary>
    /// <remarks>
    /// Доступно сразу, как только партия завершилась двумя пасами: ничего дополнительно включать
    /// не нужно. Согласование обязательно по правилам (D-064), но приглашение к нему — не ошибка
    /// и не «недосчитанный» экран: счёт, территория и пленные уже показаны и верны.
    /// </remarks>
    public bool CanConfirmScore => _scoring.CanConfirmScore;

    /// <summary>Партия начата: время идёт или стоит на паузе.</summary>
    /// <remarks>
    /// Свежезапущенное приложение и «Отменить партию» оставляют партию неначатой: часы стоят
    /// на нуле, кнопка называется «Начать партию». Смена настроек, загрузка партии и первый ход
    /// игрока партию начинают — это и есть игра, и время в ней считается.
    /// </remarks>
    public bool IsGameStarted => _gameStarted;

    /// <summary>Партия идёт: начата и не на паузе.</summary>
    public bool IsGameRunning => _gameStarted && _clock.IsRunning;

    /// <summary>Партия на паузе: время стоит, ходы не принимаются.</summary>
    /// <remarks>
    /// Завершённая партия паузой не считается: её часы просто останавливаются
    /// (<see cref="GameClock.Stop"/>), поэтому «Пауза» в панели не показывается,
    /// а «Продолжить» не предлагается.
    /// </remarks>
    public bool IsGamePaused => _clock.IsPaused;

    /// <summary>Время партии строкой: «ММ:СС», а после часа — «Ч:ММ:СС».</summary>
    public string ClockDisplay => _clock.Display;

    /// <summary>Время партии коротко: всегда «ММ:СС» — для узкой строки на телефоне.</summary>
    public string ClockShortDisplay => _clock.ShortDisplay;

    /// <summary>Сколько идёт партия: точное время, а не округлённая строка.</summary>
    /// <remarks>
    /// Нужно проверочному режиму <c>--clock</c> и тестам: строка времени округляет до секунд,
    /// а «идут ли часы» видно только по приросту времени.
    /// </remarks>
    public TimeSpan GameTime => _clock.Elapsed;

    /// <summary>Часы можно остановить: партия идёт прямо сейчас.</summary>
    public bool CanPauseClock => IsGameRunning;

    /// <summary>Часы можно продолжить: партия стоит на паузе.</summary>
    public bool CanResumeClock => _gameStarted && _clock.IsPaused;

    /// <summary>Кнопка часов доступна: есть что останавливать или продолжать.</summary>
    public bool CanTogglePause => CanPauseClock || CanResumeClock;

    /// <summary>Партию можно отменить: она начата.</summary>
    /// <remarks>
    /// До старта отменять нечего — партия и так в исходном состоянии. После «Отменить партию»
    /// кнопка снова недоступна, пока игрок не начнёт партию.
    /// </remarks>
    public bool CanAbortGame => _gameStarted;

    /// <summary>Ход разрешён: партия не на паузе.</summary>
    /// <remarks>Пауза — не украшение: на ней ходы отклоняются по-настоящему, а не только на вид.</remarks>
    public bool CanPlayMove => !_clock.IsPaused;

    /// <summary>Партия ещё не начата: на доске нет ни одного камня.</summary>
    /// <remarks>
    /// По этому признаку кнопка называется «Начать партию» до первого хода и «Новая партия» после
    /// него (жалоба 2026-09-30): игрок должен видеть, что партия ждёт его первого хода, а не что
    /// она уже идёт. Пас и сдача нового камня не ставят, но партию завершают — такие партии
    /// начатыми не считаются только до первого настоящего хода, поэтому проверяется и ход, и статус.
    /// </remarks>
    public bool IsFirstMove =>
        _game.Status == GameStatus.InProgress && !_game.Moves.Any(move => move.Type == MoveType.Play);

    /// <summary>Разметку территории можно включать и выключать переключателем.</summary>
    /// <remarks>
    /// Завершённая партия показывает территорию всегда — переключать нечего, поэтому он
    /// в этом состоянии скрыт: меньше кнопок, которые ничего не меняют.
    /// </remarks>
    public bool CanToggleTerritory => _game.Status == GameStatus.InProgress;

    /// <summary>Помеченные мёртвыми камни в порядке обхода доски.</summary>
    /// <remarks>Пометки ведёт <see cref="ScoringAgreement"/>; здесь они под именем, которое знает вид.</remarks>
    public IReadOnlyList<Point> DeadPoints => _scoring.DeadPoints;

    /// <summary>Сколько раз менялись пометки мёртвых.</summary>
    /// <remarks>
    /// Вид перерисовывает доску по этому счётчику: список <see cref="DeadPoints"/> может остаться
    /// тем же объектом, и подписка на него одного изменения не заметит.
    /// </remarks>
    public int DeadRevision => _scoring.DeadRevision;

    /// <summary>Сколько раз перебор предлагал мёртвые группы за партию.</summary>
    /// <remarks>
    /// Открыт ради проверок: предложение мёртвых — самый дорогой перебор в подсчёте
    /// (<see cref="Endgame.ProposeDead"/>, до 20 000 позиций на группу), и он обязан считаться
    /// один раз на позицию. Тест читает счёт, территорию, разбор и пленных десятки раз и требует,
    /// чтобы счётчик не вырос, а проверочный режим печатает его рядом с разбором.
    /// </remarks>
    public int DeadProposalCount => _scoring.DeadProposalCount;

    /// <summary>Помечает или снимает пометку группы, которой принадлежит точка.</summary>
    /// <param name="point">Точка на доске: клик игрока по камню.</param>
    /// <returns><c>true</c>, если пометки изменились.</returns>
    /// <remarks>
    /// Правила пометок — в <see cref="ScoringAgreement"/>: здесь метод стоит под именем, которое
    /// знает вид, и меняет то же состояние, что читают счёт, территория и пленные.
    /// </remarks>
    public bool ToggleDeadAt(Point point) => _scoring.ToggleDeadAt(point);

    /// <summary>Подтверждает подсчёт: мёртвые снимаются, становятся пленными и входят в итог.</summary>
    /// <remarks>
    /// Итог считает партия, а хранит его согласование: вызов вне согласования и повторный вызов
    /// ничего не делают, и панель тогда не будится зря. Вернуться к согласованию можно отменой хода.
    /// </remarks>
    public void ConfirmScore()
    {
        if (_scoring.ConfirmScore())
        {
            NotifyAll();
        }
    }

    /// <summary>Начинает партию: новая позиция и часы с нуля.</summary>
    /// <remarks>
    /// Кнопка «Начать партию» до первого хода и «Новая партия» после него делает одно и то же:
    /// партия начинается заново, а время считается с этого мгновения. Первый ход соперника
    /// (когда игрок играет белыми) считается на месте; интерфейс пользуется
    /// <see cref="StartGameAsync"/>, чтобы окно не замирало на время поиска.
    /// </remarks>
    public void StartGame() => ApplySettings(_settings);

    /// <summary>Начинает партию и считает первый ход соперника в фоне.</summary>
    /// <returns>Задача, завершающаяся после ответа соперника.</returns>
    public Task StartGameAsync() => ApplySettingsAsync(_settings);

    /// <summary>Ставит партию на паузу: время стоит, ходы не принимаются, соперник не думает.</summary>
    /// <remarks>
    /// Пауза настоящая, а не косметическая: ходы и пас отклоняются (<see cref="CanPlayMove"/>),
    /// а поиск хода соперника отменяется — его результат не должен прийти в остановленную партию.
    /// </remarks>
    public void PauseGame()
    {
        if (!IsGameRunning)
        {
            return;
        }

        CancelThinking();
        _clock.Pause();
        NotifyAll();
    }

    /// <summary>Снимает паузу: время идёт дальше, ходы снова принимаются.</summary>
    /// <remarks>
    /// Если пауза застала соперника за раздумьями, его ход отменён, и партия ждёт его ответа.
    /// Синхронный путь его не доигрывает: это делает фоновая версия метода, которой пользуется
    /// интерфейс, — иначе окно замирало бы на время поиска.
    /// </remarks>
    public void ResumeGame()
    {
        if (!CanResumeClock)
        {
            return;
        }

        _clock.Resume();
        NotifyAll();
    }

    /// <summary>Снимает паузу и считает ответ соперника в фоне.</summary>
    /// <returns>Задача, завершающаяся после ответа соперника.</returns>
    public async Task ResumeGameAsync()
    {
        if (!CanResumeClock)
        {
            return;
        }

        _clock.Resume();
        NotifyAll();
        await PlayAiTurnAsync().ConfigureAwait(true);
    }

    /// <summary>Отменяет партию: всё возвращается в состояние «партия не начата».</summary>
    /// <remarks>
    /// Партия начинается заново, но начатой не считается: часы обнулены и стоят, ходы очищены,
    /// доска пуста, а если игрок играет белыми — соперник снова делает первый ход, как при новой
    /// партии. Ходы при этом доступны: первый ход снова начнёт партию — ровно так же, как в только
    /// что запущенном приложении. Доигрывание хода соперника — в <see cref="AbortGameAsync"/>.
    /// </remarks>
    public void AbortGame()
    {
        PrepareAborted();
        ShowAiMoveIfNeeded();
        NotifyAll();
    }

    /// <summary>Отменяет партию и считает первый ход соперника в фоне.</summary>
    /// <returns>Задача, завершающаяся после ответа соперника.</returns>
    public async Task AbortGameAsync()
    {
        PrepareAborted();
        NotifyAll();
        await PlayAiTurnAsync().ConfigureAwait(true);
    }

    /// <summary>Пульс часов: вид зовёт его раз в секунду, чтобы строка времени обновилась.</summary>
    /// <remarks>
    /// Время считается по запросу, поэтому пересчитывать нечего: тик лишь сообщает панели,
    /// что строку пора показать заново. У стоящих часов тика нет — панель не будится зря.
    /// </remarks>
    public void TickClock() => _clock.Tick();

    /// <summary>Отменяет партию без хода соперника — общая часть синхронного и фонового путей.</summary>
    private void PrepareAborted()
    {
        CancelThinking();
        _gameStarted = false;
        _clock.Reset();
        BeginGame(_settings);
    }

    /// <summary>Отменяет ход игрока вместе с ответом AI.</summary>
    /// <returns><c>true</c>, если ход отменён.</returns>
    /// <remarks>
    /// Отмена идёт до тех пор, пока очередь не вернётся игроку. Если вернуть её не удалось
    /// (например, в истории только ход соперника), отмена не делается вовсе: иначе партия
    /// осталась бы ждать хода соперника, которого никто не делает, — ровно это ломало игру
    /// после нажатия «Отменить» (регрессия захода 1).
    /// </remarks>
    public bool Undo()
    {
        CancelThinking();

        if (!CanUndo || !TryUndo())
        {
            return false;
        }

        _scoring.Sync(_game.Board, _game.Status);
        ShowAiMoveIfNeeded();
        SyncClockWithGame();
        NotifyAll();

        return true;
    }

    /// <summary>Возвращает отменённый ход вместе с ответом AI.</summary>
    /// <returns><c>true</c>, если ход возвращён.</returns>
    /// <remarks>Если возврат не доводит очередь до игрока, состояние не меняется.</remarks>
    public bool Redo()
    {
        CancelThinking();

        if (!CanRedo || !TryRedo())
        {
            return false;
        }

        _scoring.Sync(_game.Board, _game.Status);
        SyncClockWithGame();
        NotifyAll();

        return true;
    }

    /// <summary>Отменяет ходы, пока очередь не вернётся игроку.</summary>
    /// <returns><c>true</c>, если очередь у игрока; <c>false</c> — состояние возвращено как было.</returns>
    private bool TryUndo()
    {
        var playerColor = _settings.ToPlayerColor();
        var steps = 0;

        do
        {
            if (!_game.Undo().IsSuccess)
            {
                break;
            }

            steps++;
        }
        while (_game.CanUndo && _game.ToMove != playerColor);

        if (_game.ToMove == playerColor)
        {
            return true;
        }

        // Ход игрока вернуть не удалось: откатываем собственные отмены, партия не меняется.
        for (var index = 0; index < steps; index++)
        {
            _ = _game.Redo();
        }

        return false;
    }

    /// <summary>Возвращает ходы, пока очередь не вернётся игроку.</summary>
    /// <returns><c>true</c>, если очередь у игрока; <c>false</c> — состояние возвращено как было.</returns>
    private bool TryRedo()
    {
        var playerColor = _settings.ToPlayerColor();
        var steps = 0;

        do
        {
            if (!_game.Redo().IsSuccess)
            {
                break;
            }

            steps++;
        }
        while (_game.CanRedo && _game.ToMove != playerColor);

        if (_game.ToMove == playerColor)
        {
            return true;
        }

        for (var index = 0; index < steps; index++)
        {
            _ = _game.Undo();
        }

        return false;
    }

    /// <summary>Возвращает партию для сохранения в SGF.</summary>
    /// <returns>Данные партии: размер доски, коми и ходы.</returns>
    public SgfGame ToSgfGame() => new(_game.Board.Size, _game.Komi, _game.Moves, null);

    /// <summary>Загружает партию из SGF и продолжает её по текущим настройкам.</summary>
    /// <param name="game">Прочитанная партия.</param>
    /// <returns>Успех или причина отказа, если ходы не проходят по правилам.</returns>
    public Result LoadGame(SgfGame game)
    {
        var prepared = PrepareLoaded(game);

        if (!prepared.IsSuccess)
        {
            return prepared;
        }

        // После загрузки партия может стоять на ходе AI — он обязан ответить.
        ShowAiMoveIfNeeded();

        return Result.Ok();
    }

    /// <summary>Загружает партию и считает ответ соперника в фоне.</summary>
    /// <param name="game">Прочитанная партия.</param>
    /// <returns>Успех или причина отказа, если ходы не проходят по правилам.</returns>
    public async Task<Result> LoadGameAsync(SgfGame game)
    {
        var prepared = PrepareLoaded(game);

        if (!prepared.IsSuccess)
        {
            return prepared;
        }

        NotifyAll();
        await PlayAiTurnAsync().ConfigureAwait(true);

        return Result.Ok();
    }

    /// <summary>Готовит загруженную партию — общая часть синхронного и фонового путей.</summary>
    /// <param name="game">Прочитанная партия.</param>
    /// <returns>Успех или причина отказа, если ходы не проходят по правилам.</returns>
    private Result PrepareLoaded(SgfGame game)
    {
        var restored = game.ToGameState();

        if (!restored.IsSuccess)
        {
            return Result.Fail(restored.Error!);
        }

        CancelThinking();
        _game = restored.Value!;
        _settings = AppSettings.From(
            game.Size,
            _settings.ToDifficultyLevel(),
            _settings.ToPlayerColor(),
            game.Komi,
            _settings.ToScoringRule(),
            _settings.SoundEnabled);

        _scoring.Sync(_game.Board, _game.Status);

        // Загруженная партия — это игра: время в ней считается с этого мгновения. Если партия
        // в файле уже завершена, часы тут же остановятся на нуле (см. StopClockIfFinished).
        MarkStarted();

        return Result.Ok();
    }

    /// <summary>Начинает новую партию по текущим настройкам.</summary>
    /// <remarks>
    /// То же, что <see cref="StartGame"/>: имя осталось ради окна и баннера итога,
    /// где кнопка называется «Новая партия».
    /// </remarks>
    public void StartNewGame() => StartGame();

    /// <summary>Начинает новую партию и считает первый ход соперника в фоне.</summary>
    /// <returns>Задача, завершающаяся после ответа соперника.</returns>
    public Task StartNewGameAsync() => StartGameAsync();

    /// <summary>Применяет настройки и начинает новую партию.</summary>
    /// <param name="settings">Новые настройки партии.</param>
    public void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        LogSettings(settings);
        BeginGame(settings);

        // Смена настроек, доски и уровня означают игру: часы идут с этого мгновения, а не ждут
        // нажатия «Начать партию». Иначе игрок, поменявший доску, играл бы без счёта времени,
        // и «Пауза» была бы недоступна в идущей партии.
        MarkStarted();

        ShowAiMoveIfNeeded();
        NotifyAll();
    }

    /// <summary>Применяет настройки и считает первый ход соперника в фоне.</summary>
    /// <param name="settings">Новые настройки партии.</param>
    /// <returns>Задача, завершающаяся после ответа соперника.</returns>
    /// <remarks>Путь интерфейса: синхронный <see cref="ApplySettings"/> оставлен тестам и API.</remarks>
    public async Task ApplySettingsAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        LogSettings(settings);
        BeginGame(settings);
        MarkStarted();
        NotifyAll();
        await PlayAiTurnAsync().ConfigureAwait(true);
    }

    /// <summary>Начинает партию по настройкам — общая часть синхронного и фонового путей.</summary>
    /// <param name="settings">Настройки партии.</param>
    private void BeginGame(AppSettings settings)
    {
        CancelThinking();
        _settings = settings;
        _game = CreateGame(settings);
        _scoring.Sync(_game.Board, _game.Status);
        LogNewGame();
    }

    /// <summary>Отмечает партию начатой и запускает часы с нуля.</summary>
    /// <remarks>
    /// Единственное место, где партия становится начатой по воле приложения: кнопка «Начать
    /// партию», смена настроек, доски или уровня, загрузка партии. Первый ход игрока — тоже
    /// начало партии, но об этом просит <see cref="MarkStartedIfNeeded"/>.
    /// </remarks>
    private void MarkStarted()
    {
        _gameStarted = true;
        _clock.Start();
        StopClockIfFinished();
    }

    /// <summary>Начинает партию первым ходом игрока, если её ещё не начинали.</summary>
    /// <remarks>
    /// Игрок вправе начать играть, не нажимая «Начать партию»: щелчок по доске — тоже начало игры,
    /// и время партии обязано считаться с него. Кнопка нужна, чтобы начать партию явно и обнулить
    /// время, а не как единственный вход в игру: запрет ходов до нажатия сломал бы и проверочные
    /// режимы (<c>--state</c>, <c>--e2e</c>), которые ходят по доске сразу после создания модели.
    /// </remarks>
    private void MarkStartedIfNeeded()
    {
        if (!_gameStarted)
        {
            MarkStarted();
        }
    }

    /// <summary>Останавливает часы, если партия завершилась: время законченной партии не растёт.</summary>
    /// <remarks>
    /// Это не пауза: игрок партию не останавливал, и «Продолжить» предлагать нечего
    /// (<see cref="GameClock.Stop"/>).
    /// </remarks>
    private void StopClockIfFinished()
    {
        if (_game.Status != GameStatus.InProgress)
        {
            _clock.Stop();
        }
    }

    /// <summary>Приводит часы в согласие с партией после отмены и возврата хода.</summary>
    /// <remarks>
    /// Партия завершилась — часы останавливаются. Отмена хода возвращает завершённую партию
    /// в игру, и время продолжает считаться с накопленного: игрок не теряет уже сыгранное время,
    /// а паузой это не считается — партия просто снова идёт.
    /// </remarks>
    private void SyncClockWithGame()
    {
        if (_game.Status != GameStatus.InProgress)
        {
            _clock.Stop();
            return;
        }

        if (_gameStarted && !_clock.IsRunning && !_clock.IsPaused)
        {
            _clock.Resume();
        }
    }

    /// <summary>Часы изменились: панель показывает новое время партии.</summary>
    private void OnClockChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ClockDisplay));
        OnPropertyChanged(nameof(ClockShortDisplay));
    }

    /// <summary>Пишет в лог смену настроек партии.</summary>
    /// <param name="settings">Применённые настройки.</param>
    private void LogSettings(AppSettings settings) => AppLogMessages.SettingsApplied(
        _log,
        settings.ToBoardSize().ToString(),
        LevelChooser.Describe(settings.ToDifficultyLevel(), settings.ToBoardSize(), HasModelFor(settings.ToBoardSize())),
        StoneColorLabels.Label(settings.ToPlayerColor()),
        settings.ToKomi().ToString());

    /// <summary>Пишет в лог начало партии: доска, уровень, цвет игрока и коми.</summary>
    private void LogNewGame() => AppLogMessages.NewGame(
        _log,
        _settings.ToBoardSize().ToString(),
        LevelChooser.Describe(EffectiveLevel(), _settings.ToBoardSize(), HasModelFor(_settings.ToBoardSize())),
        StoneColorLabels.Label(_settings.ToPlayerColor()),
        _settings.ToKomi().ToString());

    /// <summary>Пишет в лог ход: точку подписывает так же, как доска.</summary>
    /// <param name="move">Сделанный ход.</param>
    /// <param name="milliseconds">Время поиска хода соперника; 0 — ход игрока или пас.</param>
    private void LogMove(Move move, double milliseconds = 0)
    {
        var color = StoneColorLabels.Label(move.Color);

        if (move.Type != MoveType.Play)
        {
            AppLogMessages.MovePassed(_log, _game.MoveNumber, color);
            return;
        }

        var point = BoardCoordinates.Label(move.Point, _game.Board.Size);

        if (milliseconds > 0)
        {
            // В журнал идёт точная подпись: движок, который сядет играть на этой доске (D-045).
            // Короткая подпись «нейросеть или перебор» годится для списка без доски, но в журнале
            // разбора она бесполезна — по ней не видно, чем уровень играл на самом деле.
            var level = EffectiveLevel();

            AppLogMessages.AiMovePlayed(
                _log,
                color,
                point,
                LevelChooser.Describe(level, _game.Board.Size, HasModelFor(_game.Board.Size)),
                milliseconds);
            return;
        }

        AppLogMessages.MovePlayed(_log, _game.MoveNumber, color, point);
    }

    /// <summary>Пишет в лог завершение партии, если она закончилась.</summary>
    private void LogFinishedIfNeeded()
    {
        if (_game.Status == GameStatus.InProgress)
        {
            return;
        }

        AppLogMessages.GameFinished(_log, _game.Status.Name, Score, _game.MoveNumber);
    }

    /// <summary>Создаёт партию по настройкам.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <returns>Новая партия.</returns>
    private static GameState CreateGame(AppSettings settings) =>
        GameState.NewGame(settings.ToBoardSize(), settings.ToKomi());

    /// <summary>Играет ход AI, пока очередь за ним.</summary>
    private void ShowAiMoveIfNeeded()
    {
        var playerColor = _settings.ToPlayerColor();

        while (_game.Status == GameStatus.InProgress && _game.ToMove != playerColor)
        {
            var from = AppLog.Time.GetTimestamp();
            var move = ComputeAiMove();
            var milliseconds = AppLog.Time.GetElapsedTime(from).TotalMilliseconds;

            if (TryPlay(move, milliseconds) || TryPlay(Move.Pass(_game.ToMove)))
            {
                continue;
            }

            // Партия не приняла ни ход селектора, ни пас: играть дальше нельзя (партия
            // завершена или состояние не даёт ходить). Пас как запасной вариант не даёт
            // партии остаться в состоянии «ходит соперник, но он не ходит».
            break;
        }

        NotifyAll();
    }

    /// <summary>Играет ходы соперника, пока очередь за ним; поиск идёт в фоне.</summary>
    /// <returns>Задача, завершающаяся, когда соперник ответил или поиск отменён.</returns>
    /// <remarks>
    /// Поиск вынесен в <see cref="Task.Run{TResult}(Func{TResult}, CancellationToken)"/>: селектор
    /// синхронный, а поток интерфейса занят отрисовкой. Отмена не прерывает уже начатый поиск —
    /// она запрещает применять его результат, поэтому перед применением проверяются и токен,
    /// и то, что партия не изменилась.
    /// </remarks>
    public async Task PlayAiTurnAsync()
    {
        var playerColor = _settings.ToPlayerColor();

        // На паузе соперник не думает: его ход не должен прийти в остановленную партию.
        if (_clock.IsPaused || _game.Status != GameStatus.InProgress || _game.ToMove == playerColor)
        {
            return;
        }

        CancelThinking();

        using var cancellation = new CancellationTokenSource();
        _aiCancellation = cancellation;
        SetThinking(true);

        try
        {
            // Пауза во время раздумий выходит из цикла: необязательно ждать, пока поиск заметит отмену.
            while (_game.Status == GameStatus.InProgress && _game.ToMove != playerColor && !_clock.IsPaused)
            {
                var moveNumber = _game.MoveNumber;
                var from = AppLog.Time.GetTimestamp();
                var move = await Task.Run(ComputeAiMove, cancellation.Token).ConfigureAwait(true);
                var milliseconds = AppLog.Time.GetElapsedTime(from).TotalMilliseconds;

                if (cancellation.IsCancellationRequested || _game.MoveNumber != moveNumber)
                {
                    // Партия изменилась, пока считали: устаревший ход не играем.
                    AppLogMessages.AiSearchCancelled(_log);

                    return;
                }

                if (!TryPlay(move, milliseconds) && !TryPlay(Move.Pass(_game.ToMove)))
                {
                    break;
                }

                NotifyAll();
            }
        }
        catch (OperationCanceledException)
        {
            // Отмена — ожидаемый исход: результат поиска больше не нужен.
        }
        finally
        {
            if (ReferenceEquals(_aiCancellation, cancellation))
            {
                _aiCancellation = null;
            }

            SetThinking(false);
        }
    }

    /// <summary>Запрещает применять результат идущего поиска.</summary>
    /// <remarks>
    /// Сам поиск не прерывается: селектор синхронный и токена не принимает. Поэтому отмена
    /// означает «результат не применять» — очередь остаётся за игроком.
    /// </remarks>
    public void CancelThinking() => _aiCancellation?.Cancel();

    /// <summary>Считает ход соперника; вызывается и из фона, поэтому поиски сериализуются.</summary>
    /// <returns>Ход селектора для текущей позиции.</returns>
    private Move ComputeAiMove()
    {
        // Random не потокобезопасен, а синхронный и фоновый пути не должны пересекаться.
        lock (_aiLock)
        {
            var selector = AiFactory.Create(
                EffectiveLevel(),
                _random,
                _settings.ToBoardSize(),
                _evaluator,
                global::GoEngine.App.App.ModelsDirectory);

            return selector.SelectMove(_game);
        }
    }

    /// <summary>Уровень партии с оглядкой на доступность сети.</summary>
    /// <returns>Уровень, которым играет соперник.</returns>
    /// <remarks>
    /// Уровни Дан играют только с сетью и только на больших досках (D-038). Если модель
    /// не загружена, уровень заменяется на 10 кю: партия не должна срываться из-за настроек,
    /// которые интерфейс и так не предлагает.
    /// </remarks>
    private DifficultyLevel EffectiveLevel()
    {
        var level = _settings.ToDifficultyLevel();

        return level.NeedsNetwork && (_evaluator is null || global::GoEngine.App.App.ModelsDirectory is null)
            ? DifficultyLevel.Kyu10
            : level;
    }

    /// <summary>Отмечает начало и конец поиска хода соперника.</summary>
    /// <param name="thinking">Идёт поиск.</param>
    private void SetThinking(bool thinking)
    {
        if (_isThinking == thinking)
        {
            return;
        }

        _isThinking = thinking;
        OnPropertyChanged(nameof(IsThinking));
    }

    /// <summary>Играет ход в партии и учитывает снятые камни.</summary>
    /// <param name="move">Ход.</param>
    /// <param name="aiMilliseconds">Время поиска хода соперника; 0 — ход игрока или пас.</param>
    /// <returns><c>true</c>, если правила его приняли.</returns>
    /// <remarks>Снятые камни всегда принадлежат сопернику ходившего: пас ничего не снимает.</remarks>
    private bool TryPlay(Move move, double aiMilliseconds = 0)
    {
        var played = _game.Play(move);

        if (!played.IsSuccess)
        {
            AppLogMessages.MoveRejected(
                _log,
                StoneColorLabels.Label(move.Color),
                move.Type == MoveType.Play ? BoardCoordinates.Label(move.Point, _game.Board.Size) : "пас",
                played.Error ?? "правила не приняли ход");

            return false;
        }

        // Пленных ведёт партия (GameState), а пометки мёртвых относятся к прежней позиции: после
        // любого принятого хода согласование начинается заново. Заодно партия сама сообщает,
        // не завершилась ли она двумя пасами, — по этому признаку и включается согласование.
        _scoring.Sync(_game.Board, _game.Status);

        LogMove(move, aiMilliseconds);
        LogFinishedIfNeeded();

        // Партия завершилась этим ходом — время партии больше не считается.
        StopClockIfFinished();

        return true;
    }

    /// <summary>Пометки мёртвых изменились: панель показывает новый счёт и разметку.</summary>
    /// <remarks>
    /// Счёт и разметка территории считаются по доске без мёртвых камней, поэтому клик по группе
    /// меняет не только пометки: без этих уведомлений панель показывала бы старый счёт.
    /// </remarks>
    private void OnDeadChanged(object? sender, EventArgs e)
    {
        foreach (var name in new[]
        {
            nameof(DeadPoints),
            nameof(DeadRevision),
            nameof(IsCounting),
            nameof(CanConfirmScore),
            nameof(Territory),
            nameof(HasTerritory),
            nameof(ScoreBreakdown),
            nameof(Score),
            nameof(ScoreDetail),
            nameof(Outcome),
            nameof(HasOutcome),
            nameof(ResultHeadline),
            nameof(ResultDetail),
            nameof(ResultTone),
            nameof(Margin),
            nameof(PrisonersLine)
        })
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>Счёт по одной системе: очки сторон, коми и название системы.</summary>
    /// <param name="score">Итог подсчёта.</param>
    /// <param name="rule">Система подсчёта.</param>
    /// <returns>Например, «Японская: чёрные 12 (территория 9 + пленные 3) : 20.5 белые».</returns>
    /// <remarks>
    /// <para>
    /// Японская величина — территория с пленными, китайская — площадь. У японской слагаемые
    /// названы прямо: без них соседняя строка «камни 5 + территория 3» не сходилась со счётом
    /// (11), и это выглядело ошибкой подсчёта (жалоба пользователя 2026-10-02). Пленные берутся
    /// из самого итога, а не из счётчиков партии: в итог входят и камни, признанные мёртвыми,
    /// которых счётчики не знают.
    /// </para>
    /// <para>
    /// У китайской слагаемых нет намеренно: там очко даёт каждый камень и каждая окружённая точка,
    /// а пленные не считаются вовсе — разложение «камни + территория» уже показано в строке
    /// разбора площади.
    /// </para>
    /// </remarks>
    private static string SystemLine(FinalScore score, Core.ScoringRule rule)
    {
        var japanese = rule == Core.ScoringRule.Japanese;
        var black = japanese ? score.BlackTerritoryPoints : score.BlackArea;
        var white = japanese ? score.WhiteTerritoryPoints : score.WhiteArea;
        var name = japanese ? "Японская" : "Китайская";

        if (!japanese)
        {
            return $"{name}: чёрные {black} : {white + score.Komi.Value} белые";
        }

        var blackPrisoners = score.BlackTerritoryPoints - score.BlackTerritory;
        var whitePrisoners = score.WhiteTerritoryPoints - score.WhiteTerritory;

        return $"{name}: чёрные {black} (территория {score.BlackTerritory} + пленные {blackPrisoners}) : "
            + $"{white + score.Komi.Value} белые (территория {score.WhiteTerritory} + пленные {whitePrisoners} + коми {score.Komi.Value})";
    }

    /// <summary>Вторая система подсчёта: та, по которой победитель не называется.</summary>
    /// <param name="rule">Основная система.</param>
    /// <returns>Противоположная система подсчёта.</returns>
    private static Core.ScoringRule OpponentRule(Core.ScoringRule rule) =>
        rule == Core.ScoringRule.Japanese ? Core.ScoringRule.Chinese : Core.ScoringRule.Japanese;

    /// <summary>Итог по текущему состоянию: подтверждённый или предварительный.</summary>
    /// <remarks>Считает ядро; подтверждённый итог хранит согласование, предварительный — партия.</remarks>
    private FinalScore CurrentScore => _scoring.CurrentScore;

    /// <summary>Доска подсчёта: без помеченных мёртвых камней.</summary>
    /// <remarks>Территория и владение считаются по ней, а не по игровой доске: иначе мёртвый камень
    /// внутри чужого владения отменял бы территорию соперника.</remarks>
    private Board ScoringBoard => _scoring.ScoringBoard;

    /// <summary>Сообщает об изменении всех свойств партии.</summary>
    /// <remarks>Свойства выводятся из одной партии, поэтому после хода меняются все сразу.</remarks>
    private void NotifyAll()
    {
        RefreshLabels();

        foreach (var name in new[]
        {
            nameof(Board),
            nameof(LastMove),
            nameof(LastCaptured),
            nameof(ToMove),
            nameof(MoveNumber),
            nameof(Komi),
            nameof(ScoringRule),
            nameof(Score),
            nameof(ScoreDetail),
            nameof(IsCounting),
            nameof(CanConfirmScore),
            nameof(DeadPoints),
            nameof(DeadRevision),
            nameof(Status),
            nameof(Level),
            nameof(LevelDescription),
            nameof(PlayerColor),
            nameof(CapturedBlack),
            nameof(CapturedWhite),
            nameof(Captures),
            nameof(PrisonersLine),
            nameof(Outcome),
            nameof(HasOutcome),
            nameof(ResultHeadline),
            nameof(ResultDetail),
            nameof(ResultTone),
            nameof(Margin),
            nameof(IsGameStarted),
            nameof(IsGameRunning),
            nameof(IsGamePaused),
            nameof(ClockDisplay),
            nameof(ClockShortDisplay),
            nameof(CanPauseClock),
            nameof(CanResumeClock),
            nameof(CanTogglePause),
            nameof(CanAbortGame),
            nameof(CanPlayMove),
            nameof(IsFirstMove),
            nameof(CanToggleTerritory),
            nameof(CanUndo),
            nameof(CanRedo),
            nameof(HasTerritory),
            nameof(Territory),
            nameof(ScoreBreakdown),
            nameof(CurrentLevel),
            nameof(SelectedLevelIndex),
            nameof(SelectedSizeIndex),
            nameof(LevelHint),
            nameof(HasLevelHint)
        })
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>Сообщает об изменении свойства.</summary>
    /// <param name="propertyName">Имя свойства.</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
