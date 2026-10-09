using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Оболочка приложения: разделы «Партия», «Задачи» и общее меню.</summary>
/// <remarks>
/// <para>
/// Все три раздела живут одновременно: виды не пересоздаются, меняется только видимость. Поэтому
/// переключение раздела не сбрасывает партию — вернувшись в «Партию», игрок видит ту же позицию.
/// Тот же вид используется настольным окном и мобильной головой (<c>GoEngine.App.Android</c>).
/// Раскладка выбирается по ширине вида (<see cref="BoardLayoutRules.DecideLayout"/>): на широком
/// экране — прежняя строка режимов сверху, на телефоне — нижняя навигация с тремя разделами.
/// </para>
/// <para>
/// Настройки и меню партии в навигации не стоят: они принадлежат партии и открываются из её шапки
/// (<see cref="BoardView"/>). Общее меню — раздел приложения (<see cref="AppMenuView"/>): выбор
/// режима, «О программе» и выход. «О программе» показывается вложенным экраном поверх содержимого,
/// а не отдельным разделом: так у раздела «Меню» есть своя глубина, а нижняя навигация остаётся
/// видимой (замечание пользователя 2026-10-06; <c>DECISIONS.md</c>, D-075).
/// </para>
/// <para>
/// При запуске оболочка открывается на разделе «Партия» — экрана настроек вместо доски нет
/// (замечание 2026-10-03). На телефоне над доской сразу показана шторка меню партии
/// (<see cref="BoardView.ShowMenu"/>): там это единственный вход к действиям партии, а настольное
/// окно стартует без неё — у него есть меню-бар и мышь, и шторка поверх доски только мешала бы.
/// Раскладку выбирает <see cref="BoardLayoutRules.DecideLayout"/> по ширине вида, решение о меню
/// принимается там же, где применяется раскладка (<see cref="ApplyLayout"/>).
/// </para>
/// </remarks>
public sealed partial class ShellView : UserControl
{
    private readonly Panel? _content;
    private readonly ToggleButton? _gameButton;
    private readonly ToggleButton? _problemButton;
    private readonly ToggleButton? _lessonButton;
    private readonly Border? _studySwitch;
    private readonly ToggleButton? _studyLessonsButton;
    private readonly ToggleButton? _studyReviewsButton;
    private readonly Border? _desktopBar;
    private readonly Border? _mobileNav;
    private readonly Border? _aboutOverlay;
    private readonly Border? _glossaryOverlay;
    private readonly Border? _exitOverlay;
    private readonly AboutView? _aboutArea;
    private readonly UpdateBanner? _updateBanner;

    /// <summary>Применённая раскладка: <c>null</c> — ещё не выбрана.</summary>
    private LayoutMode? _layout;

    /// <summary>Стартовое меню партии ещё не решено: до первого измерения размер вида неизвестен.</summary>
    private bool _startMenuPending = true;

    /// <summary>Создаёт оболочку с настройками из файла и оценкой сети, заданной головой.</summary>
    /// <remarks>
    /// Стартовое состояние у обеих голов одно: приложение открывается на разделе «Партия», экрана
    /// настроек нет. Шторку меню партии при запуске получает только мобильная раскладка — её
    /// выбирает размер вида, а не платформа (<see cref="ApplyLayout"/>).
    /// </remarks>
    public ShellView()
        : this(ShellViewModel.Create(
            SettingsStore.Load(),
            global::GoEngine.App.App.Evaluator,
            theme: ThemeStore.Load(),
            progress: ProgressStore.Load()))
    {
    }

    /// <summary>Создаёт оболочку с готовой моделью.</summary>
    /// <param name="shell">Модель оболочки: модели режимов, настройки партии и модель обновления.</param>
    public ShellView(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        Shell = shell;

        AvaloniaXamlLoader.Load(this);

        DataContext = shell;

        _content = this.FindControl<Panel>("ModeContent");
        _gameButton = this.FindControl<ToggleButton>("GameModeButton");
        _problemButton = this.FindControl<ToggleButton>("ProblemModeButton");
        _lessonButton = this.FindControl<ToggleButton>("LessonModeButton");
        _studySwitch = this.FindControl<Border>("StudySwitch");
        _studyLessonsButton = this.FindControl<ToggleButton>("StudyLessonsButton");
        _studyReviewsButton = this.FindControl<ToggleButton>("StudyReviewsButton");
        _desktopBar = this.FindControl<Border>("DesktopBar");
        _mobileNav = this.FindControl<Border>("MobileNav");
        _aboutOverlay = this.FindControl<Border>("AboutOverlay");
        _glossaryOverlay = this.FindControl<Border>("GlossaryOverlay");
        _exitOverlay = this.FindControl<Border>("ExitOverlay");
        _aboutArea = this.FindControl<AboutView>("AboutArea");

        if (_content is not null)
        {
            // Модели берутся у оболочки: у видов партии, задач и меню они те же, что в модели оболочки.
            GameArea = new BoardView(shell.Settings, global::GoEngine.App.App.Evaluator, shell.Game);
            ProblemArea = new ProblemView(shell.Problems);
            LessonArea = new LessonView(shell.Lessons);
            ReviewArea = new ReviewView(shell.Reviews);
            MenuArea = new AppMenuView(shell);

            _content.Children.Add(GameArea);
            _content.Children.Add(ProblemArea);
            _content.Children.Add(LessonArea);
            _content.Children.Add(ReviewArea);
            _content.Children.Add(MenuArea);
        }

        if (this.FindControl<Panel>("UpdateHost") is { } updateHost)
        {
            // Приглашение обновления — общее для обеих раскладок: карточка показывает состояние
            // той же модели, что и экран «О программе», поэтому проверка в них одна.
            _updateBanner = new UpdateBanner(shell.Updates);

            updateHost.Children.Add(_updateBanner);
        }

        if (_gameButton is not null)
        {
            _gameButton.Click += OnGameClick;
        }

        if (_problemButton is not null)
        {
            _problemButton.Click += OnProblemClick;
        }

        if (_lessonButton is not null)
        {
            _lessonButton.Click += OnLessonClick;
        }

        WireButton("NavGameButton", OnNavGameClick);
        WireButton("NavProblemButton", OnNavProblemClick);
        WireButton("NavLessonButton", OnNavLessonClick);
        WireButton("NavMenuButton", OnNavMenuClick);
        WireButton("AboutBackButton", OnAboutBackClick);
        WireButton("GlossaryBackButton", OnGlossaryBackClick);
        WireButton("ExitCancelButton", OnExitCancelClick);
        WireButton("ExitConfirmButton", OnExitConfirmClick);

        if (MenuArea is not null)
        {
            // «О программе» и выход — действия раздела «Меню»: показывает их оболочка, потому что
            // оба перекрывают её целиком, а не только содержимое раздела.
            MenuArea.AboutRequested += OnAboutRequested;
            MenuArea.ExitRequested += OnExitRequested;
        }

        if (LessonArea is not null)
        {
            // Словарь терминов — отдельный экран раздела «Обучение»: его показывает оболочка,
            // как «О программе» у раздела «Меню».
            LessonArea.GlossaryRequested += OnGlossaryRequested;
        }

        if (ReviewArea is not null)
        {
            ReviewArea.GlossaryRequested += OnGlossaryRequested;
        }

        WireButton("StudyLessonsButton", OnStudyLessonsClick);
        WireButton("StudyReviewsButton", OnStudyReviewsClick);

        // Смену оформления применяет приложение: оболочка только сообщает о выборе игрока.
        shell.ThemeChanged += OnThemeChanged;

        // Прогресс обучения записывает вид: модели уроков и разбора только сообщают об изменениях.
        shell.ProgressChanged += OnStudyProgressChanged;

        shell.PropertyChanged += OnShellPropertyChanged;
        SizeChanged += OnViewSizeChanged;

        // Раскладка применяется здесь, а стартовое меню партии открывается в ApplyLayout: до первого
        // измерения Bounds пуст, и решать по нулю нельзя (см. комментарий там).
        UpdateMode();
        ApplyLayout(Bounds.Width, Bounds.Height);

        // Проверка обновления при первом показе оболочки: приложение offline-first, поэтому она
        // идёт в фоне и при отказе молчит — игрок в это время уже может ходить по доске.
        _ = Shell.CheckUpdatesOnStartAsync();
    }

    /// <summary>Модель оболочки.</summary>
    public ShellViewModel Shell { get; }

    /// <summary>Вид партии: его же использует меню окна.</summary>
    public BoardView? GameArea { get; }

    /// <summary>Вид задач.</summary>
    public ProblemView? ProblemArea { get; }

    /// <summary>Вид обучения: уроки шаг за шагом.</summary>
    public LessonView? LessonArea { get; }

    /// <summary>Вид разбора партий: практика раздела «Обучение».</summary>
    public ReviewView? ReviewArea { get; }

    /// <summary>Раздел общего меню: режим, «О программе» и выход.</summary>
    public AppMenuView? MenuArea { get; }

    /// <summary>Показан ли сейчас экран «О программе».</summary>
    public bool IsAboutShown => _aboutOverlay?.IsVisible == true;

    /// <summary>Показано ли подтверждение выхода.</summary>
    public bool IsExitConfirmShown => _exitOverlay?.IsVisible == true;

    /// <summary>Показан ли словарь терминов.</summary>
    public bool IsGlossaryShown => _glossaryOverlay?.IsVisible == true;

    /// <summary>Показывает «О программе» вложенным экраном раздела «Меню».</summary>
    /// <remarks>
    /// Сведения перечитываются при каждом показе: экран создан один раз при запуске, а модели
    /// к тому моменту могли быть ещё не загружены.
    /// </remarks>
    public void ShowAbout()
    {
        _aboutArea?.Refresh();

        ShowOverlay(_aboutOverlay);
    }

    /// <summary>Закрывает экран «О программе»: игрок возвращается в раздел «Меню».</summary>
    public void HideAbout() => HideOverlay(_aboutOverlay);

    /// <summary>Показывает словарь терминов вложенным экраном раздела «Обучение».</summary>
    public void ShowGlossary() => ShowOverlay(_glossaryOverlay);

    /// <summary>Закрывает словарь: игрок возвращается к уроку.</summary>
    public void HideGlossary() => HideOverlay(_glossaryOverlay);

    /// <summary>Показывает полноэкранный слой и убирает из кадра содержимое раздела.</summary>
    /// <param name="overlay">Слой или <c>null</c>, если его нет в разметке.</param>
    /// <remarks>
    /// Пока открыт «О программе» или словарь, содержимое раздела под ними не рисуется: на телефоне
    /// это заметная доля кадра, а на экране 120 Гц лишняя работа видна как рывки прокрутки
    /// (замечание пользователя 2026-10-07). Раскладка при этом не меняется: оба слоя и содержимое
    /// стоят в одной строке сетки.
    /// </remarks>
    private void ShowOverlay(Border? overlay)
    {
        if (overlay is not null)
        {
            overlay.IsVisible = true;
        }

        UpdateContentVisibility();
    }

    /// <summary>Прячет полноэкранный слой и возвращает содержимое раздела в кадр.</summary>
    /// <param name="overlay">Слой или <c>null</c>, если его нет в разметке.</param>
    private void HideOverlay(Border? overlay)
    {
        if (overlay is not null)
        {
            overlay.IsVisible = false;
        }

        UpdateContentVisibility();
    }

    /// <summary>Показывает содержимое раздела только тогда, когда его не закрывает полный экран.</summary>
    private void UpdateContentVisibility()
    {
        if (_content is not null)
        {
            _content.IsVisible = !IsAboutShown && !IsGlossaryShown;
        }
    }

    /// <summary>Показывает подтверждение выхода из игры.</summary>
    public void ShowExitConfirm()
    {
        if (_exitOverlay is not null)
        {
            _exitOverlay.IsVisible = true;
        }
    }

    /// <summary>Закрывает подтверждение выхода: игра продолжается.</summary>
    public void HideExitConfirm()
    {
        if (_exitOverlay is not null)
        {
            _exitOverlay.IsVisible = false;
        }
    }

    /// <summary>Показывает режим партии.</summary>
    /// <param name="sender">Кнопка режима.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnGameClick(object? sender, RoutedEventArgs e) => Shell.ShowGame();

    /// <summary>Показывает режим задач.</summary>
    /// <param name="sender">Кнопка режима.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnProblemClick(object? sender, RoutedEventArgs e) => Shell.ShowProblems();

    /// <summary>Показывает режим обучения.</summary>
    /// <param name="sender">Кнопка режима.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLessonClick(object? sender, RoutedEventArgs e) => Shell.ShowLessons();

    /// <summary>Открывает в обучении уроки.</summary>
    /// <param name="sender">Кнопка «Уроки».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnStudyLessonsClick(object? sender, RoutedEventArgs e)
    {
        Shell.ShowLessons();
        Shell.SelectStudy(reviews: false);
    }

    /// <summary>Открывает в обучении разбор партий: практику после уроков.</summary>
    /// <param name="sender">Кнопка «Партии».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnStudyReviewsClick(object? sender, RoutedEventArgs e)
    {
        Shell.ShowLessons();
        Shell.SelectStudy(reviews: true);
    }

    /// <summary>Открывает раздел партии из нижней навигации.</summary>
    /// <param name="sender">Кнопка раздела.</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Уход из раздела закрывает то, что партии не принадлежит: шторку меню партии и «О программе».
    /// </remarks>
    private void OnNavGameClick(object? sender, RoutedEventArgs e)
    {
        HideAbout();
        HideGlossary();
        GameArea?.CloseMenu();
        Shell.ShowGame();
    }

    /// <summary>Открывает раздел задач из нижней навигации.</summary>
    /// <param name="sender">Кнопка раздела.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNavProblemClick(object? sender, RoutedEventArgs e)
    {
        HideAbout();
        HideGlossary();
        GameArea?.CloseSettingsScreen();
        GameArea?.CloseMenu();
        Shell.ShowProblems();
    }

    /// <summary>Открывает раздел обучения из нижней навигации.</summary>
    /// <param name="sender">Кнопка раздела.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNavLessonClick(object? sender, RoutedEventArgs e)
    {
        HideAbout();
        HideGlossary();
        GameArea?.CloseSettingsScreen();
        GameArea?.CloseMenu();
        Shell.ShowLessons();
    }

    /// <summary>Открывает раздел общего меню.</summary>
    /// <param name="sender">Кнопка «Меню».</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Раздел занимает место содержимого: партия и задачи остаются в памяти и не сбрасываются,
    /// а «О программе» и подтверждение выхода закрываются, чтобы раздел открывался с начала.
    /// </remarks>
    private void OnNavMenuClick(object? sender, RoutedEventArgs e)
    {
        HideAbout();
        HideGlossary();
        HideExitConfirm();
        GameArea?.CloseMenu();
        Shell.ShowMenu();
    }

    /// <summary>Открывает «О программе» из раздела «Меню».</summary>
    /// <param name="sender">Вид общего меню.</param>
    /// <param name="e">Признак запроса.</param>
    private void OnAboutRequested(object? sender, EventArgs e) => ShowAbout();

    /// <summary>Закрывает «О программе» и возвращает раздел «Меню».</summary>
    /// <param name="sender">Кнопка «Назад».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnAboutBackClick(object? sender, RoutedEventArgs e) => HideAbout();

    /// <summary>Открывает словарь терминов из раздела «Обучение».</summary>
    /// <param name="sender">Вид обучения.</param>
    /// <param name="e">Признак запроса.</param>
    private void OnGlossaryRequested(object? sender, EventArgs e) => ShowGlossary();

    /// <summary>Закрывает словарь и возвращает урок.</summary>
    /// <param name="sender">Кнопка «Назад».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnGlossaryBackClick(object? sender, RoutedEventArgs e) => HideGlossary();

    /// <summary>Спрашивает подтверждение выхода из игры.</summary>
    /// <param name="sender">Вид общего меню.</param>
    /// <param name="e">Признак запроса.</param>
    private void OnExitRequested(object? sender, EventArgs e) => ShowExitConfirm();

    /// <summary>Отменяет выход: игра продолжается, раздел «Меню» остаётся открытым.</summary>
    /// <param name="sender">Кнопка «Отмена».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnExitCancelClick(object? sender, RoutedEventArgs e) => HideExitConfirm();

    /// <summary>Закрывает программу средствами платформы.</summary>
    /// <param name="sender">Кнопка «Выйти».</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Чем именно закрывается программа, решает голова: у настольной системы это остановка цикла
    /// сообщений, у Android — завершение задачи приложения (<see cref="global::GoEngine.App.App.RequestExit"/>).
    /// </remarks>
    private void OnExitConfirmClick(object? sender, RoutedEventArgs e) => global::GoEngine.App.App.RequestExit();

    /// <summary>Применяет выбранное оформление и запоминает выбор.</summary>
    /// <param name="sender">Модель оболочки.</param>
    /// <param name="choice">Новая тема.</param>
    /// <remarks>
    /// Тема применяется сразу — перезапуск не нужен; отказ записи в файл не мешает играть:
    /// выбор уже действует, а файл перепишется при следующей смене темы.
    /// </remarks>
    private void OnThemeChanged(object? sender, ThemeChoice choice)
    {
        _ = sender;

        global::GoEngine.App.App.ApplyTheme(choice);
        _ = ThemeStore.Save(choice);
    }

    /// <summary>Записывает прогресс обучения в файл.</summary>
    /// <remarks>
    /// Отказ записи не мешает учиться: прогресс уже в памяти, а в лог попадает причина — тем же
    /// способом обрабатывается выбор оформления.
    /// </remarks>
    private void OnStudyProgressChanged(object? sender, EventArgs e)
    {
        _ = sender;

        var saved = ProgressStore.Save(Shell.Progress);

        if (!saved.IsSuccess)
        {
            AppLogMessages.ProgressSaveFailed(AppLog.For<ShellView>(), saved.Error ?? "неизвестная причина");
        }
    }

    /// <summary>Обновляет видимость разделов, когда модель сообщает о переключении.</summary>
    /// <param name="sender">Модель оболочки.</param>
    /// <param name="e">Имя изменившегося свойства.</param>
    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsGameSection)
            or nameof(ShellViewModel.IsProblemSection)
            or nameof(ShellViewModel.IsLessonSection)
            or nameof(ShellViewModel.IsMenuSection))
        {
            UpdateMode();
        }
    }

    /// <summary>Перестраивает раскладку при изменении размера вида.</summary>
    /// <param name="sender">Вид.</param>
    /// <param name="e">Новый размер.</param>
    private void OnViewSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ApplyLayout(e.NewSize.Width, e.NewSize.Height);

    /// <summary>Показывает настольную или мобильную оболочку по ширине вида.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    /// <remarks>
    /// Содержимое у раскладок общее: меняется только обрамление — строка режимов сверху
    /// или нижняя навигация. Поэтому переключение раскладки не пересоздаёт виды режимов
    /// и не сбрасывает партию.
    /// </remarks>
    private void ApplyLayout(double width, double height)
    {
        var layout = BoardLayoutRules.DecideLayout(width, height);

        // Стартовое меню партии открывается только в мобильной раскладке: на телефоне это
        // единственный вход к действиям партии, а настольному окну шторка поверх доски мешала бы —
        // у него есть меню-бар и мышь (замечание пользователя 2026-10-03: «окно меню должно быть
        // первым активным окном над партией» — речь про Android). Решаем по настоящему размеру:
        // до первого измерения Bounds пуст, а DecideLayout(0, 0) отвечает «настольная» — по нулю
        // решение было бы неверным. Показываем после UpdateMode: раздел «Партия» уже выбран,
        // и вид партии видим.
        if (_startMenuPending && width > 0 && height > 0)
        {
            _startMenuPending = false;

            if (layout == LayoutMode.Mobile)
            {
                GameArea?.ShowMenu();
            }
        }

        if (_layout == layout)
        {
            return;
        }

        _layout = layout;

        if (_desktopBar is not null)
        {
            _desktopBar.IsVisible = layout == LayoutMode.Desktop;
        }

        if (_mobileNav is not null)
        {
            _mobileNav.IsVisible = layout == LayoutMode.Mobile;
        }

        if (_updateBanner is not null)
        {
            // На телефоне кнопки приглашения крупнее: цель нажатия там — палец, а не курсор.
            _updateBanner.IsTouch = layout == LayoutMode.Mobile;
        }
    }

    /// <summary>Показывает выбранный раздел и приводит кнопки в согласованное состояние.</summary>
    private void UpdateMode()
    {
        if (GameArea is not null)
        {
            GameArea.IsVisible = Shell.IsGameSection;
        }

        if (ProblemArea is not null)
        {
            ProblemArea.IsVisible = Shell.IsProblemSection;
        }

        // В обучении показывается то, что выбрано переключателем: уроки или разбор партий.
        if (LessonArea is not null)
        {
            LessonArea.IsVisible = Shell.IsLessonSection && !Shell.IsReviewMode;
        }

        if (ReviewArea is not null)
        {
            ReviewArea.IsVisible = Shell.IsLessonSection && Shell.IsReviewMode;
        }

        if (_studySwitch is not null)
        {
            _studySwitch.IsVisible = Shell.IsLessonSection;
        }

        if (_studyLessonsButton is not null)
        {
            _studyLessonsButton.IsChecked = Shell.IsLessonSection && !Shell.IsReviewMode;
        }

        if (_studyReviewsButton is not null)
        {
            _studyReviewsButton.IsChecked = Shell.IsLessonSection && Shell.IsReviewMode;
        }

        if (MenuArea is not null)
        {
            MenuArea.IsVisible = Shell.IsMenuSection;
        }

        if (_gameButton is not null)
        {
            _gameButton.IsChecked = Shell.IsGameMode;
        }

        if (_problemButton is not null)
        {
            _problemButton.IsChecked = Shell.IsProblemMode;
        }

        if (_lessonButton is not null)
        {
            _lessonButton.IsChecked = Shell.IsLessonMode;
        }
    }

    /// <summary>Подписывается на системную кнопку «Назад», пока вид показан.</summary>
    /// <param name="e">Признак подключения к дереву видов.</param>
    /// <remarks>
    /// На телефоне «Назад» — системный жест и кнопка: он обязан закрывать то, что открыто поверх,
    /// а не выходить из приложения сразу (DECISIONS.md, D-075). Верхний уровень у вида появляется
    /// только при подключении к дереву — в конструкторе его ещё нет.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.BackRequested -= OnBackRequested;
            top.BackRequested += OnBackRequested;
        }
    }

    /// <summary>Снимает подписку на системную кнопку «Назад».</summary>
    /// <param name="e">Признак отключения от дерева видов.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.BackRequested -= OnBackRequested;
        }

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Закрывает верхний открытый слой: «Назад» идёт снаружи внутрь.</summary>
    /// <param name="sender">Верхний уровень.</param>
    /// <param name="e">Событие запроса.</param>
    private void OnBackRequested(object? sender, RoutedEventArgs e) => e.Handled = HandleBack();

    /// <summary>Закрывает верхний открытый слой и говорит, был ли он.</summary>
    /// <returns><c>true</c> — «Назад» обработан видом; <c>false</c> — закрывать нечего.</returns>
    /// <remarks>
    /// <para>
    /// Порядок обратный открытию: подтверждение выхода, «О программе», шторка меню партии, экран
    /// «Настройка партии» и только потом раздел «Меню» уступает партии. Если закрывать нечего,
    /// событие остаётся необработанным — им распоряжается система (выход из приложения).
    /// </para>
    /// <para>
    /// Отдельным методом, а не телом обработчика: оконной платформы в тестах нет, верхнего уровня
    /// тоже, а порядок «Назад» обязан проверяться тестом, а не живым прогоном
    /// (<c>AGENTS.md</c>, п. 14).
    /// </para>
    /// </remarks>
    public bool HandleBack()
    {
        if (IsExitConfirmShown)
        {
            HideExitConfirm();

            return true;
        }

        if (IsGlossaryShown)
        {
            HideGlossary();

            return true;
        }

        if (IsAboutShown)
        {
            HideAbout();

            return true;
        }

        if (GameArea?.IsMenuShown == true)
        {
            GameArea.CloseMenu();

            return true;
        }

        if (GameArea?.IsSettingsShown == true)
        {
            GameArea.CloseSettingsScreen();

            return true;
        }

        if (Shell.IsMenuSection)
        {
            // «Меню» — не партия и не задачи: возвращаем игрока в партию.
            Shell.ShowGame();

            return true;
        }

        return false;
    }

    /// <summary>Подписывает кнопку на обработчик нажатия.</summary>
    /// <param name="name">Имя кнопки в разметке.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void WireButton(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += handler;
        }
    }
}
