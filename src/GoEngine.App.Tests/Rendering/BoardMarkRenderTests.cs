using GoEngine.App.Rendering;
using GoEngine.Core;
using SkiaSharp;

namespace GoEngine.App.Tests;

/// <summary>Отрисовка пометок конца партии: мёртвые камни и подсказка.</summary>
/// <remarks>
/// Проверяется на настоящем холсте Skia по пикселям: помеченный камень обязан выглядеть иначе,
/// чем живой, — иначе подтверждение подсчёта было бы кнопкой вслепую. Пометка пустой точки
/// не должна менять картинку вовсе: мёртвым помечают камень, а не пересечение.
/// </remarks>
public sealed class BoardMarkRenderTests
{
    /// <summary>Сторона холста проверок в пикселях.</summary>
    private const int CanvasSize = 300;

    [Fact]
    public void Помеченный_Мёртвый_Камень_Выглядит_Иначе_Живого()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        var alive = Pixel(board, deadPoints: null, new Point(4, 4));
        var dead = Pixel(board, deadPoints: [new Point(4, 4)], new Point(4, 4));

        // Живой камень в центре чёрный, помеченный приглушён и перечёркнут: центр светлеет.
        Assert.True(alive.Red < 60, $"живой камень должен быть тёмным, а не {alive}");
        Assert.True(dead.Red > 150, $"помеченный камень должен светлеть, а не {dead}");
    }

    [Fact]
    public void Пометка_Пустой_Точки_Не_Меняет_Картинку()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Equal(Render(board, deadPoints: null), Render(board, deadPoints: [new Point(2, 2)]));
    }

    [Fact]
    public void Подсказка_Рисует_Маркер_На_Пустой_Точке()
    {
        var board = new Board(BoardSize.Size9);

        var without = Pixel(board, deadPoints: null, new Point(4, 4));
        var with = Pixel(board, deadPoints: null, new Point(4, 4), hint: new Point(4, 4));

        Assert.NotEqual(without, with);
    }

    /// <summary>Рисует доску и возвращает цвет пикселя в центре точки.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="deadPoints">Помеченные мёртвыми камни или <c>null</c>.</param>
    /// <param name="point">Точка, цвет которой проверяется.</param>
    /// <param name="hint">Точка подсказки или <c>null</c>.</param>
    /// <returns>Цвет пикселя в центре точки.</returns>
    private static SKColor Pixel(Board board, IReadOnlyList<Point>? deadPoints, Point point, Point? hint = null)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(CanvasSize, CanvasSize));
        using var canvas = new SKCanvas(bitmap);

        BoardRenderer.Draw(canvas, board, CanvasSize, CanvasSize, deadPoints: deadPoints, hint: hint);

        var center = BoardGeometry.Fit(board.Size, CanvasSize, CanvasSize).Pixel(point);

        return bitmap.GetPixel((int)center.X, (int)center.Y);
    }

    /// <summary>Рисует доску на холсте Skia и возвращает снимок в виде строки.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="deadPoints">Помеченные мёртвыми камни или <c>null</c>.</param>
    /// <returns>Двоичное содержимое PNG: сравнением видно, изменилась ли картинка.</returns>
    private static string Render(Board board, IReadOnlyList<Point>? deadPoints)
    {
        using var surface = SKSurface.Create(new SKImageInfo(CanvasSize, CanvasSize));

        BoardRenderer.Draw(surface.Canvas, board, CanvasSize, CanvasSize, deadPoints: deadPoints);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return Convert.ToBase64String(data.ToArray());
    }
}
