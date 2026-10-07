using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты хранения выбранного оформления.</summary>
/// <remarks>
/// Тема — свойство приложения, а не партии: у неё свой файл рядом с настройками, и его порча
/// не должна мешать играть — игрок получает системную тему, а не исключение.
/// </remarks>
public sealed class ThemeStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "goengine-theme-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Без_Файла_Выбрана_Системная_Тема()
    {
        Assert.Equal(ThemeChoice.System, ThemeStore.Load(Path.Combine(_directory, "нет-файла.json")));
    }

    [Fact]
    public void Тёмная_Тема_Читается_После_Записи()
    {
        var path = Path.Combine(_directory, "theme.json");

        Assert.True(ThemeStore.Save(ThemeChoice.Dark, path).IsSuccess);
        Assert.Equal(ThemeChoice.Dark, ThemeStore.Load(path));
    }

    [Fact]
    public void Светлая_Тема_Читается_После_Записи()
    {
        var path = Path.Combine(_directory, "theme.json");

        Assert.True(ThemeStore.Save(ThemeChoice.Light, path).IsSuccess);
        Assert.Equal(ThemeChoice.Light, ThemeStore.Load(path));
    }

    [Fact]
    public void Испорченный_Файл_Даёт_Системную_Тему()
    {
        var path = Path.Combine(_directory, "theme.json");
        _ = Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "это не json");

        Assert.Equal(ThemeChoice.System, ThemeStore.Load(path));
    }

    [Fact]
    public void Неизвестное_Имя_Темы_Даёт_Системную()
    {
        var path = Path.Combine(_directory, "theme.json");
        _ = Directory.CreateDirectory(_directory);
        File.WriteAllText(path, """{"theme":"Neon"}""");

        Assert.Equal(ThemeChoice.System, ThemeStore.Load(path));
    }

    [Fact]
    public void Все_Темы_Перечислены_В_Порядке_Показа()
    {
        Assert.Equal(
            [ThemeChoice.System, ThemeChoice.Light, ThemeChoice.Dark],
            ThemeChoice.All);
    }

    [Fact]
    public void Выбор_Темы_Переводится_В_Вариант_Платформы()
    {
        Assert.Equal(Avalonia.Styling.ThemeVariant.Dark, global::GoEngine.App.App.ThemeVariantOf(ThemeChoice.Dark));
        Assert.Equal(Avalonia.Styling.ThemeVariant.Light, global::GoEngine.App.App.ThemeVariantOf(ThemeChoice.Light));
        Assert.Equal(Avalonia.Styling.ThemeVariant.Default, global::GoEngine.App.App.ThemeVariantOf(ThemeChoice.System));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
