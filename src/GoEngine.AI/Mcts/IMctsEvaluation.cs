namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Чем MCTS оценивает позицию узла: доигрываниями или сетью.</summary>
/// <remarks>
/// Шов разделяет две обязанности поиска: дерево и бюджет ведёт <see cref="MctsSearch"/>, а как
/// получить оценку листовой позиции решает эта реализация. Разные оценки обновляют дерево
/// по-разному: доигрывание приносит победителя партии и ходы для статистики RAVE, сеть — вероятность
/// исхода и приоритеты ходов из политики, поэтому обновление дерева входит в саму оценку (D-051).
/// </remarks>
public interface IMctsEvaluation
{
    /// <summary>Даёт ли оценка приоритеты ходов из политики сети.</summary>
    /// <remarks>
    /// Приоритеты меняют способ спуска: с ними дерево считает PUCT, без них — UCB1 с поправкой RAVE.
    /// </remarks>
    bool GivesPriors { get; }

    /// <summary>Оценивает позицию узла и передаёт оценку по дереву.</summary>
    /// <param name="tree">Дерево поиска, которому принадлежит узел.</param>
    /// <param name="node">Узел, чья позиция оценивается.</param>
    /// <param name="history">Ходы партии до корня дерева: нужны сети для плоскостей истории.</param>
    /// <param name="komi">Коми, по которому считаются доигрывания.</param>
    void Evaluate(MctsTree tree, MctsNode node, IReadOnlyList<Move> history, Komi komi);
}
