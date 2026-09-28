using GoEngine.App.Rendering;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты видимости анимации камней.</summary>
/// <remarks>
/// Анимация должна быть заметна глазом: при длительности 180 мс она на телефоне не читалась
/// (жалоба «всё происходит резко в один миг»). Проверка сторожит длительность и постепенность.
/// </remarks>
public sealed class AnimationVisibilityTests
{
    [Fact]
    public void Анимация_Длится_Достаточно_Долго_Чтобы_Её_Было_Видно()
    {
        Assert.True(
            StoneAnimation.DurationSeconds >= 0.2,
            $"анимация {StoneAnimation.DurationSeconds} с короче 200 мс — её не видно");
    }

    [Fact]
    public void Появляющийся_Камень_Растёт_И_Проявляется()
    {
        var start = new StoneAnimation(new Point(4, 4), [], StoneColor.Empty, 0);
        var middle = start.Advance(StoneAnimation.DurationSeconds / 2);

        Assert.True(middle.AppearanceScale > start.AppearanceScale);
        Assert.True(middle.AppearanceAlpha > start.AppearanceAlpha);
        Assert.True(middle.AppearanceAlpha < 255);
    }

    [Fact]
    public void Снятые_Камни_Исчезают_Постепенно()
    {
        var start = new StoneAnimation(null, [new Point(0, 0)], StoneColor.Black, 0);
        var middle = start.Advance(StoneAnimation.DurationSeconds / 2);
        var end = middle.Advance(StoneAnimation.DurationSeconds);

        Assert.Equal((byte)255, start.DisappearanceAlpha);
        Assert.True(middle.DisappearanceAlpha is > 0 and < 255);
        Assert.Equal((byte)0, end.DisappearanceAlpha);
        Assert.False(end.IsActive);
    }
}
