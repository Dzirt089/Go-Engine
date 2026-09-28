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

    [Fact]
    public void Цепочка_Исключений_Показывает_Вложенную_Причину()
    {
        // Регрессия с телефона: снаружи было видно только TypeInitializationException, а настоящая
        // причина (отказ загрузки нативной библиотеки) терялась — по строке нельзя было поставить
        // диагноз, и жалоба «модели не загружены» оставалась без подробностей.
        var inner = new DllNotFoundException("libonnxruntime.so не найдена");
        var outer = new TypeInitializationException("Microsoft.ML.OnnxRuntime.NativeMethods", inner);

        var text = ModelStatusText.Chain(outer);

        Assert.Contains(nameof(TypeInitializationException), text, StringComparison.Ordinal);
        Assert.Contains(nameof(DllNotFoundException), text, StringComparison.Ordinal);
        Assert.Contains("libonnxruntime.so не найдена", text, StringComparison.Ordinal);
        Assert.Contains("→", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Цепочка_Исключений_Ограничена_Глубиной()
    {
        // AggregateException не годится: его сообщение включает вложенные, и проверка глубины
        // сравнивала бы не то. Берём обычную цепочку.
        var inner = new ArgumentException("третий");
        var middle = new IOException("второй", inner);
        var outer = new InvalidOperationException("первый", middle);

        var text = ModelStatusText.Chain(outer, depth: 1);

        Assert.Equal($"{nameof(InvalidOperationException)}: первый", text);
    }

    [Fact]
    public void Цепочка_Без_Сообщения_Называет_Хотя_Бы_Тип()
    {
        var text = ModelStatusText.Chain(new SilentException());

        Assert.Equal(nameof(SilentException), text);
    }

    /// <summary>Исключение без сообщения: проверяет ветку, где показывать нечего, кроме типа.</summary>
    private sealed class SilentException : Exception
    {
        /// <inheritdoc />
        public override string Message => string.Empty;
    }

    [Fact]
    public void Длинная_Причина_Обрезается()
    {
        var text = ModelStatusText.Limit(new string('ж', 500));

        Assert.Equal(401, text.Length);
        Assert.EndsWith("…", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Короткая_Причина_Не_Меняется()
    {
        Assert.Equal("мало места", ModelStatusText.Limit("мало места", limit: 400));
    }
}
