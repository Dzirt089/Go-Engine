using GoEngine.App.Rendering;
using GoEngine.Core;
using SkiaSharp;

namespace GoEngine.App.Tests;

/// <summary>Читаемость подписей координат на камнях крайних линий.</summary>
/// <remarks>
/// Дефект 3 QA 2026-10-17: буква столбца и цифра строки рисовались поверх камня крайней линии
/// и тонули в нём — тёмное на тёмном. Проверка идёт по настоящему холсту Skia: подпись обязана
/// стоять на подложке цвета доски, а камень под ней — закрываться, а не просвечивать.
/// </remarks>
public sealed class BoardCoordinateRenderTests
{
    /// <summary>Сторона холста проверок в пикселях.</summary>
    private const int CanvasSize = 300;

    /// <summary>Порог «камня» по каналам: чёрный камень доски (<c>0x1A1A1A</c>) темнее этого.</summary>
    private const int StoneChannel = 40;

    /// <summary>Доля камня под подписью, которую обязана закрыть подложка.</summary>
    private const double CoveredShare = 0.7;

    [Fact]
    public void Подпись_Координаты_На_Камне_Крайней_Линии_Стоит_На_Подложке()
    {
        // Худший случай: камень стоит на верхней линии в столбце D (третьем). Подпись рисуется
        // в поле над сеткой — ровно там, где камень крайней линии и оказывается.
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(3, 0), StoneColor.Black));

        using var withCoordinates = Render(board, showCoordinates: true);
        using var withoutCoordinates = Render(board, showCoordinates: false);

        var plate = Plate(board, column: 3, letter: "D");
        var stone = 0;
        var covered = 0;

        for (var x = (int)MathF.Ceiling(plate.Left); x <= (int)MathF.Floor(plate.Right); x++)
        {
            for (var y = (int)MathF.Ceiling(plate.Top); y <= (int)MathF.Floor(plate.Bottom); y++)
            {
                var stonePixel = withoutCoordinates.GetPixel(x, y);

                if (stonePixel.Red >= StoneChannel || stonePixel.Green >= StoneChannel || stonePixel.Blue >= StoneChannel)
                {
                    continue;
                }

                stone++;

                if (withCoordinates.GetPixel(x, y) != stonePixel)
                {
                    covered++;
                }
            }
        }

        Assert.True(stone > 20, $"под подписью нет камня: {stone} тёмных пикселей");
        Assert.True(
            covered > stone * CoveredShare,
            $"подложка закрыла только {covered} из {stone} пикселей камня — подпись тонет в нём");
    }

    /// <summary>Рисует доску на холсте Skia.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="showCoordinates">Рисовать ли подписи координат.</param>
    /// <returns>Снимок холста; владелец обязан его освободить.</returns>
    private static SKBitmap Render(Board board, bool showCoordinates)
    {
        var bitmap = new SKBitmap(new SKImageInfo(CanvasSize, CanvasSize));

        using var canvas = new SKCanvas(bitmap);

        BoardRenderer.Draw(canvas, board, CanvasSize, CanvasSize, showCoordinates: showCoordinates);

        return bitmap;
    }

    /// <summary>Подложка подписи над верхней линией сетки.</summary>
    /// <param name="board">Позиция: из неё берётся размер доски.</param>
    /// <param name="column">Столбец подписи от 0.</param>
    /// <param name="letter">Буква столбца, как её рисует доска.</param>
    /// <returns>Прямоугольник подложки в пикселях холста.</returns>
    /// <remarks>
    /// Центр подписи над сеткой — на половине поля доски: так же его считает отрисовка
    /// (<c>BoardRenderer.DrawCoordinates</c>). Кегль — от доли клетки (<c>CoordinateSizeRatio</c>).
    /// </remarks>
    private static SKRect Plate(Board board, int column, string letter)
    {
        var geometry = BoardGeometry.Fit(board.Size, CanvasSize, CanvasSize);
        var textSize = Math.Max(8f, geometry.Cell * BoardRenderer.CoordinateSizeRatio);
        var centre = new SKPoint(geometry.Center(column), geometry.Center(0) - (geometry.Origin / 2));

        return BoardRenderer.CoordinatePlateRect(letter, textSize, centre);
    }
}
