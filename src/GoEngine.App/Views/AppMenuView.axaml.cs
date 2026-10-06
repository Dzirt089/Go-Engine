using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Services;
using GoEngine.App.Services.Updates;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Общее меню приложения: выбор режима, «О программе» и выход из игры.</summary>
/// <remarks>
/// <para>
/// Это раздел «Меню» нижней навигации телефона. Всё, что здесь есть, принадлежит приложению,
/// а не партии: выбор режима (тот же, что в нижней навигации), справка и выход. Меню партии
/// и «Настройка партии» живут в разделе «Партия» — они действуют на партию
/// (замечание пользователя 2026-10-06; <c>DECISIONS.md</c>, D-075).
/// </para>
/// <para>
/// Своих решений вид не принимает: переключение режима — вызов модели оболочки, а «О программе»
/// и выход — события. «О программе» и подтверждение выхода перекрывают оболочку целиком, поэтому
/// показывает их она (<see cref="ShellView"/>), а вид только сообщает о нажатии.
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

        WireButton("GameModeRow", OnGameModeClick);
        WireButton("ProblemModeRow", OnProblemModeClick);
        WireButton("AboutRow", OnAboutClick);
        WireButton("ExitRow", OnExitClick);
    }

    /// <summary>Запрошен экран «О программе».</summary>
    public event EventHandler? AboutRequested;

    /// <summary>Запрошен выход из игры.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Модель оболочки: её раздел показывает вид.</summary>
    public ShellViewModel Shell { get; }

    /// <summary>Переходит в режим партии: раздел меняет оболочка, а не вид.</summary>
    /// <param name="sender">Строка «Партия».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnGameModeClick(object? sender, RoutedEventArgs e) => Shell.ShowGame();

    /// <summary>Переходит в режим задач.</summary>
    /// <param name="sender">Строка «Задачи».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnProblemModeClick(object? sender, RoutedEventArgs e) => Shell.ShowProblems();

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
