namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Бюджет поиска с нейросетью: сколько итераций MCTS делать на доске каждого размера.</summary>
/// <remarks>
/// Бюджет задаётся в итерациях, а не во времени: итерация — это один вызов сети, поэтому от размера
/// доски и скорости машины зависит <em>время</em> хода, а не сила уровня. Число итераций падает
/// с ростом доски: один вызов сети на 19×19 примерно вчетверо дороже, чем на 9×9 (D-047), поэтому
/// одинаковое число итераций означало бы разное время ожидания на разных досках.
/// Итерации, а не секунды, выбраны ещё и потому, что делают партии детерминированными: замеры
/// силы не зависят от загрузки машины (D-017, D-047).
/// </remarks>
public readonly record struct NeuralBudget
{
    /// <summary>Создаёт бюджет по числу итераций для трёх размеров доски.</summary>
    /// <param name="for9x9">Итераций на ход на доске 9×9.</param>
    /// <param name="for13x13">Итераций на ход на доске 13×13.</param>
    /// <param name="for19x19">Итераций на ход на доске 19×19.</param>
    /// <exception cref="ArgumentOutOfRangeException">Хотя бы одно число итераций не положительное.</exception>
    public NeuralBudget(int for9x9, int for13x13, int for19x19)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(for9x9);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(for13x13);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(for19x19);

        For9x9 = for9x9;
        For13x13 = for13x13;
        For19x19 = for19x19;
    }

    /// <summary>Итераций на ход на доске 9×9.</summary>
    public int For9x9 { get; }

    /// <summary>Итераций на ход на доске 13×13.</summary>
    public int For13x13 { get; }

    /// <summary>Итераций на ход на доске 19×19.</summary>
    public int For19x19 { get; }

    /// <summary>Сколько итераций делать на доске этого размера.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns>Число итераций для этого размера.</returns>
    /// <remarks>Для доски, которой нет в списке приложения, берётся бюджет 9×9 — как и в правилах коми.</remarks>
    public int For(BoardSize size) => size.Value switch
    {
        var value when value == BoardSize.Size19.Value => For19x19,
        var value when value == BoardSize.Size13.Value => For13x13,
        _ => For9x9
    };

    /// <summary>Собирает бюджет, одинаковый для всех размеров доски.</summary>
    /// <param name="iterations">Число итераций на ход.</param>
    /// <returns>Бюджет с одним и тем же числом итераций на всех досках.</returns>
    public static NeuralBudget Same(int iterations) => new(iterations, iterations, iterations);
}
