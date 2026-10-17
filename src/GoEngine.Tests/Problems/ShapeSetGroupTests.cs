using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests.Problems;

/// <summary>Формы позиции считаются по всей группе камней, а не по одному сыгранному камню.</summary>
/// <remarks>
/// Замечание пользователя 2026-10-17: «формы, которые состоят из двух камней, в ходе партии
/// превращаются в формы большие — но они не показываются в обучающих частях». Причина была в том,
/// что форма искалась только вокруг последнего камня.
/// </remarks>
public sealed class ShapeSetGroupTests
{
    [Fact]
    public void Пасть_Тигра_Находится_По_Одному_Камню_Группы()
    {
        // Пасть тигра: три камня углом (квадрат 2×2 без одной точки) — одна группа.
        var board = Position([
            (2, 3), (3, 3), (2, 4)
        ]);

        var shapes = ShapeSet.Of(board, StoneColor.Black, [new Point(2, 3)]);

        Assert.True(shapes.Count > 0, "по группе из трёх камней форм не найдено");
        Assert.True(
            shapes.Any(shape => shape.Points.Count >= 3),
            "выросшая форма не найдена: " + string.Join(" | ", shapes.Select(shape => shape.Name + "(" + shape.Points.Count + ")")));
    }

    [Fact]
    public void Форм_Показывается_Не_Больше_Трёх()
    {
        // Чипы форм помещаются в строку панели: больше трёх не показываем.
        var board = Position([
            (2, 2), (3, 2), (2, 3), (3, 3), (4, 3), (2, 4), (3, 4), (4, 4)
        ]);

        var shapes = ShapeSet.Of(board, StoneColor.Black, [new Point(2, 2), new Point(3, 3), new Point(4, 4)]);

        Assert.True(shapes.Count <= 3, "форм больше трёх: " + shapes.Count);
    }

    /// <summary>Собирает позицию из чёрных камней.</summary>
    /// <param name="points">Точки камней.</param>
    /// <returns>Доска с камнями.</returns>
    private static Board Position((int X, int Y)[] points)
    {
        var board = new Board(BoardSize.Size9);

        foreach (var (x, y) in points)
        {
            var move = Move.Play(new Point((byte)x, (byte)y), StoneColor.Black);
            board = board.ApplyMove(move);
        }

        return board;
    }
}
