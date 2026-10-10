using Avalonia.Controls;
using Avalonia.LogicalTree;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты вида задач: выбор задачи стоит в главном окне панели, отдельного «Условия» нет.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-03: список задач лежал внутри свёртки «Условие», и выбрать задачу
/// было нечем — списка не видно, пока не раскроешь условие. Список переехал в главное окно панели;
/// имена в разметке не менялись, поэтому код вида его по-прежнему находит. Жалоба 2026-10-04:
/// свёртка «Условие» повторяла то же, что цель, и убрана совсем — формулировка задачи показывается
/// целью задачи, а не отдельной строкой.
/// </remarks>
public sealed class ProblemViewTests
{
    [Fact]
    public void Список_Задач_Не_Внутри_Условия()
    {
        var view = new ProblemView(new ProblemViewModel());

        Assert.Null(view.FindControl<ComboBox>("ProblemBox")!.FindLogicalAncestorOfType<Expander>());
    }

    [Fact]
    public void Список_Задач_Стоит_В_Главном_Окне_Панели()
    {
        var view = new ProblemView(new ProblemViewModel());

        Assert.NotNull(view.FindControl<ComboBox>("ProblemBox"));
    }

    [Fact]
    public void Выбор_Задачи_Стоит_В_Шапке_Панели()
    {
        // Список стоит в шапке, рядом со стрелками перехода: подпись «Задача» убрана, потому что
        // стрелки и сам список объясняют себя, а место в шапке дорого (замечание 2026-10-10).
        var view = new ProblemView(new ProblemViewModel());
        var box = view.FindControl<ComboBox>("ProblemBox")!;

        Assert.NotNull(box.FindLogicalAncestorOfType<Grid>());
        Assert.Equal("PanelHeader", box.FindLogicalAncestorOfType<Grid>()!.Name);
    }

    [Fact]
    public void Управление_Задачей_Собрано_В_Одну_Строку()
    {
        // Требование замечания 2026-10-10: управление не должно занимать пол-экрана.
        var view = new ProblemView(new ProblemViewModel());
        Arrange(view);

        var actions = view.FindControl<Grid>("PanelActions")!;
        var buttons = actions.GetLogicalChildren().OfType<Button>().ToArray();

        Assert.Equal(3, buttons.Length);
        Assert.True(actions.Bounds.Height <= 52, $"строка управления {actions.Bounds.Height:F0} точек");

        Assert.All(
            buttons,
            button => Assert.True(
                button.Bounds.Right <= actions.Bounds.Width + 1,
                $"кнопка вылезла за строку: {button.Bounds.Right:F0} > {actions.Bounds.Width:F0}"));
    }

    [Fact]
    public void Условие_Задачи_Занимает_Большую_Часть_Панели()
    {
        // Полезная площадь важнее управления: текст задачи получает больше половины панели.
        var view = new ProblemView(new ProblemViewModel());
        Arrange(view);

        var panel = view.FindControl<Border>("Panel")!;
        var scroll = view.FindControl<ScrollViewer>("Conditions")!;

        Assert.True(
            scroll.Bounds.Height >= panel.Bounds.Height * 0.5,
            $"текст {scroll.Bounds.Height:F0} из панели {panel.Bounds.Height:F0}");
    }

    /// <summary>Укладывает вид в размер телефона: раскладку вид выбирает по своему размеру.</summary>
    /// <param name="view">Вид задач.</param>
    /// <remarks>
    /// Содержимое укладывается напрямую: без окна и темы шаблон <see cref="UserControl"/>
    /// не создаётся, и вложенные элементы остаются нулевого размера (проверено прогоном).
    /// </remarks>
    private static void Arrange(ProblemView view)
    {
        view.Measure(new Avalonia.Size(380, 780));
        view.Arrange(new Avalonia.Rect(0, 0, 380, 780));

        if (view.Content is Control content)
        {
            content.Measure(new Avalonia.Size(380, 780));
            content.Arrange(new Avalonia.Rect(0, 0, 380, 780));
        }
    }

    [Fact]
    public void Отдельной_Свёртки_Условия_В_Панели_Нет()
    {
        // Свёртка «Условие» говорила то же, что цель, и прятала под своим заголовком формулировку
        // задачи (замечание пользователя 2026-10-04). Разметка больше не знает ни одного Expander.
        var view = new ProblemView(new ProblemViewModel());

        Assert.Empty(view.GetLogicalDescendants().OfType<Expander>());
    }

    [Fact]
    public void Цель_Показывается_Формулировкой_Задачи()
    {
        var view = new ProblemView(new ProblemViewModel());
        var goal = view.FindControl<TextBlock>("Goal")?.Text ?? string.Empty;

        Assert.StartsWith("Цель:", goal, StringComparison.Ordinal);
        Assert.Contains(view.ViewModel.GoalText, goal, StringComparison.Ordinal);
        Assert.DoesNotContain("решение источника", goal, StringComparison.Ordinal);
    }
}
