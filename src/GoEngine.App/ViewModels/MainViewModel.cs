using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.ViewModels;

/// <summary>Состояние партии для окна: доска, панель статуса и ход AI.</summary>
/// <remarks>
/// Модель представления ничего не рисует и не знает про Avalonia: она показывает данные
/// <see cref="GameState"/> строками и играет за AI. Все правила (легальность хода, завершение
/// партии, подсчёт) выполняет <c>Core</c>, выбор хода — <c>AI</c>.
/// Ход AI считается синхронно: на слабых уровнях это доли миллисекунды, на сильных окно
/// на время поиска не отвечает — вынос в фон запланирован на полировку (T-030).
/// </remarks>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Random _random;
    private readonly IPositionEvaluator? _evaluator;
    private readonly IReadOnlySet<int> _modelSizes;
    private GameState _game;
    private AppSettings _settings;
    private int _capturedBlack;
    private int _capturedWhite;
    private bool _showTerritory;

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

        RecountCaptures();
        RefreshLabels();
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
    public string ToMove => _game.ToMove == StoneColor.Black ? "Чёрные" : "Белые";

    /// <summary>Номер последнего хода.</summary>
    public string MoveNumber => _game.MoveNumber.ToString(CultureInfo.InvariantCulture);

    /// <summary>Коми партии.</summary>
    public string Komi => _game.Komi.ToString();

    /// <summary>Текущий счёт по китайским правилам.</summary>
    public string Score => Scorer.Calculate(_game.Board, _game.Komi).ToString();

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

    /// <summary>Чем играет AI: вид селектора и модель, если она нашлась.</summary>
    public string Engine
    {
        get
        {
            var level = _settings.ToDifficultyLevel();

            if (level.NeedsNetwork)
            {
                return HasModel(_settings.ToBoardSize())
                    ? $"нейросеть {BoardSizes.Label(_settings.ToBoardSize())}"
                    : "нейросеть недоступна";
            }

            if (level.Kind == SelectorKind.Random)
            {
                return "случайные ходы";
            }

            return level.Kind == SelectorKind.Heuristic ? "эвристики" : "MCTS без сети";
        }
    }

    /// <summary>Уровень, движок и бюджет одной строкой: «5 дан · нейросеть 9×9 · 4 с/ход».</summary>
    /// <remarks>Подпись собирает <see cref="LevelChooser.Describe"/>: ранг номинальный, поэтому
    /// рядом всегда стоят движок и бюджет, а для сети — ещё и размер доски.</remarks>
    public string LevelDescription =>
        LevelChooser.Describe(CurrentLevel, _settings.ToBoardSize(), HasModel(_settings.ToBoardSize()));

    /// <summary>Уровень, которым играет партия прямо сейчас.</summary>
    public DifficultyLevel CurrentLevel => _settings.ToDifficultyLevel();

    /// <summary>Цвет игрока словами.</summary>
    public string PlayerColor => _settings.ToPlayerColor() == StoneColor.Black ? "Чёрные" : "Белые";

    /// <summary>Сколько чёрных камней снято за партию.</summary>
    public int CapturedBlack => _capturedBlack;

    /// <summary>Сколько белых камней снято за партию.</summary>
    public int CapturedWhite => _capturedWhite;

    /// <summary>Снятые камни одной строкой.</summary>
    public string Captures => $"чёрных {_capturedBlack}, белых {_capturedWhite}";

    /// <summary>Итог завершённой партии или пустая строка, пока партия идёт.</summary>
    public string Outcome
    {
        get
        {
            if (_game.Status == GameStatus.InProgress)
            {
                return string.Empty;
            }

            var winner = Scorer.Calculate(_game.Board, _game.Komi).Winner;

            return winner == StoneColor.Empty ? "Ничья" : $"Победили {(winner == StoneColor.Black ? "чёрные" : "белые")}";
        }
    }

    /// <summary>Показывать ли итог партии: он есть только у завершённой.</summary>
    public bool HasOutcome => Outcome.Length > 0;

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
            OnPropertyChanged(nameof(TerritorySummary));
        }
    }

    /// <summary>Показывается ли разметка территории сейчас.</summary>
    public bool HasTerritory => _showTerritory || _game.Status != GameStatus.InProgress;

    /// <summary>Владение точками для разметки или <c>null</c>, если разметка выключена.</summary>
    /// <remarks>
    /// Считает <see cref="Scorer.Ownership"/>: камень — своему цвету, пустая область — цвету
    /// окружения, спорная — нейтральна. Второго подсчёта территории в проекте нет.
    /// </remarks>
    public IReadOnlyList<StoneColor>? Territory => HasTerritory ? Scorer.Ownership(_game.Board) : null;

    /// <summary>Расшифровка разметки: площадь сторон, нейтральные точки и коми.</summary>
    /// <remarks>
    /// Считает <see cref="Scorer.Breakdown"/>: разбор площади живёт в <c>Core</c> и используется
    /// и подсчётом очков, и панелью партии — второго подсчёта в проекте нет.
    /// </remarks>
    public string TerritorySummary
    {
        get
        {
            if (!HasTerritory)
            {
                return string.Empty;
            }

            var area = Scorer.Breakdown(_game.Board);

            return $"Площадь: чёрные {area.Black} (камни {area.BlackStones}, территория {area.BlackTerritory}), "
                + $"белые {area.White} (камни {area.WhiteStones}, территория {area.WhiteTerritory}), "
                + $"нейтрально {area.Neutral} · коми {_game.Komi} белым";
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

            StartWith(size, LevelChooser.Resolve(_settings.ToDifficultyLevel(), size, HasModel(size)));
        }
    }

    /// <summary>Уровни, доступные для выбранной доски.</summary>
    /// <remarks>Список собирает <see cref="LevelListView"/>, состав считает <see cref="LevelChooser"/>:
    /// он знает про наличие модели (D-038, D-039).</remarks>
    public IReadOnlyList<DifficultyLevel> LevelOptions =>
        LevelListView.ForGame(_settings.ToBoardSize(), HasModel(_settings.ToBoardSize())).Levels;

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
        var labels = LevelListView.ForGame(size, HasModel(size)).Labels;

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

            if (HasModel(size))
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
    private bool HasModel(BoardSize size) => _evaluator is not null && _modelSizes.Contains(size.Value);

    /// <summary>Начинает партию с выбранными доской и уровнем.</summary>
    /// <param name="size">Сторона доски.</param>
    /// <param name="level">Уровень AI.</param>
    private void StartWith(BoardSize size, DifficultyLevel level) =>
        ApplySettings(AppSettings.From(size, level, _settings.ToPlayerColor(), Core.Komi.For(size)));

    /// <summary>Играет ход игрока, затем ход AI, если очередь за ним.</summary>
    /// <param name="point">Точка хода.</param>
    /// <returns><c>true</c>, если ход принят.</returns>
    public bool PlayMove(Point point)
    {
        if (!TryPlay(Move.Play(point, _game.ToMove)))
        {
            return false;
        }

        ShowAiMoveIfNeeded();

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
        if (!TryPlay(Move.Pass(_game.ToMove)))
        {
            return false;
        }

        ShowAiMoveIfNeeded();

        return true;
    }

    /// <summary>Можно ли отменить ход игрока: отмена обязана вернуть очередь игроку.</summary>
    /// <remarks>Кнопка «Отменить» недоступна, когда отменять ход игрока нечего.</remarks>
    public bool CanUndo => _game.CanUndoTo(_settings.ToPlayerColor());

    /// <summary>Можно ли вернуть отменённый ход игрока.</summary>
    public bool CanRedo => _game.CanRedoTo(_settings.ToPlayerColor());

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
        if (!CanUndo || !TryUndo())
        {
            return false;
        }

        RecountCaptures();
        ShowAiMoveIfNeeded();
        NotifyAll();

        return true;
    }

    /// <summary>Возвращает отменённый ход вместе с ответом AI.</summary>
    /// <returns><c>true</c>, если ход возвращён.</returns>
    /// <remarks>Если возврат не доводит очередь до игрока, состояние не меняется.</remarks>
    public bool Redo()
    {
        if (!CanRedo || !TryRedo())
        {
            return false;
        }

        RecountCaptures();
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
        var restored = game.ToGameState();

        if (!restored.IsSuccess)
        {
            return Result.Fail(restored.Error!);
        }

        _game = restored.Value!;
        _settings = AppSettings.From(game.Size, _settings.ToDifficultyLevel(), _settings.ToPlayerColor(), game.Komi);

        RecountCaptures();

        // После загрузки партия может стоять на ходе AI — он обязан ответить.
        ShowAiMoveIfNeeded();

        return Result.Ok();
    }

    /// <summary>Начинает новую партию по текущим настройкам.</summary>
    public void StartNewGame() => ApplySettings(_settings);

    /// <summary>Применяет настройки и начинает новую партию.</summary>
    /// <param name="settings">Новые настройки партии.</param>
    public void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _game = CreateGame(settings);

        RecountCaptures();
        ShowAiMoveIfNeeded();
        NotifyAll();
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
        var level = _settings.ToDifficultyLevel();
        var size = _settings.ToBoardSize();

        // Уровни Дан играют только с сетью и только на больших досках (D-038). Если модель
        // не загружена или доска 9×9, уровень заменяется на 10 кю: партия не должна срываться
        // из-за настроек, которые интерфейс и так не предлагает.
        var modelsDirectory = global::GoEngine.App.App.ModelsDirectory;

        if (level.NeedsNetwork && (_evaluator is null || modelsDirectory is null))
        {
            level = DifficultyLevel.Kyu10;
        }

        while (_game.Status == GameStatus.InProgress && _game.ToMove != playerColor)
        {
            var selector = AiFactory.Create(level, _random, size, _evaluator, modelsDirectory);

            if (TryPlay(selector.SelectMove(_game)) || TryPlay(Move.Pass(_game.ToMove)))
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

    /// <summary>Играет ход в партии и учитывает снятые камни.</summary>
    /// <param name="move">Ход.</param>
    /// <returns><c>true</c>, если правила его приняли.</returns>
    /// <remarks>Снятые камни всегда принадлежат сопернику ходившего: пас ничего не снимает.</remarks>
    private bool TryPlay(Move move)
    {
        if (!_game.Play(move).IsSuccess)
        {
            return false;
        }

        if (move.Type == MoveType.Play)
        {
            var captured = _game.Board.CapturedStones.Count;

            if (move.Color == StoneColor.Black)
            {
                _capturedWhite += captured;
            }
            else
            {
                _capturedBlack += captured;
            }
        }

        return true;
    }

    /// <summary>Считает снятые камни по всей партии заново.</summary>
    /// <remarks>
    /// Нужен после отмены, возврата и загрузки: у этих действий нет одного «последнего хода»,
    /// а счётчики должны совпадать с партией. Ходы повторяются на доске без истории — правила
    /// они уже проходили, поэтому отказов не бывает.
    /// </remarks>
    private void RecountCaptures()
    {
        _capturedBlack = 0;
        _capturedWhite = 0;

        var board = new Board(_game.Board.Size);

        foreach (var move in _game.Moves)
        {
            if (move.Type != MoveType.Play)
            {
                continue;
            }

            board = board.ApplyMove(move);

            var captured = board.CapturedStones.Count;

            if (captured == 0)
            {
                continue;
            }

            if (move.Color == StoneColor.Black)
            {
                _capturedWhite += captured;
            }
            else
            {
                _capturedBlack += captured;
            }
        }
    }

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
            nameof(Score),
            nameof(Status),
            nameof(Level),
            nameof(Engine),
            nameof(LevelDescription),
            nameof(PlayerColor),
            nameof(CapturedBlack),
            nameof(CapturedWhite),
            nameof(Captures),
            nameof(Outcome),
            nameof(HasOutcome),
            nameof(CanUndo),
            nameof(CanRedo),
            nameof(HasTerritory),
            nameof(Territory),
            nameof(TerritorySummary),
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