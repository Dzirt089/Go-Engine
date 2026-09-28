namespace GoEngine.App.Tests;

/// <summary>Часы тестов: время задаётся вручную, часовой пояс — UTC.</summary>
/// <param name="now">Начальное время.</param>
/// <remarks>
/// Свои часы вместо внешнего пакета: тестам логов нужно ровно одно — управляемое «сейчас»,
/// от которого зависят имя суточного файла и уборка старых файлов.
/// </remarks>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>Текущее время теста.</summary>
    public DateTimeOffset Now { get; set; } = now;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Now;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
