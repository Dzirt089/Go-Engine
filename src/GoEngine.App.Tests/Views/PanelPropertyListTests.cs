using System.Reflection;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты списков свойств, по которым вид пересобирает панель партии.</summary>
/// <remarks>
/// Списки живут в <see cref="BoardView"/> и сверяются с моделью представления: имя свойства
/// в списке обязано существовать (опечатка молча отключила бы обновление), а сами списки —
/// не пересекаться (свойство, попавшее в оба, пересобирало бы панель дважды).
/// Так регрессия «панель показывает старое» ловится тестом, а не глазами на экране.
/// </remarks>
public sealed class PanelPropertyListTests
{
    [Fact]
    public void Список_Свойств_Панели_Называет_Только_Существующие_Свойства()
    {
        foreach (var name in AllNames())
        {
            Assert.NotNull(typeof(MainViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance));
        }
    }

    [Fact]
    public void Списки_Свойств_Не_Пересекаются()
    {
        var panel = BoardView.PanelProperties;
        var combos = BoardView.ComboProperties;

        Assert.Empty(panel.Intersect(combos, StringComparer.Ordinal));
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
    public void Список_Списков_Выбора_Содержит_Подписи_И_Выбранные_Значения()
    {
        var combos = BoardView.ComboProperties;

        Assert.Contains(nameof(MainViewModel.LevelLabels), combos);
        Assert.Contains(nameof(MainViewModel.SelectedLevelIndex), combos);
        Assert.Contains(nameof(MainViewModel.SelectedSizeIndex), combos);
    }

    [Fact]
    public void Оба_Списка_Не_Пусты()
    {
        Assert.NotEmpty(BoardView.PanelProperties);
        Assert.NotEmpty(BoardView.ComboProperties);
    }

    /// <summary>Все имена из обоих списков: по ним проверяется, что свойства существуют.</summary>
    /// <returns>Имена свойств модели представления.</returns>
    private static IEnumerable<string> AllNames() =>
        BoardView.PanelProperties.Concat(BoardView.ComboProperties);
}
