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

    /// <summary>Помеченные мёртвыми камни: согласование конца партии.</summary>
    /// <remarks>
    /// Пометки относятся к конкретной позиции, а не к партии вообще: любое изменение партии
    /// (ход, отмена, возврат, загрузка) начинает согласование заново. Мёртвые камни не снимаются
    /// с доски молча — их помечает перебор <see cref="Endgame.ProposeDead"/> или игрок кликом.
    /// </remarks>
    private readonly HashSet<Point> _dead = [];

    /// <summary>Помеченные камни в порядке обхода доски: список для вида, а не для поиска.</summary>
    private IReadOnlyList<Point> _deadPoints = [];

    /// <summary>Сколько раз менялись пометки мёртвых: по этому счётчику вид перерисовывает доску.</summary>
    private int _deadRevision;

    /// <summary>Подтверждённый итог партии или <c>null</c>, пока подсчёт не подтверждён.</summary>
    private FinalScore? _finalScore;

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
    public MainViewModel(
        AppSettings settings,
        Random random,
        IPositionEvaluator? evaluator = null,
        IReadOnlySet<int>? modelSizes = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);

        _settings = settings;
        _random = random;
        _evaluator = evaluator;
        _modelSizes = modelSizes ?? global::GoEngine.App.App.ModelSizes;
        _game = CreateGame(settings);

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
            if (_finalScore is { } final)
            {
                _finalScore = final with { Rule = rule };
            }

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

    /// <summary>Строка о пленных: снятые по ходам и снятые мёртвыми в заключительной позиции.</summary>
    /// <remarks>
    /// <para>
    /// Мёртвые камни заключительной позиции идут в пленные того, кто их снял (правила вида спорта
    /// «го», пп. 1.10 и 1.12), но счётчик партии их не знает: их снимает подсчёт, а не ход.
    /// Поэтому строка называет обе величины — без второй игрок видел «пленные 0» рядом со счётом,
    /// в котором эти камни уже учтены (жалоба 2026-09-30).
    /// </para>
    /// <para>
    /// Слагаемые не смешиваются в одно число намеренно: снятые по ходам подтверждены партией,
    /// а снятые мёртвыми — соглашением сторон, и их ещё можно отменить, сняв пометку.
    /// </para>
    /// </remarks>
    public string PrisonersLine
    {
        get
        {
            var played = $"Пленные: {Captures} (в партии)";

            return _dead.Count == 0
                ? played
                : $"{played} · снято мёртвыми: {_dead.Count} — они тоже в пленные и в очки";
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

            // Победителя называет та же величина, что стоит в строке счёта: иначе панель спорила бы
            // сама с собой. После подтверждения это зафиксированный итог, до него — текущий счёт
            // по доске без помеченных мёртвых камней.
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
    /// Баннер красится по этому признаку: «победили чёрные» ничего не говорит игроку, который
    /// играет белыми. Сравнение идёт с цветом самого игрока, а не с цветом в настройках партии.
    /// </remarks>
    public GameTone ResultTone
    {
        get
        {
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

            return $"На доске: чёрные {area.Black} (камни {area.BlackStones} + территория {area.BlackTerritory}) · "
                + $"белые {area.White} (камни {area.WhiteStones} + территория {area.WhiteTerritory}) · "
                + $"нейтрально {area.Neutral}";
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
    public bool PlayMove(Point point)
    {
        if (_isThinking || !TryPlay(Move.Play(point, _game.ToMove)))
        {
            return false;
        }

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
        if (_isThinking || !TryPlay(Move.Play(point, _game.ToMove)))
        {
            return false;
        }

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
        if (_isThinking || !TryPlay(Move.Pass(_game.ToMove)))
        {
            return false;
        }

        ShowAiMoveIfNeeded();

        return true;
    }

    /// <summary>Передаёт ход и считает ответ соперника в фоне.</summary>
    /// <returns><c>true</c>, если пас игрока принят.</returns>
    public async Task<bool> PassAsync()
    {
        if (_isThinking || !TryPlay(Move.Pass(_game.ToMove)))
        {
            return false;
        }

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
    /// предварительным. Окончание по лимиту ходов и сдача согласования не требуют.
    /// </remarks>
    public bool IsCounting => _game.Status == GameStatus.FinishedByTwoPasses && _finalScore is null;

    /// <summary>Подсчёт можно подтвердить: идёт согласование мёртвых групп.</summary>
    public bool CanConfirmScore => IsCounting;

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
    public IReadOnlyList<Point> DeadPoints => _deadPoints;

    /// <summary>Сколько раз менялись пометки мёртвых.</summary>
    /// <remarks>
    /// Вид перерисовывает доску по этому счётчику: список <see cref="DeadPoints"/> может остаться
    /// тем же объектом, и подписка на него одного изменения не заметит.
    /// </remarks>
    public int DeadRevision => _deadRevision;

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
        if (!IsCounting || !point.IsOnBoard(_game.Board.Size) || _game.Board.At(point) == StoneColor.Empty)
        {
            return false;
        }

        var stones = GroupTracker.FindGroup(_game.Board, point).Stones;

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
        NotifyDeadChanged();

        return true;
    }

    /// <summary>Подтверждает подсчёт: мёртвые снимаются, становятся пленными и входят в итог.</summary>
    /// <remarks>
    /// До подтверждения счёт предварительный, после — окончательный. Повторный вызов ничего
    /// не делает: подтверждать нечего. Вернуться к согласованию можно отменой хода — она снова
    /// открывает партию и сбрасывает пометки.
    /// </remarks>
    public void ConfirmScore()
    {
        if (!IsCounting)
        {
            return;
        }

        _finalScore = _game.FinalScore(_dead, ScoringRule);
        NotifyAll();
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

        SyncDeadMarks();
        ShowAiMoveIfNeeded();
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

        SyncDeadMarks();
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

        SyncDeadMarks();

        return Result.Ok();
    }

    /// <summary>Начинает новую партию по текущим настройкам.</summary>
    public void StartNewGame() => ApplySettings(_settings);

    /// <summary>Начинает новую партию и считает первый ход соперника в фоне.</summary>
    /// <returns>Задача, завершающаяся после ответа соперника.</returns>
    public Task StartNewGameAsync() => ApplySettingsAsync(_settings);

    /// <summary>Применяет настройки и начинает новую партию.</summary>
    /// <param name="settings">Новые настройки партии.</param>
    public void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        LogSettings(settings);
        BeginGame(settings);
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
        SyncDeadMarks();
        LogNewGame();
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

        if (_game.Status != GameStatus.InProgress || _game.ToMove == playerColor)
        {
            return;
        }

        CancelThinking();

        using var cancellation = new CancellationTokenSource();
        _aiCancellation = cancellation;
        SetThinking(true);

        try
        {
            while (_game.Status == GameStatus.InProgress && _game.ToMove != playerColor)
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
        SyncDeadMarks();

        LogMove(move, aiMilliseconds);
        LogFinishedIfNeeded();

        return true;
    }

    /// <summary>Приводит согласование мёртвых в соответствие с партией.</summary>
    /// <remarks>
    /// Пометки живут ровно столько, сколько живёт позиция: любой ход, отмена, возврат, загрузка
    /// и новая партия их обнуляют — мёртвая группа одной позиции ничего не значит в другой.
    /// Как только партия завершается двумя пасами, перебор предлагает то, что доказано
    /// (<see cref="Endgame.ProposeDead"/>): игрок видит готовое предложение и правит его кликами.
    /// Подтверждённый итог при этом сбрасывается — он относился к прежней позиции.
    /// </remarks>
    private void SyncDeadMarks()
    {
        _finalScore = null;

        // Перебор предлагает только доказанные группы и ничего не утверждает о сэки и глазах:
        // ложное «мёртвая» испортило бы счёт молча, поэтому предложение осторожное.
        HashSet<Point> proposed = _game.Status == GameStatus.FinishedByTwoPasses
            ? [.. Endgame.ProposeDead(_game.Board)]
            : [];

        if (_dead.SetEquals(proposed))
        {
            return;
        }

        _dead.Clear();
        _dead.UnionWith(proposed);
        _deadRevision++;
        RefreshDeadPoints();
    }

    /// <summary>Обновляет список помеченных камней для вида: порядок — как у обхода доски.</summary>
    private void RefreshDeadPoints() =>
        _deadPoints = [.. _dead.OrderBy(static point => point.Y).ThenBy(static point => point.X)];

    /// <summary>Сообщает об изменении пометок мёртвых и всего, что от них зависит.</summary>
    /// <remarks>
    /// Счёт и разметка территории считаются по доске без мёртвых камней, поэтому клик по группе
    /// меняет не только пометки: без этих уведомлений панель показывала бы старый счёт.
    /// </remarks>
    private void NotifyDeadChanged()
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
    /// <returns>Например, «Японская: чёрные 45 : 50.5 белые».</returns>
    /// <remarks>
    /// Японская величина — территория с пленными, китайская — площадь. Разбор слагаемых убран
    /// намеренно: он нужен тому, кто проверяет подсчёт, а не тому, кто играет; для проверки
    /// есть строка <see cref="ScoreBreakdown"/> и переключатель территории.
    /// </remarks>
    private static string SystemLine(FinalScore score, Core.ScoringRule rule)
    {
        var japanese = rule == Core.ScoringRule.Japanese;
        var black = japanese ? score.BlackTerritoryPoints : score.BlackArea;
        var white = japanese ? score.WhiteTerritoryPoints : score.WhiteArea;
        var name = japanese ? "Японская" : "Китайская";

        return $"{name}: чёрные {black} : {white + score.Komi.Value} белые";
    }

    /// <summary>Вторая система подсчёта: та, по которой победитель не называется.</summary>
    /// <param name="rule">Основная система.</param>
    /// <returns>Противоположная система подсчёта.</returns>
    private static Core.ScoringRule OpponentRule(Core.ScoringRule rule) =>
        rule == Core.ScoringRule.Japanese ? Core.ScoringRule.Chinese : Core.ScoringRule.Japanese;

    /// <summary>Итог по текущему состоянию: подтверждённый или предварительный.</summary>
    /// <remarks>
    /// Считает Core одной функцией от доски, пометок, пленных партии и выбранной системы.
    /// Отдельного «предварительного» подсчёта в модели нет: иначе он разошёлся бы с итогом.
    /// </remarks>
    private FinalScore CurrentScore => _finalScore ?? _game.FinalScore(_dead, ScoringRule);

    /// <summary>Доска подсчёта: без помеченных мёртвых камней.</summary>
    /// <remarks>
    /// Территория и владение считаются по ней, а не по игровой доске: мёртвый камень внутри
    /// чужого владения не должен отменять территорию соперника. Кэша нет намеренно — пометки
    /// меняются кликами, и сбрасывать кэш пришлось бы в каждом месте, где меняется партия.
    /// </remarks>
    private Board ScoringBoard => _dead.Count == 0 ? _game.Board : Endgame.ClearedBoard(_game.Board, _dead);

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
