using System.Runtime.InteropServices;
using GoEngine.App.Services.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoEngine.App.Services.Logging;

/// <summary>Логирование приложения: файл в каталоге пользователя и запись о сбое.</summary>
/// <remarks>
/// Логи нужны, чтобы игрок мог прислать файл после падения, поэтому писатель держится открытым
/// на всё время работы, а строки сбрасываются на диск сразу. Сам лог не является частью игры:
/// если каталог недоступен, приложение работает дальше без файла, а состояние видно в
/// <see cref="IsFileLogging"/> — экран настроек честно показывает путь и отказ.
/// </remarks>
public static class AppLog
{
    /// <summary>Категория сообщений самого логирования: у статического типа нет имени для логгера.</summary>
    private const string NameCategory = "GoEngine.App.Services.Logging.AppLog";

    private static readonly object Gate = new();
    private static ILoggerFactory _factory = NullLoggerFactory.Instance;
    private static FileLoggerProvider? _provider;
    private static FileLogWriter? _writer;

    /// <summary>Источник времени: отметки в логе и имя суточного файла.</summary>
    public static TimeProvider Time { get; private set; } = TimeProvider.System;

    /// <summary>Каталог логов; <c>null</c> — логирование ещё не инициализировано.</summary>
    public static string? Directory { get; private set; }

    /// <summary>Пишется ли лог в файл.</summary>
    public static bool IsFileLogging { get; private set; }

    /// <summary>Файл, в который идёт запись сейчас.</summary>
    public static string? CurrentFile => _provider?.CurrentFile;

    /// <summary>Сбой прошлого запуска, если он был: по нему экран настроек подсказывает игроку файл.</summary>
    public static CrashReport? PreviousCrash { get; private set; }

    /// <summary>Сбой этого запуска.</summary>
    public static CrashReport? LastCrash { get; internal set; }

    /// <summary>Путь к файлу-признаку сбоя.</summary>
    public static string MarkerPath =>
        Directory is { Length: > 0 } directory ? Path.Combine(directory, CrashReport.MarkerFileName) : string.Empty;

    /// <summary>Писатель файла: нужен перехвату падений для копии лога.</summary>
    internal static FileLogWriter? Writer => _writer;

    /// <summary>Логгер для типа.</summary>
    /// <typeparam name="T">Тип, чьё имя становится категорией.</typeparam>
    /// <returns>Логгер.</returns>
    public static ILogger For<T>() => For(typeof(T).FullName ?? typeof(T).Name);

    /// <summary>Логгер для категории.</summary>
    /// <param name="category">Имя категории.</param>
    /// <returns>Логгер.</returns>
    public static ILogger For(string category) => _factory.CreateLogger(category);

    /// <summary>Включает логирование в файл и записывает строку о запуске.</summary>
    /// <param name="directory">Каталог логов; <c>null</c> — каталог по умолчанию для системы.</param>
    /// <param name="time">Источник времени; <c>null</c> — системные часы.</param>
    /// <param name="minimum">Наименьший уровень, попадающий в файл.</param>
    /// <remarks>
    /// Никогда не бросает: недоступный каталог, отсутствие прав или сломанный диск оставляют
    /// приложение без файла лога, но рабочим. Сбой прошлого запуска читается здесь же — до того,
    /// как интерфейс спросит о нём, — и файл-признак после этого удаляется, чтобы сообщение
    /// не показывалось вечно (сама копия лога остаётся на диске).
    /// </remarks>
    public static void Initialize(string? directory = null, TimeProvider? time = null, LogLevel minimum = LogLevel.Information)
    {
        var target = directory ?? LogPaths.DefaultDirectory;
        var clock = time ?? TimeProvider.System;

        try
        {
            var writer = new FileLogWriter(target, clock);
            var provider = new FileLoggerProvider(writer, minimum, clock);
            var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(minimum);
                builder.AddProvider(provider);
            });

            var logger = factory.CreateLogger(NameCategory);
            AppLogMessages.AppStarting(
                logger,
                AppVersion.Display,
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                target);

            // Доступность лога определяется первой записью: каталог мог оказаться файлом или
            // быть закрыт для записи, и обещать игроку файл в этом случае нельзя.
            var available = writer.CurrentFile is not null;

            lock (Gate)
            {
                _writer = writer;
                _provider = provider;
                _factory = factory;
                Time = clock;
                Directory = target;
                IsFileLogging = available;
            }
        }
        catch
        {
            // Каталог логов недоступен — игра продолжается без файла: сообщить об этом некуда,
            // кроме экрана настроек, который читает IsFileLogging.
            lock (Gate)
            {
                Directory = target;
                IsFileLogging = false;
            }
        }

        PreviousCrash = ReadPreviousCrash(For(NameCategory));
    }

    /// <summary>Сбрасывает записанное на диск.</summary>
    public static void Flush() => _writer?.Flush();

    /// <summary>Записывает завершение приложения и закрывает файл.</summary>
    /// <param name="exitCode">Код возврата процесса.</param>
    public static void Shutdown(int exitCode = 0)
    {
        var logger = For(NameCategory);

        if (IsFileLogging)
        {
            AppLogMessages.AppFinished(logger, exitCode);
        }

        lock (Gate)
        {
            _provider?.Dispose();
            _writer?.Dispose();
            _provider = null;
            _writer = null;
            _factory = NullLoggerFactory.Instance;
            IsFileLogging = false;
        }
    }

    /// <summary>Читает запись о сбое прошлого запуска и убирает файл-признак.</summary>
    /// <param name="logger">Логгер для предупреждения.</param>
    /// <returns>Запись о сбое или <c>null</c>.</returns>
    private static CrashReport? ReadPreviousCrash(ILogger logger)
    {
        if (MarkerPath is not { Length: > 0 } marker)
        {
            return null;
        }

        var report = CrashReport.TryRead(marker);

        if (report is not { } crash)
        {
            return null;
        }

        AppLogMessages.PreviousCrashFound(
            logger,
            crash.Moment.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            crash.Reason,
            crash.CrashFile);

        // Признак показывается один раз: копия лога и запись в логе остаются, поэтому информация
        // о сбое не теряется, а сообщение в интерфейсе не висит вечно.
        CrashReport.Delete(marker);

        return crash;
    }
}
