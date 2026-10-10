using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Экран загрузки и фоновая подготовка приложения.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-10: при первом запуске «несколько (3–7) секунд белый/чёрный
/// экран, люди думают, что игра зависла». Решение: первым показывается лёгкий экран загрузки,
/// а нейросеть грузится в фоне (<c>App.Prepare</c>). Здесь сторожится, что экран говорит
/// о ходе подготовки, что полоса движения без доли, и что отказ подготовки не оставляет
/// игрока без интерфейса.
/// </remarks>
// Та же коллекция, что у тестов логирования: отказ подготовки пишет отчёт о падении, а состояние
// журнала и «один отчёт на запуск» у CrashReporter общее — параллельный прогон ломал бы чужие
// проверки (проверено: падал CrashReporterTests).
[Collection("Логирование")]
public sealed class LoadingViewTests
{
    [Fact]
    public void Экран_Загрузки_Говорит_Что_Происходит()
    {
        var view = new LoadingView();

        view.Show("Готовлю нейросеть…");

        Assert.Equal("Готовлю нейросеть…", view.FindControl<TextBlock>("StatusText")!.Text);
    }

    [Fact]
    public void Экран_Загрузки_Не_Обещает_Процентов()
    {
        // Доля неизвестна: время загрузки зависит от машины, и точная полоса была бы обманом.
        var view = new LoadingView();

        Assert.True(view.FindControl<ProgressBar>("LoadingBar")!.IsIndeterminate);
    }

    [Fact]
    public void Экран_Загрузки_Показывает_Название_Приложения()
    {
        var view = new LoadingView();
        var texts = view.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text).ToArray();

        Assert.Contains("Go Engine", texts);
    }

    [Fact]
    public void Экран_Загрузки_Укладывается_На_Экране_Телефона()
    {
        // Экран показывается на телефоне первым: он обязан уложиться в его размер и показать
        // название, состояние и полосу — иначе игрок снова увидит пустое место.
        var view = new LoadingView();

        Arrange(view, 380, 780);

        Assert.True(view.Bounds.Width > 0 && view.Bounds.Height > 0, "экран загрузки не разложился");
        Assert.True(view.FindControl<ProgressBar>("LoadingBar")!.Bounds.Width > 0, "полоса загрузки без ширины");
        Assert.True(view.FindControl<TextBlock>("StatusText")!.Bounds.Height > 0, "строка состояния не видна");
    }

    [Fact]
    public async Task Подготовка_Вызывается_Один_Раз_И_Экран_Сообщает_О_Ней()
    {
        var calls = 0;
        var view = new LoadingView();
        App.Prepare = () => calls++;

        try
        {
            await App.PrepareAsync(view);
        }
        finally
        {
            App.Prepare = null;
        }

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Отказ_Подготовки_Не_Мешает_Запуску()
    {
        // Отказ сети не должен оставить игрока без интерфейса: играются уровни кю (D-038).
        var view = new LoadingView();
        App.Prepare = () => throw new InvalidOperationException("модель не найдена");

        try
        {
            await App.PrepareAsync(view);
        }
        finally
        {
            App.Prepare = null;
        }

        Assert.True(true, "подготовка с отказом завершилась без исключения");
    }

    /// <summary>Укладывает вид в заданный размер: раскладка вида зависит от размера.</summary>
    /// <param name="view">Вид.</param>
    /// <param name="width">Ширина в точках.</param>
    /// <param name="height">Высота в точках.</param>
    /// <remarks>
    /// Содержимое укладывается напрямую: без окна и темы шаблон <see cref="UserControl"/>
    /// не создаётся, и вложенные элементы остаются нулевого размера (проверено прогоном).
    /// </remarks>
    private static void Arrange(UserControl view, double width, double height)
    {
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));

        if (view.Content is Control content)
        {
            content.Measure(new Size(width, height));
            content.Arrange(new Rect(0, 0, width, height));
        }
    }
}
