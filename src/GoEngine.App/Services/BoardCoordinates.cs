using System.Globalization;
using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Подписи точек доски: буква столбца и номер строки.</summary>
/// <remarks>
/// Правило берётся у отрисовки доски (<c>BoardRenderer.DrawCoordinates</c>): буква «I» пропускается,
/// а строки нумеруются сверху вниз, то есть подпись строки равна <c>y + 1</c>. Подсказка в режиме
/// задач обязана называть точку так же, как её подписывает доска, иначе игрок ищет не ту точку.
/// </remarks>
public static class BoardCoordinates
{
    /// <summary>Буквы столбцов: без «I».</summary>
    private const string ColumnLetters = "ABCDEFGHJKLMNOPQRST";

    /// <summary>Собирает подпись точки.</summary>
    /// <param name="point">Точка доски.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Например, «D6» для точки (3,5) на доске 9×9: строки нумеруются сверху вниз.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Точка вне доски.</exception>
    public static string Label(Point point, BoardSize size)
    {
        if (point.X >= size.Value || point.Y >= size.Value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(point),
                $"Точка {point.X},{point.Y} находится вне доски {size.Value}×{size.Value}.");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{ColumnLetters[point.X]}{point.Y + 1}");
    }
}
