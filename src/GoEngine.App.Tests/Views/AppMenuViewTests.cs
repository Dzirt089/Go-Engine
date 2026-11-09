using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ShapePath = Avalonia.Controls.Shapes.Path;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты общего меню приложения: выбор экрана, «О программе» и выход.</summary>
/// <remarks>
/// <para>
/// Общее меню — раздел «Меню» нижней навигации телефона. В нём то, что принадлежит приложению,
/// а не партии: главный экран, партия с ИИ, обучение, оформление, сведения о сборке и выход.
/// Задачи — подраздел обучения, поэтому отдельной строки режима в меню нет (уточнение интерфейса
/// 2026-11-09). Меню партии и «Настройка партии» живут в разделе «Партия» (D-075).
/// </para>
/// <para>
/// «Главная», «О программе» и подтверждение выхода перекрывают оболочку целиком, поэтому вид
/// только сообщает о нажатии событием, а показывает их <see cref="ShellView"/>.
/// </para>
/// </remarks>
public sealed class AppMenuViewTests
{
    [Fact]
    public void Меню_Показывает_Экраны_Приложения()
    {
        var (view, _) = Create();

        Assert.NotNull(view.FindControl<Button>("HomeRow"));
        Assert.NotNull(view.FindControl<Button>("GameModeRow"));
        Assert.NotNull(view.FindControl<Button>("LessonModeRow"));
    }

    [Fact]
    public void Меню_Не_Показывает_Задач_Отдельным_Экраном()
    {
        // Задачи — третий подраздел обучения, а не отдельный режим.
        var (view, _) = Create();

        Assert.Null(view.FindControl<Button>("ProblemModeRow"));
    }

    [Fact]
    public void Строка_Партия_Выбирает_Режим_Партии()
    {
        var (view, shell) = Create();
        shell.ShowStudy(StudyPart.Problems);

        Click(view.FindControl<Button>("GameModeRow")!);

        Assert.True(shell.IsGameMode);
    }

    [Fact]
    public void Строка_Обучение_Выбирает_Режим_Обучения()
    {
        var (view, shell) = Create();

        Click(view.FindControl<Button>("LessonModeRow")!);

        Assert.True(shell.IsLessonMode);
    }

    [Fact]
    public void Выбор_Экрана_В_Меню_Уводит_Из_Раздела_Меню()
    {
        // Выбор экрана — обычный переход: раздел «Меню» уступает место выбранному экрану.
        var (view, shell) = Create();
        shell.ShowMenu();

        Click(view.FindControl<Button>("LessonModeRow")!);

        Assert.True(shell.IsLessonSection);
    }

    [Fact]
    public void Строка_Главная_Поднимает_Событие()
    {
        // Главный экран показывает оболочка: вид только сообщает о нажатии.
        var (view, _) = Create();
        var requested = false;
        view.HomeRequested += (_, _) => requested = true;

        Click(view.FindControl<Button>("HomeRow")!);

        Assert.True(requested);
    }

    [Fact]
    public void Пометка_Стоит_У_Главного_Экрана()
    {
        var (view, _) = Create();

        Assert.True(view.FindControl<ShapePath>("HomeModeCheck")!.IsVisible);
        Assert.False(view.FindControl<ShapePath>("GameModeCheck")!.IsVisible);
        Assert.False(view.FindControl<ShapePath>("LessonModeCheck")!.IsVisible);
    }

    [Fact]
    public void Пометка_Снимается_С_Главной_При_Переходе_В_Партию()
    {
        var (view, shell) = Create();

        shell.ShowGame();

        Assert.False(view.FindControl<ShapePath>("HomeModeCheck")!.IsVisible);
        Assert.True(view.FindControl<ShapePath>("GameModeCheck")!.IsVisible);
    }

    [Fact]
    public void Пометка_Переходит_На_Обучение()
    {
        var (view, shell) = Create();

        shell.ShowLessons();

        Assert.True(view.FindControl<ShapePath>("LessonModeCheck")!.IsVisible);
        Assert.False(view.FindControl<ShapePath>("GameModeCheck")!.IsVisible);
        Assert.False(view.FindControl<ShapePath>("HomeModeCheck")!.IsVisible);
    }

    [Fact]
    public void Строка_Тёмной_Темы_Включает_Тёмную()
    {
        var (view, shell) = Create();

        Click(view.FindControl<Button>("ThemeDarkRow")!);

        Assert.True(shell.IsDarkTheme);
    }

    [Fact]
    public void Строка_Светлой_Темы_Включает_Светлую()
    {
        var (view, shell) = Create();

        Click(view.FindControl<Button>("ThemeLightRow")!);

        Assert.True(shell.IsLightTheme);
    }

    [Fact]
    public void Строка_Системной_Темы_Возвращает_Системную()
    {
        var (view, shell) = Create();
        shell.SelectTheme(ThemeChoice.Dark);

        Click(view.FindControl<Button>("ThemeSystemRow")!);

        Assert.True(shell.IsSystemTheme);
    }

    [Fact]
    public void Пометка_Стоит_У_Текущей_Темы()
    {
        var (view, shell) = Create();

        shell.SelectTheme(ThemeChoice.Dark);

        Assert.True(view.FindControl<ShapePath>("ThemeDarkCheck")!.IsVisible);
        Assert.False(view.FindControl<ShapePath>("ThemeLightCheck")!.IsVisible);
        Assert.False(view.FindControl<ShapePath>("ThemeSystemCheck")!.IsVisible);
    }

    [Fact]
    public void Меню_Показывает_Три_Варианта_Оформления()
    {
        var (view, _) = Create();

        Assert.NotNull(view.FindControl<Button>("ThemeSystemRow"));
        Assert.NotNull(view.FindControl<Button>("ThemeLightRow"));
        Assert.NotNull(view.FindControl<Button>("ThemeDarkRow"));
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

    [Fact]
    public void Версия_В_Меню_И_В_О_Программе_Совпадает()
    {
        // Регрессия QA 2026-10-17: «О программе» показывало 0.2.1, а меню — 0.2.100. Число версии
        // одно на приложение, поэтому обе строки обязаны называть один и тот же номер.
        var (view, _) = Create();
        var about = new AboutView().FindControl<TextBlock>("VersionText")!.Text ?? string.Empty;
        var menu = view.FindControl<TextBlock>("VersionText")!.Text ?? string.Empty;

        Assert.Equal($"Go Engine {about["Версия: ".Length..]}", menu);
    }

    [Fact]
    public void Строка_Игра_С_Другом_Поднимает_Событие()
    {
        var (view, _) = Create();
        var requested = false;
        view.NetworkRequested += (_, _) => requested = true;

        Click(view.FindControl<Button>("NetworkRow")!);

        Assert.True(requested);
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
