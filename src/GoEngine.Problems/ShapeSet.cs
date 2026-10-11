using GoEngine.Core;

namespace GoEngine.Problems;

/// <summary>Независимые формы позиции: что игрок построил своими камнями, без вложенных форм.</summary>
/// <remarks>
/// <para>
/// Замечание пользователя 2026-10-11: «если независимых форм станет несколько, тогда их различают
/// номерами и списком, а не цветом». Набор собирается по камням, о которых идёт речь (ходы решения,
/// ход урока, ход партии): для каждого камня распознаётся форма, одинаковые наборы сливаются, а
/// вложенные отбрасываются — пасть тигра включает камни косыми, и показывать обе формы нельзя.
/// </para>
/// <para>
/// Набор упорядочен по числу камней: первая форма — самая крупная, её и стоит смотреть первой.
/// Одна форма в наборе — обычное дело, и тогда номера не нужны: вид показывает подсветку без меток.
/// </para>
/// </remarks>
public static class ShapeSet
{
    /// <summary>Собирает независимые формы по указанным камням.</summary>
    /// <param name="board">Позиция после ходов.</param>
    /// <param name="colour">Цвет того, кто строил формы.</param>
    /// <param name="stones">Камни, о которых идёт речь: ходы решения, урока или партии.</param>
    /// <returns>Формы по убыванию числа камней, без вложенных друг в друга.</returns>
    public static IReadOnlyList<ShapeFacts> Of(Board board, StoneColor colour, IReadOnlyList<Point> stones)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(colour);
        ArgumentNullException.ThrowIfNull(stones);

        List<ShapeFacts> found = [];

        foreach (var point in stones)
        {
            if (board.At(point) != colour)
            {
                continue;
            }

            var shape = MoveShape.Of(board, Move.Play(point, colour));

            // Форма из одного камня — это не форма: «отдельный камень» и «рядом со своими» ничего
            // не объясняют и только засоряют список.
            if (shape.Points.Count < 2)
            {
                continue;
            }

            if (!found.Any(existing => Same(existing, shape)))
            {
                found.Add(shape);
            }
        }

        // Вложенные формы убираем: если все камни одной формы входят в другую, показывать обе
        // бессмысленно — это один и тот же участок доски.
        var ordered = found.OrderByDescending(shape => shape.Points.Count).ToList();

        return [.. ordered.Where(shape => !ordered.Any(other => other != shape && shape.Points.All(other.Points.Contains)))];
    }

    /// <summary>Совпадают ли формы по названию и камням.</summary>
    /// <param name="first">Первая форма.</param>
    /// <param name="second">Вторая форма.</param>
    /// <returns><c>true</c>, если это одна и та же форма.</returns>
    private static bool Same(ShapeFacts first, ShapeFacts second) =>
        string.Equals(first.Name, second.Name, StringComparison.Ordinal)
        && first.Points.Count == second.Points.Count
        && first.Points.All(second.Points.Contains);
}
