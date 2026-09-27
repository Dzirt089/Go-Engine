using Android.App;
using GoEngine.AI.Onnx;
using GoEngine.App.Services;

namespace GoEngine.App.Android;

/// <summary>Готовит модели нейросети на Android до создания интерфейса.</summary>
/// <remarks>
/// На настольных системах каталог <c>models</c> лежит рядом с приложением, и его находит
/// <c>Program.FindModelsDirectory</c>. На Android так нельзя: файлы внутри пакета доступны
/// только на чтение и только через ресурсы, а приложение ищет каталог в файловой системе.
/// Поэтому модели копируются в каталог приложения при первом запуске, а дальше используются
/// оттуда — сеть не требует интернета (`PROJECT.md`: offline-first).
/// </remarks>
internal static class AndroidModelStartup
{
    /// <summary>Копирует модели из пакета и настраивает уровни с нейросетью.</summary>
    /// <param name="activity">Активность: её ресурсы и файловый каталог.</param>
    /// <exception cref="ArgumentNullException">Активность не задана.</exception>
    public static void Prepare(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        try
        {
            PrepareCore(activity);
        }
        catch (Exception exception)
        {
            // Причина осознанная: интерфейс обязан запуститься даже без моделей — на телефоне
            // может не хватить места, нативная библиотека ONNX может отсутствовать, файл может
            // быть битым. В любом из случаев игрок получает уровни кю, а не падение приложения.
            // Подробность не логируем: логи в Core и AI запрещены (AGENTS.md, п. 7).
            _ = exception;
        }
    }

    /// <summary>Копирует модели и подключает оценщик; отказ обрабатывает вызывающий.</summary>
    /// <param name="activity">Активность: её ресурсы и файловый каталог.</param>
    private static void PrepareCore(Activity activity)
    {
        var ranges = ModelProfiles.All
            .Select(static profile => new ModelRange(profile.FileName, profile.MinBoardSize, profile.MaxBoardSize))
            .ToList();

        var directory = Path.Combine(activity.FilesDir!.AbsolutePath, "models");
        var result = new ModelInstaller(new AndroidModelSource(activity.Assets!))
            .Install([.. ranges.Select(static range => range.FileName)], directory);

        var present = result.Installed.Concat(result.Skipped).ToList();

        global::GoEngine.App.App.ModelsDirectory = result.Directory;
        global::GoEngine.App.App.ModelSizes = ModelSizes.Available(ranges, present);

        AttachEvaluator(result.Directory, present);
    }

    /// <summary>Подключает оценщик позиции, начиная с самой маленькой модели.</summary>
    /// <param name="directory">Каталог с установленными моделями.</param>
    /// <param name="present">Имена установленных файлов.</param>
    /// <remarks>
    /// Оценщик нужен, чтобы уровни Дан появились в списке: модель под размер доски он догрузит
    /// сам (`LoadModelForBoardSize`). Начинаем с самого маленького файла, чтобы запуск
    /// на телефоне не ждал лишние 72 МБ.
    /// </remarks>
    private static void AttachEvaluator(string directory, IReadOnlyList<string> present)
    {
        foreach (var fileName in present.OrderBy(name => new FileInfo(Path.Combine(directory, name)).Length))
        {
            var loaded = OnnxEvaluator.Load(Path.Combine(directory, fileName));

            if (loaded.IsSuccess)
            {
                global::GoEngine.App.App.Evaluator = loaded.Value;

                return;
            }
        }
    }
}
