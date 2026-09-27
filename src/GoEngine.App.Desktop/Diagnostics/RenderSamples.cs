using GoEngine.App.Rendering;
using GoEngine.Core;
using SkiaSharp;
using GoPoint = GoEngine.Core.Point;

namespace GoEngine.App.Diagnostics;

/// <summary>Проверочные кадры: тот же рендерер, что и на экране, пишет PNG без окна.</summary>
/// <remarks>
/// Размеры кадра заданы здесь, потому что проверка попадания щелчка в точку (`CheckModes`)
/// считает геометрию по тому же размеру, что и рендер.
/// </remarks>
internal static class RenderSamples
{
    /// <summary>Ширина проверочного рендера в пикселях.</summary>
    public const int Width = 600;

    /// <summary>Высота проверочного рендера в пикселях.</summary>
    public const int Height = 600;

    /// <summary>Рисует проверочную позицию в PNG-файл.</summary>
    /// <param name="path">Путь к файлу PNG.</param>
    public static void ToPng(string path)
    {
        var board = DemoPosition();

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        BoardRenderer.Draw(surface.Canvas, board, Width, Height, new GoPoint(7, 7), new GoPoint(4, 6));

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);

        data.SaveTo(file);
    }

    /// <summary>Рисует кадр анимации в PNG-файл.</summary>
    /// <param name="path">Путь к файлу PNG.</param>
    public static void AnimationFrame(string path)
    {
        var board = DemoCapture();
        var frame = new StoneAnimation(
            new GoPoint(1, 1),
            [new GoPoint(0, 0), new GoPoint(1, 0)],
            StoneColor.Black,
            0.6);

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        BoardRenderer.Draw(surface.Canvas, board, Width, Height, new GoPoint(1, 1), null, frame);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);

        data.SaveTo(file);
    }

    /// <summary>Строит позицию для проверочного рендера.</summary>
    /// <returns>Доска 9×9 с камнями в углах и центре.</returns>
    private static Board DemoPosition()
    {
        var board = new Board(BoardSize.Size9);

        foreach (var (x, y) in new[] { (2, 2), (6, 2), (4, 4), (2, 6) })
        {
            board = board.ApplyMove(Move.Play(new GoPoint((byte)x, (byte)y), StoneColor.Black));
        }

        foreach (var (x, y) in new[] { (3, 3), (6, 6), (4, 2), (5, 5) })
        {
            board = board.ApplyMove(Move.Play(new GoPoint((byte)x, (byte)y), StoneColor.White));
        }

        return board;
    }

    /// <summary>Строит позицию с только что снятыми камнями.</summary>
    /// <returns>Доска 9×9, где белые сняли два чёрных камня.</returns>
    private static Board DemoCapture()
    {
        var board = new Board(BoardSize.Size9);

        foreach (var (x, y, color) in new (int X, int Y, StoneColor Color)[]
        {
            (0, 0, StoneColor.Black),
            (1, 0, StoneColor.Black),
            (2, 0, StoneColor.White),
            (0, 1, StoneColor.White),
            (1, 1, StoneColor.White)
        })
        {
            board = board.ApplyMove(Move.Play(new GoPoint((byte)x, (byte)y), color));
        }

        return board;
    }
}
