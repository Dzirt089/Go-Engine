using Avalonia;
using Avalonia.Controls;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.Core;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Правила мобильной раскладки не изменились от настольных правок.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-11: «на android доска поехала. Нельзя угробить его». Здесь сторожатся
/// сами правила: узкий экран по-прежнему выбирает вертикальную раскладку, доска считается по высоте
/// и ширине, панель действий выравнивается по доске. Как это выглядит на телефоне, проверяют живые
/// стенды на 380×780 — они и снимают экран.
/// </remarks>
public sealed class MobileLayoutRegressionTests
{
    [Theory]
    [InlineData(380, 780)]
    [InlineData(360, 640)]
    [InlineData(412, 915)]
    public void Узкий_Экран_Выбирает_Мобильную_Раскладку(double width, double height)
    {
        Assert.Equal(LayoutMode.Mobile, BoardLayoutRules.DecideLayout(width, height));
        Assert.True(BoardLayoutRules.IsNarrow(width, height));
    }

    [Theory]
    [InlineData(380, 780)]
    [InlineData(360, 640)]
    public void Доска_Телефона_Считается_По_Экрану(double width, double height)
    {
        var side = BoardLayoutRules.BoardHeight(width, height);

        Assert.True(side > 0, "доска не помещается");
        Assert.True(side <= width, $"доска {side:F0} шире экрана {width:F0}");
        Assert.True(side <= height, $"доска {side:F0} выше экрана {height:F0}");
        Assert.Equal(Math.Min(width, height), BoardLayoutRules.MobileBoardSide(width, height));
    }

    [Theory]
    [InlineData(380, 780)]
    [InlineData(360, 640)]
    public void Доска_Вписывается_После_Настольного_Окна(double width, double height)
    {
        // Переход «настольное окно → телефон»: доска не должна сохранить настольную сторону
        // и вылезти за экран (жалоба пользователя 2026-10-11: «на android доска поехала»).
        foreach (var view in Views())
        {
            Arrange(view, 1600, 900);
            Arrange(view, width, height);

            var board = view.FindControl<GoEngine.App.Controls.BoardControl>("Board")!;

            Assert.True(board.Bounds.Width > 0, "доска не разложена после смены размера");
            Assert.True(
                board.Bounds.Width <= width + 0.5,
                "${view.GetType().Name}: доска ${board.Bounds.Width:F0} шире экрана ${width:F0}");
            Assert.True(
                board.Bounds.Height <= height + 0.5,
                "${view.GetType().Name}: доска ${board.Bounds.Height:F0} выше экрана ${height:F0}");
        }
    }

    // Раскладку самого раздела «Партия» сторожит живой стенд: у неё своя мобильная сетка,
    // и в headless-прогоне без окна её размеры не считаются. Стенд меняет окно на телефонное
    // и проверяет, что доска вписалась в экран (verify-games.ps1).

    /// <summary>Разделы, у которых доска и панель: задачи, уроки, разбор партий.</summary>
    /// <returns>Виды разделов.</returns>
    private static UserControl[] Views() =>
    [
        new ProblemView(new ProblemViewModel()),
        new LessonView(new LessonViewModel()),
        new ReviewView(new ReviewViewModel())
    ];

    /// <summary>Укладывает вид в заданный размер и возвращает его же.</summary>
    /// <typeparam name="T">Тип вида.</typeparam>
    /// <param name="view">Вид.</param>
    /// <param name="width">Ширина.</param>
    /// <param name="height">Высота.</param>
    /// <returns>Тот же вид.</returns>
    private static T Arrange<T>(T view, double width, double height)
        where T : UserControl
    {
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

    [Fact]
    public void Панель_Действий_Выравнивается_По_Доске()
    {
        var side = BoardLayoutRules.BoardHeight(380, 780);
        var bar = BoardLayoutRules.MobileActionBarWidth(side, 380);

        Assert.True(bar >= BoardLayoutRules.MinimumActionBarWidth, $"панель {bar:F0} уже читаемой");
        Assert.True(bar <= 380, $"панель {bar:F0} шире экрана");
    }
}
