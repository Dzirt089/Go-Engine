using GoEngine.App.Services.Updates;

namespace GoEngine.App.Services.Logging;

/// <summary>Перехват падений: запись в лог, копия для отправки и признак сбоя.</summary>
/// <remarks>
/// Обработчики ставятся как можно раньше — до создания окна и до загрузки моделей, иначе падение
/// на старте останется без следа. Копия лога (<c>crash-ГГГГММДД-ЧЧММСС.log</c>) — тот файл,
/// который игрок присылает разработчику; файл-признак <c>ПОСЛЕДНИЙ_СБОЙ.txt</c> нужен, чтобы
/// следующий запуск мог показать в настройках, что прошлый закончился сбоем.
/// </remarks>
public static class CrashReporter
{
    private static int _installed;
    private static int _reported;

    /// <summary>Ставит обработчики падений: домена приложения и необработанных задач.</summary>
    /// <remarks>
    /// Повторный вызов ничего не делает. Исключение диспетчера интерфейса ставит голова: библиотека
    /// интерфейса не знает, есть ли у платформы диспетчер.
    /// </remarks>
    public static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>Записывает сбой: полную причину в лог, копию лога и файл-признак.</summary>
    /// <param name="exception">Исключение; <c>null</c> — сбой без исключения.</param>
    /// <param name="source">Источник: обработчик или место вызова.</param>
    /// <returns>Запись о сбое или <c>null</c>, если копию сделать не удалось.</returns>
    /// <remarks>
    /// Записывается только первый сбой за запуск: при падении в обработчике успевает сработать
    /// и обработчик диспетчера, и обработчик домена, а второй файл только запутал бы игрока.
    /// </remarks>
    public static CrashReport? Report(Exception? exception, string source)
    {
        if (Interlocked.Exchange(ref _reported, 1) == 1)
        {
            return AppLog.LastCrash;
        }

        var logger = AppLog.For("CrashReporter");
        var moment = AppLog.Time.GetLocalNow();

        AppLogMessages.Crash(logger, source, exception ?? new InvalidOperationException("сбой без исключения"));
        AppLog.Flush();

        var crashFile = AppLog.Writer?.CopyForCrash(moment) ?? string.Empty;

        if (crashFile.Length > 0)
        {
            AppLogMessages.CrashCopyMade(logger, crashFile);
        }

        var report = new CrashReport(moment, AppVersion.Display, crashFile, ExceptionText.Summarize(exception));

        if (AppLog.MarkerPath is { Length: > 0 } marker)
        {
            report.Write(marker);
        }

        AppLog.LastCrash = report;
        AppLog.Flush();

        return report;
    }

    /// <summary>Обработчик падения в домене приложения.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="args">Аргументы с исключением.</param>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        _ = sender;
        _ = Report(args.ExceptionObject as Exception, "AppDomain.UnhandledException");
        _ = args.IsTerminating;
    }

    /// <summary>Обработчик необработанной задачи.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="args">Аргументы с исключением.</param>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        _ = sender;
        _ = Report(args.Exception, "TaskScheduler.UnobservedTaskException");
        args.SetObserved();
    }
}
