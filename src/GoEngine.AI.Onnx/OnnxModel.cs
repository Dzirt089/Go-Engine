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
