using Avalonia;
using GoEngine.App.Rendering;
using GoEngine.Core;
using SkiaSharp;
using GoPoint = GoEngine.Core.Point;

namespace GoEngine.App;

/// <summary>Точка входа приложения.</summary>
internal static class Program
{
    /// <summary>Аргумент проверки запуска без окна: сборка и точка входа целы.</summary>
    private const string SmokeArgument = "--smoke";

    /// <summary>Аргумент проверочного рендера доски в PNG.</summary>
    private const string RenderArgument = "--render";

    /// <summary>Аргумент проверки попадания щелчка в точку доски.</summary>
    private const string CheckArgument = "--check";

    /// <summary>Ширина проверочного рендера в пикселях.</summary>
    private const int RenderWidth = 600;

    /// <summary>Высота проверочного рендера в пикселях.</summary>
    private const int RenderHeight = 600;

    /// <summary>Запускает приложение.</summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Код возврата процесса.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains(SmokeArgument))
        {
            // Проверка запуска без графической сессии: на сборочных агентах окно открыть нельзя.
            Console.WriteLine("Go Engine: приложение собрано, точка входа работает.");
            return 0;
        }

        if (args.Contains(CheckArgument))
        {
            // Проверка ввода без окна: попадание щелчка в точку доски считается той же
            // геометрией, что и на экране.
            return CheckPointMapping();
        }

        if (args is [RenderArgument, var path, ..])
        {
            // Проверка рисования без окна: тот же рендерер, что и на экране, пишет PNG.
            RenderToPng(path);
            Console.WriteLine($"Go Engine: доска отрисована в {path}.");
            return 0;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        return 0;
    }

    /// <summary>Собирает конфигурацию Avalonia.</summary>
    /// <returns>Построитель приложения для запуска и тестов.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>Проверяет, что щелчок по центру точки попадает в неё на всех размерах доски.</summary>
    /// <returns>0, если попадание точное; иначе 1.</returns>
    private static int CheckPointMapping()
    {
        var failures = 0;

        foreach (var size in new[] { BoardSize.Size9, BoardSize.Size13, BoardSize.Size19 })
        {
            var geometry = BoardGeometry.Fit(size, RenderWidth, RenderHeight);

            foreach (var point in new Board(size).AllPoints())
            {
                var pixel = geometry.Pixel(point);
                var back = geometry.PointAt(pixel.X, pixel.Y);

                if (back != point)
                {
                    Console.WriteLine($"не совпало: {size} {point} → ({pixel.X:F1}, {pixel.Y:F1}) → {back}");
                    failures++;
                }
            }
        }

        Console.WriteLine(failures == 0
            ? "Go Engine: попадание щелчка в точку проверено на досках 9×9, 13×13 и 19×19."
            : $"Go Engine: ошибок попадания — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Рисует проверочную позицию в PNG-файл.</summary>
    /// <param name="path">Путь к файлу PNG.</param>
    private static void RenderToPng(string path)
    {
        var board = DemoPosition();

        using var surface = SKSurface.Create(new SKImageInfo(RenderWidth, RenderHeight));
        BoardRenderer.Draw(surface.Canvas, board, RenderWidth, RenderHeight, new GoPoint(7, 7), new GoPoint(4, 6));

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
}