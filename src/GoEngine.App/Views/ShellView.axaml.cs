using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Оболочка приложения: переключатель режимов «Партия» и «Задачи».</summary>
/// <remarks>
/// Оба режима живут одновременно: виды не пересоздаются, меняется только видимость. Поэтому
/// переключение режима не сбрасывает партию — вернувшись в «Партию», игрок видит ту же позицию.
/// Тот же вид используется настольным окном и мобильной головой (<c>GoEngine.App.Android</c>).
/// Раскладка выбирается по ширине вида (<see cref="BoardLayoutRules.DecideLayout"/>): на широком
/// экране — прежняя строка режимов сверху, на телефоне — нижняя навигация с тремя разделами:
/// партия, задачи и настройки. Настройки на телефоне — отдельный экран, а не окно-диалог.
/// <para>
/// При запуске оболочка открывается на разделе «Партия» — экрана настроек вместо доски нет
/// (замечание 2026-10-03). На телефоне над доской сразу показана карточка «Меню»
/// (<see cref="BoardView.ShowMenu"/>): там это единственный вход к действиям партии, а настольное
/// окно стартует без неё — у него есть меню-бар и мышь, и карточка поверх доски только мешала бы.
/// Раскладку выбирает <see cref="BoardLayoutRules.DecideLayout"/> по ширине вида, решение о карточке
/// принимается там же, где применяется раскладка (<see cref="ApplyLayout"/>).
/// </para>
/// </remarks>
public sealed partial class ShellView : UserControl
{
    private readonly Panel? _content;
    private readonly ToggleButton? _gameButton;
    private readonly ToggleButton? _problemButton;
    private readonly Border? _desktopBar;
    private readonly Border? _mobileNav;
    private readonly UpdateBanner? _updateBanner;

    /// <summary>Применённая раскладка: <c>null</c> — ещё не выбрана.</summary>
    private LayoutMode? _layout;

    /// <summary>Стартовое «Меню» ещё не решено: до первого измерения размер вида неизвестен.</summary>
    private bool _startMenuPending = true;

    /// <summary>Создаёт оболочку с настройками из файла и оценкой сети, заданной головой.</summary>
    /// <remarks>
    /// Стартовое состояние у обеих голов одно: приложение открывается на разделе «Партия», экрана
    /// настроек нет. Карточку «Меню» при запуске получает только мобильная раскладка — её выбирает
    /// размер вида, а не платформа (<see cref="ApplyLayout"/>).
    /// </remarks>
    public ShellView()
        : this(ShellViewModel.Create(SettingsStore.Load(), global::GoEngine.App.App.Evaluator))
    {
    }

    /// <summary>Создаёт оболочку с готовой моделью.</summary>
    /// <param name="shell">Модель оболочки: обе модели режимов и настройки партии.</param>
    public ShellView(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        Shell = shell;

        AvaloniaXamlLoader.Load(this);

        DataContext = shell;

        _content = this.FindControl<Panel>("ModeContent");
        _gameButton = this.FindControl<ToggleButton>("GameModeButton");
        _problemButton = this.FindControl<ToggleButton>("ProblemModeButton");
        _desktopBar = this.FindControl<Border>("DesktopBar");
        _mobileNav = this.FindControl<Border>("MobileNav");

        if (_content is not null)
        {
            // Модели берутся у оболочки: у вида партии и вида задач они те же, что в модели оболочки.
            GameArea = new BoardView(shell.Settings, global::GoEngine.App.App.Evaluator, shell.Game);
            ProblemArea = new ProblemView(shell.Problems);

            _content.Children.Add(GameArea);
            _content.Children.Add(ProblemArea);
        }

        if (this.FindControl<Panel>("UpdateHost") is { } updateHost)
        {
            // Приглашение обновления — общее для обеих раскладок: карточка показывает состояние
            // той же модели, что и экран настроек, поэтому проверка в них одна.
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

        WireButton("NavGameButton", OnNavGameClick);
        WireButton("NavProblemButton", OnNavProblemClick);
        WireButton("NavSettingsButton", OnNavSettingsClick);

        // «Меню» — не раздел: кнопка открывает карточку меню поверх вида партии и не меняет
        // подсветку разделов (замечание пользователя 2026-10-03).
        WireButton("NavMenuButton", OnNavMenuClick);

        if (GameArea is not null)
        {
            // Экран настроек закрывается и кнопкой «Назад» внутри вида партии: подсветка раздела
            // нижней навигации должна вернуться к партии.
            GameArea.SettingsClosed += OnSettingsClosed;

            // Экран настроек открывается и кнопкой в шторке: подсветка раздела настроек
            // в нижней навигации должна включаться в любом случае.
            GameArea.SettingsShown += OnSettingsShown;
        }

        shell.PropertyChanged += OnShellPropertyChanged;
        SizeChanged += OnViewSizeChanged;

        // Раскладка применяется здесь, а стартовое «Меню» открывается в ApplyLayout: до первого
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

    /// <summary>Показывает режим партии.</summary>
    /// <param name="sender">Кнопка режима.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnGameClick(object? sender, RoutedEventArgs e) => Shell.ShowGame();

    /// <summary>Показывает режим задач.</summary>
    /// <param name="sender">Кнопка режима.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnProblemClick(object? sender, RoutedEventArgs e) => Shell.ShowProblems();

    /// <summary>Открывает раздел партии из нижней навигации.</summary>
    /// <param name="sender">Кнопка раздела.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNavGameClick(object? sender, RoutedEventArgs e)
    {
        GameArea?.CloseSettingsScreen();
        GameArea?.CloseMenu();
        Shell.ShowGame();
    }

    /// <summary>Открывает раздел задач из нижней навигации.</summary>
    /// <param name="sender">Кнопка раздела.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNavProblemClick(object? sender, RoutedEventArgs e)
    {
        GameArea?.CloseSettingsScreen();
        GameArea?.CloseMenu();
        Shell.ShowProblems();
    }

    /// <summary>Открывает меню партии: раздел при этом не меняется.</summary>
    /// <param name="sender">Кнопка «Меню».</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Меню принадлежит партии: в нём новая партия, отмена партии, файлы и настройки.
    /// Кнопка — переключатель: повторное нажатие убирает карточку.
    /// </remarks>
    private void OnNavMenuClick(object? sender, RoutedEventArgs e) => GameArea?.ToggleMenu();

    /// <summary>Открывает раздел настроек из нижней навигации.</summary>
    /// <param name="sender">Кнопка раздела.</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Экран настроек принадлежит виду партии: там живут настройки и применение к партии.
    /// Оболочка только подсвечивает раздел.
    /// </remarks>
    private void OnNavSettingsClick(object? sender, RoutedEventArgs e)
    {
        GameArea?.CloseMenu();
        Shell.ShowSettingsScreen();
        GameArea?.ShowSettings();
    }

    /// <summary>Подсвечивает раздел настроек, когда он открыт.</summary>
    /// <param name="sender">Вид партии.</param>
    /// <param name="e">Признак открытия.</param>
    private void OnSettingsShown(object? sender, EventArgs e) => Shell.ShowSettingsScreen();

    /// <summary>Возвращает подсветку раздела партии, когда экран настроек закрыт.</summary>
    /// <param name="sender">Вид партии.</param>
    /// <param name="e">Признак закрытия.</param>
    private void OnSettingsClosed(object? sender, EventArgs e) => Shell.HideSettingsScreen();

    /// <summary>Обновляет видимость режимов, когда модель сообщает о переключении.</summary>
    /// <param name="sender">Модель оболочки.</param>
    /// <param name="e">Имя изменившегося свойства.</param>
    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsGameMode) or nameof(ShellViewModel.IsProblemMode))
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

        // Стартовое «Меню» открывается только в мобильной раскладке: на телефоне это единственный
        // вход к действиям партии, а настольному окну карточка поверх доски мешала бы — у него есть
        // меню-бар и мышь (замечание пользователя 2026-10-03: «окно меню должно быть первым активным
        // окном над партией» — речь про Android). Решаем по настоящему размеру: до первого измерения
        // Bounds пуст, а DecideLayout(0, 0) отвечает «настольная» — по нулю решение было бы неверным.
        // Показываем после UpdateMode: раздел «Партия» уже выбран, и вид партии видим.
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

    /// <summary>Показывает выбранный режим и приводит кнопки в согласованное состояние.</summary>
    private void UpdateMode()
    {
        if (GameArea is not null)
        {
            GameArea.IsVisible = Shell.IsGameMode;
        }

        if (ProblemArea is not null)
        {
            ProblemArea.IsVisible = Shell.IsProblemMode;
        }

        if (_gameButton is not null)
        {
            _gameButton.IsChecked = Shell.IsGameMode;
        }

        if (_problemButton is not null)
        {
            _problemButton.IsChecked = Shell.IsProblemMode;
        }
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
