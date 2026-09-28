using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты установки моделей в каталог приложения — доставка сети на Android.</summary>
/// <remarks>
/// На Android файлы моделей лежат внутри пакета и доступны только на чтение, поэтому при первом
/// запуске копируются в каталог приложения. Логика проверяется здесь, без Android и без ONNX.
/// </remarks>
public sealed class ModelInstallerTests
{
    [Fact]
    public void Установщик_Копирует_Модель_В_Пустой_Каталог()
    {
        var source = new FakeSource().Add("model.onnx", 1024);
        var directory = NewDirectory();

        var result = new ModelInstaller(source).Install(["model.onnx"], directory);

        Assert.Equal(["model.onnx"], result.Installed);
        Assert.Equal(1024, new FileInfo(Path.Combine(directory, "model.onnx")).Length);
    }

    [Fact]
    public void Установщик_Не_Копирует_Повторно_Файл_Того_Же_Размера()
    {
        var source = new FakeSource().Add("model.onnx", 1024);
        var directory = NewDirectory();
        var installer = new ModelInstaller(source);

        _ = installer.Install(["model.onnx"], directory);
        var second = installer.Install(["model.onnx"], directory);

        Assert.Empty(second.Installed);
        Assert.Equal(["model.onnx"], second.Skipped);
    }

    [Fact]
    public void Установщик_Перезаписывает_Файл_Другого_Размера()
    {
        var directory = NewDirectory();
        File.WriteAllBytes(Path.Combine(directory, "model.onnx"), new byte[10]);
        var source = new FakeSource().Add("model.onnx", 2048);

        var result = new ModelInstaller(source).Install(["model.onnx"], directory);

        Assert.Equal(["model.onnx"], result.Installed);
        Assert.Equal(2048, new FileInfo(Path.Combine(directory, "model.onnx")).Length);
    }

    [Fact]
    public void Установщик_Сообщает_Об_Отсутствующей_Модели()
    {
        var source = new FakeSource();
        var directory = NewDirectory();

        var result = new ModelInstaller(source).Install(["missing.onnx"], directory);

        Assert.Equal(["missing.onnx"], result.Missing);
        Assert.False(result.HasAny);
    }

    [Fact]
    public void Установщик_Копирует_Файл_Когда_Источник_Не_Знает_Размер()
    {
        // Длина неизвестна — значит копируем всегда: лучше медленнее, чем битая модель.
        var source = new FakeSource().Add("model.onnx", 512, knownLength: false);
        var directory = NewDirectory();
        var installer = new ModelInstaller(source);

        _ = installer.Install(["model.onnx"], directory);
        var second = installer.Install(["model.onnx"], directory);

        Assert.Equal(["model.onnx"], second.Installed);
    }

    [Fact]
    public void Установщик_Создаёт_Каталог()
    {
        var source = new FakeSource().Add("model.onnx", 16);
        var directory = Path.Combine(NewDirectory(), "files", "models");

        var result = new ModelInstaller(source).Install(["model.onnx"], directory);

        Assert.True(Directory.Exists(result.Directory));
    }

    [Fact]
    public void Метка_Не_Даёт_Копировать_Модель_Повторно_При_Неизвестной_Длине()
    {
        // Сжатый ресурс Android не отдаёт длину потока: без метки файл копировался бы при каждом
        // запуске — это сотня мегабайт в потоке интерфейса и жалоба «моделей не видно».
        var source = new FakeSource().Add("model.onnx", 256, knownLength: false);
        var directory = NewDirectory();
        var installer = new ModelInstaller(source);

        _ = installer.Install(["model.onnx"], directory, "1.0.0");
        var second = installer.Install(["model.onnx"], directory, "1.0.0");

        Assert.Empty(second.Installed);
        Assert.Equal(["model.onnx"], second.Skipped);
    }

    [Fact]
    public void Новая_Метка_Заставляет_Установить_Модель_Заново()
    {
        var source = new FakeSource().Add("model.onnx", 256, knownLength: false);
        var directory = NewDirectory();
        var installer = new ModelInstaller(source);

        _ = installer.Install(["model.onnx"], directory, "1.0.0");
        var second = installer.Install(["model.onnx"], directory, "1.0.1");

        Assert.Equal(["model.onnx"], second.Installed);
    }

    [Fact]
    public void Повреждённая_Копия_Ставится_Заново()
    {
        var source = new FakeSource().Add("model.onnx", 256, knownLength: false);
        var directory = NewDirectory();
        var installer = new ModelInstaller(source);

        _ = installer.Install(["model.onnx"], directory, "1.0.0");
        File.WriteAllBytes(Path.Combine(directory, "model.onnx"), new byte[10]);
        var second = installer.Install(["model.onnx"], directory, "1.0.0");

        Assert.Equal(["model.onnx"], second.Installed);
        Assert.Equal(256, new FileInfo(Path.Combine(directory, "model.onnx")).Length);
    }

    [Fact]
    public void Неполный_Набор_Не_Считается_Установленным()
    {
        var source = new FakeSource().Add("model.onnx", 128, knownLength: false);
        var directory = NewDirectory();
        var installer = new ModelInstaller(source);

        var first = installer.Install(["model.onnx", "second.onnx"], directory, "1.0.0");
        var second = installer.Install(["model.onnx"], directory, "1.0.0");

        Assert.Equal(["second.onnx"], first.Missing);
        Assert.False(first.IsComplete);
        Assert.Equal(["model.onnx"], second.Installed);
    }

    [Fact]
    public void Недокопированная_Модель_Удаляется_И_Сообщается_Об_Ошибке()
    {
        var source = new FakeSource().AddTruncated("model.onnx", declared: 512, actual: 64);
        var directory = NewDirectory();

        _ = Assert.Throws<IOException>(() => new ModelInstaller(source).Install(["model.onnx"], directory));

        Assert.False(File.Exists(Path.Combine(directory, "model.onnx")));
    }

    /// <summary>Создаёт пустой временный каталог для теста.</summary>
    /// <returns>Путь к каталогу.</returns>
    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"go-engine-models-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);

        return directory;
    }

    /// <summary>Подставной источник моделей: содержимое задаётся в памяти.</summary>
    private sealed class FakeSource : IModelSource
    {
        private readonly Dictionary<string, int> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _actual = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unknownLength = new(StringComparer.Ordinal);

        /// <summary>Добавляет файл заданного размера.</summary>
        /// <param name="fileName">Имя файла.</param>
        /// <param name="length">Размер в байтах.</param>
        /// <param name="knownLength">Известна ли длина источнику.</param>
        /// <returns>Этот же источник для цепочки вызовов.</returns>
        public FakeSource Add(string fileName, int length, bool knownLength = true)
        {
            _files[fileName] = length;

            if (!knownLength)
            {
                _ = _unknownLength.Add(fileName);
            }

            return this;
        }

        /// <inheritdoc />
        public bool Contains(string fileName) => _files.ContainsKey(fileName);

        /// <summary>Добавляет файл, который отдаёт меньше байт, чем обещает: обрыв копирования.</summary>
        /// <param name="fileName">Имя файла.</param>
        /// <param name="declared">Сколько байт обещает источник.</param>
        /// <param name="actual">Сколько байт он отдаёт на самом деле.</param>
        /// <returns>Этот же источник для цепочки вызовов.</returns>
        public FakeSource AddTruncated(string fileName, int declared, int actual)
        {
            _files[fileName] = declared;
            _actual[fileName] = actual;

            return this;
        }

        /// <inheritdoc />
        public Stream Open(string fileName)
        {
            if (!_files.TryGetValue(fileName, out var length))
            {
                throw new FileNotFoundException(fileName);
            }

            return new MemoryStream(new byte[_actual.TryGetValue(fileName, out var actual) ? actual : length]);
        }

        /// <inheritdoc />
        public long Length(string fileName) =>
            _files.TryGetValue(fileName, out var length) && !_unknownLength.Contains(fileName) ? length : -1;
    }
}
