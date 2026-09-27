using GoEngine.App.Services.Updates;

namespace GoEngine.App.Tests;

/// <summary>Тесты версии приложения: разбор и сравнение.</summary>
/// <remarks>
/// Версия выпуска приходит из манифеста обновления, версия приложения — из сборки головы.
/// Локальная сборка без номера считается старее любого выпуска, иначе обновление ей не предложат.
/// </remarks>
public sealed class AppVersionTests
{
    [Theory]
    [InlineData("0.2.15", 0, 2, 15)]
    [InlineData("v0.2.15", 0, 2, 15)]
    [InlineData("V1.0.0", 1, 0, 0)]
    [InlineData("0.2.15+abc123", 0, 2, 15)]
    [InlineData("0.2.15-beta.1", 0, 2, 15)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("7", 7, 0, 0)]
    [InlineData("  0.2.15  ", 0, 2, 15)]
    public void Разбор_Читает_Версию(string text, int major, int minor, int patch)
    {
        var parsed = AppVersion.Parse(text);

        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(new AppVersion(major, minor, patch), parsed.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0.2.abc")]
    [InlineData("-1.0.0")]
    public void Разбор_Отказывает_На_Непонятной_Строке(string? text)
    {
        var parsed = AppVersion.Parse(text);

        Assert.False(parsed.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(parsed.Error));
    }

    [Fact]
    public void Локальная_Сборка_Без_Версии_Старее_Любого_Выпуска()
    {
        var current = AppVersion.ParseOrUnknown(null);
        var release = AppVersion.ParseOrUnknown("0.2.1");

        Assert.Equal(AppVersion.Unknown, current);
        Assert.True(release > current);
    }

    [Theory]
    [InlineData("0.2.15", "0.2.9", 1)]
    [InlineData("0.2.9", "0.2.15", -1)]
    [InlineData("0.2.15", "0.2.15", 0)]
    [InlineData("0.3.0", "0.2.99", 1)]
    [InlineData("1.0.0", "0.99.99", 1)]
    [InlineData("0.0.0", "0.0.1", -1)]
    public void Сравнение_Упорядочивает_Версии(string left, string right, int expected)
    {
        var result = AppVersion.ParseOrUnknown(left).CompareTo(AppVersion.ParseOrUnknown(right));

        Assert.Equal(expected, Math.Sign(result));
    }

    [Fact]
    public void Строка_Версии_Читается_Человеком()
    {
        Assert.Equal("0.2.15", new AppVersion(0, 2, 15).ToString());
    }
}
