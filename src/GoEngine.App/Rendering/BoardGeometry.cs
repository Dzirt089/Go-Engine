using GoEngine.Core;
using SkiaSharp;

namespace GoEngine.App.Rendering;

/// <summary>Геометрия доски: перевод точек <c>Core.Point</c> в пиксели и обратно.</summary>
/// <remarks>
/// Доска вписывается в отведённую область с полями по краям, поэтому масштаб зависит
/// от размера окна. Геометрия — только вычисления: ни рисования, ни правил здесь нет.
/// </remarks>
public readonly record struct BoardGeometry
{
    /// <summary>Доля меньшей стороны области, занятая полем вокруг сетки.</summary>
    private const float MarginRatio = 0.08f;

    /// <summary>Доля клетки, которую занимает камень.</summary>
    private const float StoneRadiusRatio = 0.47f;

    private BoardGeometry(BoardSize size, float cell, float origin)
    {
        Size = size;
        Cell = cell;
        Origin = origin;
    }

    /// <summary>Размер доски.</summary>
    public BoardSize Size { get; }

    /// <summary>Расстояние между линиями сетки в пикселях.</summary>
    public float Cell { get; }

    /// <summary>Координата первой линии сетки в пикселях.</summary>
    public float Origin { get; }

    /// <summary>Радиус камня в пикселях.</summary>
    public float StoneRadius => Cell * StoneRadiusRatio;

    /// <summary>Вписывает доску в область рисования.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="width">Ширина области в пикселях.</param>
    /// <param name="height">Высота области в пикселях.</param>
    /// <returns>Геометрия доски.</returns>
    public static BoardGeometry Fit(BoardSize size, float width, float height)
    {
        var side = Math.Min(width, height);
        var margin = side * MarginRatio;
        var cell = (side - (2 * margin)) / (size.Value - 1);

        // Сетка центрируется по меньшей стороне области, поэтому доска не «уезжает» при вытянутом окне.
        var originX = (width - (cell * (size.Value - 1))) / 2;
        var originY = (height - (cell * (size.Value - 1))) / 2;

        return new BoardGeometry(size, cell, Math.Min(originX, originY));
    }

    /// <summary>Возвращает координату линии сетки в пикселях.</summary>
    /// <param name="line">Номер линии от 0.</param>
    /// <returns>Пиксельная координата линии.</returns>
    public float Center(int line) => Origin + (Cell * line);

    /// <summary>Возвращает центр точки доски в пикселях.</summary>
    /// <param name="point">Точка доски.</param>
    /// <returns>Пиксельные координаты центра.</returns>
    public SKPoint Pixel(Point point) => new(Center(point.X), Center(point.Y));

    /// <summary>Переводит пиксельные координаты в точку доски.</summary>
    /// <param name="x">Координата по горизонтали в пикселях.</param>
    /// <param name="y">Координата по вертикали в пикселях.</param>
    /// <returns>Ближайшая точка доски или <c>null</c>, если щелчок пришёлся мимо доски.</returns>
    public Point? PointAt(float x, float y)
    {
        var column = MathF.Round((x - Origin) / Cell);
        var line = MathF.Round((y - Origin) / Cell);

        if (column < 0 || column >= Size.Value || line < 0 || line >= Size.Value)
        {
            return null;
        }

        // Щелчок мимо пересечения линий дальше половины клетки не считается ходом.
        if (Math.Abs(x - Center((int)column)) > Cell / 2 || Math.Abs(y - Center((int)line)) > Cell / 2)
        {
            return null;
        }

        return new Point((byte)column, (byte)line);
    }
}
