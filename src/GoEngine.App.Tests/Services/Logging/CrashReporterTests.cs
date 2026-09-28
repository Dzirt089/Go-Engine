using GoEngine.App.Services.Logging;

namespace GoEngine.App.Tests;

/// <summary>Тесты перехвата падений: копия лога, файл-признак и единственная запись на запуск.</summary>
/// <remarks>
/// <see cref="CrashReporter.Install"/> здесь не вызывается: тест не подменяет обработчики процесса,
/// а проверяет саму запись о сбое. Состояние <see cref="AppLog"/> общее, поэтому тесты собраны
/// в один класс.
/// </remarks>
[Collection("Логирование")]
public sealed class CrashReporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "goengine-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Отчёт_Создаёт_Копию_Лога_И_Признак_Сбоя()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        AppLog.Initialize(_directory, clock);

        try
        {
            AppLogMessages.NewGame(AppLog.For("Game"), "9×9", "20 кю", "Чёрные", "5.5");

            var report = CrashReporter.Report(new InvalidOperationException("намеренный сбой теста"), "Тест");

            Assert.NotNull(report);
            Assert.True(File.Exists(report!.Value.CrashFile), "копия лога должна существовать");
            Assert.Contains("намеренный сбой теста", LogFile.Text(report.Value.CrashFile), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(_directory, CrashReport.MarkerFileName)), "признак сбоя должен существовать");
            Assert.Contains("InvalidOperationException", report.Value.Reason, StringComparison.Ordinal);

            // Второй сбой в том же запуске не создаёт второй файл: игрок пришлёт один.
            var again = CrashReporter.Report(new InvalidOperationException("второй сбой"), "Тест");

            Assert.Equal(report.Value.CrashFile, again!.Value.CrashFile);
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
