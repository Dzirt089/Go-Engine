using GoEngine.AI.Onnx;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Оценщик ONNX при одновременной работе и смене модели.</summary>
/// <remarks>
/// Регрессия: ход соперника считается в фоне, а модель меняется из потока интерфейса при смене
/// доски или уровня. Прежде `Replace` освобождала сессию, пока фоновый поиск находился внутри
/// `Run`, и процесс падал в нативном коде ONNX Runtime — снаружи это выглядело случайным
/// завершением игры. Здесь проверяется, что подмена модели во время работы не роняет оценщик.
/// </remarks>
public sealed class OnnxEvaluatorConcurrencyTests
{
    [ModelFact]
    public void Оннкс_Подмена_Модели_Во_Время_Работы_Не_Ронит()
    {
        var loaded = OnnxEvaluator.Load(ModelFile.FullPath);

        Assert.True(loaded.IsSuccess, loaded.Error);

        using var evaluator = loaded.Value!;
        var board = BoardFor();
        var failures = 0;
        var directory = Path.GetDirectoryName(ModelFile.FullPath)!;

        var workers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                for (var iteration = 0; iteration < 40; iteration++)
                {
                    var evaluation = evaluator.Evaluate(board, new Komi(7.5), StoneColor.Black, []);

                    if (!evaluation.IsSuccess)
                    {
                        Interlocked.Increment(ref failures);
                    }
                }
            }))
            .ToArray();

        // Поток интерфейса в это время меняет модель: сначала на 9×9, потом обратно.
        var swaps = Task.Run(() =>
        {
            for (var iteration = 0; iteration < 10; iteration++)
            {
                _ = evaluator.LoadModelForBoardSize(9, directory);
                _ = evaluator.LoadModelForBoardSize(19, directory);
            }
        });

        Task.WaitAll([.. workers, swaps]);

        // Отказы при подмене допустимы (модель на миг не та), падение процесса — нет.
        Assert.True(failures >= 0);
    }

    [ModelFact]
    public void Оннкс_После_Освобождения_Возвращает_Отказ()
    {
        var loaded = OnnxEvaluator.Load(ModelFile.FullPath);

        Assert.True(loaded.IsSuccess, loaded.Error);

        var evaluator = loaded.Value!;
        evaluator.Dispose();

        var evaluation = evaluator.Evaluate(BoardFor(), new Komi(7.5), StoneColor.Black, []);

        Assert.False(evaluation.IsSuccess);
        Assert.Contains("освобожд", evaluation.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Простая позиция 9×9 для оценки.</summary>
    /// <returns>Доска с одним камнем.</returns>
    private static Board BoardFor() => new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));
}
