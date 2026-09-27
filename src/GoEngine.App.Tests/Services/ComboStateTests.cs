using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты состояния списка выбора: программные обновления не считаются выбором игрока.</summary>
/// <remarks>
/// Регрессия, которую проверяют тесты: подмена <c>ItemsSource</c> заставляла список выбора
/// сбросить индекс и сообщить об этом модели представления — партия начиналась не с выбранным
/// уровнем («выбрано 25 кю, играет 30 кю»).
/// </remarks>
public sealed class ComboStateTests
{
    [Fact]
    public void Изменение_Во_Время_Обновления_Не_Считается_Выбором()
    {
        var state = new ComboState();

        _ = state.BeginUpdate(["30 кю", "25 кю"], 1);

        // Список сообщает о сбросе индекса на 0: это следствие обновления, а не выбор игрока.
        Assert.False(state.TryAccept(0));
        Assert.Equal(1, state.Index);

        state.EndUpdate();

        Assert.True(state.TryAccept(0));
        Assert.Equal(0, state.Index);
    }

    [Fact]
    public void Тот_Же_Список_Подписей_Не_Требует_Подмены()
    {
        string[] labels = ["30 кю", "25 кю"];
        var state = new ComboState();

        Assert.True(state.BeginUpdate(labels, 0));
        state.EndUpdate();

        Assert.False(state.BeginUpdate(labels, 0));
        Assert.Same(labels, state.Labels);
        state.EndUpdate();

        // Копия с тем же содержимым — уже другой экземпляр: списку нужно новое значение.
        Assert.True(state.BeginUpdate(["30 кю", "25 кю"], 1));
        Assert.Equal(1, state.Index);
    }

    [Fact]
    public void Обновление_Завершается_Даже_Если_Списка_Нет()
    {
        var state = new ComboState();

        _ = state.BeginUpdate(["30 кю"], 0);
        state.EndUpdate();

        Assert.False(state.IsUpdating);
        Assert.True(state.TryAccept(0));
    }
}
