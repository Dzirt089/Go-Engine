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
    public void Заголовок_Задачи_Стоит_Над_Списком()
    {
        // Подпись «Задача» — сосед списка в том же контейнере, а не строка внутри условия.
        var view = new ProblemView(new ProblemViewModel());
        var box = view.FindControl<ComboBox>("ProblemBox")!;
        var parent = box.GetLogicalParent()!;

        Assert.Contains(
            parent.GetLogicalChildren().OfType<TextBlock>(),
            text => string.Equals(text.Text, "Задача", StringComparison.Ordinal));
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
