using Android.App;
using GoEngine.AI.Onnx;
using GoEngine.App.Services;
using GoEngine.App.Services.Updates;

namespace GoEngine.App.Android;

/// <summary>Готовит модели нейросети на Android до создания интерфейса.</summary>
/// <remarks>
/// На настольных системах каталог <c>models</c> лежит рядом с приложением, и его находит
/// <c>Program.FindModelsDirectory</c>. На Android так нельзя: файлы внутри пакета доступны
/// только на чтение и только через ресурсы, а приложение ищет каталог в файловой системе.
/// Поэтому модели копируются в каталог приложения при первом запуске, а дальше используются
/// оттуда — сеть не требует интернета (<c>PROJECT.md</c>: offline-first).
/// Отказ не роняет приложение, но и не молчит: причина попадает в состояние моделей и видна
/// игроку в настройках. Без этого жалоба «моделей не видно» неотличима от «моделей нет».
/// </remarks>
internal static class AndroidModelStartup
{
    /// <summary>Сколько символов причины показываем: технический текст не должен разносить экран.</summary>
    private const int ReasonLimit = 160;

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
            // может не хватить места, не открыться ресурс пакета или не подняться нативная
            // библиотека ONNX. Игрок получает уровни без сети, но причину видит в настройках:
            // молчаливый отказ превращал диагноз в догадки.
            global::GoEngine.App.App.ModelsStatus = ModelStatusText.Failed(Describe(exception));
        }
    }

    /// <summary>Копирует модели и подключает оценщик; отказ обрабатывает вызывающий.</summary>
    /// <param name="activity">Активность: её ресурсы и файловый каталог.</param>
    private static void PrepareCore(Activity activity)
    {
        var ranges = ModelProfiles.All
            .Select(static profile => new ModelRange(profile.FileName, profile.MinBoardSize, profile.MaxBoardSize))
            .ToList();

        var names = ranges.Select(static range => range.FileName).ToList();
        var directory = Path.Combine(activity.FilesDir!.AbsolutePath, "models");

        // Метка набора — версия сборки: у сжатого ресурса длина неизвестна, и без метки приложение
        // копировало бы сто мегабайт при каждом запуске (в потоке интерфейса — это ещё и риск ANR).
        var marker = $"{AppVersion.InformationalVersion}|{string.Join(';', names)}";

        var result = new ModelInstaller(new AndroidModelSource(activity.Assets!))
            .Install(names, directory, marker);

        var present = result.Installed.Concat(result.Skipped).ToList();
        var sizes = ModelSizes.Available(ranges, present);

        global::GoEngine.App.App.ModelsDirectory = result.Directory;
        global::GoEngine.App.App.ModelSizes = sizes;

        if (result.Missing.Count > 0)
        {
            global::GoEngine.App.App.ModelsStatus =
                ModelStatusText.Failed($"в пакете нет файлов моделей: {string.Join(", ", result.Missing)}");

            return;
        }

        global::GoEngine.App.App.ModelsStatus = AttachEvaluator(result.Directory, present) is { } failure
            ? ModelStatusText.Failed(failure)
            : ModelStatusText.Loaded(sizes);
    }

    /// <summary>Подключает оценщик позиции, начиная с самой маленькой модели.</summary>
    /// <param name="directory">Каталог с установленными моделями.</param>
    /// <param name="present">Имена установленных файлов.</param>
    /// <returns>Причина отказа или <c>null</c>, если оценщик подключён.</returns>
    /// <remarks>
    /// Оценщик нужен, чтобы уровни с сетью появились в списке: модель под размер доски он догрузит
    /// сам (<c>LoadModelForBoardSize</c>). Начинаем с самого маленького файла, чтобы запуск
    /// на телефоне не ждал лишние 72 МБ. Причина отказа нужна целиком: «модель не загрузилась»
    /// без подробностей не отличает битый файл от отсутствующей нативной библиотеки.
    /// </remarks>
    private static string? AttachEvaluator(string directory, IReadOnlyList<string> present)
    {
        if (present.Count == 0)
        {
            return "файлы моделей не найдены";
        }

        List<string> failures = [];

        foreach (var fileName in present.OrderBy(name => new FileInfo(Path.Combine(directory, name)).Length))
        {
            var loaded = OnnxEvaluator.Load(Path.Combine(directory, fileName));

            if (loaded.IsSuccess)
            {
                global::GoEngine.App.App.Evaluator = loaded.Value;

                return null;
            }

            failures.Add($"{fileName}: {loaded.Error ?? "без причины"}");
        }

        return string.Join("; ", failures);
    }

    /// <summary>Коротко описывает причину отказа для экрана настроек.</summary>
    /// <param name="exception">Исключение, остановившее подготовку моделей.</param>
    /// <returns>Причина словами, не длиннее <see cref="ReasonLimit"/> символов.</returns>
    private static string Describe(Exception exception)
    {
        var message = exception.Message?.Trim();
        var text = string.IsNullOrEmpty(message) ? exception.GetType().Name : $"{exception.GetType().Name}: {message}";

        return text.Length <= ReasonLimit ? text : text[..ReasonLimit] + "…";
    }
}
