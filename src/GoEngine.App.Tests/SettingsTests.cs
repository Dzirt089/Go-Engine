using GoEngine.AI;
using GoEngine.App.Rendering;
using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты настроек партии — T-028.</summary>
public sealed class SettingsTests
{
    [Fact]
    public void Settings_Путь_В_Windows()
    {
        var path = SettingsStore.PathFor("C:\\Users\\player", "C:\\Users\\player\\AppData\\Roaming", isWindows: true, isMacOs: false);

        Assert.EndsWith(Path.Combine("GoEngine", "settings.json"), path, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_Путь_В_Linux()
    {
        var path = SettingsStore.PathFor("/home/player", string.Empty, isWindows: false, isMacOs: false);

        Assert.Equal(Path.Combine("/home/player", ".config", "goengine", "settings.json"), path);
    }

    [Fact]
    public void Settings_Путь_В_macOS()
    {
        var path = SettingsStore.PathFor("/Users/player", string.Empty, isWindows: false, isMacOs: true);

        Assert.Equal(
            Path.Combine("/Users/player", "Library", "Application Support", "GoEngine", "settings.json"),
            path);
    }

    [Fact]
    public void Settings_Круговорот_Через_Файл()
    {
        var path = Path.Combine(Path.GetTempPath(), $"go-engine-settings-{Guid.NewGuid():N}.json");
        var settings = AppSettings.From(BoardSize.Size19, DifficultyLevel.Kyu8, StoneColor.White, Komi.For19x19);

        try
        {
            Assert.True(SettingsStore.Save(settings, path).IsSuccess);
            Assert.Equal(settings, SettingsStore.Load(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Settings_Битый_Файл_Даёт_Значения_По_Умолчанию()
    {
        var path = Path.Combine(Path.GetTempPath(), $"go-engine-broken-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{ это не JSON");

            Assert.Equal(AppSettings.Default, SettingsStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Settings_Неизвестный_Уровень_Даёт_Двадцать_Кю()
    {
        var settings = new AppSettings { AiRankKyu = 999 };

        Assert.Equal(DifficultyLevel.Kyu20, settings.ToDifficultyLevel());
    }

    [Fact]
    public void Settings_Недопустимое_Коми_Заменяется_Стандартным()
    {
        var settings = new AppSettings { BoardSize = 13, Komi = -1 };

        Assert.Equal(Komi.For13x13, settings.ToKomi());
    }
}
