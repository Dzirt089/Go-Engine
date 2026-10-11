using GoEngine.App.Rendering;
using GoEngine.Core;
using SkiaSharp;

namespace GoEngine.App.Tests;

/// <summary>Неоновая подсветка формы: под камнями появляется зелёное свечение.</summary>
/// <remarks>
/// Замечание пользователя 2026-10-11: «показывать не только разбор, но и на столе подсвечивать
/// зелёным цветом форму… неоновой подсветкой снизу камня, как автомобили внизу». Проверка идёт
/// по пикселям: считаются точки, где зелёный заметно ярче красного и синего.
/// </remarks>
public sealed class BoardShapeGlowRenderTests
{
    /// <summary>Сторона холста проверок в пикселях.</summary>
    private const int CanvasSize = 320;

    [Fact]
    public void Без_Формы_Зелёного_Свечения_Нет()
    {
        var board = Position();

        Assert.Equal(0, GreenPixels(Render(board, null)));
    }

    [Fact]
    public void Подсветка_Формы_Даёт_Зелёные_Пиксели()
    {
        var board = Position();
        var shape = new[] { new Point(3, 3), new Point(4, 3) };

        Assert.True(GreenPixels(Render(board, shape)) > 100, "свечения под камнями не видно");
    }

    [Fact]
    public void Подсветка_Не_Закрашивает_Камни()
    {
        // Свечение рисуется под камнями: их середина остаётся чёрной, иначе позиция стала бы
        // нечитаемой. Считаем не все тёмные пиксели (свечение подсвечивает соседние линии),
        // а центр каждого камня формы.
        var board = Position();
        var geometry = BoardGeometry.Fit(BoardSize.Size9, CanvasSize, CanvasSize);

        foreach (var shape in new[] { new[] { new Point(3, 3) }, new[] { new Point(4, 3) } })
        {
            var pixels = Render(board, shape);
            var centre = geometry.Pixel(shape[0]);
            var pixel = pixels[((int)centre.Y * CanvasSize) + (int)centre.X];

            Assert.True(
                pixel.Red < 60 && pixel.Green < 60 && pixel.Blue < 60,
                $"камень закрашен свечением: R{pixel.Red} G{pixel.Green} B{pixel.Blue}");
        }
    }

    [Fact]
    public void Плохая_Форма_Светится_Предупреждающим_Цветом()
    {
        // Цвет означает смысл: у плохой формы (пустой треугольник, кирпич) подсветка тёплая,
        // а не зелёная — иначе игрок читал бы «всё хорошо» там, где форма вредит.
        var board = Position();
        var good = RenderWith(board, [new Point(3, 3), new Point(4, 3)], good: true);
        var bad = RenderWith(board, [new Point(3, 3), new Point(4, 3)], good: false);

        Assert.True(GreenPixels(good) > 100, "зелёной подсветки нет");
        Assert.True(GreenPixels(bad) < 30, $"плохая форма светится зелёным: {GreenPixels(bad)}");
        Assert.True(WarmPixels(bad) > 100, "предупреждающей подсветки нет");
    }

    [Fact]
    public void Точки_Формы_Приходят_От_Распознавания()
    {
        // Связка: распознаватель отдаёт камни формы, а не только название.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(3, 3), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(3, 4), StoneColor.Black));

        var shape = Problems.MoveShape.Of(board, Move.Play(new Point(3, 4), StoneColor.Black));

        Assert.Equal("ноби", shape.Name);
        Assert.Equal(2, shape.Points.Count);
        Assert.Contains(new Point(3, 3), shape.Points);
        Assert.Contains(new Point(3, 4), shape.Points);
    }

    /// <summary>Позиция проверок: два чёрных камня рядом.</summary>
    /// <returns>Доска.</returns>
    private static Board Position() =>
        new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(3, 3), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(4, 3), StoneColor.Black));

    /// <summary>Рисует доску и возвращает пиксели.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="shape">Камни формы или <c>null</c>.</param>
    /// <returns>Пиксели в формате RGBA.</returns>
    private static SKColor[] Render(Board board, IReadOnlyList<Point>? shape) =>
        RenderWith(board, shape, good: true);

    /// <summary>Рисует доску с подсветкой заданного смысла.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="shape">Камни формы.</param>
    /// <param name="good">Хорошая ли форма.</param>
    /// <returns>Пиксели в формате RGBA.</returns>
    private static SKColor[] RenderWith(Board board, IReadOnlyList<Point>? shape, bool good)
    {
        using var surface = SKSurface.Create(new SKImageInfo(CanvasSize, CanvasSize));
        BoardRenderer.Draw(surface.Canvas, board, CanvasSize, CanvasSize, shape: shape, shapeIsGood: good);

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);

        return [.. Enumerable.Range(0, CanvasSize).SelectMany(y => Enumerable.Range(0, CanvasSize).Select(x => bitmap.GetPixel(x, y)))];
    }

    /// <summary>Считает пиксели, где зелёный заметно ярче остальных каналов.</summary>
    /// <param name="pixels">Пиксели.</param>
    /// <returns>Число зелёных пикселей.</returns>
    private static int GreenPixels(IReadOnlyList<SKColor> pixels) =>
        pixels.Count(pixel => pixel.Green > pixel.Red + 30 && pixel.Green > pixel.Blue + 30);

    /// <summary>Считает тёплые пиксели: по ним видно предупреждающую подсветку плохой формы.</summary>
    /// <param name="pixels">Пиксели.</param>
    /// <returns>Число тёплых пикселей.</returns>
    private static int WarmPixels(IReadOnlyList<SKColor> pixels) =>
        pixels.Count(pixel => pixel.Red > pixel.Green + 30 && pixel.Red > pixel.Blue + 60);

    /// <summary>Считает тёмные пиксели: по ним видно, что камни не закрашены свечением.</summary>
    /// <param name="pixels">Пиксели.</param>
    /// <returns>Число тёмных пикселей.</returns>
    private static int DarkPixels(IReadOnlyList<SKColor> pixels) =>
        pixels.Count(pixel => pixel.Red < 60 && pixel.Green < 60 && pixel.Blue < 60);
}
