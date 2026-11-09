using System.ComponentModel;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.Problems;
using Microsoft.Extensions.Logging;

namespace GoEngine.App.ViewModels;

/// <summary>Подраздел обучения: уроки, разбор партий или задачи.</summary>
/// <remarks>
/// Задачи — не отдельный режим приложения, а третий подраздел обучения: после уроков и разбора
/// партий (уточнение интерфейса 2026-11-09). Так у ребёнка один вход «Обучение», а виды практики
/// не спорят за место в нижней навигации.
/// </remarks>
public enum StudyPart
{
    /// <summary>Уроки шаг за шагом.</summary>
    Lessons,

    /// <summary>Разбор партий: практика после уроков.</summary>
    Reviews,

    /// <summary>Задачи на решение: позиции с целью.</summary>
    Problems
}

/// <summary>Оболочка приложения: главный экран, партия с ИИ и обучение.</summary>
/// <remarks>
/// <para>
/// Режимы не смешиваются: партия живёт в <see cref="MainViewModel"/> и своём виде, обучение —
/// в <see cref="LessonViewModel"/>, <see cref="ReviewViewModel"/> и <see cref="ProblemViewModel"/>.
/// Оболочка только показывает один из экранов и владеет всеми моделями, поэтому переключение
/// режима партию не пересоздаёт: вернувшись в «Партию», игрок видит ту же позицию
/// (<c>DECISIONS.md</c>, D-061).
/// </para>
/// <para>
/// Первый экран приложения — <see cref="IsHomeMode"/>: выбор режима крупными карточками. Партия
/// с ИИ при запуске не начинается сама — только после явного выбора и первого хода игрока
/// (замечание пользователя 2026-11-09).
/// </para>
/// <para>
/// Разделов нижней навигации телефона четыре: «Партия с ИИ», «Партия с другом», «Обучение» и
/// «Меню». Партия с другом — сетевое лобби, его показывает вид (<see cref="GoEngine.App.Views.ShellView"/>),
/// а не режим оболочки: лобби перекрывает содержимое и возвращает игрока туда, откуда он пришёл.
/// Настройка партии в навигации не стоит: она принадлежит партии и живёт в её разделе (D-075).
/// </para>
/// <para>
/// Меню режим не меняет: <see cref="IsGameMode"/> и родственные свойства говорят, какой режим
/// выбран, а <see cref="IsGameSection"/> и родственные — какой раздел показан сейчас. Поэтому,
/// закрыв «Меню», игрок возвращается туда же, где был (D-075).
/// </para>
/// </remarks>
public sealed class ShellViewModel : INotifyPropertyChanged
{
    private static readonly string[] PropertyNames =
    [
        nameof(IsHomeMode), nameof(IsGameMode), nameof(IsLessonMode), nameof(ModeHint),
        nameof(Reviews), nameof(SelectedStudy), nameof(IsLessonsStudy), nameof(IsReviewsStudy),
        nameof(IsProblemsStudy), nameof(Progress),
        nameof(IsHomeSection), nameof(IsGameSection), nameof(IsLessonSection), nameof(IsMenuSection),
        nameof(Theme), nameof(IsSystemTheme), nameof(IsLightTheme), nameof(IsDarkTheme)
    ];

    /// <summary>Выбранный режим: то, что вернётся на экран после закрытия общего меню.</summary>
    private Mode _mode = Mode.Home;

    /// <summary>Подраздел обучения, выбранный переключателем: уроки, партии или задачи.</summary>
    private StudyPart _study = StudyPart.Lessons;

    /// <summary>Открыт раздел общего меню: режим при этом не меняется, содержимое уступает ему место.</summary>
    private bool _menuSection;

    /// <summary>Выбранное оформление: светлое, тёмное или системное.</summary>
    private ThemeChoice _theme;

    /// <summary>Проверка обновления при запуске уже начата: второй раз она не идёт.</summary>
    private bool _updateCheckStarted;

    /// <summary>Логгер режимов: в логе видно, чем игрок занимался до сбоя.</summary>
    private readonly ILogger _log = AppLog.For<ShellViewModel>();

    /// <summary>Создаёт оболочку с готовыми моделями режимов.</summary>
    /// <param name="settings">Настройки партии: их же получает вид партии.</param>
    /// <param name="game">Модель представления партии.</param>
    /// <param name="problems">Модель представления задач: третий подраздел обучения.</param>
    /// <param name="updates">
    /// Модель обновления: приглашение и ход загрузки. <c>null</c> — общая модель приложения.
    /// </param>
    /// <param name="lessons">Модель представления обучения; <c>null</c> — встроенные уроки.</param>
    /// <param name="theme">Выбранное оформление; <c>null</c> — системная тема.</param>
    /// <param name="reviews">Разбор партий; <c>null</c> — встроенная библиотека.</param>
    /// <param name="progress">Прогресс обучения; <c>null</c> — начать с чистого листа.</param>
    public ShellViewModel(
        AppSettings settings,
        MainViewModel game,
        ProblemViewModel problems,
        UpdateViewModel? updates = null,
        LessonViewModel? lessons = null,
        ThemeChoice? theme = null,
        ReviewViewModel? reviews = null,
        StudyProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(problems);

        Settings = settings;
        Game = game;
        Problems = problems;
        Progress = progress ?? StudyProgress.Empty;

        Lessons = lessons ?? new LessonViewModel(LessonLibrary.All, Progress);
        Reviews = reviews ?? new ReviewViewModel(ReviewLibrary.All, Progress);
        Updates = updates ?? global::GoEngine.App.App.Updates;
        _theme = theme ?? ThemeChoice.System;

        // Прогресс обучения пишет вид: оболочка только сообщает, что он изменился.
        Lessons.ProgressChanged += OnStudyProgressChanged;
        Reviews.ProgressChanged += OnStudyProgressChanged;
    }

    /// <summary>Игрок выбрал другое оформление.</summary>
    /// <remarks>
    /// Событие, а не прямая смена темы: тему применяет приложение (<c>App.ApplyTheme</c>),
    /// а модель оболочки только хранит выбор — так правило «кто что делает» не размывается,
    /// и модель остаётся проверяемой без окна.
    /// </remarks>
    public event EventHandler<ThemeChoice>? ThemeChanged;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Настройки партии: их показывает «Настройка партии».</summary>
    public AppSettings Settings { get; }

    /// <summary>Модель представления партии: живёт, пока приложение открыто.</summary>
    public MainViewModel Game { get; }

    /// <summary>Модель представления задач: третий подраздел обучения.</summary>
    public ProblemViewModel Problems { get; }

    /// <summary>Модель представления обучения: уроки и их прохождение.</summary>
    public LessonViewModel Lessons { get; }

    /// <summary>Прогресс обучения: что пройдено и где игрок остановился.</summary>
    /// <remarks>
    /// Общий для уроков и разбора партий: файл прогресса один, и оболочка записывает его целиком.
    /// </remarks>
    public StudyProgress Progress { get; }

    /// <summary>Прогресс обучения изменился: вид записывает его в файл.</summary>
    /// <remarks>
    /// Запись — дело вида (как оформление и настройки): модель только сообщает о смене состояния,
    /// поэтому её поведение проверяется без диска.
    /// </remarks>
    public event EventHandler? ProgressChanged;

    /// <summary>Модель представления разбора партий: практика после уроков.</summary>
    /// <remarks>
    /// Разбор живёт в разделе «Обучение» рядом с уроками: уроки объясняют приём, партии показывают,
    /// как он работает в целой игре, — это две ступени одного обучения (замечание 2026-10-09).
    /// </remarks>
    public ReviewViewModel Reviews { get; }

    /// <summary>Приглашение обновления и ход загрузки: общее с экраном «О программе» состояние.</summary>
    /// <remarks>
    /// Модель приходит снаружи и по умолчанию берётся у приложения: проверка из «О программе» и
    /// приглашение в оболочке должны говорить об одном обновлении, а не о двух разных.
    /// </remarks>
    public UpdateViewModel Updates { get; }

    /// <summary>Выбран главный экран: приложение открывается на выборе режима.</summary>
    public bool IsHomeMode => _mode == Mode.Home;

    /// <summary>Выбран режим партии с ИИ.</summary>
    public bool IsGameMode => _mode == Mode.Game;

    /// <summary>Выбран режим обучения.</summary>
    public bool IsLessonMode => _mode == Mode.Lessons;

    /// <summary>Показан главный экран — выбор режима.</summary>
    public bool IsHomeSection => _mode == Mode.Home && !_menuSection;

    /// <summary>Показан раздел партии.</summary>
    public bool IsGameSection => _mode == Mode.Game && !_menuSection;

    /// <summary>Показан раздел обучения.</summary>
    public bool IsLessonSection => _mode == Mode.Lessons && !_menuSection;

    /// <summary>В обучении открыты уроки: первый подраздел.</summary>
    public bool IsLessonsStudy => _study == StudyPart.Lessons;

    /// <summary>В обучении открыт разбор партий: практика после уроков.</summary>
    public bool IsReviewsStudy => _study == StudyPart.Reviews;

    /// <summary>В обучении открыты задачи: третий подраздел, после уроков и партий.</summary>
    public bool IsProblemsStudy => _study == StudyPart.Problems;

    /// <summary>Выбранный подраздел обучения: его показывает переключатель раздела.</summary>
    public StudyPart SelectedStudy => _study;

    /// <summary>Открыт раздел общего меню: режим, «О программе» и выход.</summary>
    public bool IsMenuSection => _menuSection;

    /// <summary>Выбранное оформление.</summary>
    public ThemeChoice Theme => _theme;

    /// <summary>Тема следует за системой.</summary>
    public bool IsSystemTheme => _theme == ThemeChoice.System;

    /// <summary>Выбрана светлая тема.</summary>
    public bool IsLightTheme => _theme == ThemeChoice.Light;

    /// <summary>Выбрана тёмная тема.</summary>
    public bool IsDarkTheme => _theme == ThemeChoice.Dark;

    /// <summary>Подсказка о текущем экране — чтобы игрок понимал, где он находится.</summary>
    /// <remarks>
    /// Одна короткая строка: полное описание режима занимало пол-экрана и повторяло то, что
    /// и так видно (жалоба 2026-09-30 — «очень много старого текста»).
    /// </remarks>
    public string ModeHint => _mode switch
    {
        Mode.Home => "Выберите, во что играть: партия с ИИ, партия с другом или обучение.",
        Mode.Lessons => _study switch
        {
            StudyPart.Reviews => "Обучение: разберите партию ход за ходом — в отмеченных местах ход ищете вы.",
            StudyPart.Problems => "Задачи: решите позицию, подсказка покажет ход.",
            _ => "Обучение: пройдите урок на доске — шаг за шагом."
        },
        _ => "Партия с ИИ: щёлкните по доске, чтобы сделать ход."
    };

    /// <summary>Собирает оболочку с настройками из файла и оценкой сети, заданной головой.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <param name="evaluator">Оценка позиции нейросетью для уровней Дан; <c>null</c> — без сети.</param>
    /// <param name="problems">Задачи; <c>null</c> — взять встроенную библиотеку.</param>
    /// <param name="updates">Модель обновления; <c>null</c> — общая модель приложения.</param>
    /// <param name="lessons">Уроки; <c>null</c> — встроенная библиотека уроков.</param>
    /// <param name="theme">Выбранное оформление; <c>null</c> — системная тема.</param>
    /// <param name="reviews">Разбор партий; <c>null</c> — встроенная библиотека.</param>
    /// <param name="progress">Прогресс обучения; <c>null</c> — начать с чистого листа.</param>
    /// <returns>Оболочку с главным экраном, партией и обучением.</returns>
    public static ShellViewModel Create(
        AppSettings settings,
        IPositionEvaluator? evaluator = null,
        IReadOnlyList<Problem>? problems = null,
        UpdateViewModel? updates = null,
        IReadOnlyList<Lesson>? lessons = null,
        ThemeChoice? theme = null,
        IReadOnlyList<ReviewGame>? reviews = null,
        StudyProgress? progress = null) =>
        new(
            settings,
            new MainViewModel(settings, Random.Shared, evaluator),
            problems is null ? new ProblemViewModel() : new ProblemViewModel(problems),
            updates,
            lessons is null ? null : new LessonViewModel(lessons, progress),
            theme,
            reviews is null ? null : new ReviewViewModel(reviews, progress),
            progress);

    /// <summary>Проверяет обновление при первом показе оболочки: один раз, в фоне и молча.</summary>
    /// <returns>Задача проверки; повторные вызовы ничего не делают.</returns>
    /// <remarks>
    /// Приложение offline-first: проверка не задерживает ни запуск, ни первый ход игрока, а её
    /// отказ (нет сети, нет манифеста) — обычное дело, которое видно в логе, а не на экране.
    /// Приглашение появляется, только если обновление действительно есть. Возвращаемая задача
    /// нужна проверкам: они ждут её, чтобы утверждать об исходе; вид её не ожидает.
    /// </remarks>
    public async Task CheckUpdatesOnStartAsync()
    {
        if (_updateCheckStarted)
        {
            return;
        }

        _updateCheckStarted = true;

        await Updates.CheckAsync(silent: true).ConfigureAwait(true);
    }

    /// <summary>Сообщает виду, что прогресс обучения изменился.</summary>
    /// <param name="sender">Модель уроков или разбора партий.</param>
    /// <param name="e">Событие изменения.</param>
    private void OnStudyProgressChanged(object? sender, EventArgs e)
    {
        _ = sender;

        ProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Показывает главный экран — выбор режима.</summary>
    public void ShowHome() => SetSection(Mode.Home, menu: false);

    /// <summary>Показывает режим партии с ИИ.</summary>
    public void ShowGame() => SetSection(Mode.Game, menu: false);

    /// <summary>Показывает режим обучения: тот подраздел, который был выбран.</summary>
    public void ShowLessons() => SetSection(Mode.Lessons, menu: false);

    /// <summary>Открывает подраздел обучения: уроки, разбор партий или задачи.</summary>
    /// <param name="part">Подраздел обучения.</param>
    /// <remarks>
    /// Задачи — третий подраздел обучения, а не отдельный режим. При выборе задач поиск хода
    /// соперника отменяется: доска уходит с экрана, и результат поиска не должен применяться,
    /// пока игрок решает позицию (то же правило, что было у прежнего режима задач).
    /// </remarks>
    public void ShowStudy(StudyPart part)
    {
        if (!Enum.IsDefined(part))
        {
            throw new ArgumentOutOfRangeException(nameof(part), part, "Неизвестный подраздел обучения.");
        }

        var studyChanged = _study != part;
        var sectionChanged = _mode != Mode.Lessons || _menuSection;

        if (!studyChanged && !sectionChanged)
        {
            return;
        }

        if (part == StudyPart.Problems)
        {
            Game.CancelThinking();
        }

        if (studyChanged)
        {
            _study = part;

            AppLogMessages.StudySelected(_log, StudyName(part));
        }

        if (sectionChanged)
        {
            _mode = Mode.Lessons;
            _menuSection = false;

            AppLogMessages.ModeChanged(_log, SectionName(Mode.Lessons, menu: false));
        }

        NotifyAll();
    }

    /// <summary>Открывает общее меню: режим, «О программе» и выход из игры.</summary>
    /// <remarks>
    /// Режим меню не меняет: закрыв его, игрок возвращается туда же, где был. Так «Меню» остаётся
    /// разделом приложения, а не игровым режимом, и выбор режима внутри него — обычный переход.
    /// </remarks>
    public void ShowMenu() => SetSection(_mode, menu: true);

    /// <summary>Закрывает общее меню и возвращает игрока в текущий раздел.</summary>
    public void CloseMenu() => SetSection(_mode, menu: false);

    /// <summary>Меняет оформление и сообщает об этом виду.</summary>
    /// <param name="theme">Новое оформление.</param>
    /// <remarks>
    /// Повторный выбор той же темы ничего не делает: иначе вид перерисовывался бы и файл
    /// оформления переписывался на каждое нажатие уже выбранной строки.
    /// </remarks>
    public void SelectTheme(ThemeChoice theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (_theme == theme)
        {
            return;
        }

        AppLogMessages.ThemeChanged(_log, theme.Name);

        _theme = theme;

        ThemeChanged?.Invoke(this, theme);

        NotifyAll();
    }

    /// <summary>Переключает раздел и сообщает об этом виду.</summary>
    /// <param name="mode">Режим, который показывается.</param>
    /// <param name="menu">Показать общее меню вместо содержимого режима.</param>
    /// <remarks>
    /// Повторный выбор того же раздела ничего не меняет и ни о чём не сообщает: иначе вид
    /// перерисовывался бы на каждое нажатие выбранной кнопки.
    /// </remarks>
    private void SetSection(Mode mode, bool menu)
    {
        if (_mode == mode && _menuSection == menu)
        {
            return;
        }

        AppLogMessages.ModeChanged(_log, SectionName(mode, menu));

        _mode = mode;
        _menuSection = menu;

        if (mode == Mode.Lessons && _study == StudyPart.Problems)
        {
            // Партия уходит с экрана: поиск хода соперника больше не нужен, а его результат
            // не должен примениться, пока игрок решает задачи.
            Game.CancelThinking();
        }

        NotifyAll();
    }

    /// <summary>Называет подраздел обучения для журнала.</summary>
    /// <param name="part">Подраздел обучения.</param>
    /// <returns>Название подраздела.</returns>
    private static string StudyName(StudyPart part) => part switch
    {
        StudyPart.Reviews => "разбор партий",
        StudyPart.Problems => "задачи",
        _ => "уроки"
    };

    /// <summary>Называет раздел для журнала: в логе видно, что игрок открыл перед сбоем.</summary>
    /// <param name="mode">Режим.</param>
    /// <param name="menu">Показано общее меню.</param>
    /// <returns>Название раздела.</returns>
    private static string SectionName(Mode mode, bool menu) => menu
        ? "Меню"
        : mode switch
        {
            Mode.Home => "Главная",
            Mode.Lessons => "Обучение",
            _ => "Партия с ИИ"
        };

    /// <summary>Сообщает виду обо всех свойствах оболочки.</summary>
    /// <remarks>
    /// Сообщаем обо всех: разделы нижней навигации зависят и от режима, и от общего меню,
    /// а вычислять, что именно изменилось, здесь нечего — свойств немного.
    /// </remarks>
    private void NotifyAll()
    {
        foreach (var name in PropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>Режим оболочки: что показывается, когда общее меню закрыто.</summary>
    private enum Mode
    {
        /// <summary>Главный экран — выбор режима.</summary>
        Home,

        /// <summary>Партия с ИИ.</summary>
        Game,

        /// <summary>Обучение: уроки, разбор партий и задачи.</summary>
        Lessons
    }
}
