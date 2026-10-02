using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты часов партии: время идёт, замирает на паузе и сбрасывается.</summary>
/// <remarks>
/// Часы — единственное место, где считается время партии: панель только показывает строку,
/// поэтому проверяется именно служба. Время двигает сам тест (<see cref="FakeTime"/>): проверки
/// не зависят от системных часов и не ждут настоящих секунд.
/// </remarks>
public sealed class GameClockTests
{
    [Fact]
    public void Новые_Часы_Стоят_И_Показывают_Ноль()
    {
        var clock = new GameClock(new FakeTime());

        Assert.False(clock.IsRunning);
        Assert.False(clock.IsPaused);
        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
        Assert.Equal("00:00", clock.Display);
    }

    [Fact]
    public void После_Старта_Время_Растёт()
    {
        var time = new FakeTime();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.True(clock.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(5), clock.Elapsed);
        Assert.Equal("00:05", clock.Display);
    }

    [Fact]
    public void На_Паузе_Время_Не_Растёт()
    {
        var time = new FakeTime();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromSeconds(30));
        clock.Pause();
        time.Advance(TimeSpan.FromMinutes(5));
        clock.Tick();

        Assert.False(clock.IsRunning);
        Assert.True(clock.IsPaused);
        Assert.Equal("00:30", clock.Display);
    }

    [Fact]
    public void Продолжение_Считает_Время_С_Накопленного()
    {
        var time = new FakeTime();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromSeconds(30));
        clock.Pause();
        time.Advance(TimeSpan.FromMinutes(5));
        clock.Resume();
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.True(clock.IsRunning);
        Assert.False(clock.IsPaused);
        Assert.Equal("00:40", clock.Display);
    }

    [Fact]
    public void Повторный_Старт_Начинает_Счёт_Заново()
    {
        var time = new FakeTime();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromMinutes(2));
        clock.Start();

        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }

    [Fact]
    public void Сброс_Обнуляет_Время_И_Останавливает_Часы()
    {
        var time = new FakeTime();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromMinutes(3));
        clock.Reset();
        time.Advance(TimeSpan.FromMinutes(3));

        Assert.False(clock.IsRunning);
        Assert.False(clock.IsPaused);
        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
        Assert.Equal("00:00", clock.Display);
    }

    [Fact]
    public void Остановка_Сохраняет_Время_И_Не_Считается_Паузой()
    {
        // Партия завершилась: время замирает на итоговом значении, но «Паузы» в панели нет.
        var time = new FakeTime();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromSeconds(45));
        clock.Stop();
        time.Advance(TimeSpan.FromSeconds(45));

        Assert.False(clock.IsRunning);
        Assert.False(clock.IsPaused);
        Assert.Equal("00:45", clock.Display);
    }

    [Fact]
    public void Формат_Строки_Показывает_Часы_Только_Когда_Они_Есть()
    {
        var time = new FakeTime();
        var clock = new GameClock(time);

        clock.Start();

        time.Advance(new TimeSpan(0, 5, 3));
        Assert.Equal("05:03", clock.Display);

        // Второй час партии: часы появляются, минуты не обнуляются.
        time.Advance(new TimeSpan(0, 57, 0));
        Assert.Equal("1:02:03", clock.Display);
    }

    [Fact]
    public void Короткая_Строка_Всегда_Минуты_И_Секунды()
    {
        // Узкая строка телефона: часы не показываются, а минуты идут дальше шестидесяти.
        var time = new FakeTime();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(new TimeSpan(0, 7, 9));

        Assert.Equal("07:09", clock.ShortDisplay);

        time.Advance(new TimeSpan(1, 0, 0));

        Assert.Equal("67:09", clock.ShortDisplay);
    }

    [Fact]
    public void Тик_У_Стоящих_Часов_Молчит()
    {
        var clock = new GameClock(new FakeTime());
        var changes = 0;

        clock.Changed += (_, _) => changes++;
        clock.Tick();

        Assert.Equal(0, changes);
    }

    [Fact]
    public void Тик_У_Идущих_Часов_Сообщает_Об_Изменении()
    {
        var clock = new GameClock(new FakeTime());
        var changes = 0;

        clock.Changed += (_, _) => changes++;

        clock.Start();
        clock.Tick();
        clock.Pause();
        clock.Tick();

        // Старт, тик и пауза: у стоящих часов тика нет.
        Assert.Equal(3, changes);
    }
}
