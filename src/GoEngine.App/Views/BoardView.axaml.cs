using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GoEngine.AI;
using GoEngine.App.Controls;
using GoEngine.App.Rendering;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.App.ViewModels;
using GoEngine.Core;
using Microsoft.Extensions.Logging;

namespace GoEngine.App.Views;

/// <summary>Доска партии и панель управления — общая часть окна и мобильного вида.</summary>
/// <remarks>
/// Вид связывает доску с моделью представления: щелчок по доске превращается в ход игрока,
/// после которого отвечает AI. Здесь же всё, что нужно и без окна: пас, отмена, возврат, новая
/// партия, выбор доски и уровня, сохранение и загрузка партии, настройки.
/// Раскладок две, выбор — чистая функция <see cref="BoardLayoutRules.DecideLayout"/> по ширине вида.
/// Шире 760 точек — прежний настольный вид: доска слева, панель управления справа. Уже — мобильный:
/// доска занимает экран, действия стоят компактной панелью поверх доски, состояние и подробности
/// собраны в шторке снизу, а настройки открываются отдельным экраном, а не окном-диалогом.
/// На настольной системе настройки по-прежнему показывает окно (<see cref="SettingsWindow"/>,
/// подписка на <see cref="SettingsRequested"/>).
/// </remarks>
public sealed partial class BoardView : UserControl
{
    /// <summary>Ширина настольной панели управления: та же, что в разметке.</summary>
    private const double DesktopPanelWidth = 360;

    /// <summary>Сколько живёт сообщение о сохранении, загрузке или отказе.</summary>
    /// <remarks>
    /// Строка подсказки исчезает сама: раньше она висела до конца сеанса и на телефоне
    /// занимала место в шторке даже после того, как партия давно загрузилась (жалоба 2026-09-30).
    /// </remarks>
    private static readonly TimeSpan HintLifetime = TimeSpan.FromSeconds(8);

    /// <summary>Как часто обновляется строка времени партии.</summary>
    /// <remarks>Раз в секунду — по секундной стрелке: чаще незачем, время и так показано до секунд.</remarks>
    private static readonly TimeSpan ClockTickInterval = TimeSpan.FromSeconds(1);

    /// <summary>Отступ мобильной доски от краёв отведённого места.</summary>
    private const double MobileBoardMargin = 8;

    /// <summary>Отступ панели действий от доски: тот же, что в разметке.</summary>
    private const double MobileActionMargin = 8;

    /// <summary>Высота, ниже которой мобильная раскладка становится компактной.</summary>
    /// <remarks>
    /// Телефон в ландшафте: высоты мало, и строку состояния вместе со строками краткого состояния
    /// приходится убирать — иначе доска превратилась бы в полоску. Подробности остаются
    /// в развёрнутой шторке.
    /// </remarks>
    private const double CompactHeight = 460;

    /// <summary>Подпись кнопки до первого хода: партия ещё не начата.</summary>
    private const string StartGameLabel = "Начать партию";

    /// <summary>Подпись кнопки после первого хода: партия идёт, кнопка начинает новую.</summary>
    private const string NewGameLabel = "Новая партия";

    /// <summary>Подпись кнопки часов, когда партия идёт.</summary>
    private const string PauseLabel = "Пауза";

    /// <summary>Подпись кнопки часов, когда партия стоит на паузе.</summary>
    private const string ResumeLabel = "Дальше";

    /// <summary>Состояние партии на паузе словами: остановившиеся цифры об этом не скажут.</summary>
    private const string PausedHeadline = "Пауза";

    /// <summary>Пояснение к паузе: почему щелчок по доске не играет ход.</summary>
    private const string PausedDetail = "Ходы не принимаются";

    /// <summary>Слова о паузе рядом со временем: время стоит, и это должно быть видно.</summary>
    /// <remarks>
    /// Состояние «партия не начата» в строке времени больше не пишется: подпись переносилась
    /// на две строки в панели 320 точек, а состояние видно по строке статуса и кнопке старта.
    /// </remarks>
    private const string PausedMark = "пауза";

    /// <summary>Ответ на ход, сделанный на паузе: ход не играется, и игрок должен понять почему.</summary>
    private const string PausedHint = "Партия на паузе: нажмите «Продолжить».";

    /// <summary>Логгер вида: сюда попадают сохранение и загрузка партии с их отказами.</summary>
    private readonly ILogger _log = AppLog.For<BoardView>();

    private readonly BoardControl? _boardControl;
    private readonly Grid? _layout;
    private readonly Grid? _mobileLayout;
    private readonly Border? _desktopBoardHost;
    private readonly Border? _mobileBoardHost;
    private readonly Border? _overlay;
    private readonly SettingsView? _settingsView;
    private readonly TextBlock? _hint;
    private readonly TextBlock? _mobileHint;
    private readonly TextBlock? _mobileTurnText;
    private readonly TextBlock? _mobileMoveText;
    private readonly Ellipse? _mobileTurnBlack;
    private readonly Ellipse? _mobileTurnWhite;
    private readonly Border? _mobileStatus;
    private readonly Border? _mobileActionBar;
    private readonly Border? _menuOverlay;
    private readonly Panel? _mobileBoardRow;
    private readonly Border? _sheet;
    private readonly TextBlock? _sheetSummary;
    private readonly TextBlock? _sheetSummaryDetails;
    private readonly TextBlock? _sheetChevron;
    private readonly ScrollViewer? _sheetDetails;
    private readonly StackPanel? _sheetSummaryPanel;
    private readonly TextBlock? _desktopPrisoners;
    private readonly TextBlock? _desktopScore;
    private readonly TextBlock? _desktopStatus;
    private readonly TextBlock? _mobileStatusText;
    private readonly TextBlock? _desktopScoreSystems;
    private readonly TextBlock? _mobileScoreDetail;
    private readonly TextBlock? _desktopBreakdown;
    private readonly TextBlock? _mobileBreakdown;
    private readonly TextBlock? _desktopStats;
    private readonly TextBlock? _mobileStats;
    private readonly TextBlock? _desktopLevelHint;
    private readonly StackPanel? _desktopDetails;
    private readonly ToggleButton? _desktopDetailsButton;
    private readonly Button? _desktopNewGame;
    private readonly Button? _menuNewGame;
    private readonly TextBlock? _desktopClock;
    private readonly TextBlock? _mobileClock;
    private readonly Button? _desktopPause;
    private readonly Button? _mobilePause;

    /// <summary>Баннер итога партии: один на обе раскладки.</summary>
    /// <remarks>
    /// Баннер стоит оверлеем поверх вида и по центру экрана, поэтому переезжать между раскладками
    /// ему не нужно — в отличие от доски. Раскладка меняет только размер кнопок
    /// (<see cref="GameResultBanner.IsTouch"/>): на телефоне в карточку попадают пальцем.
    /// </remarks>
    private readonly GameResultBanner? _resultBanner;

    /// <summary>Таймер, по которому исчезает подсказка.</summary>
    private DispatcherTimer? _hintTimer;

    /// <summary>Таймер строки времени партии: заводится только у идущей партии.</summary>
    /// <remarks>Создаётся лениво: у стоящих часов таймер не нужен, а закрытый вид его отпускает
    /// (<see cref="OnDetachedFromVisualTree"/>), иначе таймер держал бы вид и тикал впустую.</remarks>
    private DispatcherTimer? _clockTimer;

    /// <summary>Снимок настроек на входе в экран: по нему «Отмена» возвращает применённое сразу.</summary>
    /// <remarks>
    /// Значения для показа берутся у модели представления (<c>ViewModel.Settings</c>) — она один
    /// источник правды, и своих копий настроек вид больше не держит (жалоба 2026-10-03
    /// «не синхронизирован уровень AI»). Снимок нужен только затем, чтобы отменить то, что
    /// действует немедленно: правило подсчёта и звук.
    /// </remarks>
    private AppSettings _settingsOnOpen = AppSettings.Default;

    /// <summary>Состояние списка размеров: программное обновление не считается выбором игрока.</summary>
    private readonly ComboState _sizeCombo = new();

    /// <summary>Состояние списка уровней: программное обновление не считается выбором игрока.</summary>
    private readonly ComboState _levelCombo = new();

    /// <summary>Текущая раскладка: <c>null</c> — ещё не выбрана.</summary>
    private LayoutMode? _layoutMode;

    /// <summary>Шторка развёрнута: подробности показаны.</summary>
    private bool _detailsExpanded;

    /// <summary>Экран настроек открыт перед новой партией: «Готово» начнёт её.</summary>
    /// <remarks>
    /// Разница между «Новой партией» в меню (закрытие начинает партию) и разделом «Настройки»
    /// (обычный просмотр и правка, партия не пересоздаётся) держится в одном месте — здесь.
    /// Иначе кнопка «Готово» вела бы себя по-разному в двух кусках кода.
    /// </remarks>
    private bool _startGameAfterSettings;

    /// <summary>Создаёт вид с настройками из файла и оценкой сети, заданной головой.</summary>
    public BoardView() : this(SettingsStore.Load(), global::GoEngine.App.App.Evaluator)
    {
    }

    /// <summary>Создаёт вид с готовыми настройками.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <param name="evaluator">Оценка позиции нейросетью для уровней Дан.</param>
    /// <param name="viewModel">Готовая модель партии; <c>null</c> — создать свою.</param>
    /// <remarks>
    /// Готовая модель нужна оболочке режимов: она владеет моделью партии, поэтому переключение
    /// между партией и задачами не сбрасывает партию (<c>DECISIONS.md</c>, D-061). Без неё вид
    /// создаёт модель сам — так его по-прежнему можно собрать одним конструктором.
    /// </remarks>
    public BoardView(AppSettings settings, IPositionEvaluator? evaluator = null, MainViewModel? viewModel = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Настройка звука применяется сразу: до первого хода она уже должна действовать.
        StoneSoundPlayer.SoundEnabled = settings.SoundEnabled;

        AvaloniaXamlLoader.Load(this);

        ViewModel = viewModel ?? new MainViewModel(settings, Random.Shared, evaluator);
        DataContext = ViewModel;

        _boardControl = this.FindControl<BoardControl>("Board");
        _layout = this.FindControl<Grid>("Layout");
        _mobileLayout = this.FindControl<Grid>("MobileLayout");
        _desktopBoardHost = this.FindControl<Border>("DesktopBoardHost");
        _mobileBoardHost = this.FindControl<Border>("MobileBoardHost");
        _overlay = this.FindControl<Border>("SettingsOverlay");
        _settingsView = this.FindControl<SettingsView>("SettingsArea");
        _hint = this.FindControl<TextBlock>("Hint");
        _mobileHint = this.FindControl<TextBlock>("MobileHint");
        _mobileTurnText = this.FindControl<TextBlock>("MobileTurnText");
        _mobileMoveText = this.FindControl<TextBlock>("MobileMoveText");
        _mobileTurnBlack = this.FindControl<Ellipse>("MobileTurnBlack");
        _mobileTurnWhite = this.FindControl<Ellipse>("MobileTurnWhite");
        _mobileStatus = this.FindControl<Border>("MobileStatus");
        _mobileActionBar = this.FindControl<Border>("MobileActionBar");
        _menuOverlay = this.FindControl<Border>("MenuOverlay");
        _mobileBoardRow = this.FindControl<Panel>("MobileBoardRow");
        _sheet = this.FindControl<Border>("Sheet");
        _sheetSummary = this.FindControl<TextBlock>("SheetSummary");
        _sheetSummaryDetails = this.FindControl<TextBlock>("SheetSummaryDetails");
        _sheetChevron = this.FindControl<TextBlock>("SheetChevron");
        _sheetDetails = this.FindControl<ScrollViewer>("SheetDetails");
        _sheetSummaryPanel = this.FindControl<StackPanel>("SheetSummaryPanel");
        _desktopPrisoners = this.FindControl<TextBlock>("DesktopPrisonersText");
        _desktopScore = this.FindControl<TextBlock>("DesktopScoreText");
        _desktopStatus = this.FindControl<TextBlock>("DesktopStatusText");
        _mobileStatusText = this.FindControl<TextBlock>("MobileStatusText");
        _desktopScoreSystems = this.FindControl<TextBlock>("DesktopScoreSystemsText");
        _mobileScoreDetail = this.FindControl<TextBlock>("MobileScoreDetailText");
        _desktopBreakdown = this.FindControl<TextBlock>("DesktopBreakdownText");
        _mobileBreakdown = this.FindControl<TextBlock>("MobileBreakdownText");
        _desktopStats = this.FindControl<TextBlock>("DesktopStatsText");
        _mobileStats = this.FindControl<TextBlock>("MobileStatsText");
        _desktopLevelHint = this.FindControl<TextBlock>("DesktopLevelHintText");
        _desktopDetails = this.FindControl<StackPanel>("DesktopDetailsPanel");
        _desktopDetailsButton = this.FindControl<ToggleButton>("DesktopDetailsButton");
        _desktopNewGame = this.FindControl<Button>("NewGameButton");
        _menuNewGame = this.FindControl<Button>("MenuNewGameButton");
        _desktopClock = this.FindControl<TextBlock>("DesktopClockText");
        _mobileClock = this.FindControl<TextBlock>("MobileClockText");
        _desktopPause = this.FindControl<Button>("DesktopPauseButton");
        _mobilePause = this.FindControl<Button>("MobilePauseButton");
        _resultBanner = this.FindControl<GameResultBanner>("ResultBanner");

        // Баннер создаёт разметка без модели: подключаем его к партии здесь, иначе итог партии
        // не доходит до экрана, а кнопки карточки мертвы (D-065 §6).
        _resultBanner?.Attach(ViewModel);

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
            _boardControl.SizeChanged += OnBoardSizeChanged;
        }

        if (_settingsView is not null)
        {
            _settingsView.Accepted += OnSettingsAccepted;
            _settingsView.Cancelled += OnSettingsCancelled;

            // Правило подсчёта — не свойство партии: оно вступает в силу сразу, поэтому вид
            // партии применяет его к текущей игре, не дожидаясь новой (жалоба 2026-10-03).
            // Подписка одна на оба хозяина настроек — у самого вида (H1).
            SettingsView.ApplyScoringRuleOnChange(_settingsView, ViewModel);
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        WireButton("PassButton", OnPassClick);
        WireButton("UndoButton", OnUndoClick);
        WireButton("RedoButton", OnRedoClick);
        WireButton("NewGameButton", OnNewGameClick);
        WireButton("SettingsButton", OnSettingsClick);
        WireButton("SaveButton", OnSaveClick);
        WireButton("LoadButton", OnLoadClick);

        // Часы партии: «Пауза» / «Продолжить» — одна кнопка на два состояния в каждой раскладке,
        // а «Отменить партию» прекращает партию целиком.
        WireButton("DesktopPauseButton", OnPauseResumeClick);
        WireButton("MobilePauseButton", OnPauseResumeClick);
        WireButton("DesktopAbortButton", OnAbortGameClick);

        // Кнопки мобильной раскладки делают то же самое: обработчики общие.
        WireButton("MobilePassButton", OnPassClick);
        WireButton("MobileUndoButton", OnUndoClick);
        WireButton("MobileRedoButton", OnRedoClick);
        WireButton("MobileSaveButton", OnSaveClick);
        WireButton("MobileLoadButton", OnLoadClick);
        WireButton("MobileSettingsButton", OnSettingsClick);
        WireButton("SheetToggleButton", OnSheetToggleClick);

        // Меню партии на телефоне: те же действия, что у кнопок настольной панели, — своих
        // обработчиков у пунктов меню нет, они лишь закрывают карточку перед действием.
        WireButton("MenuNewGameButton", OnMenuNewGameClick);
        WireButton("MenuAbortButton", OnMenuAbortClick);
        WireButton("MenuSaveButton", OnMenuSaveClick);
        WireButton("MenuLoadButton", OnMenuLoadClick);
        WireButton("MenuSettingsButton", OnMenuSettingsClick);
        WireButton("MenuCloseButton", OnMenuCloseClick);

        if (_desktopDetailsButton is not null)
        {
            _desktopDetailsButton.Click += OnDetailsToggleClick;
        }

        if (this.FindControl<ComboBox>("SizeBox") is { } sizeBox)
        {
            sizeBox.SelectionChanged += OnSizeChanged;
        }

        if (this.FindControl<ComboBox>("LevelBox") is { } levelBox)
        {
            levelBox.SelectionChanged += OnLevelChanged;
        }

        SizeChanged += OnViewSizeChanged;

        // Размеры мобильных частей меняются от содержимого и от раскрытия шторки: по ним
        // пересчитывается сторона доски.
        if (_mobileLayout is not null)
        {
            _mobileLayout.SizeChanged += OnMobilePartSizeChanged;
        }

        if (_mobileStatus is not null)
        {
            _mobileStatus.SizeChanged += OnMobilePartSizeChanged;
        }

        // Высота панели действий меняется от подписей кнопок и от меню «Ещё» — вместе с ней
        // меняется и высота строки доски. Подписка на обе части: по одной считается сторона,
        // по другой видно, что место доски стало другим. Без второй подписки доска оставалась
        // прежнего размера и заезжала под панель (жалоба пользователя 2026-10-02).
        if (_mobileActionBar is not null)
        {
            _mobileActionBar.SizeChanged += OnMobilePartSizeChanged;
        }

        if (_mobileBoardRow is not null)
        {
            _mobileBoardRow.SizeChanged += OnMobilePartSizeChanged;
        }

        if (_sheet is not null)
        {
            _sheet.SizeChanged += OnMobilePartSizeChanged;
        }

        SyncCombos();
        RefreshSummary();
        ApplyLayout(Bounds.Width, Bounds.Height);
    }

    /// <summary>Запрошены настройки: окно, если оно есть, показывает диалог.</summary>
    /// <remarks>Без подписчика (мобильный вид) настройки показываются поверх доски.</remarks>
    public event EventHandler? SettingsRequested;

    /// <summary>Экран настроек показан: оболочка подсвечивает раздел настроек.</summary>
    public event EventHandler? SettingsShown;

    /// <summary>Экран настроек закрыт: оболочка возвращает подсветку раздела партии.</summary>
    public event EventHandler? SettingsClosed;

    /// <summary>Модель представления партии.</summary>
    public MainViewModel ViewModel { get; }

    /// <summary>Показывает меню партии: карточку по центру экрана.</summary>
    /// <remarks>
    /// Меню открывает кнопка «Меню» в нижней навигации оболочки: на телефоне это единственный вход
    /// к «Начать партию», отмене партии, файлам и настройкам — в ряду действий они не помещались
    /// (замечание пользователя 2026-10-03). Действия меню те же, что у кнопок настольной панели.
    /// </remarks>
    public void ShowMenu()
    {
        if (_menuOverlay is not null)
        {
            _menuOverlay.IsVisible = true;
        }
    }

    /// <summary>Закрывает меню партии без действий.</summary>
    public void CloseMenu()
    {
        if (_menuOverlay is not null)
        {
            _menuOverlay.IsVisible = false;
        }
    }

    /// <summary>Открывает меню, если оно закрыто, и закрывает, если открыто.</summary>
    /// <remarks>Кнопка «Меню» в навигации — переключатель: повторное нажатие убирает карточку.</remarks>
    public void ToggleMenu()
    {
        if (_menuOverlay?.IsVisible == true)
        {
            CloseMenu();

            return;
        }

        ShowMenu();
    }

    /// <summary>Показывает настройки: в окне, если окно есть, иначе отдельным экраном.</summary>
    public void ShowSettings()
    {
        // Снимок на входе: «Отмена» вернёт по нему то, что действует немедленно, — правило
        // подсчёта и звук. Снимается до всех ветвей, чтобы он был свежим и для телефона,
        // и для настольного окна.
        _settingsOnOpen = ViewModel.Settings;

        // На телефоне настройки — отдельный экран, даже если окно умеет показать диалог:
        // диалог шириной 440 на экране 360 не помещается, а на Android окна нет вовсе.
        if (_layoutMode != LayoutMode.Mobile && SettingsRequested is not null)
        {
            SettingsRequested.Invoke(this, EventArgs.Empty);
            return;
        }

        // Значения для показа берутся у модели: она один источник правды, поэтому диалог
        // не может показать «25 кю», когда партия играет «10 кю» (жалоба 2026-10-03).
        _settingsView?.Initialize(_settingsOnOpen, ViewModel.HasModelFor);

        if (_overlay is not null)
        {
            _overlay.IsVisible = true;
        }

        SettingsShown?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Показывает настройки перед новой партией: «Готово» начнёт её.</summary>
    /// <remarks>
    /// Так действует «Новая партия» в меню: игрок сначала выбирает, чем играть, и только потом
    /// партия начинается (жалоба пользователя 2026-10-03). В настольной раскладке настройки
    /// показывает окно, и о закрытии решает оно — там признак не нужен.
    /// </remarks>
    public void ShowSettingsForNewGame()
    {
        _startGameAfterSettings = true;
        ShowSettings();

        if (_overlay?.IsVisible != true)
        {
            _startGameAfterSettings = false;
        }

        _settingsView?.SelectGameTab();
    }

    /// <summary>Закрывает экран настроек без изменений: переход в другой раздел — это отмена.</summary>
    /// <remarks>
    /// Нужно оболочке: переход в другой раздел нижней навигации закрывает настройки, а игрок
    /// их не сохранял. Значит, и применять нечего — работает та же отмена со снимком на входе.
    /// </remarks>
    public void CloseSettingsScreen() => CancelSettings();

    /// <summary>Спрашивает файл и записывает в него партию.</summary>
    /// <returns>Задача сохранения.</returns>
    /// <remarks>
    /// Пишем в поток, а не в путь: на телефоне у выбранного файла локального пути может не быть.
    /// </remarks>
    public async Task SaveGameAsync()
    {
        var file = await PickSaveFileAsync();

        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenWriteAsync();
            var saved = SgfStore.Save(ViewModel.ToSgfGame(), stream);

            if (saved.IsSuccess)
            {
                AppLogMessages.SgfSaved(_log, file.Name, ViewModel.ToSgfGame().Moves.Count);
            }
            else
            {
                AppLogMessages.SgfFailed(_log, "Сохранение партии", saved.Error ?? "причина неизвестна");
            }

            ShowHint(saved.IsSuccess
                ? $"Партия сохранена: {file.Name}"
                : saved.Error ?? "Не удалось сохранить партию.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            AppLogMessages.SgfFailed(_log, "Сохранение партии", exception.Message);
            ShowHint($"Не удалось сохранить партию: {exception.Message}");
        }
    }

    /// <summary>Спрашивает файл и загружает из него партию.</summary>
    /// <returns>Задача загрузки.</returns>
    public async Task LoadGameAsync()
    {
        var file = await PickOpenFileAsync();

        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenReadAsync();
            var loaded = SgfStore.Load(stream);

            if (!loaded.IsSuccess)
            {
                AppLogMessages.SgfFailed(_log, "Загрузка партии", loaded.Error ?? "причина неизвестна");
                ShowHint(loaded.Error ?? "Не удалось прочитать партию.");
                return;
            }

            _ = ViewModel.LoadGameAsync(loaded.Value);
            AppLogMessages.SgfLoaded(_log, file.Name, loaded.Value.Moves.Count);
            ShowHint($"Партия загружена: {file.Name}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            AppLogMessages.SgfFailed(_log, "Загрузка партии", exception.Message);
            ShowHint($"Не удалось прочитать партию: {exception.Message}");
        }
    }

    /// <summary>Выбирает файл для сохранения партии.</summary>
    /// <returns>Файл или <c>null</c>, если игрок отказался.</returns>
    private async Task<IStorageFile?> PickSaveFileAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } provider)
        {
            ShowHint("Система не даёт сохранить файл: нет доступа к выбору файлов.");
            return null;
        }

        return await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить партию",
            SuggestedFileName = "game",
            DefaultExtension = SgfStore.Extension,
            FileTypeChoices = [SgfFileType]
        });
    }

    /// <summary>Выбирает файл с партией для загрузки.</summary>
    /// <returns>Файл или <c>null</c>, если игрок отказался.</returns>
    private async Task<IStorageFile?> PickOpenFileAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } provider)
        {
            ShowHint("Система не даёт открыть файл: нет доступа к выбору файлов.");
            return null;
        }

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Загрузить партию",
            AllowMultiple = false,
            FileTypeFilter = [SgfFileType]
        });

        return files.Count > 0 ? files[0] : null;
    }

    /// <summary>Тип файла SGF для диалогов выбора файла.</summary>
    private static FilePickerFileType SgfFileType => new("Партия Go (SGF)")
    {
        Patterns = [$"*.{SgfStore.Extension}"]
    };

    /// <summary>Показывает сообщение под панелью и в мобильной шторке.</summary>
    /// <param name="message">Текст сообщения.</param>
    private void ShowHint(string message)
    {
        if (_hint is not null)
        {
            _hint.Text = message;
            _hint.IsVisible = true;
        }

        if (_mobileHint is not null)
        {
            // На телефоне сообщение видно и в свёрнутой шторке: системных диалогов там нет.
            _mobileHint.Text = message;
            _mobileHint.IsVisible = true;
        }

        ScheduleHintHide();
    }

    /// <summary>Убирает подсказку через несколько секунд: сообщение не должно висеть вечно.</summary>
    /// <remarks>
    /// Таймер один и перезапускается: два быстрых сообщения подряд не должны оставить первое
    /// на экране. Диспетчер берётся у самого вида — отдельного потока здесь не нужно.
    /// </remarks>
    private void ScheduleHintHide()
    {
        _hintTimer ??= new DispatcherTimer { Interval = HintLifetime };

        if (!_hintTimer.IsEnabled)
        {
            _hintTimer.Tick += OnHintTimeout;
        }

        _hintTimer.Stop();
        _hintTimer.Start();
    }

    /// <summary>Прячет подсказку по таймеру.</summary>
    /// <param name="sender">Таймер.</param>
    /// <param name="e">Событие таймера.</param>
    private void OnHintTimeout(object? sender, EventArgs e)
    {
        _hintTimer?.Stop();

        if (_hint is not null)
        {
            _hint.IsVisible = false;
        }

        if (_mobileHint is not null)
        {
            _mobileHint.IsVisible = false;
        }
    }

    /// <summary>Обновляет строку времени партии и подпись кнопки часов.</summary>
    /// <remarks>
    /// Обновляются только время и подпись кнопки: панель целиком по таймеру не пересобирается,
    /// иначе каждую секунду переписывались бы и состояние, и счёт.
    /// </remarks>
    private void RefreshClock()
    {
        // Время — это время: подпись «Время партии: 00:00» переносилась на две строки в панели
        // 320 точек, поэтому состояние партии называется отдельно (его показывает строка статуса),
        // а рядом со временем остаётся только пометка паузы.
        var desktop = ViewModel.IsGamePaused
            ? $"{ViewModel.ClockShortDisplay} · {PausedMark}"
            : ViewModel.ClockShortDisplay;

        SetText(_desktopClock, desktop);
        SetText(
            _mobileClock,
            ViewModel.IsGamePaused ? $"{ViewModel.ClockShortDisplay} · {PausedMark}" : ViewModel.ClockShortDisplay);

        // Кнопка часов одна на два состояния: подпись меняется вместе с состоянием партии,
        // а доступность задаёт привязка — кнопки, которая ничего не делает, в панели нет.
        var label = ViewModel.CanResumeClock ? ResumeLabel : PauseLabel;

        SetText(_desktopPause, label);
        SetText(_mobilePause, label);

        SyncClockTimer();
    }

    /// <summary>Заводит и останавливает таймер строки времени.</summary>
    /// <remarks>Таймер идёт только у идущей партии: у стоящих часов обновлять нечего.</remarks>
    private void SyncClockTimer()
    {
        if (!ViewModel.IsGameRunning)
        {
            _clockTimer?.Stop();

            return;
        }

        _clockTimer ??= CreateClockTimer();

        if (!_clockTimer.IsEnabled)
        {
            _clockTimer.Start();
        }
    }

    /// <summary>Создаёт таймер строки времени.</summary>
    /// <returns>Таймер с подписанным тиком.</returns>
    private DispatcherTimer CreateClockTimer()
    {
        var timer = new DispatcherTimer { Interval = ClockTickInterval };

        timer.Tick += OnClockTick;

        return timer;
    }

    /// <summary>Обновляет строку времени партии по таймеру.</summary>
    /// <param name="sender">Таймер.</param>
    /// <param name="e">Событие таймера.</param>
    private void OnClockTick(object? sender, EventArgs e) => ViewModel.TickClock();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Вид вернулся в дерево: строку времени и таймер нужно согласовать с партией заново.
        RefreshClock();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Таймер живёт на диспетчере и держит вид: у закрытого вида он обязан быть остановлен,
        // иначе тикал бы впустую до конца работы приложения.
        if (_clockTimer is not null)
        {
            _clockTimer.Stop();
            _clockTimer.Tick -= OnClockTick;
            _clockTimer = null;
        }
    }

    /// <summary>Перестраивает раскладку при изменении размера вида.</summary>
    /// <param name="sender">Вид.</param>
    /// <param name="e">Новый размер.</param>
    private void OnViewSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ApplyLayout(e.NewSize.Width, e.NewSize.Height);

    /// <summary>Пересчитывает сторону мобильной доски при изменении её частей.</summary>
    /// <param name="sender">Часть мобильной раскладки.</param>
    /// <param name="e">Новый размер.</param>
    private void OnMobilePartSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateMobileBoardSize();

    /// <summary>Определяет сторону доски по её настоящему месту на экране.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Новый размер.</param>
    /// <remarks>
    /// Координаты рисуются не всегда: на телефоне доска 19×19 занимает те же точки, что и 9×9,
    /// и подписи в поле вокруг сетки стали бы серой кашей. Решение принимает чистая функция
    /// <see cref="BoardLayoutRules.CoordinatesFit"/> по настоящему размеру доски.
    /// </remarks>
    private void OnBoardSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_boardControl is null)
        {
            return;
        }

        var side = Math.Min(e.NewSize.Width, e.NewSize.Height);

        _boardControl.ShowCoordinates = BoardLayoutRules.CoordinatesFit(side, ViewModel.Board.Size.Value);
    }

    /// <summary>Раскладывает доску и панель по размеру вида.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    private void ApplyLayout(double width, double height)
    {
        if (_boardControl is null || _layout is null || _mobileLayout is null)
        {
            return;
        }

        var mode = BoardLayoutRules.DecideLayout(width, height);

        if (_layoutMode != mode)
        {
            _layoutMode = mode;
            ApplyMode(mode);
        }

        UpdateMobileBoardSize();
    }

    /// <summary>Показывает выбранную раскладку и переносит в неё доску.</summary>
    /// <param name="mode">Раскладка.</param>
    private void ApplyMode(LayoutMode mode)
    {
        var mobile = mode == LayoutMode.Mobile;

        _layout!.IsVisible = !mobile;
        _mobileLayout!.IsVisible = mobile;

        if (_mobileBoardHost is not null && _desktopBoardHost is not null && _boardControl is not null)
        {
            // Доска одна на обе раскладки: она переносится, а не создаётся заново, иначе
            // анимация и подписка на щелчки жили бы в двух местах.
            MoveBoard(mobile ? _mobileBoardHost : _desktopBoardHost);
        }

        if (_resultBanner is not null)
        {
            // Баннер один на обе раскладки: меняется только размер цели нажатия — на телефоне
            // по кнопкам карточки попадают пальцем.
            _resultBanner.IsTouch = mobile;
        }

    }

    /// <summary>Переносит доску в другую раскладку.</summary>
    /// <param name="host">Место, куда встаёт доска.</param>
    private void MoveBoard(Border host)
    {
        if (_boardControl is null || ReferenceEquals(host.Child, _boardControl))
        {
            return;
        }

        if (_boardControl.Parent is Border previous)
        {
            previous.Child = null;
        }

        host.Child = _boardControl;
    }

    /// <summary>Считает сторону доски для мобильной раскладки.</summary>
    /// <remarks>
    /// Доска занимает всё, что осталось от строки состояния, шторки и отступов: сторона равна
    /// меньшей из доступных величин, поэтому на телефоне она упирается в ширину экрана.
    /// </remarks>
    private void UpdateMobileBoardSize()
    {
        if (_layoutMode != LayoutMode.Mobile || _mobileLayout is null || _mobileBoardHost is null)
        {
            return;
        }

        // Тесная высота: убираем строку состояния и строки краткого состояния шторки.
        // Видимость зависит только от высоты раскладки, а не от стороны доски, поэтому
        // повторного пересчёта по кругу не возникает.
        var compact = _mobileLayout.Bounds.Height > 0 && _mobileLayout.Bounds.Height < CompactHeight;

        if (_mobileStatus is not null)
        {
            _mobileStatus.IsVisible = !compact;
        }

        if (_sheetSummaryPanel is not null)
        {
            _sheetSummaryPanel.IsVisible = !compact;
        }

        // Место доски считает раскладка: доска и панель действий стоят в разных строках сетки
        // (BoardView.axaml), поэтому строка доски уже не включает в себя кнопки, а её настоящая
        // высота известна после раскладки. Ширина берётся у раскладки, высота — у строки доски.
        // Из высоты вычитается зазор: доска не должна наезжать на панель действий.
        var width = _mobileLayout.Bounds.Width - (2 * MobileBoardMargin);
        var height = (_mobileBoardRow?.Bounds.Height ?? 0) - (2 * MobileBoardMargin);

        var side = BoardLayoutRules.MobileBoardSide(width, height);

        if (side <= 0)
        {
            return;
        }

        _mobileBoardHost.Width = side;
        _mobileBoardHost.Height = side;

        // Панель действий идёт по ширине доски, но не уже читаемой: на узком экране (360×640)
        // доска ограничена высотой, и выровненная по ней панель обрезала бы подписи кнопок.
        // Ширину считает чистая функция, её проверяют тесты.
        if (_mobileActionBar is not null)
        {
            var barWidth = BoardLayoutRules.MobileActionBarWidth(
                side,
                _mobileLayout.Bounds.Width - (2 * MobileActionMargin));

            if (barWidth > 0)
            {
                _mobileActionBar.MaxWidth = barWidth;
            }
        }
    }

    /// <summary>Раскрывает и сворачивает шторку сведений.</summary>
    /// <param name="sender">Ручка шторки.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSheetToggleClick(object? sender, RoutedEventArgs e)
    {
        _detailsExpanded = !_detailsExpanded;

        if (_sheetDetails is not null)
        {
            _sheetDetails.IsVisible = _detailsExpanded;
        }

        if (_sheetChevron is not null)
        {
            // Стрелка показывает, что будет по нажатию: вверх — раскрыть, вниз — свернуть.
            _sheetChevron.Text = _detailsExpanded ? "▼" : "▲";
        }

        UpdateMobileBoardSize();
    }

    /// <summary>Обновляет короткое состояние вида: строку состояния, шторку и строки партии.</summary>
    /// <remarks>
    /// Строка состояния несёт одно главное сообщение — кто ходит; счёт, пленные и коми стоят
    /// в свёрнутой шторке, где для них есть место. Подробности — в развёрнутой шторке.
    /// Пленные называются явно и отдельной строкой: под японской системой это очки, и путать
    /// их со «снято» нельзя. Мёртвые камни заключительной позиции называются отдельно:
    /// они тоже идут в пленные (правила вида спорта «го», пп. 1.10 и 1.12), но счётчик партии
    /// их не знает — их снимает подсчёт, а не ход. Без этой строки панель показывала «пленные 0»,
    /// а счёт уже включал снятые мёртвые камни, и числа выглядели противоречиво (жалоба 2026-09-30).
    /// </remarks>
    private void RefreshSummary()
    {
        // Строку о пленных собирает модель представления: она знает и счётчики партии,
        // и пометки мёртвых камней, которые тоже идут в пленные (правила, пп. 1.10 и 1.12).
        var prisoners = ViewModel.PrisonersLine;

        if (_desktopPrisoners is not null)
        {
            _desktopPrisoners.Text = prisoners;
        }

        // Правило «что показывать» живёт в чистых функциях GameStatusLines, чтобы его проверяли
        // тесты: пока итога нет, приговор не называется. Шага согласования подсчёта больше нет —
        // мёртвые группы помечает перебор, а игрок правит пометки нажатием по доске (D-070).
        var verdict = ViewModel.HasOutcome;
        var paused = ViewModel.IsGamePaused;

        // На паузе слова «кто ходит» ничего не объясняют: ход не принимается, и игрок обязан
        // видеть, что партия остановлена, а не думать, что интерфейс завис.
        var status = paused
            ? PausedHeadline
            : GameStatusLines.Headline(verdict, ViewModel.Status, ViewModel.ToMove);
        var second = paused
            ? PausedDetail
            : GameStatusLines.Detail(verdict, ViewModel.Outcome, ViewModel.MoveNumber);
        // Счёт предварительный, пока партия идёт: позиция недоиграна, и окончательным счётом
        // её называть нельзя (жалоба 2026-10-02).
        var provisional = ViewModel.Status == "Идёт";
        var score = GameStatusLines.ScoreLine(provisional, ViewModel.Score);
        var detail = ViewModel.ScoreDetail;
        var breakdown = ViewModel.ScoreBreakdown;
        var stats = GameStatusLines.StatsLine(
            ViewModel.LevelDescription,
            ViewModel.PlayerColor.ToLowerInvariant(),
            ViewModel.MoveNumber,
            ViewModel.Komi);
        var stone = GameStatusLines.ShowTurnStone(verdict);

        if (_mobileTurnText is not null)
        {
            _mobileTurnText.Text = status;
        }

        if (_mobileMoveText is not null)
        {
            // Время партии идёт рядом с ходом: строка состояния — то место, куда игрок смотрит
            // за партией, и времени там самое место (на узком экране строка обрезается редко:
            // она последняя и без переноса).
            _mobileMoveText.Text = ViewModel.IsGameStarted ? $"{second} · {ViewModel.ClockShortDisplay}" : second;
        }

        if (_mobileTurnBlack is not null)
        {
            _mobileTurnBlack.IsVisible = stone && IsBlackTurn();
        }

        if (_mobileTurnWhite is not null)
        {
            _mobileTurnWhite.IsVisible = stone && !IsBlackTurn();
        }

        SetText(_desktopStatus, status);
        SetText(_mobileStatusText, $"Статус: {ViewModel.Status}");
        SetText(_desktopScore, score);
        SetText(_desktopScoreSystems, detail);
        SetText(_mobileScoreDetail, detail);
        SetText(_desktopBreakdown, breakdown);
        SetText(_mobileBreakdown, breakdown);
        SetText(_desktopStats, stats);
        SetText(_mobileStats, stats);
        SetText(_sheetSummary, score);
        SetText(_sheetSummaryDetails, prisoners);

        // Числа подсчёта видит тот, кто их запросил: строки стоят рядом с «Территорией»
        // и появляются, только когда разметка на доске включена (жалоба 2026-10-02).
        // Исключение — пленные: пока партия идёт, это накопительная величина партии,
        // и она нужна в панели независимо от разметки; после конца партии её сменяет итог.
        SetVisible(_desktopBreakdown, breakdown.Length > 0);
        SetVisible(_mobileBreakdown, breakdown.Length > 0);
        SetVisible(_desktopPrisoners, prisoners.Length > 0 && ViewModel.Status == "Идёт");

        // До первого хода кнопка называется «Начать партию»: партия ждёт игрока, а не идёт.
        var newGameLabel = ViewModel.IsFirstMove ? StartGameLabel : NewGameLabel;
        SetText(_desktopNewGame, newGameLabel);
        SetText(_menuNewGame, newGameLabel);

        SetText(_desktopLevelHint, ViewModel.LevelHint);

        // Часы обновляются здесь же: смена состояния партии меняет и строку времени, и подпись
        // кнопки. Тиканье (раз в секунду) идёт отдельным путём и панель целиком не пересобирает.
        RefreshClock();

        RefreshDeadMarks();
        RefreshOutcomeHighlight();

        // Баннер итога пересобирается вместе с панелью: строки карточки те же, что у строки
        // состояния, и отдельного пути обновления у них нет.
        _resultBanner?.Refresh();
    }

    /// <summary>Подсвечивает доску цветом итога: выигрыш, проигрыш или ничья.</summary>
    /// <remarks>
    /// Рамку по краю доски рисует рендерер, а решение «выиграл или проиграл» принимает модель
    /// представления: отрисовка не знает ни про счёт, ни про цвет игрока.
    /// </remarks>
    private void RefreshOutcomeHighlight()
    {
        if (_boardControl is null)
        {
            return;
        }

        _boardControl.Outcome = ViewModel.ResultTone switch
        {
            GameTone.Win => BoardOutcome.Win,
            GameTone.Loss => BoardOutcome.Loss,
            GameTone.Draw => BoardOutcome.Draw,
            _ => BoardOutcome.None
        };
    }

    /// <summary>Ставит текст, если элемент нашёлся в разметке.</summary>
    /// <param name="target">Элемент или <c>null</c>, если его нет в этой раскладке.</param>
    /// <param name="text">Новый текст.</param>
    private static void SetText(TextBlock? target, string? text)
    {
        if (target is not null)
        {
            target.Text = text ?? string.Empty;
        }
    }

    /// <summary>Ставит подпись кнопки, если она нашлась в разметке.</summary>
    /// <param name="target">Кнопка или <c>null</c>, если её нет в этой раскладке.</param>
    /// <param name="text">Новая подпись.</param>
    private static void SetText(Button? target, string text)
    {
        if (target is not null)
        {
            target.Content = text;
        }
    }

    /// <summary>Показывает или прячет строку панели.</summary>
    /// <param name="target">Строка или <c>null</c>, если её нет в этой раскладке.</param>
    /// <param name="visible">Показывать ли строку.</param>
    /// <remarks>
    /// Отдельный помощник, а не привязка: строки заполняет код вида, и признак видимости
    /// у них тот же, что у текста, — «есть что сказать».
    /// </remarks>
    private static void SetVisible(TextBlock? target, bool visible)
    {
        if (target is not null)
        {
            target.IsVisible = visible;
        }
    }

    /// <summary>Показывает на доске пометки мёртвых камней.</summary>
    /// <remarks>
    /// Список берётся у модели представления, но перерисовка заказывается по счётчику изменений
    /// (<c>DeadRevision</c>): тот же экземпляр списка мог измениться внутри, и подписка на свойство
    /// этого не заметила бы.
    /// </remarks>
    private void RefreshDeadMarks()
    {
        if (_boardControl is null)
        {
            return;
        }

        // Крестики — часть разметки подсчёта, а не постоянная картинка: без включённой разметки
        // доска обязана оставаться чистой, иначе игрок видит пометки, которых не просил
        // (замечание пользователя 2026-10-03: «показывать мёртвые группы только по кнопке
        // территория»). Знаки территории ведёт сама модель: Territory = null без разметки.
        _boardControl.DeadPoints = ViewModel.HasTerritory && ViewModel.DeadPoints.Count > 0
            ? ViewModel.DeadPoints
            : null;
        _boardControl.InvalidateVisual();
    }

    /// <summary>Ходят ли чёрные.</summary>
    /// <returns><c>true</c>, если очередь чёрных.</returns>
    /// <remarks>
    /// Подпись очереди берётся из того же источника, что и в модели представления: своей
    /// копии слов «Чёрные» и «Белые» здесь быть не должно.
    /// </remarks>
    private bool IsBlackTurn() => ViewModel.ToMove == StoneColorLabels.Label(StoneColor.Black);

    /// <summary>Применяет выбор кнопки «Сохранить» и убирает экран настроек.</summary>
    /// <param name="sender">Вид настроек.</param>
    /// <param name="e">Событие согласия.</param>
    /// <remarks>
    /// Два разных входа — две разные судьбы партии. Из меню «Новая партия» сохранение начинает
    /// партию с выбранными настройками (первый ход соперника считается в фоне). Обычный вход
    /// (раздел «Настройки») партию не пересоздаёт, если не изменились её свойства: уровень AI
    /// начинает играть со следующего хода соперника, а новый размер доски, цвет или коми
    /// начинают новую партию — иначе панель и экран настроек снова разошлись бы (жалоба 2026-10-03).
    /// </remarks>
    private void OnSettingsAccepted(object? sender, EventArgs e)
    {
        if (_settingsView is null)
        {
            return;
        }

        var chosen = _settingsView.Selected;

        // Звук — выбор игрока: он живёт в настройках, а решает о звуке обвязка звука.
        StoneSoundPlayer.SoundEnabled = chosen.SoundEnabled;

        if (_startGameAfterSettings)
        {
            // «Новая партия» из меню: партия начинается сразу, первый ход соперника — в фоне,
            // иначе окно замирало бы на время поиска, а анимация хода не была бы видна.
            _ = ViewModel.ApplySettingsAsync(chosen);
        }
        else
        {
            // Обычный вход: модель сама решает, продолжать партию или начать новую.
            _ = ViewModel.ApplyChosenSettings(chosen);
        }

        // Неудачная запись настроек не мешает играть: значения уже применены к партии.
        _ = SettingsStore.Save(chosen);

        HideSettings();
    }

    /// <summary>Закрывает настройки кнопкой «Отмена»: выбор не применяется.</summary>
    /// <param name="sender">Вид настроек.</param>
    /// <param name="e">Событие отказа.</param>
    /// <remarks>
    /// Правило подсчёта и звук действуют сразу, пока экран открыт, поэтому «Отмена» возвращает
    /// их по снимку, снятому при открытии: ни партия, ни файл настроек не меняются
    /// (жалоба пользователя 2026-10-03 «не хватает кнопки отмена»).
    /// </remarks>
    private void OnSettingsCancelled(object? sender, EventArgs e) => CancelSettings();

    /// <summary>Возвращает партию и звук к снимку на входе и закрывает настройки.</summary>
    private void CancelSettings()
    {
        ViewModel.ScoringRule = _settingsOnOpen.ToScoringRule();
        StoneSoundPlayer.SoundEnabled = _settingsOnOpen.SoundEnabled;

        HideSettings();
    }

    /// <summary>Прячет панель настроек.</summary>
    /// <remarks>
    /// О закрытии сообщается оболочке: на телефоне настройки — раздел нижней навигации,
    /// и подсветка должна вернуться к партии. Решение «начать партию после настроек» снимается
    /// вместе с экраном: переход в другой раздел не должен оставлять его в силе.
    /// </remarks>
    private void HideSettings()
    {
        var visible = _overlay?.IsVisible == true;

        _startGameAfterSettings = false;

        if (_overlay is not null)
        {
            _overlay.IsVisible = false;
        }

        if (visible)
        {
            SettingsClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Обновляет вид, когда модель сообщает об изменении.</summary>
    /// <param name="sender">Модель представления.</param>
    /// <param name="e">Имя изменившегося свойства.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Тиканье часов: обновляется только строка времени. Панель целиком по таймеру
        // не пересобирается — иначе каждую секунду переписывались бы все строки состояния.
        if (e.PropertyName is nameof(MainViewModel.ClockDisplay) or nameof(MainViewModel.ClockShortDisplay))
        {
            RefreshClock();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.LastMove))
        {
            _boardControl?.Animate(ViewModel.LastMove, ViewModel.LastCaptured, ViewModel.LastCapturedColor);

            // Звук — там же, где анимация: он её сопровождает, а не живёт отдельно.
            _ = StoneSoundPlayer.Shared.OnMove(ViewModel.MoveNumber, ViewModel.LastCaptured);
            RefreshSummary();
            return;
        }

        // Пометки мёртвых камней: список может остаться тем же объектом, поэтому доска
        // перерисовывается и по счётчику изменений, и по самому списку. Вместе с пометками
        // меняется и счёт: мёртвые камни идут в пленные.
        if (e.PropertyName is nameof(MainViewModel.DeadPoints)
            or nameof(MainViewModel.DeadRevision))
        {
            RefreshDeadMarks();
            RefreshSummary();
            return;
        }

        // Партия пересоздана (смена доски, уровня, настроек) — списки выбора должны это показать.
        if (IsPanelProperty(e.PropertyName, ComboProperties))
        {
            SyncCombos();
            RefreshSummary();
            return;
        }

        // Состояние, счёт, очередь хода и подписи кнопок: всё это показывает одной панелью
        // RefreshSummary, поэтому список один — раньше он был разбит на два и один и тот же
        // вызов стоял в двух ветках.
        if (IsPanelProperty(e.PropertyName, PanelProperties))
        {
            RefreshSummary();
        }
    }

    /// <summary>Свойства, после которых панель пересобирается целиком.</summary>
    /// <remarks>
    /// Список задан статически и сверяется с моделью тестом: свойство, забытое здесь, — это
    /// застывшая надпись на экране (регрессия «панель показывает старый текст»).
    /// </remarks>
    internal static readonly string[] PanelProperties =
    [
        nameof(MainViewModel.ToMove), nameof(MainViewModel.Status), nameof(MainViewModel.Score),
        nameof(MainViewModel.Outcome), nameof(MainViewModel.MoveNumber), nameof(MainViewModel.CapturedBlack),
        nameof(MainViewModel.CapturedWhite), nameof(MainViewModel.Komi), nameof(MainViewModel.Board),
        nameof(MainViewModel.IsGameStarted), nameof(MainViewModel.IsGameRunning), nameof(MainViewModel.IsGamePaused),
        nameof(MainViewModel.IsFirstMove)
    ];

    /// <summary>Свойства, после которых панель пересобирается и списки выбора синхронизируются.</summary>
    internal static readonly string[] ComboProperties =
    [
        nameof(MainViewModel.LevelLabels), nameof(MainViewModel.SelectedLevelIndex),
        nameof(MainViewModel.SelectedSizeIndex), nameof(MainViewModel.LevelDescription),
        nameof(MainViewModel.HasLevelHint)
    ];

    /// <summary>Входит ли изменившееся свойство в список.</summary>
    /// <param name="propertyName">Имя свойства из уведомления; <c>null</c> — уведомление обо всех.</param>
    /// <param name="names">Список имён.</param>
    /// <returns><c>true</c>, если свойство в списке или имя не названо.</returns>
    /// <remarks>
    /// Имя <c>null</c> считается совпадением: так модель сообщает «изменилось всё», и панель
    /// обязана пересобраться.
    /// </remarks>
    private static bool IsPanelProperty(string? propertyName, string[] names) =>
        propertyName is null || Array.IndexOf(names, propertyName) >= 0;

    /// <summary>Показывает в списках выбора то, чем играет партия сейчас.</summary>
    private void SyncCombos()
    {
        Sync(_sizeCombo, "SizeBox", ViewModel.SizeLabels, ViewModel.SelectedSizeIndex);
        Sync(_levelCombo, "LevelBox", ViewModel.LevelLabels, ViewModel.SelectedLevelIndex);
    }

    /// <summary>Показывает в одном списке то, что показывает модель представления.</summary>
    /// <param name="state">Состояние списка.</param>
    /// <param name="name">Имя списка в разметке.</param>
    /// <param name="labels">Подписи из модели представления.</param>
    /// <param name="index">Выбранный индекс.</param>
    /// <remarks>
    /// На время обновления изменения списка игнорируются: он сообщает о сбросе индекса, а не
    /// о выборе игрока. Подписи присваиваются только новым экземпляром: подмена того же самого
    /// списка сбрасывала бы выбранный уровень.
    /// </remarks>
    private void Sync(ComboState state, string name, IReadOnlyList<string> labels, int index)
    {
        var replace = state.BeginUpdate(labels, index);

        try
        {
            if (this.FindControl<ComboBox>(name) is not { } box)
            {
                return;
            }

            if (replace)
            {
                box.ItemsSource = labels;
            }

            box.SelectedIndex = index;
        }
        finally
        {
            state.EndUpdate();
        }
    }

    /// <summary>Применяет выбранный размер доски.</summary>
    /// <param name="sender">Список размеров.</param>
    /// <param name="e">Событие смены выбора.</param>
    private void OnSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || !_sizeCombo.TryAccept(box.SelectedIndex))
        {
            return;
        }

        ViewModel.SelectedSizeIndex = box.SelectedIndex;
        SyncCombos();
    }

    /// <summary>Применяет выбранный уровень.</summary>
    /// <param name="sender">Список уровней.</param>
    /// <param name="e">Событие смены выбора.</param>
    private void OnLevelChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || !_levelCombo.TryAccept(box.SelectedIndex))
        {
            return;
        }

        ViewModel.SelectedLevelIndex = box.SelectedIndex;
        SyncCombos();
    }

    /// <summary>Подписывает кнопку на обработчик.</summary>
    /// <param name="name">Имя кнопки.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void WireButton(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += handler;
        }
    }


    /// <summary>Раскрывает и сворачивает подробности настольной панели.</summary>
    /// <param name="sender">Переключатель «Подробности».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnDetailsToggleClick(object? sender, RoutedEventArgs e)
    {
        if (_desktopDetails is not null)
        {
            _desktopDetails.IsVisible = _desktopDetailsButton?.IsChecked == true;
        }
    }

    /// <summary>Обрабатывает щелчок по доске.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка хода.</param>
    /// <remarks>
    /// <para>
    /// Партия окончена — щелчок не играет ход, а поправляет пометку мёртвой группы: оценку
    /// даёт перебор, но последнее слово за игроком, а на телефоне правой кнопки нет — там
    /// поправить оценку можно только нажатием по камню. Счёт пересчитывается сразу.
    /// </para>
    /// <para>
    /// Во время партии пометка ставится правой кнопкой (<c>MoveRequestedEventArgs.MarksDead</c>):
    /// левая занята ходом, и отбирать её под пометки нельзя.
    /// </para>
    /// </remarks>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e)
    {
        // Пометка мёртвой группы: после конца партии — нажатием (ходов там нет), во время
        // партии — правой кнопкой (левая занята ходом). Счёт пересчитывается сразу.
        if (ViewModel.HasOutcome || e.MarksDead)
        {
            _ = ViewModel.ToggleDeadAt(e.Point);
            return;
        }

        if (ViewModel.IsGamePaused)
        {
            // Ход на паузе не играется: игрок должен увидеть, почему доска не отвечает,
            // а не решить, что приложение сломалось.
            ShowHint(PausedHint);
            return;
        }

        _ = ViewModel.PlayMoveAsync(e.Point);
    }

    /// <summary>Передаёт ход.</summary>
    /// <param name="sender">Кнопка «Пас».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnPassClick(object? sender, RoutedEventArgs e) => _ = ViewModel.PassAsync();

    /// <summary>Отменяет последний ход игрока вместе с ответом AI.</summary>
    /// <param name="sender">Кнопка «Отменить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnUndoClick(object? sender, RoutedEventArgs e) => ViewModel.Undo();

    /// <summary>Возвращает отменённый ход.</summary>
    /// <param name="sender">Кнопка «Вернуть».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnRedoClick(object? sender, RoutedEventArgs e) => ViewModel.Redo();

    /// <summary>Начинает партию заново: новая позиция и часы с нуля.</summary>
    /// <param name="sender">Кнопка «Начать партию» / «Новая партия».</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>Путь фоновый: первый ход соперника считается, пока окно остаётся живым.</remarks>
    private void OnNewGameClick(object? sender, RoutedEventArgs e) => _ = ViewModel.StartGameAsync();

    /// <summary>Останавливает партию или снимает паузу — одна кнопка на два состояния.</summary>
    /// <param name="sender">Кнопка «Пауза» / «Продолжить».</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Подпись кнопки ставит вид по состоянию партии, а действие выбирается здесь: пауза
    /// мгновенная, а продолжение может потребовать хода соперника — его считает фоновая версия.
    /// </remarks>
    private void OnPauseResumeClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.CanResumeClock)
        {
            _ = ViewModel.ResumeGameAsync();

            return;
        }

        ViewModel.PauseGame();
    }

    /// <summary>Отменяет партию: доска возвращается в состояние «партия не начата».</summary>
    /// <param name="sender">Кнопка «Отменить партию».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnAbortGameClick(object? sender, RoutedEventArgs e) => _ = ViewModel.AbortGameAsync();

    /// <summary>Показывает настройки.</summary>
    /// <param name="sender">Кнопка «Настройки».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSettingsClick(object? sender, RoutedEventArgs e) => ShowSettings();

    /// <summary>Меню: показывает настройки партии перед новой партией.</summary>
    /// <param name="sender">Кнопка «Начать партию» в меню.</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Карточка меню закрывается, а вместо неё открывается экран настроек на подразделе «Партия»:
    /// партия начнётся, когда игрок закроет его кнопкой «Готово» (жалоба пользователя 2026-10-03).
    /// </remarks>
    private void OnMenuNewGameClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        CloseMenu();
        ShowSettingsForNewGame();
    }

    /// <summary>Меню: отменяет партию тем же путём, что кнопка панели.</summary>
    /// <param name="sender">Кнопка «Отменить партию» в меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnMenuAbortClick(object? sender, RoutedEventArgs e)
    {
        CloseMenu();
        OnAbortGameClick(sender, e);
    }

    /// <summary>Меню: сохраняет партию тем же путём, что кнопка панели.</summary>
    /// <param name="sender">Кнопка «Сохранить партию» в меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnMenuSaveClick(object? sender, RoutedEventArgs e)
    {
        CloseMenu();
        OnSaveClick(sender, e);
    }

    /// <summary>Меню: загружает партию тем же путём, что кнопка панели.</summary>
    /// <param name="sender">Кнопка «Загрузить партию» в меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnMenuLoadClick(object? sender, RoutedEventArgs e)
    {
        CloseMenu();
        OnLoadClick(sender, e);
    }

    /// <summary>Меню: открывает настройки тем же путём, что кнопка панели.</summary>
    /// <param name="sender">Кнопка «Настройки» в меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnMenuSettingsClick(object? sender, RoutedEventArgs e)
    {
        CloseMenu();
        OnSettingsClick(sender, e);
    }

    /// <summary>Меню: закрывает карточку без действий.</summary>
    /// <param name="sender">Кнопка «Закрыть» в меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnMenuCloseClick(object? sender, RoutedEventArgs e) => CloseMenu();

    /// <summary>Сохраняет партию.</summary>
    /// <param name="sender">Кнопка «Сохранить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = SaveGameAsync();

    /// <summary>Загружает партию.</summary>
    /// <param name="sender">Кнопка «Загрузить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLoadClick(object? sender, RoutedEventArgs e) => _ = LoadGameAsync();
}
