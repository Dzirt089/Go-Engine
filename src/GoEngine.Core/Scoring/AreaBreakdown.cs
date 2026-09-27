namespace GoEngine.Core;

/// <summary>Разбор площади доски по владельцам: камни и территория каждого цвета, нейтральные точки.</summary>
/// <param name="BlackStones">Чёрные камни на доске.</param>
/// <param name="BlackTerritory">Пустые точки, окружённые только чёрными камнями.</param>
/// <param name="WhiteStones">Белые камни на доске.</param>
/// <param name="WhiteTerritory">Пустые точки, окружённые только белыми камнями.</param>
/// <param name="Neutral">Точки, не принадлежащие никому: области на границе обоих цветов, пустая доска.</param>
/// <remarks>
/// Нужен и подсчёту, и интерфейсу: панель партии показывает, сколько территории у каждой стороны
/// и сколько точек осталось нейтральными, а <see cref="Scorer.Calculate"/> берёт отсюда очки.
/// Разбор не заменяет <see cref="Scorer.Ownership"/>: поточечное владение нужно рисованию
/// и кодированию позиции для нейросети.
/// </remarks>
public readonly record struct AreaBreakdown(
    int BlackStones,
    int BlackTerritory,
    int WhiteStones,
    int WhiteTerritory,
    int Neutral)
{
    /// <summary>Очки чёрных без коми: камни плюс территория.</summary>
    public int Black => BlackStones + BlackTerritory;

    /// <summary>Очки белых без коми: камни плюс территория.</summary>
    public int White => WhiteStones + WhiteTerritory;

    /// <summary>Все точки доски: очки обоих цветов плюс нейтральные точки.</summary>
    public int Total => Black + White + Neutral;

    /// <summary>Возвращает разбор в виде «Чёрные 12 (5 + 7) : 9 (4 + 5) Белые, нейтрально 60».</summary>
    /// <returns>Разбор площади строкой.</returns>
    public override string ToString() =>
        $"Чёрные {Black} ({BlackStones} + {BlackTerritory}) : {White} ({WhiteStones} + {WhiteTerritory}) Белые, нейтрально {Neutral}";
}
