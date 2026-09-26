using GoEngine.Core;
using Microsoft.ML.OnnxRuntime;

namespace GoEngine.AI.Onnx;

/// <summary>Описание загруженной нейросети: входы и выходы модели.</summary>
/// <param name="Path">Путь к файлу модели.</param>
/// <param name="Inputs">Имена и типы входов.</param>
/// <param name="Outputs">Имена и типы выходов.</param>
public readonly record struct OnnxModelInfo(string Path, IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs);

/// <summary>Загрузка нейросети ONNX для оценки позиции (v2).</summary>
/// <remarks>
/// Слой живёт отдельно от <c>AI</c>: MCTS в v1 работает без сети (<c>DECISIONS.md</c>, D-003),
/// а здесь только подключение рантайма. Ошибки загрузки — ожидаемый исход: файла нет, файл
/// повреждён, рантайм не установлен — всё возвращается <see cref="Result{T}"/> с причиной.
/// Вход сети готовит <see cref="KataGoFeatures"/>.
/// </remarks>
public sealed class OnnxEvaluator : IDisposable
{
    private readonly InferenceSession _session;

    private OnnxEvaluator(string path, InferenceSession session, OnnxModelInfo info)
    {
        Path = path;
        _session = session;
        Info = info;
    }

    /// <summary>Путь к загруженной модели.</summary>
    public string Path { get; }

    /// <summary>Описание модели: входы и выходы.</summary>
    public OnnxModelInfo Info { get; }

    /// <summary>Загружает модель из файла.</summary>
    /// <param name="modelPath">Путь к файлу модели.</param>
    /// <returns>Оценщик или причина отказа.</returns>
    public static Result<OnnxEvaluator> Load(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        if (!File.Exists(modelPath))
        {
            return Result<OnnxEvaluator>.Fail($"Файл модели не найден: {modelPath}");
        }

        try
        {
            var session = new InferenceSession(modelPath);
            var info = Describe(modelPath, session);

            return Result<OnnxEvaluator>.Ok(new OnnxEvaluator(modelPath, session, info));
        }
        catch (OnnxRuntimeException exception)
        {
            // Рантайм установлен и работает: он сам сообщает, чем модель ему не подошла.
            return Result<OnnxEvaluator>.Fail($"Модель не загружена: {exception.Message}");
        }
        catch (DllNotFoundException exception)
        {
            return Result<OnnxEvaluator>.Fail($"Не найдена библиотека ONNX Runtime: {exception.Message}");
        }
    }

    /// <summary>Проверяет, что рантайм ONNX доступен в системе.</summary>
    /// <returns>Версия рантайма или причина отказа.</returns>
    public static Result<string> RuntimeVersion()
    {
        try
        {
            return Result<string>.Ok(typeof(InferenceSession).Assembly.GetName().Version?.ToString() ?? "неизвестна");
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException)
        {
            return Result<string>.Fail($"Библиотека ONNX Runtime недоступна: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();

    /// <summary>Собирает описание модели.</summary>
    /// <param name="path">Путь к файлу модели.</param>
    /// <param name="session">Сессия вывода.</param>
    /// <returns>Описание входов и выходов.</returns>
    private static OnnxModelInfo Describe(string path, InferenceSession session) => new(
        path,
        session.InputMetadata.Keys.ToList().AsReadOnly(),
        session.OutputMetadata.Keys.ToList().AsReadOnly());
}
