using GoEngine.Core;

namespace GoEngine.AI.Onnx;

/// <summary>Разбирает сырые выходы сети: проверяет форму и превращает логиты в вероятности.</summary>
/// <remarks>
/// Отделено от сессии (<see cref="ModelSession"/>) и от оценщика (<see cref="OnnxEvaluator"/>):
/// здесь только математика над тензорами, поэтому её можно проверить тестами без модели и без
/// рантайма ONNX. Порядок значений задан моделью: сначала ходы первого канала политики, затем
/// вспомогательные головы; логит исхода — три значения (победа, поражение, ничья).
/// </remarks>
internal static class EvaluationReader
{
    /// <summary>Собирает оценку позиции из сырых выходов.</summary>
    /// <param name="raw">Логиты политики и исхода, площадь доски.</param>
    /// <returns>Оценка с вероятностями ходов.</returns>
    /// <exception cref="DomainException">
    /// Модель вернула политику короче доски: ходов+пас должно быть не меньше, чем площадь+1.
    /// </exception>
    public static OnnxEvaluation Read(RawEvaluation raw)
    {
        if (raw.BoardArea <= 0)
        {
            throw new DomainException($"Сеть оценена на доске без точек: площадь {raw.BoardArea}.");
        }

        var count = raw.BoardArea + 1;

        if (raw.PolicyLogits.Length < count)
        {
            throw new DomainException(
                $"Сеть вернула политику короче доски: {raw.PolicyLogits.Length} значений на {count} ходов.");
        }

        var policy = Softmax(raw.PolicyLogits, count);

        return new OnnxEvaluation(policy.AsReadOnly(), raw.ValueLogits.AsReadOnly(), raw.BoardArea);
    }

    /// <summary>Превращает логиты в вероятности.</summary>
    /// <param name="logits">Логиты ходов всех каналов политики.</param>
    /// <param name="count">Сколько значений брать из первого канала.</param>
    /// <returns>Вероятности ходов: сумма равна единице с точностью float.</returns>
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
}
