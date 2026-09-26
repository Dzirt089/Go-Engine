namespace GoEngine.AI;

/// <summary>Настройки playout-политики.</summary>
/// <param name="AtariProbability">Вероятность того, что политика воспользуется эвристиками атари — от 0 до 1.</param>
/// <param name="NeighborProbability">Вероятность хода рядом с камнями, если эвристики атари не сработали, — от 0 до 1.</param>
/// <remarks>
/// Значения проверяет <see cref="PlayoutPolicy"/>: позиционная запись не может валидировать
/// аргументы, поэтому недопустимая вероятность отклоняется при создании политики.
/// </remarks>
public readonly record struct PlayoutConfig(double AtariProbability, double NeighborProbability)
{
    /// <summary>Вероятность эвристик атари по умолчанию.</summary>
    public const double DefaultAtariProbability = 0.9;

    /// <summary>Вероятность хода рядом с камнями по умолчанию.</summary>
    public const double DefaultNeighborProbability = 0.5;

    /// <summary>Настройки по умолчанию: атари 0.9, соседний ход 0.5.</summary>
    public static PlayoutConfig Default => new(DefaultAtariProbability, DefaultNeighborProbability);
}
