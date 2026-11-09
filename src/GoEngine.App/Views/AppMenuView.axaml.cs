using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Services;
using GoEngine.App.Services.Updates;
// ThemeChoice — из Services: оформление это свойство приложения, а не партии.
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Общее меню приложения: выбор экрана, «О программе» и выход из игры.</summary>
/// <remarks>
/// <para>
/// Это раздел «Меню» нижней навигации телефона. Всё, что здесь есть, принадлежит приложению,
/// а не партии: выбор экрана (главная, партия с ИИ, обучение), оформление, справка и выход.
/// Меню партии и «Настройка партии» живут в разделе «Партия» — они действуют на партию
/// (замечание пользователя 2026-10-06; <c>DECISIONS.md</c>, D-075).
/// </para>
/// <para>
/// Своих решений вид не принимает: переключение экрана — вызов модели оболочки или событие,
/// а «Главная», «О программе» и выход — события. Эти экраны перекрывают оболочку целиком,
/// поэтому показывает их она (<see cref="ShellView"/>), а вид только сообщает о нажатии.
/// </para>
/// </remarks>
public sealed partial class AppMenuView : UserControl
{
    /// <summary>Создаёт вид общего меню с настройками из файла и оценкой сети, заданной головой.</summary>
    public AppMenuView()
        : this(ShellViewModel.Create(SettingsStore.Load(), global::GoEngine.App.App.Evaluator))
    {
    }

    /// <summary>Создаёт вид общего меню с моделью оболочки.</summary>
    /// <param name="shell">Модель оболочки: в ней режим и его переключение.</param>
    public AppMenuView(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        Shell = shell;

        AvaloniaXamlLoader.Load(this);

        DataContext = shell;

        // Версия — та же, что на экране «О программе»: игрок видит одно и то же число в обоих местах.
        if (this.FindControl<TextBlock>("VersionText") is { } versionText)
        {
            versionText.Text = $"Go Engine {AppVersion.Display}";
        }

        WireButton("HomeRow", OnHomeClick);
        WireButton("GameModeRow", OnGameModeClick);
        WireButton("LessonModeRow", OnLessonModeClick);
        WireButton("ThemeSystemRow", OnSystemThemeClick);
        WireButton("ThemeLightRow", OnLightThemeClick);
        WireButton("ThemeDarkRow", OnDarkThemeClick);
        WireButton("NetworkRow", OnNetworkClick);
        WireButton("AboutRow", OnAboutClick);
        WireButton("ExitRow", OnExitClick);
    }

    /// <summary>Запрошен главный экран: выбор режима.</summary>
    public event EventHandler? HomeRequested;

    /// <summary>Запрошен экран сетевой игры.</summary>
    public event EventHandler? NetworkRequested;

    /// <summary>Запрошен экран «О программе».</summary>
    public event EventHandler? AboutRequested;

    /// <summary>Запрошен выход из игры.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Модель оболочки: её раздел показывает вид.</summary>
    public ShellViewModel Shell { get; }

    /// <summary>Просит главный экран: его показывает оболочка, он заменяет содержимое раздела.</summary>
    /// <param name="sender">Строка «Главная».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnHomeClick(object? sender, RoutedEventArgs e) => HomeRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Переходит в режим партии: раздел меняет оболочка, а не вид.</summary>
    /// <param name="sender">Строка «Партия с ИИ».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnGameModeClick(object? sender, RoutedEventArgs e) => Shell.ShowGame();

    /// <summary>Переходит в режим обучения.</summary>
    /// <param name="sender">Строка «Обучение».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLessonModeClick(object? sender, RoutedEventArgs e) => Shell.ShowLessons();

    /// <summary>Включает тему «как в системе».</summary>
    /// <param name="sender">Строка «Как в системе».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSystemThemeClick(object? sender, RoutedEventArgs e) => Shell.SelectTheme(ThemeChoice.System);

    /// <summary>Включает светлую тему.</summary>
    /// <param name="sender">Строка «Светлая».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLightThemeClick(object? sender, RoutedEventArgs e) => Shell.SelectTheme(ThemeChoice.Light);

    /// <summary>Включает тёмную тему.</summary>
    /// <param name="sender">Строка «Тёмная».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnDarkThemeClick(object? sender, RoutedEventArgs e) => Shell.SelectTheme(ThemeChoice.Dark);

    /// <summary>Просит экран сетевой игры: его открывает оболочка, он перекрывает содержимое.</summary>
    /// <param name="sender">Строка «Игра с другом».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNetworkClick(object? sender, RoutedEventArgs e) => NetworkRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Показывает «О программе»: экран открывает оболочка, он перекрывает её целиком.</summary>
    /// <param name="sender">Строка «О программе».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnAboutClick(object? sender, RoutedEventArgs e) => AboutRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Просит выход из игры: спрашивает подтверждение оболочка.</summary>
    /// <param name="sender">Строка «Выход».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnExitClick(object? sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Подписывает строку меню на обработчик нажатия.</summary>
    /// <param name="name">Имя строки в разметке.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void WireButton(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += handler;
        }
    }
}
