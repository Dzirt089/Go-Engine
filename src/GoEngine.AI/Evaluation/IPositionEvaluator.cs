namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Оценка позиции сетью: вероятности ходов и шансы ходящего.</summary>
/// <param name="Policy">Вероятности ходов: точки доски по порядку, затем пас.</param>
/// <param name="WinProbability">Вероятность победы ходящего от 0 до 1.</param>
/// <param name="BoardArea">Число точек доски: индекс паса равен этому числу.</param>
public readonly record struct PositionEvaluation(IReadOnlyList<double> Policy, double WinProbability, int BoardArea)
{
    /// <summary>Индекс паса в векторе вероятностей.</summary>
    public int PassIndex => BoardArea;

    /// <summary>Вероятность хода по индексу: 0, если индекса нет.</summary>
    /// <param name="index">Индекс хода.</param>
    /// <returns>Вероятность от 0 до 1.</returns>
    public double ProbabilityOf(int index) =>
        index >= 0 && index < Policy.Count ? Policy[index] : 0.0;
}

/// <summary>Источник оценки позиции для MCTS: нейросеть или её замена в тестах.</summary>
/// <remarks>
/// Порт объявлен в слое <c>AI</c>, а реализация живёт в <c>AI.Onnx</c>: движок не должен
/// зависеть от ONNX Runtime (<c>AGENTS.md</c>, раздел 1). Благодаря этому MCTS с оценкой
/// проверяется на подставном оценщике — быстро и без файла модели.
/// </remarks>
public interface IPositionEvaluator
{
    /// <summary>Оценивает позицию с точки зрения цвета, который ходит.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="moves">Ходы партии — для плоскостей истории.</param>
    /// <returns>Вероятности ходов и шансы ходящего.</returns>
    PositionEvaluation Evaluate(Board board, StoneColor toMove, Komi komi, IReadOnlyList<Move> moves);

    /// <summary>Загружает модель, подходящую для указанного размера доски.</summary>
    /// <param name="boardSize">Сторона доски: 9, 13 или 19.</param>
    /// <param name="modelsDirectory">Каталог, в котором лежат файлы моделей.</param>
    /// <returns><c>true</c>, если модель для этого размера загружена; иначе <c>false</c>.</returns>
    /// <remarks>
    /// Профили моделей — <c>DECISIONS.md</c>, D-039: у каждого размера доски своя сеть.
    /// Возврат <c>false</c> — ожидаемый исход (профиля или файла нет), а не ошибка: решение
    /// об уровнях принимает вызывающий код.
    /// </remarks>
    bool LoadModelForBoardSize(int boardSize, string modelsDirectory);
}
