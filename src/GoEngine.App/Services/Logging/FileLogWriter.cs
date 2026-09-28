using System.Globalization;
using System.Text;

namespace GoEngine.App.Services.Logging;

/// <summary>Пишет строки лога в файл: один файл на сутки, части по размеру, хранение несколько дней.</summary>
/// <remarks>
/// Требования к писателю — из задачи «лог, который игрок может прислать»: файл на каждый день
/// (<c>goengine-ГГГГММДД.log</c>), продолжение <c>-2</c>, <c>-3</c> при превышении размера,
/// хранение нескольких дней и запись со сбросом на диск сразу, чтобы последние строки уцелели
/// после падения. Все операции ввода-вывода обёрнуты пустым <c>catch</c>: логирование не имеет
/// права уронить игру, а сообщить о своей ошибке ему некуда (см. замечание в конце класса).
/// </remarks>
public sealed class FileLogWriter : IDisposable
{
    /// <summary>Начало имени суточного файла лога.</summary>
    public const string FilePrefix = "goengine-";

    /// <summary>Начало имени копии лога, снятой при падении.</summary>
    public const string CrashPrefix = "crash-";

    /// <summary>Расширение файлов лога.</summary>
    public const string FileExtension = ".log";

    /// <summary>Сколько дней хранятся суточные файлы.</summary>
    public const int RetentionDays = 7;

    /// <summary>Предельный размер одного файла лога.</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Сколько копий лога о падении хранится (их присылает игрок).</summary>
    public const int CrashFilesKept = 10;

    private readonly object _gate = new();
    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly int _retentionDays;
    private readonly long _maxBytes;

    private FileStream? _stream;
    private StreamWriter? _writer;
    private string? _currentPath;
    private int _currentDay;
    private long _currentBytes;

    /// <summary>Создаёт писатель для каталога.</summary>
    /// <param name="directory">Каталог логов; создаётся при первой записи.</param>
    /// <param name="time">Источник времени; <c>null</c> — системные часы.</param>
    /// <param name="retentionDays">Сколько дней хранить суточные файлы.</param>
    /// <param name="maxBytes">Предельный размер одного файла.</param>
    /// <exception cref="ArgumentException">Каталог не задан.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Хранение или размер не положительные.</exception>
    public FileLogWriter(string directory, TimeProvider? time = null, int retentionDays = RetentionDays, long maxBytes = MaxFileBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retentionDays);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        _directory = directory;
        _time = time ?? TimeProvider.System;
        _retentionDays = retentionDays;
        _maxBytes = maxBytes;

        Cleanup();
    }

    /// <summary>Каталог, в который пишутся файлы.</summary>
    public string Directory => _directory;

    /// <summary>Файл, в который идёт запись сейчас; <c>null</c> — записи ещё не было или она недоступна.</summary>
    public string? CurrentFile
    {
        get
        {
            lock (_gate)
            {
                return _currentPath;
            }
        }
    }

    /// <summary>Записывает строку лога.</summary>
    /// <param name="line">Строка без перевода строки.</param>
    /// <remarks>
    /// Пустой <c>catch</c> здесь единственное оправданное место: логирование не должно ронять игру,
    /// а сообщить о своей ошибке ему некуда — сообщение и есть лог. Поэтому отказ молча пропускается.
    /// </remarks>
    public void Write(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                if (_writer is null || IsNewDay())
                {
                    Reopen();
                }

                if (_writer is null)
                {
                    return;
                }

                // Размер считается в байтах, а не в символах: русский текст в UTF-8 занимает
                // два байта на букву, и по символам файл вырастал бы втрое против предела.
                var bytes = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

                RollIfFull(bytes);

                _writer.WriteLine(line);
                _writer.Flush();
                _stream?.Flush(flushToDisk: true);
                _currentBytes += bytes;
            }
        }
        catch
        {
            // Причина осознанная: см. remarks класса — логирование не роняет приложение.
            Close();
        }
    }

    /// <summary>Сбрасывает записанное на диск.</summary>
    /// <remarks>Нужен перед копированием лога при падении: копия обязана содержать последние строки.</remarks>
    public void Flush()
    {
        try
        {
            lock (_gate)
            {
                _writer?.Flush();
                _stream?.Flush(flushToDisk: true);
            }
        }
        catch
        {
            // Причина та же: сбой сброса не должен влиять на игру.
        }
    }

    /// <summary>Снимает копию сегодняшнего лога для отправки: <c>crash-ГГГГММДД-ЧЧММСС.log</c>.</summary>
    /// <param name="now">Момент снятия копии.</param>
    /// <returns>Путь к копии или <c>null</c>, если копию сделать не удалось.</returns>
    public string? CopyForCrash(DateTimeOffset now)
    {
        try
        {
            lock (_gate)
            {
                Flush();
                System.IO.Directory.CreateDirectory(_directory);

                var target = Path.Combine(
                    _directory,
                    CrashPrefix + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + FileExtension);

                using var copy = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read);

                foreach (var part in PartsOfDay(now.Year, now.Month, now.Day))
                {
                    using var source = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    source.CopyTo(copy);
                }

                copy.Flush(flushToDisk: true);
                PruneCrashFiles();

                return target;
            }
        }
        catch
        {
            // Копия не сделана — игра всё равно продолжает падать по своей причине, а не по этой.
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            Close();
        }
    }

    /// <summary>Удаляет суточные файлы старше срока хранения.</summary>
    /// <remarks>Вызывается при создании писателя и при смене суток: игра может работать неделями.</remarks>
    public void Cleanup()
    {
        try
        {
            if (!System.IO.Directory.Exists(_directory))
            {
                return;
            }

            var oldest = _time.GetLocalNow().Date.AddDays(-_retentionDays);

            foreach (var file in System.IO.Directory.EnumerateFiles(_directory, FilePrefix + "*" + FileExtension))
            {
                if (DayOf(file) is { } day && day < oldest)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Уборка — не повод мешать игре: нет доступа к каталогу, значит и чистить нечего.
        }
    }

    /// <summary>Проверяет, что запись идёт в файл текущих суток.</summary>
    /// <returns><c>true</c>, если сутки сменились.</returns>
    private bool IsNewDay() => _time.GetLocalNow().Day != _currentDay;

    /// <summary>Открывает файл текущих суток, продолжая последнюю часть.</summary>
    private void Reopen()
    {
        Close();

        try
        {
            System.IO.Directory.CreateDirectory(_directory);

            var now = _time.GetLocalNow();
            _currentDay = now.Day;

            var parts = PartsOfDay(now.Year, now.Month, now.Day);
            var path = parts.Count == 0 ? PartPath(now, 1) : parts[^1];
            var size = File.Exists(path) ? new FileInfo(path).Length : 0;

            if (size >= _maxBytes)
            {
                path = Path.Combine(
                    _directory,
                    FilePrefix + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                        + "-" + (parts.Count + 1).ToString(CultureInfo.InvariantCulture) + FileExtension);
                size = 0;
            }

            _stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
                NewLine = Environment.NewLine
            };

            _currentPath = path;
            _currentBytes = size;

            Cleanup();
        }
        catch
        {
            // Каталог недоступен: лог молча не пишется, игра продолжается.
            Close();
        }
    }

    /// <summary>Переходит к следующей части, если строка не помещается в текущий файл.</summary>
    /// <param name="lineBytes">Размер записываемой строки в байтах вместе с переводом строки.</param>
    private void RollIfFull(int lineBytes)
    {
        if (_currentBytes + lineBytes <= _maxBytes)
        {
            return;
        }

        var now = _time.GetLocalNow();
        var parts = PartsOfDay(now.Year, now.Month, now.Day);

        Close();

        try
        {
            var path = Path.Combine(
                _directory,
                FilePrefix + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                    + "-" + (parts.Count + 1).ToString(CultureInfo.InvariantCulture) + FileExtension);

            _stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
                NewLine = Environment.NewLine
            };

            _currentPath = path;
            _currentBytes = new FileInfo(path).Length;
        }
        catch
        {
            Close();
        }
    }

    /// <summary>Закрывает файл, если он открыт.</summary>
    private void Close()
    {
        try
        {
            _writer?.Dispose();
        }
        catch
        {
            // Закрытие уже недоступного файла — не повод падать: писатель просто забывает его.
        }

        _writer = null;
        _stream = null;
        _currentPath = null;
        _currentBytes = 0;
    }

    /// <summary>Возвращает части лога за сутки по возрастанию номера.</summary>
    /// <param name="year">Год.</param>
    /// <param name="month">Месяц.</param>
    /// <param name="day">День.</param>
    /// <returns>Пути существующих частей.</returns>
    private List<string> PartsOfDay(int year, int month, int day)
    {
        List<string> parts = [];

        try
        {
            if (!System.IO.Directory.Exists(_directory))
            {
                return parts;
            }

            var stamp = new DateTime(year, month, day).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            for (var index = 1; ; index++)
            {
                var path = Path.Combine(
                    _directory,
                    index == 1
                        ? FilePrefix + stamp + FileExtension
                        : FilePrefix + stamp + "-" + index.ToString(CultureInfo.InvariantCulture) + FileExtension);

                if (!File.Exists(path))
                {
                    return parts;
                }

                parts.Add(path);
            }
        }
        catch
        {
            return parts;
        }
    }

    /// <summary>Путь к части лога с заданным номером.</summary>
    /// <param name="now">Дата.</param>
    /// <param name="index">Номер части, начиная с единицы.</param>
    /// <returns>Полный путь.</returns>
    private string PartPath(DateTimeOffset now, int index)
    {
        var stamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return Path.Combine(
            _directory,
            index == 1
                ? FilePrefix + stamp + FileExtension
                : FilePrefix + stamp + "-" + index.ToString(CultureInfo.InvariantCulture) + FileExtension);
    }

    /// <summary>Разбирает дату из имени суточного файла.</summary>
    /// <param name="path">Путь к файлу.</param>
    /// <returns>Дата или <c>null</c>, если имя не по шаблону.</returns>
    private static DateTime? DayOf(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);

        if (!name.StartsWith(FilePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var stamp = name[FilePrefix.Length..];
        var dash = stamp.IndexOf('-', StringComparison.Ordinal);

        if (dash >= 0)
        {
            stamp = stamp[..dash];
        }

        return DateTime.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;
    }

    /// <summary>Оставляет только несколько последних копий лога о падении.</summary>
    private void PruneCrashFiles()
    {
        try
        {
            var files = System.IO.Directory.EnumerateFiles(_directory, CrashPrefix + "*" + FileExtension)
                .OrderByDescending(static path => Path.GetFileName(path), StringComparer.Ordinal)
                .Skip(CrashFilesKept)
                .ToList();

            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
        catch
        {
            // Уборка копий — вспомогательная: её отказ не мешает игре и не портит лог.
        }
    }
}
