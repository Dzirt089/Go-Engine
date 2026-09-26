using Avalonia;
using GoEngine.AI;
using GoEngine.App.Rendering;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
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

    /// <summary>Зерно проверок: они должны повторяться от запуска к запуску.</summary>
    private const int SeedForChecks = 20260926;

    /// <summary>Аргумент проверки записи и чтения настроек.</summary>
    private const string SettingsArgument = "--settings";

    /// <summary>Аргумент проверки данных панели статуса.</summary>
    private const string StateArgument = "--state";

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

        if (args.Contains(SettingsArgument))
        {
            // Проверка настроек без окна: запись в файл и чтение обратно.
            return CheckSettings();
        }

        if (args.Contains(StateArgument))
        {
            // Проверка панели статуса без окна: модель представления показывает данные партии.
            return CheckViewModel();
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

    /// <summary>Проверяет запись и чтение настроек партии.</summary>
    /// <returns>0, если настройки сохраняются и читаются; иначе 1.</returns>
    private static int CheckSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"go-engine-settings-{Guid.NewGuid():N}.json");
        var failures = 0;

        try
        {
            var settings = AppSettings.From(BoardSize.Size13, DifficultyLevel.Kyu10, StoneColor.White, new Komi(5.5));
            failures += Expect(SettingsStore.Save(settings, path).IsSuccess, "настройки записаны");

            var loaded = SettingsStore.Load(path);
            failures += Expect(loaded.ToBoardSize() == BoardSize.Size13, "размер доски прочитан");
            failures += Expect(loaded.ToDifficultyLevel() == DifficultyLevel.Kyu10, "уровень AI прочитан");
            failures += Expect(loaded.ToPlayerColor() == StoneColor.White, "цвет игрока прочитан");
            failures += Expect(Math.Abs(loaded.ToKomi().Value - 5.5) < 0.001, "коми прочитано");

            failures += Expect(SettingsStore.Load(path + ".missing") is not null, "отсутствующий файл даёт настройки по умолчанию");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        Console.WriteLine(failures == 0
            ? "Go Engine: настройки сохраняются и читаются."
            : $"Go Engine: ошибок настроек — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Проверяет, что панель статуса получает данные партии.</summary>
    /// <returns>0, если все проверки прошли; иначе 1.</returns>
    private static int CheckViewModel()
    {
        var model = new MainViewModel(AppSettings.Default, new Random(SeedForChecks));
        var failures = 0;

        failures += Expect(model.ToMove == "Чёрные", "игрок чёрными ходит первым");
        failures += Expect(model.MoveNumber == "0", "номер хода в начале равен нулю");
        failures += Expect(model.Status == "Идёт", "партия идёт");
        failures += Expect(model.LastMove is null, "последнего хода ещё нет");
        failures += Expect(model.Level == "20 кю", "уровень AI из настроек");

        failures += Expect(model.PlayMove(new GoPoint(4, 4)), "ход игрока принят");
        failures += Expect(model.MoveNumber == "2", "AI ответил своим ходом");
        failures += Expect(model.ToMove == "Чёрные", "после ответа AI снова ход игрока");
        failures += Expect(model.LastMove is not null, "последний ход виден");
        failures += Expect(!model.PlayMove(new GoPoint(4, 4)), "ход в занятую точку отклонён");
        failures += Expect(model.Score.Contains("Чёрные", StringComparison.Ordinal), "счёт посчитан");
        failures += Expect(model.Board.At(new GoPoint(4, 4)) == StoneColor.Black, "камень стоит на доске");

        // Игрок белыми: AI обязан открыть партию своим ходом.
        var asWhite = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.White, Komi.For9x9);
        var whiteModel = new MainViewModel(asWhite, new Random(SeedForChecks));
        failures += Expect(whiteModel.MoveNumber == "1", "AI открыл партию за чёрных");

        // Смена настроек начинает новую партию с другим размером доски.
        var on13 = AppSettings.From(BoardSize.Size13, DifficultyLevel.Kyu30, StoneColor.Black, Komi.For13x13);
        model.ApplySettings(on13);
        failures += Expect(model.Board.Size == BoardSize.Size13, "доска стала 13×13");
        failures += Expect(model.MoveNumber == "0", "новая партия начата с нуля");

        Console.WriteLine(failures == 0
            ? "Go Engine: панель статуса получает данные партии."
            : $"Go Engine: ошибок панели статуса — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Проверяет условие и сообщает о нарушении.</summary>
    /// <param name="condition">Условие проверки.</param>
    /// <param name="description">Что проверялось.</param>
    /// <returns>0, если условие выполнено; иначе 1.</returns>
    private static int Expect(bool condition, string description)
    {
        if (!condition)
        {
            Console.WriteLine($"не выполнено: {description}");
            return 1;
        }

        return 0;
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