using GoEngine.App.Services.Logging;

namespace GoEngine.App.Tests;

/// <summary>Тесты фасада логирования: запуск, завершение, чтение записи о сбое.</summary>
/// <remarks>
/// Тесты этого класса трогают общее состояние приложения (<see cref="AppLog"/>), поэтому собраны
/// в один класс: xUnit выполняет тесты одного класса последовательно, и они не мешают друг другу.
/// Каталог каждый раз свой временный.
/// </remarks>
[Collection("Логирование")]
public sealed class AppLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "goengine-tests", Guid.NewGuid().ToString("N"));

    private static TestClock Clock() => new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Инициализация_Пишет_Строку_О_Запуске()
    {
        AppLog.Initialize(_directory, Clock());

        try
        {
            Assert.True(AppLog.IsFileLogging);
            Assert.Equal(_directory, AppLog.Directory);

            var text = LogFile.Text(AppLog.CurrentFile!);

            Assert.Contains("Запуск приложения", text, StringComparison.Ordinal);
            Assert.Contains("версия", text, StringComparison.Ordinal);
            Assert.Contains(_directory, text, StringComparison.Ordinal);
        }
        finally
        {
            AppLog.Shutdown();
        }
    }

    [Fact]
    public void Завершение_Попадает_В_Лог()
    {
        AppLog.Initialize(_directory, Clock());
        var file = AppLog.CurrentFile;
        AppLog.Shutdown(exitCode: 3);

        var text = LogFile.Text(file!);

        Assert.Contains("Приложение завершено", text, StringComparison.Ordinal);
        Assert.Contains("3", text, StringComparison.Ordinal);
        Assert.False(AppLog.IsFileLogging);
    }

    [Fact]
    public void Сообщения_Пишутся_С_Уровнем_И_Категорией()
    {
        AppLog.Initialize(_directory, Clock());

        try
        {
            AppLogMessages.ModelsUnavailable(AppLog.For("Models"), "модель не найдена");
            AppLogMessages.NewGame(AppLog.For("Game"), "9×9", "20 кю", "Чёрные", "5.5");
        }
        finally
        {
            AppLog.Shutdown();
        }

        var text = LogFile.Text(Path.Combine(_directory, "goengine-20260929.log"));

        Assert.Contains("[WRN]", text, StringComparison.Ordinal);
        Assert.Contains("[INF]", text, StringComparison.Ordinal);
        Assert.Contains("Models: Модели не загружены: модель не найдена", text, StringComparison.Ordinal);
        Assert.Contains("Game: Новая партия: доска 9×9", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Сбой_Прошлого_Запуска_Читается_Один_Раз()
    {
        Directory.CreateDirectory(_directory);

        var marker = Path.Combine(_directory, CrashReport.MarkerFileName);
        var crash = new CrashReport(Clock().Now, "0.2.33", Path.Combine(_directory, "crash-20260929-115900.log"), "InvalidOperationException: тест");
        crash.Write(marker);

        AppLog.Initialize(_directory, Clock());

        try
        {
            Assert.NotNull(AppLog.PreviousCrash);
            Assert.Equal("InvalidOperationException: тест", AppLog.PreviousCrash!.Value.Reason);
            Assert.False(File.Exists(marker), "признак показывается один раз");
        }
        finally
        {
            AppLog.Shutdown();
        }

        AppLog.Initialize(_directory, Clock());

        try
        {
            Assert.Null(AppLog.PreviousCrash);
        }
        finally
        {
            AppLog.Shutdown();
        }
    }

    [Fact]
    public void Недоступный_Каталог_Не_Роняет_Приложение()
    {
        var blocked = Path.Combine(_directory, "logs");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(blocked, "файл вместо каталога");

        AppLog.Initialize(blocked, Clock());

        try
        {
            Assert.False(AppLog.IsFileLogging);

            // Запись в неработающий лог — не исключение: игра обязана продолжать работать.
            AppLogMessages.ModelsUnavailable(AppLog.For("Models"), "проверка отказа");
            AppLog.Flush();
        }
        finally
        {
            AppLog.Shutdown();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        AppLog.Shutdown();

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
