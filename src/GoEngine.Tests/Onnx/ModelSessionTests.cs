using GoEngine.AI.Onnx;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты сессии вывода ONNX — срез 3 фазы 11.</summary>
/// <remarks>
/// Сессия владеет рантаймом: создание, описание модели, вызов вывода и освобождение. Отказы
/// (файла нет, файл повреждён) — ожидаемый исход, а не исключение. Тесты с настоящей моделью
/// без неё пропускаются (<see cref="ModelFactAttribute"/>).
/// </remarks>
public sealed class ModelSessionTests
{
    /// <summary>Индекс паса на доске 19×19.</summary>
    private const int PassIndex19 = 19 * 19;

    [Fact]
    public void Пустой_Путь_Бросает()
    {
        Assert.Throws<ArgumentException>(() => ModelSession.Create("  "));
    }

    [Fact]
    public void Отсутствующий_Файл_Отказ_Без_Исключения()
    {
        var path = Path.Combine(Path.GetTempPath(), $"go-engine-session-{Guid.NewGuid():N}.onnx");

        var created = ModelSession.Create(path);

        Assert.False(created.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(created.Error));
    }

    [Fact]
    public void Повреждённый_Файл_Отказ_С_Причиной()
    {
        var path = Path.Combine(Path.GetTempPath(), $"go-engine-session-broken-{Guid.NewGuid():N}.onnx");

        try
        {
            File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);

            var created = ModelSession.Create(path);

            Assert.False(created.IsSuccess);
            Assert.Contains("Модель не загружена", created.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [ModelFact]
    public void Сессия_Описывает_Входы_И_Выходы_Модели()
    {
        using var session = Load(ModelFile.FullPath);

        Assert.Contains(session.Info.Inputs, static input => input.Name == "bin_input");
        Assert.Contains(session.Info.Inputs, static input => input.Name == "global_input");
        Assert.Contains(session.Info.Outputs, static output => output.Name == "policy");
        Assert.Contains(session.Info.Outputs, static output => output.Name == "value");
        Assert.Equal(ModelFile.FullPath, session.Path);
    }

    [ModelFact]
    public void Сессия_Отдаёт_Сырые_Логиты_По_Контракту()
    {
        using var session = Load(ModelFile.FullPath);

        var raw = session.Run(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.Black, []);

        Assert.True(raw.IsSuccess, raw.Error);
        Assert.Equal(PassIndex19, raw.Value.BoardArea);
        Assert.True(raw.Value.PolicyLogits.Length >= PassIndex19 + 1, $"логитов политики: {raw.Value.PolicyLogits.Length}");
        Assert.Equal(3, raw.Value.ValueLogits.Length);
    }

    [ModelFact]
    public void Разбор_Сырого_Выхода_Совпадает_С_Оценщиком()
    {
        // Шов между сессией и разбором: оценщик обязан вернуть ровно то, что даёт разбор
        // сырых логитов той же сессии, бит в бит.
        var board = new Board(BoardSize.Size19);

        using var session = Load(ModelFile.FullPath);
        using var evaluator = LoadEvaluator();

        var raw = session.Run(board, Komi.For19x19, StoneColor.Black, []);
        var fromSession = EvaluationReader.Read(raw.Value);
        var fromEvaluator = evaluator.Evaluate(board, Komi.For19x19, StoneColor.Black, []);

        Assert.True(raw.IsSuccess, raw.Error);
        Assert.True(fromEvaluator.IsSuccess, fromEvaluator.Error);
        Assert.Equal(fromSession.Policy.Count, fromEvaluator.Value.Policy.Count);

        for (var index = 0; index < fromSession.Policy.Count; index++)
        {
            Assert.Equal(
                BitConverter.SingleToInt32Bits(fromSession.Policy[index]),
                BitConverter.SingleToInt32Bits(fromEvaluator.Value.Policy[index]));
        }
    }

    [ModelFact(ModelFile.Finetuned9x9Name)]
    public void Сессия_Девять_На_Девять_Даёт_Компактную_Политику()
    {
        // У модели 9×9 политика короче: точки доски и пас без запаса на 19×19.
        using var session = Load(ModelFile.Finetuned9x9Path);

        var raw = session.Run(new Board(BoardSize.Size9), Komi.For9x9, StoneColor.Black, []);

        Assert.True(raw.IsSuccess, raw.Error);
        Assert.Equal(81, raw.Value.BoardArea);
        Assert.True(raw.Value.PolicyLogits.Length >= 82, $"логитов политики: {raw.Value.PolicyLogits.Length}");
        Assert.Equal(3, raw.Value.ValueLogits.Length);
    }

    /// <summary>Создаёт сессию или сообщает, почему не вышло.</summary>
    /// <param name="path">Путь к файлу модели.</param>
    /// <returns>Сессия вывода.</returns>
    private static ModelSession Load(string path)
    {
        var created = ModelSession.Create(path);

        Assert.True(created.IsSuccess, $"сессия не создана: {created.Error ?? "без причины"}");

        return created.Value!;
    }

    /// <summary>Загружает оценщик или сообщает, почему не вышло.</summary>
    /// <returns>Оценщик модели.</returns>
    private static OnnxEvaluator LoadEvaluator()
    {
        var loaded = OnnxEvaluator.Load(ModelFile.FullPath);

        Assert.True(loaded.IsSuccess, $"модель не загрузилась: {loaded.Error ?? "без причины"}");

        return loaded.Value!;
    }
}
