using System.Text;
using GoEngine.App.Services.Logging;

namespace GoEngine.App.Tests;

/// <summary>Тесты писателя файлового лога: сутки, части по размеру, уборка и устойчивость к отказам.</summary>
/// <remarks>
/// Каталог для каждого теста свой (временный): рабочие логи игрока тесты трогать не должны.
/// Часы подменяются <see cref="TestClock"/>, иначе имя суточного файла и уборка зависели бы
/// от календаря запуска.
/// </remarks>
public sealed class FileLogWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "goengine-tests", Guid.NewGuid().ToString("N"));

    private static TestClock Clock(int day = 29) =>
        new(new DateTimeOffset(2026, 9, day, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Пишет_Файл_Текущих_Суток()
    {
        using var writer = new FileLogWriter(_directory, Clock());

        writer.Write("первая строка");
        writer.Write("вторая строка");

        var file = Path.Combine(_directory, "goengine-20260929.log");

        Assert.True(File.Exists(file));
        Assert.Equal(file, writer.CurrentFile);
        Assert.Equal(["первая строка", "вторая строка"], LogFile.Lines(file));
    }

    [Fact]
    public void Старые_Файлы_Удаляются_При_Создании_Писателя()
    {
        Directory.CreateDirectory(_directory);
        var old = Path.Combine(_directory, "goengine-20260901.log");
        var fresh = Path.Combine(_directory, "goengine-20260927.log");
        File.WriteAllText(old, "старое");
        File.WriteAllText(fresh, "свежее");

        using var writer = new FileLogWriter(_directory, Clock(), retentionDays: 7);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void Файл_Делится_На_Части_По_Размеру()
    {
        using var writer = new FileLogWriter(_directory, Clock(), maxBytes: 60);

        for (var index = 0; index < 6; index++)
        {
            writer.Write("строка номер " + index);
        }

        var parts = Directory.EnumerateFiles(_directory, "goengine-20260929*.log").ToList();

        Assert.True(parts.Count >= 2, "при пределе в 60 байт строки не помещаются в одну часть");

        foreach (var part in parts)
        {
            // Русский текст занимает два байта на букву: предел обязан считаться в байтах,
            // иначе файл вырастает втрое против настройки.
            Assert.True(new FileInfo(part).Length <= 60, $"{Path.GetFileName(part)} — {new FileInfo(part).Length} байт");
        }

        Assert.Equal(6, parts.SelectMany(LogFile.Lines).Count());
    }

    [Fact]
    public void Запись_Из_Нескольких_Потоков_Не_Теряет_Строк()
    {
        using var writer = new FileLogWriter(_directory, Clock());

        Parallel.For(0, 4, worker =>
        {
            for (var index = 0; index < 25; index++)
            {
                writer.Write($"поток {worker}, строка {index}");
            }
        });

        var lines = Directory.EnumerateFiles(_directory, "goengine-*.log")
            .SelectMany(LogFile.Lines)
            .ToList();

        Assert.Equal(100, lines.Count);
    }

    [Fact]
    public void Недоступный_Каталог_Не_Роняет_Запись()
    {
        // На месте каталога — файл: создать каталог нельзя, и писатель обязан молча сдаться.
        var blocked = Path.Combine(_directory, "logs");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(blocked, "это файл, а не каталог");

        using var writer = new FileLogWriter(blocked, Clock());

        writer.Write("строка в недоступный каталог");

        Assert.Null(writer.CurrentFile);
    }

    [Fact]
    public void Копия_При_Сбое_Содержит_Записи_Суток()
    {
        using var writer = new FileLogWriter(_directory, Clock());

        writer.Write("до сбоя, строка 1");
        writer.Write("до сбоя, строка 2");

        var copy = writer.CopyForCrash(Clock().Now);

        Assert.NotNull(copy);
        Assert.StartsWith("crash-20260929-120000", Path.GetFileName(copy), StringComparison.Ordinal);
        Assert.Equal(["до сбоя, строка 1", "до сбоя, строка 2"], LogFile.Lines(copy!));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
            // Временный каталог мог остаться занятым: уборка теста не влияет на результат.
        }
    }
}
