using GoEngine.App.Rendering;
using GoEngine.Core;
using SkiaSharp;

namespace GoEngine.App.Tests;

/// <summary>Отрисовка разметки территории: доска не должна падать и картинка должна меняться.</summary>
/// <remarks>
/// Проверяется на настоящем холсте Skia: разметка рисуется поверх сетки и камней, поэтому
/// ошибка в ней ломала бы всю доску, а не только знаки.
/// </remarks>
public sealed class BoardTerritoryRenderTests
{
    /// <summary>Сторона холста проверок в пикселях.</summary>
    private const int CanvasSize = 300;

    [Fact]
    public void Пустая_Доска_Рисуется_Без_Территории()
    {
        Assert.NotEmpty(Render(new Board(BoardSize.Size9), territory: null));
    }

    [Fact]
    public void Доска_С_Одним_Камнем_Рисуется_Без_Территории_И_С_Ней()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.NotEmpty(Render(board, territory: null));
        Assert.NotEmpty(Render(board, territory: Scorer.Ownership(board)));
    }

    [Fact]
    public void Разметка_Территории_Меняет_Картинку()
    {
        var board = new Board(BoardSize.Size9);

        var without = Render(board, territory: null);
        var with = Render(board, territory: Scorer.Ownership(board));

        Assert.NotEqual(without, with);
    }

    [Fact]
    public void Список_От_Другой_Доски_Не_Рисуется_И_Не_Падает()
    {
        var board = new Board(BoardSize.Size9);

        var without = Render(board, territory: null);
        var foreign = Render(board, territory: new StoneColor[4]);

        Assert.Equal(without, foreign);
    }

    /// <summary>Рисует доску на холсте Skia и возвращает снимок в виде строки.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="territory">Разметка территории или <c>null</c>.</param>
    /// <returns>Двоичное содержимое PNG: сравнением видно, изменилась ли картинка.</returns>
    private static string Render(Board board, IReadOnlyList<StoneColor>? territory)
    {
        using var surface = SKSurface.Create(new SKImageInfo(CanvasSize, CanvasSize));

        BoardRenderer.Draw(surface.Canvas, board, CanvasSize, CanvasSize, territory: territory);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return Convert.ToBase64String(data.ToArray());
    }
}
