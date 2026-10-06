using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ShapePath = Avalonia.Controls.Shapes.Path;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты общего меню приложения: выбор режима, «О программе» и выход.</summary>
/// <remarks>
/// <para>
/// Общее меню — раздел «Меню» нижней навигации телефона. В нём то, что принадлежит приложению,
/// а не партии: выбор режима, сведения о сборке и выход. Меню партии и «Настройка партии» живут
/// в разделе «Партия» — они действуют на партию (замечание пользователя 2026-10-06,
/// <c>DECISIONS.md</c>, D-075).
/// </para>
/// <para>
/// «О программе» и подтверждение выхода перекрывают оболочку целиком, поэтому вид только сообщает
/// о нажатии событием, а показывает их <see cref="ShellView"/>. Это и проверяется: событие есть,
/// а своего экрана вид не открывает.
/// </para>
/// </remarks>
public sealed class AppMenuViewTests
{
    [Fact]
    public void Меню_Показывает_Обе_Строки_Режима()
    {
        var (view, _) = Create();

        Assert.NotNull(view.FindControl<Button>("GameModeRow"));
        Assert.NotNull(view.FindControl<Button>("ProblemModeRow"));
    }

    [Fact]
    public void Строка_Партия_Выбирает_Режим_Партии()
    {
        var (view, shell) = Create();
        shell.ShowProblems();

        Click(view.FindControl<Button>("GameModeRow")!);

        Assert.True(shell.IsGameMode);
    }

    [Fact]
    public void Строка_Задачи_Выбирает_Режим_Задач()
    {
        var (view, shell) = Create();

        Click(view.FindControl<Button>("ProblemModeRow")!);

        Assert.True(shell.IsProblemMode);
    }

    [Fact]
    public void Выбор_Режима_В_Меню_Уводит_Из_Раздела_Меню()
    {
        // Выбор режима — обычный переход: раздел «Меню» уступает место выбранному режиму.
        var (view, shell) = Create();
        shell.ShowMenu();

        Click(view.FindControl<Button>("ProblemModeRow")!);

        Assert.True(shell.IsProblemSection);
    }

    [Fact]
    public void Пометка_Стоит_У_Текущего_Режима()
    {
        var (view, _) = Create();

        Assert.True(view.FindControl<ShapePath>("GameModeCheck")!.IsVisible);
        Assert.False(view.FindControl<ShapePath>("ProblemModeCheck")!.IsVisible);
    }

    [Fact]
    public void Пометка_Переходит_На_Задачи()
    {
        var (view, shell) = Create();

        shell.ShowProblems();

        Assert.False(view.FindControl<ShapePath>("GameModeCheck")!.IsVisible);
        Assert.True(view.FindControl<ShapePath>("ProblemModeCheck")!.IsVisible);
    }

    [Fact]
    public void Строка_О_Программе_Сообщает_О_Запросе()
    {
        var (view, _) = Create();
        var requested = false;
        view.AboutRequested += (_, _) => requested = true;

        Click(view.FindControl<Button>("AboutRow")!);

        Assert.True(requested);
    }

    [Fact]
    public void Строка_Выход_Сообщает_О_Запросе()
    {
        var (view, _) = Create();
        var requested = false;
        view.ExitRequested += (_, _) => requested = true;

        Click(view.FindControl<Button>("ExitRow")!);

        Assert.True(requested);
    }

    [Fact]
    public void Выход_Стоит_Отдельной_Строкой_С_Красной_Подписью()
    {
        // Выход закрывает программу: его нельзя спутать с соседними строками.
        var (view, _) = Create();

        Assert.Contains("danger", view.FindControl<Button>("ExitRow")!.Classes);
    }

    [Fact]
    public void Меню_Не_Показывает_Настроек_Партии()
    {
        // Настройки принадлежат партии: их место — в разделе «Партия», а не в общем меню.
        var (view, _) = Create();

        Assert.DoesNotContain(
            Labels(view),
            label => label.StartsWith("Настрой", StringComparison.Ordinal));
    }

    [Fact]
    public void Версия_Показана_Внизу_Меню()
    {
        var (view, _) = Create();

        Assert.StartsWith("Go Engine ", view.FindControl<TextBlock>("VersionText")!.Text ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>Собирает вид общего меню с моделью оболочки на пустой партии.</summary>
    /// <returns>Вид меню и его модель.</returns>
    private static (AppMenuView View, ShellViewModel Shell) Create()
    {
        var settings = AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9);
        var game = new MainViewModel(settings, new Random(TestViewModel.Seed));
        var shell = new ShellViewModel(settings, game, new ProblemViewModel());

        return (new AppMenuView(shell), shell);
    }

    /// <summary>Собирает подписи всех строк меню.</summary>
    /// <param name="view">Вид меню.</param>
    /// <returns>Подписи строк.</returns>
    private static string[] Labels(AppMenuView view) => view
        .GetLogicalDescendants()
        .OfType<Button>()
        .SelectMany(button => button.GetLogicalDescendants().OfType<TextBlock>())
        .Select(text => text.Text ?? string.Empty)
        .ToArray();

    /// <summary>Нажимает кнопку так же, как её нажимает игрок.</summary>
    /// <param name="button">Кнопка разметки.</param>
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
