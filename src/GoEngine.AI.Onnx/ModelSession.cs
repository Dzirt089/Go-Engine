using GoEngine.AI;
using GoEngine.Core;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace GoEngine.AI.Onnx;

/// <summary>Сырые выходы сети: логиты ходов, логиты исхода и площадь доски.</summary>
/// <param name="PolicyLogits">Логиты политики: сначала ходы первого канала, затем вспомогательные головы.</param>
/// <param name="ValueLogits">Логиты исхода: победа, поражение, ничья с точки зрения ходящего.</param>
/// <param name="BoardArea">Число точек доски: индекс паса равен этому числу.</param>
internal readonly record struct RawEvaluation(float[] PolicyLogits, float[] ValueLogits, int BoardArea);

/// <summary>Сессия вывода ONNX: владеет моделью, считает позицию и отдаёт сырые выходы.</summary>
/// <remarks>
/// Отделена от <see cref="OnnxEvaluator"/> намеренно: здесь жизненный цикл рантайма (создание,
/// подмена, освобождение), имена входов и выходов и сам вызов вывода, а превращение выходов
/// в вероятности — в <see cref="EvaluationReader"/>. Отказ рантайма — ожидаемый исход
/// (<see cref="Result{T}"/>), а не исключение: файла нет, файл повреждён, модель не подошла.
/// Исключением остаётся нарушение инварианта — например, в модели нет объявленного выхода.
/// </remarks>
internal sealed class ModelSession : IDisposable
{
    /// <summary>Имя входа с плоскостями позиции.</summary>
    private const string SpatialInput = "bin_input";

    /// <summary>Имя входа с глобальными признаками.</summary>
    private const string GlobalInput = "global_input";

    /// <summary>Имя выхода с логитами ходов.</summary>
    private const string PolicyOutput = "policy";

    /// <summary>Имя выхода с исходом партии.</summary>
    private const string ValueOutput = "value";

    private readonly InferenceSession _session;

    private ModelSession(string path, InferenceSession session)
    {
        Path = path;
        _session = session;
        Info = Describe(path, session);
    }

    /// <summary>Путь к загруженной модели.</summary>
    public string Path { get; }

    /// <summary>Описание модели: входы и выходы.</summary>
    public OnnxModelInfo Info { get; }

    /// <summary>Загружает модель из файла.</summary>
    /// <param name="path">Путь к файлу модели.</param>
    /// <returns>Сессия вывода или причина отказа.</returns>
    /// <remarks>
    /// Отсутствие библиотеки рантайма ловит вызывающий: для загрузки модели это отдельный
    /// отказ (<see cref="OnnxEvaluator.Load"/>), а не «модель не подошла».
    /// </remarks>
    public static Result<ModelSession> Create(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return Result<ModelSession>.Ok(new ModelSession(path, new InferenceSession(path)));
        }
        catch (OnnxRuntimeException exception)
        {
            // Рантайм установлен и работает: он сам сообщает, чем модель ему не подошла.
            return Result<ModelSession>.Fail($"Модель не загружена: {exception.Message}");
        }
    }

    /// <summary>Считает позицию и отдаёт сырые выходы сети.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    /// <param name="moves">Сделанные ходы партии — для плоскостей истории.</param>
    /// <returns>Логиты политики и исхода или причина отказа.</returns>
    /// <remarks>
    /// Входы модели — <c>bin_input</c> (22 плоскости) и <c>global_input</c> (19 признаков),
    /// выходы — <c>policy</c> (логиты ходов) и <c>value</c> (победа, поражение, ничья).
    /// Признаки готовит <see cref="KataGoFeatures"/>.
    /// </remarks>
    public Result<RawEvaluation> Run(Board board, Komi komi, StoneColor toMove, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(toMove);
        ArgumentNullException.ThrowIfNull(moves);

        var side = board.Size.Value;
        var area = side * side;
        var spatial = KataGoFeatures.EncodeSpatial(board, toMove, moves);
        var global = KataGoFeatures.EncodeGlobal(board, komi, toMove, moves);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(SpatialInput, new DenseTensor<float>(spatial, [1, KataGoFeatures.SpatialPlanes, side, side])),
            NamedOnnxValue.CreateFromTensor(GlobalInput, new DenseTensor<float>(global, [1, KataGoFeatures.GlobalFeatures]))
        };

        try
        {
            using var results = _session.Run(inputs);

            var logits = Output(results, PolicyOutput).AsTensor<float>().ToArray();
            var value = Output(results, ValueOutput).AsTensor<float>().ToArray();

            return Result<RawEvaluation>.Ok(new RawEvaluation(logits, value, area));
        }
        catch (OnnxRuntimeException exception)
        {
            return Result<RawEvaluation>.Fail($"Сеть не посчитала позицию: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();

    /// <summary>Берёт выход модели по имени.</summary>
    /// <param name="results">Результаты вывода.</param>
    /// <param name="name">Имя выхода.</param>
    /// <returns>Тензор выхода.</returns>
    /// <exception cref="DomainException">Выхода с таким именем в модели нет.</exception>
    private static DisposableNamedOnnxValue Output(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, string name) =>
        results.FirstOrDefault(result => result.Name == name)
            ?? throw new DomainException($"В модели нет выхода {name}.");

    /// <summary>Собирает описание модели.</summary>
    /// <param name="path">Путь к файлу модели.</param>
    /// <param name="session">Сессия вывода.</param>
    /// <returns>Описание входов и выходов.</returns>
    private static OnnxModelInfo Describe(string path, InferenceSession session) => new(
        path,
        Tensors(session.InputMetadata).AsReadOnly(),
        Tensors(session.OutputMetadata).AsReadOnly());

    /// <summary>Перечисляет тензоры модели с их размерностями.</summary>
    /// <param name="metadata">Метаданные входов или выходов сессии.</param>
    /// <returns>Список тензоров в порядке модели.</returns>
    private static List<OnnxTensorInfo> Tensors(IReadOnlyDictionary<string, NodeMetadata> metadata)
    {
        var tensors = new List<OnnxTensorInfo>(metadata.Count);

        foreach (var (name, node) in metadata)
        {
            tensors.Add(new OnnxTensorInfo(name, node.Dimensions.ToArray()));
        }

        return tensors;
    }
}
