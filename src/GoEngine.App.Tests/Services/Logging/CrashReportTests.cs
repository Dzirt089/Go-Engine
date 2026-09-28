using GoEngine.App.Services.Logging;

namespace GoEngine.App.Tests;

/// <summary>Тесты записи о сбое: файл-признак, разбор, краткая причина и текст исключения.</summary>
public sealed class CrashReportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "goengine-tests", Guid.NewGuid().ToString("N"));

    private string Marker => Path.Combine(_directory, CrashReport.MarkerFileName);

    [Fact]
    public void Признак_Сбоя_Записывается_И_Читается()
    {
        Directory.CreateDirectory(_directory);
        var moment = new DateTimeOffset(2026, 9, 29, 12, 34, 56, TimeSpan.Zero);
        var report = new CrashReport(moment, "0.2.33", Path.Combine(_directory, "crash-20260929-123456.log"), "InvalidOperationException: тест");

        report.Write(Marker);
        var read = CrashReport.TryRead(Marker);

        Assert.NotNull(read);
        Assert.Equal(moment, read!.Value.Moment);
        Assert.Equal("0.2.33", read.Value.Version);
        Assert.Equal(report.CrashFile, read.Value.CrashFile);
        Assert.Equal("InvalidOperationException: тест", read.Value.Reason);
    }

    [Fact]
    public void Отсутствующий_И_Испорченный_Признак_Не_Читаются()
    {
        Assert.Null(CrashReport.TryRead(Path.Combine(_directory, "нет-файла.txt")));

        Directory.CreateDirectory(_directory);
        File.WriteAllText(Marker, "мусор без пар ключ=значение");

        Assert.Null(CrashReport.TryRead(Marker));
    }

    [Fact]
    public void Удаление_Признака_Убирает_Файл()
    {
        Directory.CreateDirectory(_directory);
        new CrashReport(DateTimeOffset.UnixEpoch, "0.2.33", "crash.log", "причина").Write(Marker);

        CrashReport.Delete(Marker);

        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void Строка_Для_Игрока_Называет_Файл()
    {
        var report = new CrashReport(
            new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero),
            "0.2.33",
            "/logs/crash-20260929-120000.log",
            "NullReferenceException: пустая ссылка");

        Assert.Contains("Прошлый запуск завершился сбоем", report.Summary, StringComparison.Ordinal);
        Assert.Contains("/logs/crash-20260929-120000.log", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Текст_Исключения_Содержит_Цепочку_И_Стек()
    {
        Exception outer;

        try
        {
            try
            {
                throw new InvalidOperationException("внутренняя причина");
            }
            catch (Exception inner)
            {
                // Внешнее исключение бросается, чтобы у обоих уровней был стек: без броска
                // StackTrace пуст, и проверять в тексте было бы нечего.
                throw new TypeInitializationException("Тип", inner);
            }
        }
        catch (Exception caught)
        {
            outer = caught;
        }

        var text = ExceptionText.Describe(outer);

        Assert.Contains("TypeInitializationException", text, StringComparison.Ordinal);
        Assert.Contains("внутренняя причина", text, StringComparison.Ordinal);
        Assert.Contains("вложено", text, StringComparison.Ordinal);
        Assert.Contains(nameof(CrashReportTests), text, StringComparison.Ordinal);
    }

    [Fact]
    public void Краткая_Причина_Берётся_Из_Самого_Глубокого_Исключения()
    {
        var inner = new InvalidOperationException("модель не загрузилась");
        var outer = new TypeInitializationException("Тип", inner);

        Assert.Equal("InvalidOperationException: модель не загрузилась", ExceptionText.Summarize(outer));
        Assert.Equal("причина неизвестна", ExceptionText.Summarize(null));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
            // Временный каталог мог остаться занятым: уборка теста не влияет на результат.
        }
    }
}
