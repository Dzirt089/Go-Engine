using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using ShapePath = Avalonia.Controls.Shapes.Path;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты главного экрана: карточки режимов, вторичная карточка «Меню» и раскладка.</summary>
/// <remarks>
/// <para>
/// Экран не принимает решений: карточки только поднимают события, а раздел показывает оболочка.
/// Поэтому здесь проверяются сами карточки, событие «Меню» и раскладка в размеры телефона (360)
/// и настольного окна (920), без окна и графической платформы.
/// </para>
/// <para>
/// Меню — вторичная карточка в одном ритме с карточками режимов (замечание пользователя
/// по скриншоту 2026-11-09): та же ширина, рамка и отступы, но ниже и без акцента.
/// </para>
/// </remarks>
public sealed class HomeViewTests
{
    /// <summary>Ширина телефона из живой приёмки: 360 dp.</summary>
    private const double PhoneWidth = 360;

    /// <summary>Высота телефона из живой приёмки.</summary>
    private const double PhoneHeight = 780;

    /// <summary>Ширина настольного окна по умолчанию (<c>MainWindow</c>).</summary>
    private const double WindowWidth = 920;

    /// <summary>Высота настольного окна по умолчанию (<c>MainWindow</c>).</summary>
    private const double WindowHeight = 720;

    [Fact]
    public void Главный_Экран_Показывает_Три_Режима_И_Меню()
    {
        var home = Layout(PhoneWidth);

        Assert.NotNull(home.FindControl<Button>("GameCard"));
        Assert.NotNull(home.FindControl<Button>("NetworkCard"));
        Assert.NotNull(home.FindControl<Button>("LessonCard"));
        Assert.NotNull(home.FindControl<Button>("MenuButton"));
        Assert.Null(home.FindControl<Button>("ProblemCard"));
    }

    [Fact]
    public void Карточки_Поднимают_События_Выбора()
    {
        var home = new HomeView();
        var game = false;
        var network = false;
        var lessons = false;
        var menu = false;

        home.GameRequested += (_, _) => game = true;
        home.NetworkRequested += (_, _) => network = true;
        home.LessonsRequested += (_, _) => lessons = true;
        home.MenuRequested += (_, _) => menu = true;

        Click(home.FindControl<Button>("GameCard")!);
        Click(home.FindControl<Button>("NetworkCard")!);
        Click(home.FindControl<Button>("LessonCard")!);
        Click(home.FindControl<Button>("MenuButton")!);

        Assert.True(game);
        Assert.True(network);
        Assert.True(lessons);
        Assert.True(menu);
    }

    [Fact]
    public void Меню_Оформлено_Вторичной_Карточкой()
    {
        // Регрессия по скриншоту 2026-11-09: «Меню» было центрированной ссылкой без рамки
        // и выпадало из ряда карточек. Теперь это карточка той же ширины, со значком и подписью.
        var home = Layout(PhoneWidth);
        var menu = home.FindControl<Button>("MenuButton")!;
        var caption = home.FindControl<TextBlock>("MenuCaption")!;

        Assert.Contains("home-menu", menu.Classes);
        Assert.NotNull(home.FindControl<Border>("MenuIcon"));
        Assert.NotEmpty(menu.GetLogicalDescendants().OfType<ShapePath>());
        Assert.Contains("настройки", caption.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Меню_Выровнено_По_Ширине_Карточек_На_Телефоне()
    {
        // Карточки режимов и «Меню» стоят в одной колонке страницы и растянуты на её ширину:
        // прежняя ссылка по центру выпадала из этого ряда (замечание по скриншоту 2026-11-09).
        AssertMenuInCardRow(Layout(PhoneWidth), CardWidth(PhoneWidth));
    }

    [Fact]
    public void Меню_Выровнено_По_Ширине_Карточек_На_Рабочем_Столе()
    {
        AssertMenuInCardRow(Layout(WindowWidth), 640);
    }

    [Fact]
    public void На_Телефоне_Карточки_В_Один_Столбец()
    {
        var home = Layout(PhoneWidth);

        Assert.True(home.IsCompactLayout);
        Assert.Contains("compact", home.FindControl<Button>("MenuButton")!.Classes);
    }

    [Fact]
    public void На_Рабочем_Столе_Карточки_В_Два_Столбца()
    {
        var home = Layout(WindowWidth);

        Assert.False(home.IsCompactLayout);
        Assert.DoesNotContain("compact", home.FindControl<Button>("MenuButton")!.Classes);
    }

    /// <summary>Собирает главный экран и раскладывает его в заданную ширину.</summary>
    /// <param name="width">Ширина экрана.</param>
    /// <returns>Разложенный вид.</returns>
    /// <remarks>
    /// Окно в тестах не открывается (<c>AGENTS.md</c>, п. 14), а раскладка выбирается по ширине
    /// вида, поэтому размер задаётся измерением и укладкой вручную — так же, как это делает окно.
    /// </remarks>
    private static HomeView Layout(double width)
    {
        var height = width < 600 ? PhoneHeight : WindowHeight;
        var home = new HomeView();

        home.ApplyLayout(width);
        home.Measure(new Size(width, height));
        home.Arrange(new Rect(0, 0, width, height));

        return home;
    }

    /// <summary>Ширина карточки на телефоне: страница минус поля по 20 точек.</summary>
    /// <param name="width">Ширина экрана.</param>
    /// <returns>Ширина карточки.</returns>
    private static double CardWidth(double width) => width - 40;

    /// <summary>Проверяет, что «Меню» стоит в ряду карточек и растянуто на ширину страницы.</summary>
    /// <param name="home">Главный экран.</param>
    /// <param name="pageWidth">Ожидаемая ширина колонки страницы.</param>
    /// <remarks>
    /// Окно в тестах не открывается, поэтому у детей нет рассчитанных <c>Bounds</c>: проверяется
    /// то, из чего раскладка складывается, — ширина колонки страницы и растяжение обеих карточек
    /// на неё без боковых отступов.
    /// </remarks>
    private static void AssertMenuInCardRow(HomeView home, double pageWidth)
    {
        var page = home.FindControl<StackPanel>("HomeContent")!;
        var game = home.FindControl<Button>("GameCard")!;
        var menu = home.FindControl<Button>("MenuButton")!;

        Assert.Equal(pageWidth, page.Width);
        Assert.Equal(HorizontalAlignment.Stretch, game.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Stretch, menu.HorizontalAlignment);
        Assert.Equal(0, menu.Margin.Left);
        Assert.Equal(0, menu.Margin.Right);
    }

    /// <summary>Нажимает кнопку так же, как её нажимает игрок.</summary>
    /// <param name="button">Кнопка разметки.</param>
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
