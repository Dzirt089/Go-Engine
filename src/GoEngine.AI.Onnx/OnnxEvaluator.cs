using GoEngine.AI;
using GoEngine.Core;
using Microsoft.ML.OnnxRuntime;

namespace GoEngine.AI.Onnx;

/// <summary>Загрузка нейросети ONNX для оценки позиции (v2).</summary>
/// <remarks>
/// Слой живёт отдельно от <c>AI</c>: MCTS в v1 работает без сети (<c>DECISIONS.md</c>, D-003),
/// а здесь только подключение рантайма. Ошибки загрузки — ожидаемый исход: файла нет, файл
/// повреждён, рантайм не установлен — всё возвращается <see cref="Result{T}"/> с причиной.
/// Вход сети готовит <see cref="KataGoFeatures"/>, сессию держит <see cref="ModelSession"/>,
/// а выходы разбирает <see cref="EvaluationReader"/>.
/// </remarks>
public sealed class OnnxEvaluator : IPositionEvaluator, IDisposable
{
    /// <summary>Защищает сессию: поиск идёт в фоне, а модель меняется из потока интерфейса.</summary>
    /// <remarks>
    /// Без этой блокировки смена модели (`Replace`) освобождала сессию, пока фоновый поиск
    /// находился внутри `Run`: это падение в нативном коде ONNX Runtime, которое выглядит как
    /// случайное завершение процесса. Заодно сериализуются одновременные `Run` — поиск один,
    /// лишняя параллельность ничего не ускоряет.
    /// </remarks>
    private readonly object _sync = new();

    private ModelSession _session;

    private bool _disposed;

    private OnnxEvaluator(ModelSession session) => _session = session;

    /// <summary>Путь к загруженной модели.</summary>
    public string Path
    {
        get
        {
            lock (_sync)
            {
                return _session.Path;
            }
        }
    }

    /// <summary>Описание модели: входы и выходы.</summary>
    public OnnxModelInfo Info
    {
        get
        {
            lock (_sync)
            {
                return _session.Info;
            }
        }
    }

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
            var session = ModelSession.Create(modelPath);

            return session.IsSuccess
                ? Result<OnnxEvaluator>.Ok(new OnnxEvaluator(session.Value!))
                : Result<OnnxEvaluator>.Fail(session.Error!);
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
    /// <param name="board">Позиция.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    /// <param name="moves">Сделанные ходы партии — для плоскостей истории.</param>
    /// <returns>Оценка или причина отказа.</returns>
    /// <remarks>
    /// Входы модели — <c>bin_input</c> (22 плоскости) и <c>global_input</c> (19 признаков),
    /// выходы — <c>policy</c> (логиты ходов) и <c>value</c> (победа, поражение, ничья).
    /// </remarks>
    public Result<OnnxEvaluation> Evaluate(Board board, Komi komi, StoneColor toMove, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(toMove);
        ArgumentNullException.ThrowIfNull(moves);

        // Блокировка держится и на время вывода, и на время разбора: пока она не взята,
        // никто не может освободить сессию из-под работающего вывода.
        lock (_sync)
        {
            if (_disposed)
            {
                return Result<OnnxEvaluation>.Fail("Оценщик уже освобождён.");
            }

            var raw = _session.Run(board, komi, toMove, moves);

            return raw.IsSuccess
                ? Result<OnnxEvaluation>.Ok(EvaluationReader.Read(raw.Value))
                : Result<OnnxEvaluation>.Fail(raw.Error!);
        }
    }

    /// <summary>Оценивает позицию для MCTS: вероятности ходов и шансы ходящего.</summary>
    /// <param name="board">Позиция на доске.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="moves">Ходы партии — для плоскостей истории.</param>
    /// <returns>Оценка в терминах MCTS: вероятности и вероятность победы.</returns>
    /// <exception cref="AiException">Сеть не смогла оценить позицию.</exception>
    /// <remarks>
    /// Порт <see cref="IPositionEvaluator"/> не возвращает <c>Result</c>: поиск идёт тысячами
    /// итераций, и разбирать отказ на каждой — не выход. Отказ здесь означает, что модель
    /// или позиция не подходят движку, то есть ошибку настройки, а не ожидаемый исход.
    /// </remarks>
    PositionEvaluation IPositionEvaluator.Evaluate(Board board, StoneColor toMove, Komi komi, IReadOnlyList<Move> moves)
    {
        var evaluation = Evaluate(board, komi, toMove, moves);

        if (!evaluation.IsSuccess)
        {
            throw new AiException($"Сеть не оценила позицию: {evaluation.Error ?? "без причины"}");
        }

        var value = evaluation.Value;
        var policy = new double[value.Policy.Count];

        for (var index = 0; index < policy.Length; index++)
        {
            policy[index] = value.Policy[index];
        }

        return new PositionEvaluation(policy, Math.Clamp(value.WinProbability, 0.0, 1.0), value.BoardArea);
    }

    /// <summary>Загружает модель, подходящую для размера доски, заменяя текущую.</summary>
    /// <param name="boardSize">Сторона доски: 9, 13 или 19.</param>
    /// <param name="modelsDirectory">Каталог с файлами моделей.</param>
    /// <returns><c>true</c>, если модель для этого размера найдена и загружена.</returns>
    /// <remarks>
    /// Профили моделей — <c>DECISIONS.md</c>, D-039. Если профиля или файла нет, прежняя модель
    /// остаётся на месте: оценщик продолжает работать с тем, что было, а уровни Дан для этого
    /// размера интерфейс не предлагает.
    /// </remarks>
    public bool LoadModelForBoardSize(int boardSize, string modelsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelsDirectory);

        if (ModelProfiles.FindForBoardSize(boardSize) is not { } profile)
        {
            return false;
        }

        var path = System.IO.Path.Combine(modelsDirectory, profile.FileName);

        return File.Exists(path) && Replace(path);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Освобождение ждёт окончания идущего вывода: закрыть сессию под работающим `Run`
    /// означает падение в нативном коде.
    /// </remarks>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _session.Dispose();
        }
    }

    /// <summary>Заменяет сессию вывода моделью из файла.</summary>
    /// <param name="path">Путь к файлу модели.</param>
    /// <returns><c>true</c>, если модель загружена; иначе <c>false</c>.</returns>
    /// <remarks>Новая сессия создаётся до освобождения прежней: при отказе прежняя остаётся рабочей.</remarks>
    private bool Replace(string path)
    {
        var created = ModelSession.Create(path);

        if (!created.IsSuccess)
        {
            // Файл есть, но модель не подошла: прежняя сессия остаётся рабочей.
            return false;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                // Оценщик уже закрыт: новую сессию не оставляем висеть.
                created.Value!.Dispose();

                return false;
            }

            var previous = _session;
            _session = created.Value!;
            previous.Dispose();
        }

        return true;
    }
}
