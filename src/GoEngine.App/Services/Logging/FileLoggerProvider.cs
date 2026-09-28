using Microsoft.Extensions.Logging;

namespace GoEngine.App.Services.Logging;

/// <summary>Провайдер логирования в файл: свой, без внешних библиотек.</summary>
/// <remarks>
/// Внешние провайдеры (Serilog и подобные) в проект не добавляются: нужен один формат строки
/// и полный контроль над тем, что попадает в файл, который игрок присылает разработчику.
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLogWriter _writer;
    private readonly LogLevel _minimum;
    private readonly TimeProvider _time;

    /// <summary>Создаёт провайдер поверх писателя.</summary>
    /// <param name="writer">Писатель файлов лога.</param>
    /// <param name="minimum">Наименьший уровень, который попадает в файл.</param>
    /// <param name="time">Источник времени для отметок.</param>
    /// <exception cref="ArgumentNullException">Писатель не задан.</exception>
    public FileLoggerProvider(FileLogWriter writer, LogLevel minimum, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(writer);

        _writer = writer;
        _minimum = minimum;
        _time = time;
    }

    /// <summary>Файл, в который идёт запись сейчас.</summary>
    public string? CurrentFile => _writer.CurrentFile;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer, _minimum, _time);

    /// <inheritdoc />
    public void Dispose() => _writer.Dispose();
}

/// <summary>Логгер, пишущий строки в файл через <see cref="FileLogWriter"/>.</summary>
internal sealed class FileLogger : ILogger
{
    private readonly string _category;
    private readonly FileLogWriter _writer;
    private readonly LogLevel _minimum;
    private readonly TimeProvider _time;

    /// <summary>Создаёт логгер одной категории.</summary>
    /// <param name="category">Имя категории (обычно тип).</param>
    /// <param name="writer">Писатель файлов лога.</param>
    /// <param name="minimum">Наименьший уровень, который попадает в файл.</param>
    /// <param name="time">Источник времени для отметок.</param>
    public FileLogger(string category, FileLogWriter writer, LogLevel minimum, TimeProvider time)
    {
        _category = category;
        _writer = writer;
        _minimum = minimum;
        _time = time;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _minimum;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        var moment = _time.GetLocalNow();
        var message = formatter(state, exception);
        var line = string.Concat(
            moment.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
            " [", Level(logLevel), "] ",
            _category,
            ": ",
            message);

        if (exception is not null)
        {
            line += Environment.NewLine + ExceptionText.Describe(exception);
        }

        _writer.Write(line);
    }

    /// <summary>Короткое имя уровня для строки лога.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>Обозначение уровня.</returns>
    private static string Level(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };
}
