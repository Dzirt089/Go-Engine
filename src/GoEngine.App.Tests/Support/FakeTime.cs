namespace GoEngine.App.Tests;

/// <summary>Часы тестов партии: время двигает сам тест.</summary>
/// <remarks>
/// <para>
/// Часы общие для наборов, где идёт время: тесты часов (<c>GameClockTests</c>) и тесты хода партии
/// (<c>GameFlowTests</c>). Прежде класс был объявлен внутри тестов часов; вынесен в опору, чтобы
/// поддельные часы были одни на проект.
/// </para>
/// <para>
/// Свои часы, а не пакет <c>Microsoft.Extensions.TimeProvider.Testing</c>: к проекту тестов
/// интерфейса он не подключён — ссылка на пакет живёт только в <c>GoEngine.Tests</c>, — а заводить
/// зависимость значит править файл сборки, который ведёт не этот заход. Метки времени — тики,
/// поэтому <c>GetElapsedTime</c> считает ровно то, что подвинул тест.
/// </para>
/// <para>
/// От <see cref="TestClock"/> отличается тем, что тот отдаёт только «сейчас»: его читают тесты
/// логов, а часам партии нужны метки времени и сдвиг обоих значений одной командой.
/// </para>
/// </remarks>
internal sealed class FakeTime : TimeProvider
{
    private long _timestamp;
    private DateTimeOffset _now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <inheritdoc />
    public override long GetTimestamp() => _timestamp;

    /// <summary>Двигает время вперёд.</summary>
    /// <param name="delta">На сколько подвинуть.</param>
    public void Advance(TimeSpan delta)
    {
        _timestamp += delta.Ticks;
        _now += delta;
    }
}
