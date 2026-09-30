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

    /// <summary>Класс фона экрана настроек: на телефоне он закрывает вид целиком.</summary>
    private const string SettingsScreenClass = "app-settings-screen";

    /// <summary>Класс карточки настроек: на телефоне она растягивается на весь экран.</summary>
    private const string SettingsCardClass = "app-settings-card-mobile";

    /// <summary>Подпись кнопки до первого хода: партия ещё не начата.</summary>
    private const string StartGameLabel = "Начать партию";

    /// <summary>Подпись кнопки после первого хода: партия идёт, кнопка начинает новую.</summary>
    private const string NewGameLabel = "Новая партия";

    /// <summary>Логгер вида: сюда попадают сохранение и загрузка партии с их отказами.</summary>
    private readonly ILogger _log = AppLog.For<BoardView>();

    private readonly BoardControl? _boardControl;
    private readonly Grid? _layout;
    private readonly Grid? _mobileLayout;
    private readonly Border? _desktopBoardHost;
    private readonly Border? _mobileBoardHost;
    private readonly Border? _overlay;
    private readonly Border? _settingsCard;
    private readonly Border? _settingsHeader;
    private readonly SettingsView? _settingsView;
    private readonly TextBlock? _hint;
    private readonly TextBlock? _mobileHint;
    private readonly TextBlock? _mobileTurnText;
    private readonly TextBlock? _mobileMoveText;
    private readonly Ellipse? _mobileTurnBlack;
    private readonly Ellipse? _mobileTurnWhite;
    private readonly Border? _mobileStatus;
    private readonly Border? _mobileActionBar;
    private readonly Border? _sheet;
    private readonly TextBlock? _sheetSummary;
    private readonly TextBlock? _sheetSummaryDetails;
    private readonly TextBlock? _sheetChevron;
    private readonly ScrollViewer? _sheetDetails;
    private readonly StackPanel? _sheetSummaryPanel;
    private readonly TextBlock? _desktopPrisoners;
    private readonly TextBlock? _mobilePrisoners;
    private readonly TextBlock? _desktopScore;
    private readonly TextBlock? _mobileScore;
    private readonly TextBlock? _desktopStatus;
    private readonly TextBlock? _mobileStatusText;
    private readonly TextBlock? _desktopScoreDetail;
    private readonly TextBlock? _mobileScoreDetail;
    private readonly TextBlock? _desktopBreakdown;
    private readonly TextBlock? _mobileBreakdown;
    private readonly TextBlock? _desktopStats;
    private readonly TextBlock? _mobileStats;
    private readonly TextBlock? _desktopLevelHint;
    private readonly StackPanel? _desktopDetails;
    private readonly ToggleButton? _desktopDetailsButton;
    private readonly Button? _desktopNewGame;
    private readonly Button? _mobileNewGame;
    private readonly GameResultBanner? _desktopResultBanner;
    private readonly GameResultBanner? _mobileResultBanner;
    private readonly Border? _mobileResultHost;

    /// <summary>Показанный сейчас баннер итога: раскладок две, и баннер в каждой свой.</summary>
    /// <remarks>
    /// Баннеры не переезжают между раскладками (в отличие от доски): у каждого своя рамка
    /// и своё место в разметке, поэтому вид показывает тот, что стоит в текущей раскладке.
    /// </remarks>
    private GameResultBanner? _resultBanner;

    /// <summary>Таймер, по которому исчезает подсказка.</summary>
    private DispatcherTimer? _hintTimer;

    /// <summary>Настройки, с которыми играет вид: их показывает панель настроек.</summary>
    private AppSettings _settings;

    /// <summary>Состояние списка размеров: программное обновление не считается выбором игрока.</summary>
    private readonly ComboState _sizeCombo = new();

    /// <summary>Состояние списка уровней: программное обновление не считается выбором игрока.</summary>
    private readonly ComboState _levelCombo = new();

    /// <summary>Текущая раскладка: <c>null</c> — ещё не выбрана.</summary>
    private LayoutMode? _layoutMode;

    /// <summary>Шторка развёрнута: подробности показаны.</summary>
    private bool _detailsExpanded;

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

        _settings = settings;

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
        _settingsCard = this.FindControl<Border>("SettingsCard");
        _settingsHeader = this.FindControl<Border>("SettingsHeader");
        _settingsView = this.FindControl<SettingsView>("SettingsArea");
        _hint = this.FindControl<TextBlock>("Hint");
        _mobileHint = this.FindControl<TextBlock>("MobileHint");
        _mobileTurnText = this.FindControl<TextBlock>("MobileTurnText");
        _mobileMoveText = this.FindControl<TextBlock>("MobileMoveText");
        _mobileTurnBlack = this.FindControl<Ellipse>("MobileTurnBlack");
        _mobileTurnWhite = this.FindControl<Ellipse>("MobileTurnWhite");
        _mobileStatus = this.FindControl<Border>("MobileStatus");
        _mobileActionBar = this.FindControl<Border>("MobileActionBar");
        _sheet = this.FindControl<Border>("Sheet");
        _sheetSummary = this.FindControl<TextBlock>("SheetSummary");
        _sheetSummaryDetails = this.FindControl<TextBlock>("SheetSummaryDetails");
        _sheetChevron = this.FindControl<TextBlock>("SheetChevron");
        _sheetDetails = this.FindControl<ScrollViewer>("SheetDetails");
        _sheetSummaryPanel = this.FindControl<StackPanel>("SheetSummaryPanel");
        _desktopPrisoners = this.FindControl<TextBlock>("DesktopPrisonersText");
        _mobilePrisoners = this.FindControl<TextBlock>("MobilePrisonersText");
        _desktopScore = this.FindControl<TextBlock>("DesktopScoreText");
        _mobileScore = this.FindControl<TextBlock>("MobileScoreText");
        _desktopStatus = this.FindControl<TextBlock>("DesktopStatusText");
        _mobileStatusText = this.FindControl<TextBlock>("MobileStatusText");
        _desktopScoreDetail = this.FindControl<TextBlock>("DesktopScoreDetailText");
        _mobileScoreDetail = this.FindControl<TextBlock>("MobileScoreDetailText");
        _desktopBreakdown = this.FindControl<TextBlock>("DesktopBreakdownText");
        _mobileBreakdown = this.FindControl<TextBlock>("MobileBreakdownText");
        _desktopStats = this.FindControl<TextBlock>("DesktopStatsText");
        _mobileStats = this.FindControl<TextBlock>("MobileStatsText");
        _desktopLevelHint = this.FindControl<TextBlock>("DesktopLevelHintText");
        _desktopDetails = this.FindControl<StackPanel>("DesktopDetailsPanel");
        _desktopDetailsButton = this.FindControl<ToggleButton>("DesktopDetailsButton");
        _desktopNewGame = this.FindControl<Button>("NewGameButton");
        _mobileNewGame = this.FindControl<Button>("MobileNewGameButton");
        _desktopResultBanner = this.FindControl<GameResultBanner>("DesktopResultBanner");
        _mobileResultBanner = this.FindControl<GameResultBanner>("MobileResultBanner");
        _mobileResultHost = this.FindControl<Border>("MobileResultHost");

        // До выбора раскладки показывается настольный баннер: раскладку выберет ApplyLayout.
        _resultBanner = _desktopResultBanner;

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
            _boardControl.SizeChanged += OnBoardSizeChanged;
        }

        if (_settingsView is not null)
        {
            _settingsView.Accepted += OnSettingsAccepted;
            _settingsView.Cancelled += OnSettingsCancelled;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        WireButton("PassButton", OnPassClick);
        WireButton("UndoButton", OnUndoClick);
        WireButton("RedoButton", OnRedoClick);
        WireButton("NewGameButton", OnNewGameClick);
        WireButton("SettingsButton", OnSettingsClick);
        WireButton("SaveButton", OnSaveClick);
        WireButton("LoadButton", OnLoadClick);

        // Кнопки мобильной раскладки делают то же самое: обработчики общие.
        WireButton("MobilePassButton", OnPassClick);
        WireButton("MobileUndoButton", OnUndoClick);
        WireButton("MobileRedoButton", OnRedoClick);
        WireButton("MobileNewGameButton", OnNewGameClick);
        WireButton("MobileSaveButton", OnSaveClick);
        WireButton("MobileLoadButton", OnLoadClick);
        WireButton("MobileSettingsButton", OnSettingsClick);
        WireButton("SettingsBackButton", OnSettingsBackClick);
        WireButton("SheetToggleButton", OnSheetToggleClick);

        // Файловые действия телефона живут в меню кнопки «Файл»: у пунктов меню обработчик
        // тот же, что у кнопок настольной панели.
        WireMenuItem("MobileSaveMenuItem", OnSaveClick);
        WireMenuItem("MobileLoadMenuItem", OnLoadClick);

        if (_desktopDetailsButton is not null)
        {
            _desktopDetailsButton.Click += OnDetailsToggleClick;
        }

        // Подтверждение подсчёта: кнопка есть в обеих раскладках, действие одно.
        WireButton("DesktopConfirmButton", OnConfirmScoreClick);
        WireButton("MobileConfirmButton", OnConfirmScoreClick);
        WireButton("SheetConfirmButton", OnConfirmScoreClick);

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

        // Высота панели действий меняется от подписей кнопок и от скрытой кнопки территории:
        // по ней пересчитывается сторона доски.
        if (_mobileActionBar is not null)
        {
            _mobileActionBar.SizeChanged += OnMobilePartSizeChanged;
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

    /// <summary>Показывает настройки: в окне, если окно есть, иначе отдельным экраном.</summary>
    public void ShowSettings()
    {
        // На телефоне настройки — отдельный экран, даже если окно умеет показать диалог:
        // диалог шириной 440 на экране 360 не помещается, а на Android окна нет вовсе.
        if (_layoutMode != LayoutMode.Mobile && SettingsRequested is not null)
        {
            SettingsRequested.Invoke(this, EventArgs.Empty);
            return;
        }

        _settingsView?.Initialize(_settings, ViewModel.HasModelFor);

        if (_overlay is not null)
        {
            _overlay.IsVisible = true;
        }

        SettingsShown?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Закрывает экран настроек без изменений.</summary>
    /// <remarks>Нужно оболочке: переход в другой раздел нижней навигации закрывает настройки.</remarks>
    public void CloseSettingsScreen() => HideSettings();

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

        // Баннер показывает только текущая раскладка: у каждой он свой, со своей рамкой
        // и своим местом. Кнопка баннера на телефоне крупнее — цель нажатия 48 точек.
        _resultBanner = mobile ? _mobileResultBanner : _desktopResultBanner;

        if (_desktopResultBanner is not null)
        {
            _desktopResultBanner.IsVisible = false;
        }

        if (_mobileResultBanner is not null)
        {
            _mobileResultBanner.IsTouch = true;
        }

        if (_mobileResultHost is not null)
        {
            _mobileResultHost.IsVisible = false;
        }

        RefreshResultBanner();

        if (_overlay is not null)
        {
            _overlay.Classes.Set(SettingsScreenClass, mobile);
        }

        if (_settingsCard is not null)
        {
            _settingsCard.Classes.Set(SettingsCardClass, mobile);
        }

        if (_settingsHeader is not null)
        {
            // Заголовок с кнопкой «Назад» нужен только экрану телефона.
            _settingsHeader.IsVisible = mobile;
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

        // Панель действий стоит поверх доски, но место под неё отводится заранее: иначе доска
        // уходила бы под кнопки, и нижние ряды стали бы недоступны для щелчка. Раньше в панели
        // был один ряд кнопок, теперь два — высота берётся по факту, а не константой.
        var width = _mobileLayout.Bounds.Width - (2 * MobileBoardMargin);
        var height = _mobileLayout.Bounds.Height
            - (_mobileStatus?.Bounds.Height ?? 0)
            - (_sheet?.Bounds.Height ?? 0)
            - ((_mobileActionBar?.Bounds.Height ?? 0) + MobileActionMargin)
            - (2 * MobileBoardMargin);

        var side = BoardLayoutRules.MobileBoardSide(width, height);

        if (side <= 0)
        {
            return;
        }

        _mobileBoardHost.Width = side;
        _mobileBoardHost.Height = side;
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

    /// <summary>Закрывает экран настроек кнопкой «Назад».</summary>
    /// <param name="sender">Кнопка «Назад».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSettingsBackClick(object? sender, RoutedEventArgs e) => HideSettings();

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

        if (_mobilePrisoners is not null)
        {
            _mobilePrisoners.Text = prisoners;
        }

        // Идёт согласование мёртвых групп: победитель ещё не назван, счёт предварительный.
        // Правило «что показывать» живёт в чистых функциях GameStatusLines, чтобы его проверяли
        // тесты: пока подсчёт не подтверждён, приговор показывать нельзя.
        var counting = ViewModel.IsCounting;
        var verdict = ViewModel.HasOutcome;

        var status = GameStatusLines.Headline(counting, verdict, ViewModel.Status, ViewModel.ToMove);
        var second = GameStatusLines.Detail(counting, verdict, ViewModel.Outcome, ViewModel.MoveNumber);
        var score = GameStatusLines.ScoreLine(counting, ViewModel.Score);
        var detail = ViewModel.ScoreDetail;
        var breakdown = ViewModel.ScoreBreakdown;
        var stats = GameStatusLines.StatsLine(
            ViewModel.LevelDescription,
            ViewModel.PlayerColor.ToLowerInvariant(),
            ViewModel.MoveNumber,
            ViewModel.Komi);
        var stone = GameStatusLines.ShowTurnStone(counting, verdict);

        if (_mobileTurnText is not null)
        {
            _mobileTurnText.Text = status;
        }

        if (_mobileMoveText is not null)
        {
            _mobileMoveText.Text = second;
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
        SetText(_mobileScore, score);
        SetText(_desktopScoreDetail, detail);
        SetText(_mobileScoreDetail, detail);
        SetText(_desktopBreakdown, breakdown);
        SetText(_mobileBreakdown, breakdown);
        SetText(_desktopStats, stats);
        SetText(_mobileStats, stats);
        SetText(_sheetSummary, score);
        SetText(_sheetSummaryDetails, prisoners);

        // До первого хода кнопка называется «Начать партию»: партия ждёт игрока, а не идёт.
        var newGameLabel = ViewModel.IsFirstMove ? StartGameLabel : NewGameLabel;
        SetText(_desktopNewGame, newGameLabel);
        SetText(_mobileNewGame, newGameLabel);

        SetText(_desktopLevelHint, ViewModel.LevelHint);

        RefreshDeadMarks();
        RefreshOutcomeHighlight();
        RefreshResultBanner();
    }

    /// <summary>Показывает баннер итога в текущей раскладке.</summary>
    /// <remarks>
    /// Строка баннера на телефоне — отдельная строка сетки, и она появляется вместе с баннером:
    /// пустая рамка над доской съедала бы место на низком экране.
    /// </remarks>
    private void RefreshResultBanner()
    {
        _resultBanner?.Refresh();

        if (_mobileResultHost is not null)
        {
            _mobileResultHost.IsVisible = _mobileResultBanner?.IsVisible == true;
        }
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

        var dead = ViewModel.IsCounting ? ViewModel.DeadPoints : null;

        _boardControl.DeadPoints = dead;
        _boardControl.InvalidateVisual();
    }

    /// <summary>Подтверждает подсчёт: мёртвые камни снимаются и входят в итог.</summary>
    /// <param name="sender">Кнопка «Посчитать».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnConfirmScoreClick(object? sender, RoutedEventArgs e) => ViewModel.ConfirmScore();

    /// <summary>Ходят ли чёрные.</summary>
    /// <returns><c>true</c>, если очередь чёрных.</returns>
    /// <remarks>
    /// Подпись очереди берётся из того же источника, что и в модели представления: своей
    /// копии слов «Чёрные» и «Белые» здесь быть не должно.
    /// </remarks>
    private bool IsBlackTurn() => ViewModel.ToMove == StoneColorLabels.Label(StoneColor.Black);

    /// <summary>Применяет настройки, выбранные в панели настроек, и убирает её.</summary>
    /// <param name="sender">Вид настроек.</param>
    /// <param name="e">Событие согласия.</param>
    private void OnSettingsAccepted(object? sender, EventArgs e)
    {
        if (_settingsView is null)
        {
            return;
        }

        _settings = _settingsView.Selected;

        // Звук — выбор игрока: он живёт в настройках, а решает о звуке обвязка звука.
        StoneSoundPlayer.SoundEnabled = _settings.SoundEnabled;

        // Партия начинается сразу, а первый ход соперника считается в фоне: иначе окно
        // замирало бы на время поиска, а анимация хода не была бы видна.
        _ = ViewModel.ApplySettingsAsync(_settings);

        // Неудачная запись настроек не мешает играть: значения уже применены к партии.
        _ = SettingsStore.Save(_settings);

        HideSettings();
    }

    /// <summary>Убирает панель настроек без изменений.</summary>
    /// <param name="sender">Вид настроек.</param>
    /// <param name="e">Событие отказа.</param>
    private void OnSettingsCancelled(object? sender, EventArgs e) => HideSettings();

    /// <summary>Прячет панель настроек.</summary>
    /// <remarks>
    /// О закрытии сообщается оболочке: на телефоне настройки — раздел нижней навигации,
    /// и подсветка должна вернуться к партии.
    /// </remarks>
    private void HideSettings()
    {
        var visible = _overlay?.IsVisible == true;

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
        if (e.PropertyName == nameof(MainViewModel.LastMove))
        {
            _boardControl?.Animate(ViewModel.LastMove, ViewModel.LastCaptured, ViewModel.LastCapturedColor);

            // Звук — там же, где анимация: он её сопровождает, а не живёт отдельно.
            _ = StoneSoundPlayer.Shared.OnMove(ViewModel.MoveNumber, ViewModel.LastCaptured);
            RefreshSummary();
            return;
        }

        // Пометки мёртвых камней: список может остаться тем же объектом, поэтому доска
        // перерисовывается и по счётчику изменений, и по самому списку.
        if (e.PropertyName is nameof(MainViewModel.DeadPoints)
            or nameof(MainViewModel.DeadRevision)
            or nameof(MainViewModel.IsCounting))
        {
            RefreshDeadMarks();

            // Вход в подсчёт и выход из него меняют и подписи: до подтверждения победитель
            // не называется, счёт объявляется предварительным.
            RefreshSummary();
            return;
        }

        // Партия пересоздана (смена доски, уровня, настроек) — списки выбора должны это показать.
        if (e.PropertyName is nameof(MainViewModel.LevelLabels)
            or nameof(MainViewModel.SelectedLevelIndex)
            or nameof(MainViewModel.SelectedSizeIndex)
            or nameof(MainViewModel.LevelDescription)
            or nameof(MainViewModel.HasLevelHint))
        {
            SyncCombos();
            RefreshSummary();
            return;
        }

        // Состояние, счёт и очередь хода показываются и на телефоне: строка состояния и шторка.
        if (e.PropertyName is nameof(MainViewModel.ToMove)
            or nameof(MainViewModel.Status)
            or nameof(MainViewModel.Score)
            or nameof(MainViewModel.Outcome)
            or nameof(MainViewModel.MoveNumber)
            or nameof(MainViewModel.CapturedBlack)
            or nameof(MainViewModel.CapturedWhite)
            or nameof(MainViewModel.Komi)
            or nameof(MainViewModel.Board))
        {
            RefreshSummary();
            return;
        }

        // Кнопка «Начать партию» / «Новая партия»: подпись меняется на первом ходу партии.
        if (e.PropertyName is nameof(MainViewModel.IsFirstMove))
        {
            RefreshSummary();
        }
    }

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

    /// <summary>Подписывает пункт меню на обработчик.</summary>
    /// <param name="name">Имя пункта.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void WireMenuItem(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<MenuItem>(name) is { } item)
        {
            item.Click += handler;
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
    /// Пока идёт согласование мёртвых групп, щелчок не играет ход, а помечает группу мёртвой
    /// или снимает пометку: в этом режиме ходы не принимаются (контракт конца партии, T-A).
    /// </remarks>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e)
    {
        if (ViewModel.IsCounting)
        {
            _ = ViewModel.ToggleDeadAt(e.Point);
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

    /// <summary>Начинает новую партию по выбранным доске и уровню.</summary>
    /// <param name="sender">Кнопка «Новая партия».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNewGameClick(object? sender, RoutedEventArgs e) => _ = ViewModel.StartNewGameAsync();

    /// <summary>Показывает настройки.</summary>
    /// <param name="sender">Кнопка «Настройки».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSettingsClick(object? sender, RoutedEventArgs e) => ShowSettings();

    /// <summary>Сохраняет партию.</summary>
    /// <param name="sender">Кнопка «Сохранить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = SaveGameAsync();

    /// <summary>Загружает партию.</summary>
    /// <param name="sender">Кнопка «Загрузить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLoadClick(object? sender, RoutedEventArgs e) => _ = LoadGameAsync();
}
