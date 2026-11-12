using System.Reflection;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты списка свойств, по которым вид пересобирает панель партии.</summary>
/// <remarks>
/// Список живёт в <see cref="BoardView"/> и сверяется с моделью представления: имя свойства
/// в списке обязано существовать (опечатка молча отключила бы обновление). Так регрессия
/// «панель показывает старое» ловится тестом, а не глазами на экране. Отдельного списка
/// для выбора доски и уровня больше нет: самих списков выбора на панели нет — их даёт
/// меню окна «Партия» (замечание владельца 2026-11-12).
/// </remarks>
public sealed class PanelPropertyListTests
{
    [Fact]
    public void Список_Свойств_Панели_Называет_Только_Существующие_Свойства()
    {
        foreach (var name in BoardView.PanelProperties)
        {
            Assert.NotNull(typeof(MainViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance));
        }
    }

    [Fact]
    public void Список_Панели_Содержит_Свойства_Счёта_И_Состояния()
    {
        // Минимум, без которого панель застывает: очередь хода, состояние, счёт, итог и подписи часов.
        var panel = BoardView.PanelProperties;

        Assert.Contains(nameof(MainViewModel.ToMove), panel);
        Assert.Contains(nameof(MainViewModel.Status), panel);
        Assert.Contains(nameof(MainViewModel.Score), panel);
        Assert.Contains(nameof(MainViewModel.Outcome), panel);
        Assert.Contains(nameof(MainViewModel.IsGameStarted), panel);
        Assert.Contains(nameof(MainViewModel.IsGamePaused), panel);
        Assert.Contains(nameof(MainViewModel.IsFirstMove), panel);
    }

    [Fact]
    public void Список_Панели_Содержит_Подсказку_Об_Уровне()
    {
        // Строки о силе соперника живут в «Подробностях» и пересобираются вместе с панелью:
        // один список свойств вместо прежнего списка выбора доски и уровня.
        var panel = BoardView.PanelProperties;

        Assert.Contains(nameof(MainViewModel.LevelDescription), panel);
        Assert.Contains(nameof(MainViewModel.HasLevelHint), panel);
    }

    [Fact]
    public void Список_Панели_Не_Пуст()
    {
        Assert.NotEmpty(BoardView.PanelProperties);
    }
}
