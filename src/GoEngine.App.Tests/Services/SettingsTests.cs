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
    public void Settings_Звук_По_Умолчанию_Включён()
    {
        // Свежая установка играет звук: так же ведёт себя и старый файл без этого ключа.
        Assert.True(AppSettings.Default.SoundEnabled);
    }

    [Fact]
    public void Settings_Выключенный_Звук_Переживает_Запись_И_Чтение()
    {
        var settings = AppSettings.From(
            BoardSize.Size9,
            DifficultyLevel.Kyu20,
            StoneColor.Black,
            Komi.For9x9,
            soundEnabled: false);

        var restored = AppSettings.Parse(settings.ToJson());

        Assert.NotNull(restored);
        Assert.False(restored!.SoundEnabled);
    }

    [Fact]
    public void Settings_Старый_Файл_Без_Ключа_Звука_Играет_Звук()
    {
        // Терпимость к старому файлу: ключа нет — звук включён, а не «сломанное значение».
        var restored = AppSettings.Parse("{\"boardSize\":13,\"aiRankKyu\":20}");

        Assert.NotNull(restored);
        Assert.True(restored!.SoundEnabled);
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
