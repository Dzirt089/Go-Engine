using GoEngine.Core;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace GoEngine.AI.Onnx;

/// <summary>Один тензор модели: имя и размерности.</summary>
/// <param name="Name">Имя входа или выхода.</param>
/// <param name="Dimensions">Размерности; <c>-1</c> — динамическая ось.</param>
public readonly record struct OnnxTensorInfo(string Name, IReadOnlyList<int> Dimensions)
{
    /// <summary>Размерности в виде текста, например <c>1×22×19×19</c>.</summary>
    /// <returns>Имя и размерности для отчёта.</returns>
    public override string ToString() => $"{Name} [{string.Join('×', Dimensions)}]";
}

/// <summary>Описание загруженной нейросети: входы и выходы модели.</summary>
/// <param name="Path">Путь к файлу модели.</param>
/// <param name="Inputs">Входы модели с размерностями.</param>
/// <param name="Outputs">Выходы модели с размерностями.</param>
public readonly record struct OnnxModelInfo(string Path, IReadOnlyList<OnnxTensorInfo> Inputs, IReadOnlyList<OnnxTensorInfo> Outputs);

/// <summary>Оценка позиции нейросетью: вероятности ходов и исход партии.</summary>
/// <param name="Policy">Вероятности ходов: точки доски по порядку, затем пас.</param>
/// <param name="Value">Три логита исхода: победа, поражение, ничья — с точки зрения ходящего.</param>
/// <param name="BoardArea">Число точек доски: индекс паса равен этому числу.</param>
/// <remarks>
/// Выход <c>value</c> модели — не вероятности, а логиты: их сумма не равна единице.
/// Вероятность победы считается мягким максимумом по трём логитам.
/// </remarks>
public readonly record struct OnnxEvaluation(IReadOnlyList<float> Policy, IReadOnlyList<float> Value, int BoardArea)
{
    /// <summary>Индекс паса в векторе вероятностей.</summary>
    public int PassIndex => BoardArea;

    /// <summary>Вероятность победы ходящего.</summary>
    public double WinProbability
    {
        get
        {
            if (Value.Count == 0)
            {
                return 0.0;
            }

            var max = Value.Max();
            var sum = Value.Sum(logit => Math.Exp(logit - max));

            return sum > 0 ? Math.Exp(Value[0] - max) / sum : 0.0;
        }
    }

    /// <summary>Ход с наибольшей вероятностью.</summary>
    /// <returns>Индекс хода: точка доски или пас.</returns>
    public int BestMove()
    {
        var best = -1;

        for (var index = 0; index < Policy.Count; index++)
        {
            if (best < 0 || Policy[index] > Policy[best])
            {
                best = index;
            }
        }

        return best;
    }
}

/// <summary>Загрузка нейросети ONNX для оценки позиции (v2).</summary>
/// <remarks>
/// Слой живёт отдельно от <c>AI</c>: MCTS в v1 работает без сети (<c>DECISIONS.md</c>, D-003),
/// а здесь только подключение рантайма. Ошибки загрузки — ожидаемый исход: файла нет, файл
/// повреждён, рантайм не установлен — всё возвращается <see cref="Result{T}"/> с причиной.
/// Вход сети готовит <see cref="KataGoFeatures"/>.
/// </remarks>
public sealed class OnnxEvaluator : IDisposable
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

    /// <summary>Оценивает позицию: вероятности ходов и исход партии.</summary>
    /// <param name="board">Позиция на доске 19×19.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    /// <param name="moves">Сделанные ходы партии — для плоскостей истории.</param>
    /// <returns>Оценка или причина отказа.</returns>
    /// <remarks>
    /// Входы модели — `bin_input` (22 плоскости 19×19) и `global_input` (19 признаков),
    /// выходы — `policy` (логиты ходов) и `value` (победа, поражение, ничья).
    /// </remarks>
    public Result<OnnxEvaluation> Evaluate(Board board, Komi komi, StoneColor toMove, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(toMove);
        ArgumentNullException.ThrowIfNull(moves);

        if (board.Size != BoardSize.Size19)
        {
            return Result<OnnxEvaluation>.Fail($"Модель обучена на доске 19×19, получена {board.Size}.");
        }

        try
        {
            return Result<OnnxEvaluation>.Ok(Run(board, komi, toMove, moves));
        }
        catch (OnnxRuntimeException exception)
        {
            return Result<OnnxEvaluation>.Fail($"Сеть не посчитала позицию: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();

    /// <summary>Считает позицию и разбирает выходы сети.</summary>
    /// <param name="board">Позиция на доске 19×19.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    /// <param name="moves">Сделанные ходы партии.</param>
    /// <returns>Оценка позиции.</returns>
    /// <exception cref="OnnxRuntimeException">Сеть не смогла посчитать позицию.</exception>
    private OnnxEvaluation Run(Board board, Komi komi, StoneColor toMove, IReadOnlyList<Move> moves)
    {
        var side = board.Size.Value;
        var area = side * side;
        var spatial = KataGoFeatures.EncodeSpatial(board, toMove, moves);
        var global = KataGoFeatures.EncodeGlobal(board, komi, toMove, moves);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(SpatialInput, new DenseTensor<float>(spatial, [1, KataGoFeatures.SpatialPlanes, side, side])),
            NamedOnnxValue.CreateFromTensor(GlobalInput, new DenseTensor<float>(global, [1, KataGoFeatures.GlobalFeatures]))
        };

        using var results = _session.Run(inputs);

        var logits = Output(results, PolicyOutput).AsTensor<float>().ToArray();
        var value = Output(results, ValueOutput).AsTensor<float>().ToArray();

        // Первый канал политики — вероятности ходов; остальные пять — вспомогательные головы.
        // Логиты ходов идут блоками по 362: точка доски, затем пас.
        var policy = Softmax(logits, area + 1);

        return new OnnxEvaluation(policy.AsReadOnly(), value.AsReadOnly(), area);
    }

    /// <summary>Берёт выход модели по имени.</summary>
    /// <param name="results">Результаты вывода.</param>
    /// <param name="name">Имя выхода.</param>
    /// <returns>Тензор выхода.</returns>
    /// <exception cref="DomainException">Выхода с таким именем в модели нет.</exception>
    private static DisposableNamedOnnxValue Output(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, string name) =>
        results.FirstOrDefault(result => result.Name == name)
            ?? throw new DomainException($"В модели нет выхода {name}.");

    /// <summary>Превращает логиты в вероятности.</summary>
    /// <param name="logits">Логиты ходов всех каналов политики.</param>
    /// <param name="count">Сколько значений брать из первого канала.</param>
    /// <returns>Вероятности ходов.</returns>
    private static float[] Softmax(float[] logits, int count)
    {
        var probabilities = new float[count];
        var max = float.NegativeInfinity;

        for (var index = 0; index < count && index < logits.Length; index++)
        {
            max = Math.Max(max, logits[index]);
        }

        var sum = 0.0;

        for (var index = 0; index < count && index < logits.Length; index++)
        {
            var value = Math.Exp(logits[index] - max);
            probabilities[index] = (float)value;
            sum += value;
        }

        if (sum > 0)
        {
            for (var index = 0; index < count; index++)
            {
                probabilities[index] = (float)(probabilities[index] / sum);
            }
        }

        return probabilities;
    }

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