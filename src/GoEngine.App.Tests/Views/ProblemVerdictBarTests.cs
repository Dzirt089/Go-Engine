using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using GoEngine.App.Controls;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Закреплённая строка вердикта в задачах: видна всегда и не двигает управление.</summary>
/// <remarks>
/// Регрессия QA 2026-10-17 (задача 26): длинный разбор вытеснял «Задача решена» за пределы
/// прокрутки, и подтверждение решения приходилось искать. Вердикт вынесен из прокрутки в полосу
/// над кнопками постоянной высоты, полный текст остался в прокрутке. Проверки идут по разметке:
/// окно в этой среде не открывается (<c>AGENTS.md</c>, п. 14).
/// </remarks>
public sealed class ProblemVerdictBarTests
{
    /// <summary>Высота закреплённой полосы вердикта: место под две строки занято всегда.</summary>
    private const double VerdictBarHeight = 44;

    [Fact]
    public void Вердикт_Стоит_Над_Кнопками_Вне_Прокрутки()
    {
        var view = new ProblemView(new ProblemViewModel());
        Arrange(view);

        var bar = view.FindControl<Border>("VerdictBar")!;
        var actions = view.FindControl<Grid>("PanelActions")!;

        Assert.Null(bar.FindLogicalAncestorOfType<ScrollViewer>());
        Assert.True(
            bar.Bounds.Bottom <= actions.Bounds.Top + 0.5,
            $"вердикт ниже кнопок: {bar.Bounds.Bottom:F0} > {actions.Bounds.Top:F0}");
    }

    [Fact]
    public void Место_Под_Вердикт_Занято_До_Первого_Хода()
    {
        // Пустой вердикт не должен прятать полосу: иначе при первом ходе кнопки и доска
        // сдвинулись бы вниз на её высоту.
        var view = new ProblemView(new ProblemViewModel());
        Arrange(view);

        var bar = view.FindControl<Border>("VerdictBar")!;
        var pinned = view.FindControl<TextBlock>("VerdictPinned")!;

        Assert.True(bar.IsVisible);
        Assert.Equal(VerdictBarHeight, bar.Bounds.Height, 1);
        Assert.True(string.IsNullOrEmpty(pinned.Text), $"вердикт до хода: «{pinned.Text}»");
    }

    [Fact]
    public void Вердикт_Ограничен_Двумя_Строками_С_Многоточием()
    {
        var view = new ProblemView(new ProblemViewModel());
        var pinned = view.FindControl<TextBlock>("VerdictPinned")!;

        Assert.Equal(2, pinned.MaxLines);
        Assert.Equal("CharacterEllipsis", pinned.TextTrimming.ToString());
    }

    [Fact]
    public void Решение_Показывает_Вердикт_И_Не_Двигает_Кнопки()
    {
        var model = new ProblemViewModel();
        var view = new ProblemView(model);
        Arrange(view);

        var bar = view.FindControl<Border>("VerdictBar")!;
        var actions = view.FindControl<Grid>("PanelActions")!;
        var board = view.FindControl<BoardControl>("Board")!;
        var actionsTop = actions.Bounds.Top;
        var boardTop = board.Bounds.Top;
        var boardHeight = board.Bounds.Height;

        Solve(model);
        Arrange(view);

        Assert.True(model.IsSolved, "задача не решена: вердикт решения не проверить");
        Assert.StartsWith(
            ProblemViewModel.SolvedText,
            view.FindControl<TextBlock>("VerdictPinned")!.Text ?? string.Empty,
            StringComparison.Ordinal);
        Assert.Equal(actionsTop, actions.Bounds.Top, 1);
        Assert.Equal(boardTop, board.Bounds.Top, 1);
        Assert.Equal(boardHeight, board.Bounds.Height, 1);
    }

    [Fact]
    public void Полный_Вердикт_Остаётся_В_Прокрутке()
    {
        // В закреплённой полосе строка обрезана, поэтому полный текст обязан оставаться
        // в прокручиваемой части панели — иначе длинный вердикт не дочитать.
        var model = new ProblemViewModel();
        var view = new ProblemView(model);

        Solve(model);

        var full = view.FindControl<TextBlock>("Verdict")!;
        var scroll = view.FindControl<ScrollViewer>("Conditions")!;

        Assert.Equal(model.Verdict, full.Text ?? string.Empty);
        Assert.Same(scroll, full.FindLogicalAncestorOfType<ScrollViewer>());
    }

    /// <summary>Доводит задачу до решения по принимаемым ходам.</summary>
    /// <param name="model">Модель задач.</param>
    private static void Solve(ProblemViewModel model)
    {
        var guard = 0;

        while (!model.IsSolved && guard++ < 30 && model.AcceptedMoves.Count > 0)
        {
            model.Play(model.AcceptedMoves[0].Point);
        }
    }

    /// <summary>Укладывает вид в размер телефона: раскладку вид выбирает по своему размеру.</summary>
    /// <param name="view">Вид задач.</param>
    /// <remarks>
    /// Два прохода, как в тестах раскладки: на первом вид выбирает строки сетки, на втором
    /// появляются размеры элементов. Без окна и темы шаблон <see cref="UserControl"/>
    /// не создаётся, поэтому содержимое укладывается напрямую.
    /// </remarks>
    private static void Arrange(ProblemView view)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            view.Measure(new Size(380, 780));
            view.Arrange(new Rect(0, 0, 380, 780));

            if (view.Content is Control content)
            {
                content.Measure(new Size(380, 780));
                content.Arrange(new Rect(0, 0, 380, 780));
            }
        }
    }
}
