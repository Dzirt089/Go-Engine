using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты строки о состоянии моделей: по ней игрок понимает, почему нет уровней с сетью.</summary>
/// <remarks>
/// Строку собирают головы платформ (на Android — после копирования моделей из пакета), а показывают
/// настройки. Раньше отказ был невидим: игрок видел только отсутствие уровней с нейросетью.
/// </remarks>
public sealed class ModelStatusTextTests
{
    [Fact]
    public void Загруженные_Модели_Называют_Размеры_Доски()
    {
        var text = ModelStatusText.Loaded([19, 9, 9]);

        Assert.Equal("Модели загружены: 9×9, 19×19", text);
    }

    [Fact]
    public void Без_Размеров_Строка_Говорит_Что_Моделей_Нет()
    {
        Assert.Equal(ModelStatusText.Missing, ModelStatusText.Loaded([]));
    }

    [Fact]
    public void Отказ_Называет_Причину()
    {
        Assert.Equal("Модели не загружены: не хватило места", ModelStatusText.Failed("не хватило места"));
    }

    [Fact]
    public void Отказ_Без_Причины_Это_Ошибка_Вызова()
    {
        _ = Assert.Throws<ArgumentException>(() => ModelStatusText.Failed("   "));
    }

    [Fact]
    public void Список_Размеров_Обязателен()
    {
        _ = Assert.Throws<ArgumentNullException>(() => ModelStatusText.Loaded(null!));
    }
}
