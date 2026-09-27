namespace GoEngine.AI.Onnx;

/// <summary>Мягкий максимум над логитами сети: одно место на политику и на исход.</summary>
/// <remarks>
/// Выход сети — логиты, а не вероятности: их сумма не равна единице, а разброс значений требует
/// вычитания максимума, иначе <see cref="Math.Exp(double)"/> переполняется. Раньше преобразование
/// было записано дважды — отдельно для политики и отдельно для исхода; формулы совпадали не
/// случайно, и расходиться им нельзя.
/// </remarks>
internal static class Softmax
{
    /// <summary>Превращает первые <paramref name="count"/> логитов в вероятности.</summary>
    /// <param name="logits">Логиты ходов всех каналов политики.</param>
    /// <param name="count">Сколько значений брать из первого канала.</param>
    /// <returns>Вероятности: сумма равна единице с точностью float.</returns>
    public static float[] Over(float[] logits, int count)
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

    /// <summary>Вероятность первого логита: им сеть помечает победу ходящего.</summary>
    /// <param name="logits">Логиты исхода: победа, поражение, ничья.</param>
    /// <returns>Вероятность победы или <c>0</c>, если логитов нет.</returns>
    public static double ProbabilityOfFirst(IReadOnlyList<float> logits)
    {
        ArgumentNullException.ThrowIfNull(logits);

        if (logits.Count == 0)
        {
            return 0.0;
        }

        var max = logits.Max();
        var sum = logits.Sum(logit => Math.Exp(logit - max));

        return sum > 0 ? Math.Exp(logits[0] - max) / sum : 0.0;
    }
}
