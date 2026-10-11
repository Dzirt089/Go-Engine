using Avalonia;
using Avalonia.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Настольная раскладка: панель по ширине окна, доска квадратом по центру.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-11: «сделай для Desktop версии, в полноэкранном режиме, более лучшее
/// отображение: доску центровать, а правую панель с информацией сделать пошире… для всех режимов».
/// Здесь проверяются числа: ширина панели в границах читаемости, доска — квадрат со сторонами,
/// равными отступами и без выхода за высоту окна. Проверка идёт на всех разделах: партия, задачи,
/// уроки и разбор партий раскладываются одним помощником.
/// </remarks>
public sealed class DesktopLayoutTests
{
    /// <summary>Типичные размеры настольного окна: ноутбук и большой монитор.</summary>
    public static TheoryData<double, double> Screens => new()
    {
        { 1366, 768 },
        { 1600, 900 },
        { 1920, 1080 }
    };

    [Theory]
    [MemberData(nameof(Screens))]
    public void Панель_Занимает_Читаемую_Долю_Окна(double width, double height)
    {
        _ = height;
        var panel = BoardLayoutRules.DesktopPanelWidth(width);

        Assert.InRange(panel, BoardLayoutRules.MinimumDesktopPanelWidth, BoardLayoutRules.MaximumDesktopPanelWidth);
        Assert.True(panel > 320, $"панель {panel:F0} не шире прежних 320");
        Assert.True(width - panel > panel, "доске должно остаться больше места, чем панели");
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void Доска_Квадратная_И_Помещается_По_Высоте(double width, double height)
    {
        var panel = BoardLayoutRules.DesktopPanelWidth(width);
        var side = BoardLayoutRules.DesktopBoardSide(width - panel, height);

        Assert.True(side > 0, "доска не помещается");
        Assert.True(side <= height - (2 * BoardLayoutRules.DesktopGutter) + 0.5, $"доска {side:F0} выше окна {height:F0}");
        Assert.True(side <= BoardLayoutRules.MaximumBoardSide, $"доска {side:F0} больше предела");
    }

    [Fact]
    public void Доска_Стоит_По_Центру_Своего_Места()
    {
        var view = Arrange(new ReviewView(new ReviewViewModel()), 1920, 1080);
        var board = view.FindControl<GoEngine.App.Controls.BoardControl>("Board")!;

        Assert.True(board.Bounds.Width > 0, "доска не разложена");
        Assert.Equal(board.Bounds.Width, board.Bounds.Height, 1);

        // Место доски — вся ширина окна без панели: по нему и считаются зазоры.
        var host = view.FindControl<Grid>("Layout")!;
        var panel = view.FindControl<Border>("Panel")!;
        var area = host.Bounds.Width - panel.Bounds.Width;
        var left = board.Bounds.Left;
        var right = area - board.Bounds.Right;

        Assert.True(Math.Abs(left - right) <= 1, $"зазоры доски не равны: слева {left:F0}, справа {right:F0} из места {area:F0}");
    }

    [Fact]
    public void Панель_Разбора_Шире_Прежних_320()
    {
        var view = Arrange(new ReviewView(new ReviewViewModel()), 1920, 1080);
        var panel = view.FindControl<Border>("Panel")!;

        Assert.True(panel.Bounds.Width >= BoardLayoutRules.MinimumDesktopPanelWidth, $"панель {panel.Bounds.Width:F0}");
    }

    [Fact]
    public void Разделы_Раскладываются_Одним_Правилом()
    {
        // Партия, задачи, уроки и разбор: панель везде одной ширины, доска везде квадратная.
        var views = new UserControl[]
        {
            new ProblemView(new ProblemViewModel()),
            new LessonView(new LessonViewModel()),
            new ReviewView(new ReviewViewModel())
        };

        foreach (var view in views)
        {
            Arrange(view, 1920, 1080);

            var panel = view.FindControl<Border>("Panel")!;
            var board = view.FindControl<GoEngine.App.Controls.BoardControl>("Board")!;
            var expected = BoardLayoutRules.DesktopPanelWidth(1920);

            Assert.True(Math.Abs(panel.Bounds.Width - expected) <= 2, $"{view.GetType().Name}: панель {panel.Bounds.Width:F0}, ожидалось {expected:F0}");
            Assert.Equal(board.Bounds.Width, board.Bounds.Height, 1);
        }
    }

    /// <summary>Укладывает вид в размер настольного окна.</summary>
    /// <param name="view">Вид раздела.</param>
    /// <param name="width">Ширина окна.</param>
    /// <param name="height">Высота окна.</param>
    /// <returns>Тот же вид: раскладка видом выбирается по своему размеру.</returns>
    private static T Arrange<T>(T view, double width, double height)
        where T : UserControl
    {
        // Два прохода: раскладку вид выбирает по своему размеру на первом, а размеры элементов
        // появляются на втором — после смены строк и колонок сетки.
        for (var pass = 0; pass < 2; pass++)
        {
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));

            if (view.Content is Control content)
            {
                content.Measure(new Size(width, height));
                content.Arrange(new Rect(0, 0, width, height));
            }
        }

        return view;
    }
}
