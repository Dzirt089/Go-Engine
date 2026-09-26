using GoEngine.App.Rendering;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты кадров анимации — T-027.</summary>
public sealed class StoneAnimationTests
{
    [Fact]
    public void Анимация_К_Середине_Камень_Полупрозрачен()
    {
        var frame = Start().Advance(StoneAnimation.DurationSeconds / 2);

        Assert.InRange(frame.AppearanceAlpha, 100, 160);
    }

    [Fact]
    public void Анимация_Завершается_Полной_Непрозрачностью()
    {
        var frame = Start().Advance(StoneAnimation.DurationSeconds);

        Assert.Equal(255, frame.AppearanceAlpha);
    }

    [Fact]
    public void Анимация_Снятый_Камень_Исчезает()
    {
        var frame = Start().Advance(StoneAnimation.DurationSeconds);

        Assert.Equal(0, frame.DisappearanceAlpha);
    }

    [Fact]
    public void Анимация_Без_Хода_Неактивна()
    {
        Assert.False(StoneAnimation.None.IsActive);
    }

    [Fact]
    public void Анимация_После_Завершения_Неактивна()
    {
        Assert.False(Start().Advance(StoneAnimation.DurationSeconds).IsActive);
    }

    /// <summary>Создаёт начало анимации: новый камень и один снятый.</summary>
    /// <returns>Кадр анимации с нулевым прогрессом.</returns>
    private static StoneAnimation Start() =>
        new(new Point(4, 4), [new Point(0, 0)], StoneColor.Black, 0);
}
