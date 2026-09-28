using Avalonia;
using GoEngine.AI.Onnx;
using GoEngine.App.Diagnostics;
using GoEngine.App.Services.Logging;

namespace GoEngine.App;

/// <summary>Точка входа приложения.</summary>
/// <remarks>
/// Здесь только запуск: разбор аргументов и проверочные режимы вынесены в `Diagnostics`
/// (`ModeArguments`, `CheckModes`, `RenderSamples`), чтобы точка входа не разрасталась.
/// Проверочные режимы не открывают окно и возвращают код возврата — их вызывает
/// `check.ps1` / `check.sh`.
/// Логирование включается первым делом, а обработчики падений — сразу за ним: падение на старте
/// (до создания окна и до загрузки моделей) тоже обязано оставить файл, который игрок пришлёт.
/// </remarks>
internal static class Program
{
    /// <summary>Имя файла модели KataGo в папке models.</summary>
    private const string ModelFileName = "kata1-b28c512nbt-adam-s11165M-d5387M.uint8.onnx";

    /// <summary>Запускает приложение.</summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Код возврата процесса.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        AppLog.Initialize();
        CrashReporter.Install();
        App.OpenLogsDirectory = OpenLogsDirectory;

        var exitCode = 0;

        try
        {
            exitCode = Run(args);
        }
        catch (Exception exception)
        {
            // Верхний перехват: всё, что не поймано ниже, обязано оставить копию лога, а не
            // исчезнуть молча. Причину печатаем и в консоль: из неё запускают проверки.
            CrashReporter.Report(exception, "Program.Main");
            Console.Error.WriteLine($"Go Engine: сбой — {exception.Message}");
            exitCode = 1;
        }
        finally
        {
            AppLog.Shutdown(exitCode);
        }

        return exitCode;
    }

    /// <summary>Собирает конфигурацию Avalonia.</summary>
    /// <returns>Построитель приложения для запуска и тестов.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>Выполняет запуск: проверочные режимы или окно приложения.</summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Код возврата процесса.</returns>
    private static int Run(string[] args)
    {
        if (args.Contains(ModeArguments.Smoke))
        {
            // Проверка запуска без графической сессии: на сборочных агентах окно открыть нельзя.
            Console.WriteLine("Go Engine: приложение собрано, точка входа работает.");
            return 0;
        }

        if (args.Contains(ModeArguments.CrashTest))
        {
            // Проверка лога падений: режим намеренно завершает процесс сбоем (см. CheckModes).
            return CheckModes.CrashTest();
        }

        if (args.Contains(ModeArguments.Check))
        {
            // Проверка ввода без окна: попадание щелчка в точку доски считается той же
            // геометрией, что и на экране.
            return CheckModes.PointMapping();
        }

        if (args.Contains(ModeArguments.Stress))
        {
            // Проверка нагрузки: движок играет много партий подряд, память не растёт бесконтрольно.
            return CheckModes.Stress();
        }

        if (args.Contains(ModeArguments.EndToEnd))
        {
            // Финальный smoke-тест: игра, сохранение, загрузка, отмена, возврат, новый ход.
            return CheckModes.EndToEnd();
        }

        if (args.Contains(ModeArguments.Animation))
        {
            // Проверка кадров анимации без окна: состояние считается той же арифметикой.
            return CheckModes.Animation();
        }

        if (args is [ModeArguments.RenderAnimation, var framePath, ..])
        {
            RenderSamples.AnimationFrame(framePath);
            Console.WriteLine($"Go Engine: кадр анимации отрисован в {framePath}.");
            return 0;
        }

        if (args.Contains(ModeArguments.Sgf))
        {
            // Проверка формата без окна: партия записывается в файл и читается обратно.
            return CheckModes.Sgf();
        }

        if (args.Contains(ModeArguments.Settings))
        {
            // Проверка настроек без окна: запись в файл и чтение обратно.
            return CheckModes.Settings();
        }

        if (args.Contains(ModeArguments.State))
        {
            // Проверка панели статуса без окна: модель представления показывает данные партии.
            return CheckModes.ViewModel();
        }

        if (args is [ModeArguments.Render, var path, ..])
        {
            // Проверка рисования без окна: тот же рендерер, что и на экране, пишет PNG.
            RenderSamples.ToPng(path);
            Console.WriteLine($"Go Engine: доска отрисована в {path}.");
            return 0;
        }

        LoadNetwork();
        ConfigureUpdates();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        return 0;
    }

    /// <summary>Открывает каталог логов в файловом менеджере системы.</summary>
    /// <param name="directory">Каталог логов.</param>
    /// <remarks>
    /// Открытие — удобство, а не часть игры: отказ (нет проводника, каталог не создан) молча
    /// пропускается, потому что путь к файлам игрок всё равно видит на экране настроек.
    /// </remarks>
    private static void OpenLogsDirectory(string directory)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Причина осознанная: проводник может отсутствовать, а путь игрок видит в настройках.
        }
    }

    /// <summary>Загружает нейросеть, если файл модели найден, и отдаёт её интерфейсу.</summary>
    /// <remarks>
    /// Библиотека интерфейса не знает про ONNX (D-034), поэтому модель грузит голова.
    /// Файла нет или модель не подошла — уровни Дан остаются недоступными, партия играется
    /// уровнями кю: отказ сети не должен мешать запуску (D-038). Причина отказа попадает и в
    /// строку состояния для экрана настроек, и в лог: без лога на телефоне причину не узнать.
    /// </remarks>
    private static void LoadNetwork()
    {
        var logger = AppLog.For("Models");

        if (FindModelsDirectory() is not { } directory)
        {
            var missing = "каталог models не найден рядом с приложением";
            global::GoEngine.App.App.ModelsStatus = global::GoEngine.App.Services.ModelStatusText.Failed(missing);
            AppLogMessages.ModelsUnavailable(logger, missing);

            return;
        }

        // Каталог моделей нужен и без основной модели: для 9×9 подойдёт специализированная (D-039).
        global::GoEngine.App.App.ModelsDirectory = directory;
        var sizes = AvailableSizes(directory);
        global::GoEngine.App.App.ModelSizes = sizes;

        var path = Path.Combine(directory, ModelFileName);

        if (!File.Exists(path))
        {
            var absent = $"в каталоге {directory} нет файла {ModelFileName}";
            global::GoEngine.App.App.ModelsStatus = global::GoEngine.App.Services.ModelStatusText.Failed(absent);
            AppLogMessages.ModelsUnavailable(logger, absent);

            return;
        }

        var loaded = OnnxEvaluator.Load(path);

        if (!loaded.IsSuccess)
        {
            var reason = loaded.Error ?? "модель не загрузилась";
            global::GoEngine.App.App.ModelsStatus = global::GoEngine.App.Services.ModelStatusText.Failed(reason);
            AppLogMessages.ModelsUnavailable(logger, reason);

            Console.WriteLine($"Go Engine: модель не загружена — {reason}.");
            return;
        }

        global::GoEngine.App.App.Evaluator = loaded.Value!;
        global::GoEngine.App.App.ModelsStatus = global::GoEngine.App.Services.ModelStatusText.Loaded(sizes);
        AppLogMessages.ModelsReady(logger, directory, string.Join(", ", sizes.Order()));
        Console.WriteLine($"Go Engine: нейросеть загружена ({Path.GetFileName(path)}).");
    }

    /// <summary>Отдаёт интерфейсу установщик обновлений и каталог для скачанных файлов.</summary>
    /// <remarks>
    /// Библиотека интерфейса не знает, чем ставить обновление: на Windows это тихий установщик,
    /// на Linux и macOS — открытие папки с архивом. Скачанные файлы складываются во временный
    /// каталог пользователя: он переживает перезапуск приложения, но чистится системой.
    /// </remarks>
    private static void ConfigureUpdates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GoEngine", "updates");

        global::GoEngine.App.App.UpdateInstaller = new Updates.DesktopUpdateInstaller();
        global::GoEngine.App.App.UpdateDownloadDirectory = directory;
    }

    /// <summary>Собирает стороны доски, для которых в каталоге есть модель.</summary>
    /// <param name="directory">Каталог с файлами моделей.</param>
    /// <returns>Стороны доски, обеспеченные моделью.</returns>
    /// <remarks>
    /// Таблица профилей — единственное место, где задано соответствие размера и файла (D-039);
    /// здесь только проверяется, что файл на месте.
    /// </remarks>
    private static IReadOnlySet<int> AvailableSizes(string directory)
    {
        var sizes = new HashSet<int>();

        foreach (var profile in ModelProfiles.All)
        {
            if (!File.Exists(Path.Combine(directory, profile.FileName)))
            {
                continue;
            }

            for (var size = profile.MinBoardSize; size <= profile.MaxBoardSize; size++)
            {
                _ = sizes.Add(size);
            }
        }

        return sizes;
    }

    /// <summary>Ищет каталог моделей вверх от каталога сборки.</summary>
    /// <returns>Путь к каталогу <c>models</c> или <c>null</c>, если его нет.</returns>
    private static string? FindModelsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        for (var step = 0; step < 6 && directory is not null; step++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "models");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
