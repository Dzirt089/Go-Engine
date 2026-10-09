using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты хранения прогресса обучения.</summary>
/// <remarks>
/// Прогресс — свойство обучения, а не партии: у него свой файл рядом с настройками, и его порча
/// не должна мешать учиться — игрок теряет отметки, но приложение работает.
/// </remarks>
public sealed class ProgressStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "goengine-progress-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Без_Файла_Прогресс_Пуст()
    {
        var progress = ProgressStore.Load(Path.Combine(_directory, "нет-файла.json"));

        Assert.Empty(progress.Lessons);
        Assert.Empty(progress.Games);
        Assert.Equal(0, progress.CompletedLessons);
    }

    [Fact]
    public void Прогресс_Читается_После_Записи()
    {
        var path = Path.Combine(_directory, "progress.json");
        var progress = StudyProgress.Empty;

        progress.SaveLesson("go-03", 4, completed: false);
        progress.SaveGame("game-02", 57, completed: true);

        Assert.True(ProgressStore.Save(progress, path).IsSuccess);

        var loaded = ProgressStore.Load(path);

        Assert.Equal(4, loaded.ForLesson("go-03")?.Step);
        Assert.False(loaded.ForLesson("go-03")?.Completed);
        Assert.Equal(57, loaded.ForGame("game-02")?.Step);
        Assert.True(loaded.ForGame("game-02")?.Completed);
        Assert.Equal("go-03", loaded.LastLesson);
        Assert.Equal("game-02", loaded.LastGame);
        Assert.Equal(0, loaded.CompletedLessons);
        Assert.Equal(1, loaded.CompletedGames);
    }

    [Fact]
    public void Испорченный_Файл_Даёт_Пустой_Прогресс()
    {
        var path = Path.Combine(_directory, "progress.json");
        _ = Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "это не json");

        var progress = ProgressStore.Load(path);

        Assert.Empty(progress.Lessons);
        Assert.Null(progress.ForLesson("go-01"));
    }

    [Fact]
    public void Пустой_Текст_Даёт_Пустой_Прогресс()
    {
        Assert.Empty(StudyProgress.Parse(null).Games);
        Assert.Empty(StudyProgress.Parse(string.Empty).Lessons);
    }

    [Fact]
    public void Свой_Каталог_Данных_Важнее_Профиля()
    {
        // Переносимая установка и живые проверки задают каталог данных переменной окружения:
        // иначе песочница запрещает запись в профиль, и сохранение проверить нельзя.
        var portable = Path.Combine(Path.GetTempPath(), "goengine-portable");
        var profile = Path.Combine(Path.GetTempPath(), "goengine-profile");
        var roaming = Path.Combine(profile, "AppData", "Roaming");

        var root = AppDataPaths.RootOf(portable, profile, roaming, isWindows: true, isMacOs: false);

        Assert.Equal(portable, root);

        var byProfile = AppDataPaths.RootOf(null, profile, roaming, true, false);

        Assert.Equal(Path.Combine(roaming, AppDataPaths.FolderName), byProfile);
    }

    [Fact]
    public void Незнакомый_Идентификатор_Не_Находится()
    {
        Assert.Null(StudyProgress.Empty.ForLesson("нет-такого"));
        Assert.Null(StudyProgress.Empty.ForGame("нет-такой"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
