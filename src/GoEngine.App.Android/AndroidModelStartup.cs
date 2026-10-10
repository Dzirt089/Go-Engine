using Android.App;
using Android.OS;
using GoEngine.AI.Onnx;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.App.Services.Updates;
using Microsoft.Extensions.Logging;

namespace GoEngine.App.Android;

/// <summary>Готовит модели нейросети на Android в фоне, после появления экрана загрузки.</summary>
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
    /// <summary>Логгер моделей: на телефоне лог — единственный способ понять, что помешало сети.</summary>
    private static readonly ILogger Log = AppLog.For("Models");

    /// <summary>Копирует модели из пакета и настраивает уровни с нейросетью.</summary>
    /// <param name="activity">Активность: её ресурсы и файловый каталог.</param>
    /// <exception cref="ArgumentNullException">Активность не задана.</exception>
    /// <remarks>
    /// Вызывается в фоне (<c>App.Prepare</c>): копирование моделей и загрузка ONNX занимают
    /// секунды, и делать это до первого кадра нельзя — игрок видел пустой экран и решал,
    /// что приложение зависло (жалоба пользователя 2026-10-10). Экран загрузки показывает
    /// оболочка, она же убирает его, когда подготовка закончена.
    /// </remarks>
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
            var reason = Describe(activity, exception);
            global::GoEngine.App.App.ModelsStatus = ModelStatusText.Failed(reason);
            AppLogMessages.ModelsUnavailable(Log, reason);
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
            var missing = $"в пакете нет файлов моделей: {string.Join(", ", result.Missing)}";
            global::GoEngine.App.App.ModelsStatus = ModelStatusText.Failed(missing);
            AppLogMessages.ModelsUnavailable(Log, missing);

            return;
        }

        if (AttachEvaluator(activity, result.Directory, present) is { } failure)
        {
            global::GoEngine.App.App.ModelsStatus = ModelStatusText.Failed(failure);
            AppLogMessages.ModelsUnavailable(Log, failure);

            return;
        }

        global::GoEngine.App.App.ModelsStatus = ModelStatusText.Loaded(sizes);
        AppLogMessages.ModelsReady(Log, result.Directory, string.Join(", ", sizes.Order()));
    }

    /// <summary>Подключает оценщик позиции, начиная с самой маленькой модели.</summary>
    /// <param name="activity">Активность: её каталог нативных библиотек нужен для диагноза.</param>
    /// <param name="directory">Каталог с установленными моделями.</param>
    /// <param name="present">Имена установленных файлов.</param>
    /// <returns>Причина отказа или <c>null</c>, если оценщик подключён.</returns>
    /// <remarks>
    /// Оценщик нужен, чтобы уровни с сетью появились в списке: модель под размер доски он догрузит
    /// сам (<c>LoadModelForBoardSize</c>). Начинаем с самого маленького файла, чтобы запуск
    /// на телефоне не ждал лишние 72 МБ. Причина отказа нужна целиком: «модель не загрузилась»
    /// без подробностей не отличает битый файл от отсутствующей нативной библиотеки.
    /// </remarks>
    private static string? AttachEvaluator(Activity activity, string directory, IReadOnlyList<string> present)
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

        // Оценщик не поднялся ни на одной модели: к причинам от ONNX добавляем состояние нативной
        // библиотеки. Именно этот случай дал жалобу «модели не загружены» без единой подробности.
        return $"{string.Join("; ", failures)}; {DescribeNativeLibrary(activity)}";
    }

    /// <summary>Описывает причину отказа так, чтобы по одной строке с телефона был понятен диагноз.</summary>
    /// <param name="activity">Активность: её каталог нативных библиотек.</param>
    /// <param name="exception">Исключение, остановившее подготовку моделей.</param>
    /// <returns>Цепочка причин, список ABI устройства и состояние нативной библиотеки ONNX.</returns>
    /// <remarks>
    /// Раньше здесь было только внешнее исключение: на телефоне это выглядело как
    /// <c>TypeInitializationException_Type</c> без единой подробности, и понять, что в пакет попала
    /// не та нативная библиотека, по такой строке было нельзя.
    /// </remarks>
    private static string Describe(Activity activity, Exception exception) =>
        ModelStatusText.Limit($"{ModelStatusText.Chain(exception)}; {DescribeDevice(activity)}");

    /// <summary>Описывает устройство: его ABI и состояние библиотеки ONNX.</summary>
    /// <param name="activity">Активность: её каталог нативных библиотек.</param>
    /// <returns>Например, «устройство: arm64-v8a; libonnxruntime.so: 17584840 байт в /data/…/lib/arm64».</returns>
    private static string DescribeDevice(Activity activity) =>
        $"устройство: {DescribeDeviceAbis()}; {DescribeNativeLibrary(activity)}";

    /// <summary>Перечисляет поддерживаемые устройством ABI — по ним видно, есть ли библиотека под этот телефон.</summary>
    /// <returns>Список ABI через запятую или «неизвестны».</returns>
    private static string DescribeDeviceAbis()
    {
        var abis = Build.SupportedAbis;

        return abis is { Count: > 0 } ? string.Join(", ", abis) : "неизвестны";
    }

    /// <summary>Проверяет, что нативная библиотека ONNX есть на устройстве и загружается.</summary>
    /// <param name="activity">Активность: её каталог нативных библиотек.</param>
    /// <returns>Размер файла в каталоге библиотек и результат явной загрузки.</returns>
    /// <remarks>
    /// Явная загрузка повторяет то, что делает привязка ONNX, но её отказ виден словами: так одна
    /// строка с телефона отличает «в пакет попала библиотека не под эту систему» от «модель битая».
    /// Повторная загрузка уже загруженной библиотеки безопасна.
    /// </remarks>
    private static string DescribeNativeLibrary(Activity activity)
    {
        List<string> parts = [];
        var directory = activity.ApplicationInfo?.NativeLibraryDir;

        if (!string.IsNullOrWhiteSpace(directory))
        {
            var file = new FileInfo(Path.Combine(directory, "libonnxruntime.so"));

            parts.Add(file.Exists
                ? $"libonnxruntime.so: {file.Length} байт в {directory}"
                : $"libonnxruntime.so нет в {directory}");
        }

        try
        {
            Java.Lang.JavaSystem.LoadLibrary("onnxruntime");
            parts.Add("загрузка библиотеки прошла");
        }
        catch (Exception exception)
        {
            parts.Add($"загрузка библиотеки не прошла: {exception.GetType().Name}: {exception.Message}");
        }

        return string.Join("; ", parts);
    }
}
