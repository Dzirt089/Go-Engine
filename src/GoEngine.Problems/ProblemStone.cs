namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Камень начальной расстановки задачи.</summary>
/// <param name="Point">Точка доски.</param>
/// <param name="Color">Цвет камня: пустым он быть не может.</param>
/// <remarks>
/// Расстановка задаётся списком камней, а не ходами: позицию собирает <see cref="ProblemSetup"/>
/// легальными ходами, потому что у доски нет публичного способа поставить камень иначе.
/// </remarks>
public readonly record struct ProblemStone(Point Point, StoneColor Color)
{
    /// <summary>Создаёт камень чёрного цвета.</summary>
    /// <param name="x">Колонка.</param>
    /// <param name="y">Строка.</param>
    /// <returns>Камень расстановки.</returns>
    public static ProblemStone Black(byte x, byte y) => new(new Point(x, y), StoneColor.Black);

    /// <summary>Создаёт камень белого цвета.</summary>
    /// <param name="x">Колонка.</param>
    /// <param name="y">Строка.</param>
    /// <returns>Камень расстановки.</returns>
    public static ProblemStone White(byte x, byte y) => new(new Point(x, y), StoneColor.White);
}
