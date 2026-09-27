using GoEngine.AI.Onnx;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты разбора выходов сети — срез 3 фазы 11.</summary>
/// <remarks>
/// Математика над тензорами проверяется без модели и без рантайма ONNX: разбор отделён
/// от сессии (<c>ModelSession</c>) и от оценщика (<see cref="OnnxEvaluator"/>).
/// </remarks>
public sealed class EvaluationReaderTests
{
    /// <summary>Площадь доски 9×9: точек 81, ходов вместе с пасом 82.</summary>
    private const int Area9 = 9 * 9;

    [Fact]
    public void Вероятности_Политики_Суммируются_В_Единицу()
    {
        var evaluation = EvaluationReader.Read(new RawEvaluation(Logits(Area9 + 1), [1f, 0f, 0f], Area9));

        Assert.Equal(1.0, evaluation.Policy.Sum(static probability => (double)probability), 0.000001);
    }

    [Fact]
    public void Порядок_Вероятностей_Повторяет_Порядок_Логитов()
    {
        var logits = Logits(Area9 + 1);
        logits[7] = 10f;

        var evaluation = EvaluationReader.Read(new RawEvaluation(logits, [1f, 0f, 0f], Area9));

        Assert.Equal(7, evaluation.BestMove());
        Assert.Equal(evaluation.Policy[7], evaluation.Policy.Max());
    }

    [Fact]
    public void Индекс_Паса_Равен_Площади_Доски()
    {
        var evaluation = EvaluationReader.Read(new RawEvaluation(Logits(Area9 + 1), [1f, 0f, 0f], Area9));

        Assert.Equal(Area9, evaluation.PassIndex);
        Assert.Equal(Area9 + 1, evaluation.Policy.Count);
    }

    [Fact]
    public void Короткая_Политика_Ломает_Инвариант()
    {
        // Ходов вместе с пасом должно быть не меньше площади+1: иначе часть доски без вероятностей.
        Assert.Throws<DomainException>(() => EvaluationReader.Read(new RawEvaluation(new float[Area9 - 1], [1f, 0f, 0f], Area9)));
    }

    [Fact]
    public void Доска_Без_Точек_Ломает_Инвариант()
    {
        Assert.Throws<DomainException>(() => EvaluationReader.Read(new RawEvaluation([1f, 2f], [1f, 0f, 0f], 0)));
    }

    [Fact]
    public void Пустой_Логит_Исхода_Даёт_Нулевую_Вероятность_Победы()
    {
        // Так выглядит модель без головы исхода: политику читаем, а шансы неизвестны.
        var evaluation = EvaluationReader.Read(new RawEvaluation(Logits(Area9 + 1), [], Area9));

        Assert.Equal(0.0, evaluation.WinProbability);
    }

    [Fact]
    public void Перевес_В_Логитах_Исхода_Читается_Как_Победа()
    {
        var evaluation = EvaluationReader.Read(new RawEvaluation(Logits(Area9 + 1), [10f, 0f, 0f], Area9));

        Assert.True(evaluation.WinProbability > 0.99, $"победа: {evaluation.WinProbability}");
    }

    [Fact]
    public void Отставание_В_Логитах_Исхода_Читается_Как_Поражение()
    {
        var evaluation = EvaluationReader.Read(new RawEvaluation(Logits(Area9 + 1), [0f, 10f, 0f], Area9));

        Assert.True(evaluation.WinProbability < 0.001, $"победа: {evaluation.WinProbability}");
    }

    /// <summary>Ровные логиты: до правок все вероятности одинаковы.</summary>
    /// <param name="count">Сколько логитов нужно.</param>
    /// <returns>Массив логитов.</returns>
    private static float[] Logits(int count)
    {
        var logits = new float[count];
        Array.Fill(logits, 1f);

        return logits;
    }
}
