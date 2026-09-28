using GoEngine.App.Services;
using GoEngine.App.Services.Logging;

namespace GoEngine.App.Tests;

/// <summary>Тесты выбора каталога логов: у каждой системы он свой.</summary>
/// <remarks>
/// Правило выбирается в одном месте для настроек и логов (<see cref="AppDataPaths"/>), поэтому
/// проверяются все три системы и общий корень: иначе логи и настройки разъедутся по разным папкам.
/// Ожидаемые пути строятся через <c>Path.Combine</c>, чтобы тест
/// проходил и на Linux, и на Windows: сравнивается правило, а не разделитель.
/// </remarks>
public sealed class LogPathsTests
{
    [Fact]
    public void Windows_Каталог_Логов_Лежит_В_AppData()
    {
        var path = LogPaths.DirectoryFor("C:\\Users\\player", "C:\\Users\\player\\AppData\\Roaming", isWindows: true, isMacOs: false);

        Assert.Equal(Path.Combine("C:\\Users\\player\\AppData\\Roaming", "GoEngine", "logs"), path);
    }

    [Fact]
    public void Linux_Каталог_Логов_Лежит_В_config()
    {
        var path = LogPaths.DirectoryFor("/home/player", string.Empty, isWindows: false, isMacOs: false);

        Assert.Equal(Path.Combine("/home/player", ".config", "goengine", "logs"), path);
    }

    [Fact]
    public void MacOs_Каталог_Логов_Лежит_В_Library_Logs()
    {
        var path = LogPaths.DirectoryFor("/Users/player", string.Empty, isWindows: false, isMacOs: true);

        Assert.Equal(Path.Combine("/Users/player", "Library", "Logs", "GoEngine"), path);
    }

    [Fact]
    public void Пустой_Профиль_Отклоняется()
    {
        Assert.Throws<ArgumentException>(() => LogPaths.DirectoryFor("   ", string.Empty, isWindows: false, isMacOs: false));
    }

    [Fact]
    public void Логи_И_Настройки_В_Одном_Корне_Кроме_macOS()
    {
        var settings = SettingsStore.PathFor("/home/player", string.Empty, isWindows: false, isMacOs: false);
        var logs = LogPaths.DirectoryFor("/home/player", string.Empty, isWindows: false, isMacOs: false);

        // Сравниваются корни, а не строки: на Windows разделители путей другие, и проверяем
        // мы правило «логи лежат рядом с настройками», а не конкретный разделитель.
        Assert.Equal(Path.GetDirectoryName(settings), Path.GetDirectoryName(logs));
        Assert.Equal("logs", Path.GetFileName(logs));
    }
}
