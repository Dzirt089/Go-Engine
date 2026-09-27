using Avalonia;
using GoEngine.App.Diagnostics;

namespace GoEngine.App;

/// <summary>Точка входа приложения.</summary>
/// <remarks>
/// Здесь только запуск: разбор аргументов и проверочные режимы вынесены в `Diagnostics`
/// (`ModeArguments`, `CheckModes`, `RenderSamples`), чтобы точка входа не разрасталась.
/// Проверочные режимы не открывают окно и возвращают код возврата — их вызывает
/// `check.ps1` / `check.sh`.
/// </remarks>
internal static class Program
{
    /// <summary>Запускает приложение.</summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Код возврата процесса.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains(ModeArguments.Smoke))
        {
            // Проверка запуска без графической сессии: на сборочных агентах окно открыть нельзя.
            Console.WriteLine("Go Engine: приложение собрано, точка входа работает.");
            return 0;
        }

        if (args.Contains(ModeArguments.Check))
        {
            // Проверка ввода без окна: попадание щелчка в точку доски считается той же
            // геометрией, что и на экране.
            return CheckModes.PointMapping();
        }

        if (args.Contains(ModeArguments.Stress))
        {
            // Проверка нагрузки: движок играет много партий подряд, память не растёт бесконтрольно.
            return CheckModes.Stress();
        }

        if (args.Contains(ModeArguments.EndToEnd))
        {
            // Финальный smoke-тест: игра, сохранение, загрузка, отмена, возврат, новый ход.
            return CheckModes.EndToEnd();
        }

        if (args.Contains(ModeArguments.Animation))
        {
            // Проверка кадров анимации без окна: состояние считается той же арифметикой.
            return CheckModes.Animation();
        }

        if (args is [ModeArguments.RenderAnimation, var framePath, ..])
        {
            RenderSamples.AnimationFrame(framePath);
            Console.WriteLine($"Go Engine: кадр анимации отрисован в {framePath}.");
            return 0;
        }

        if (args.Contains(ModeArguments.Sgf))
        {
            // Проверка формата без окна: партия записывается в файл и читается обратно.
            return CheckModes.Sgf();
        }

        if (args.Contains(ModeArguments.Settings))
        {
            // Проверка настроек без окна: запись в файл и чтение обратно.
            return CheckModes.Settings();
        }

        if (args.Contains(ModeArguments.State))
        {
            // Проверка панели статуса без окна: модель представления показывает данные партии.
            return CheckModes.ViewModel();
        }

        if (args is [ModeArguments.Render, var path, ..])
        {
            // Проверка рисования без окна: тот же рендерер, что и на экране, пишет PNG.
            RenderSamples.ToPng(path);
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
}
