using GoEngine.Multiplayer;
using Microsoft.Extensions.Time.Testing;

namespace GoEngine.Tests;

/// <summary>Тесты таймера хода: 90 секунд, пауза и продолжение.</summary>
public sealed class TurnTimerTests
{
    [Fact]
    public void Таймер_Истекает_Через_Заданное_Время()
    {
        var time = new FakeTimeProvider();
        using var timer = new TurnTimer(time);
        var expired = 0;
        timer.Expired += (_, _) => expired++;

        timer.Start(TimeSpan.FromSeconds(90));
        time.Advance(TimeSpan.FromSeconds(89));
        Assert.Equal(0, expired);

        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1, expired);
        Assert.False(timer.IsRunning);
    }

    [Fact]
    public void Пауза_Останавливает_Отсчёт()
    {
        var time = new FakeTimeProvider();
        using var timer = new TurnTimer(time);
        var expired = 0;
        timer.Expired += (_, _) => expired++;
        timer.Start(TimeSpan.FromSeconds(90));
        time.Advance(TimeSpan.FromSeconds(30));

        timer.Pause();
        time.Advance(TimeSpan.FromSeconds(300));

        Assert.Equal(0, expired);
        Assert.True(timer.IsPaused);
    }

    [Fact]
    public void Продолжение_Идёт_С_Остатка()
    {
        var time = new FakeTimeProvider();
        using var timer = new TurnTimer(time);
        var expired = 0;
        timer.Expired += (_, _) => expired++;
        timer.Start(TimeSpan.FromSeconds(90));
        time.Advance(TimeSpan.FromSeconds(30));
        timer.Pause();

        timer.Resume();
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(0, expired);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, expired);
    }

    [Fact]
    public void Остановка_Не_Даёт_События()
    {
        var time = new FakeTimeProvider();
        using var timer = new TurnTimer(time);
        var expired = 0;
        timer.Expired += (_, _) => expired++;
        timer.Start(TimeSpan.FromSeconds(90));

        timer.Stop();
        time.Advance(TimeSpan.FromSeconds(100));

        Assert.Equal(0, expired);
        Assert.Equal(TimeSpan.Zero, timer.Remaining);
    }
}
