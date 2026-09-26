namespace GoEngine.Core;

/// <summary>Точка на доске.</summary>
/// <param name="X">Столбец слева направо.</param>
/// <param name="Y">Строка сверху вниз.</param>
/// <remarks>
/// <c>default(Point)</c> — валидная точка (0,0), а не «нет значения».
/// Для «нет значения» используется <c>Point?</c> или <see cref="Move.None"/>.
/// </remarks>
public readonly record struct Point(byte X, byte Y)
{
    /// <summary>Буквы столбцов по системе «A1…T19»: буква I пропущена.</summary>
    private const string ColumnLetters = "ABCDEFGHJKLMNOPQRST";

    /// <summary>Проверяет, что точка лежит на доске указанного размера.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns><c>true</c>, если обе координаты в пределах доски.</returns>
    public bool IsOnBoard(BoardSize size) => X < size.Value && Y < size.Value;

    /// <summary>Возвращает соседей по стороне, лежащих на доске.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns>От двух до четырёх соседей: диагонали не учитываются.</returns>
    public IEnumerable<Point> Neighbors(BoardSize size)
    {
        if (X > 0)
        {
            yield return new Point((byte)(X - 1), Y);
        }

        if (X < size.Value - 1)
        {
            yield return new Point((byte)(X + 1), Y);
        }

        if (Y > 0)
        {
            yield return new Point(X, (byte)(Y - 1));
        }

        if (Y < size.Value - 1)
        {
            yield return new Point(X, (byte)(Y + 1));
        }
    }

    /// <summary>Возвращает координату в формате «D4».</summary>
    /// <returns>Буква столбца без I и номер строки, начиная с единицы.</returns>
    /// <remarks>
    /// Номер строки считается сверху вниз, поэтому <c>default(Point)</c> даёт «A1».
    /// Отображение снизу вверх, как на диаграммах, зависит от размера доски
    /// и строится в слое App. Для координаты за пределами 19×19 возвращается «(x,y)»:
    /// <see cref="ToString"/> не должен бросать исключений.
    /// </remarks>
    public override string ToString() =>
        X < ColumnLetters.Length ? $"{ColumnLetters[X]}{Y + 1}" : $"({X},{Y})";
}
