namespace GoEngine.App.Diagnostics;

/// <summary>Проверочные режимы командной строки: выбор режима и его запуск.</summary>
/// <remarks>
/// <para>
/// Режимы не открывают окно и возвращают код возврата — их вызывает <c>check.ps1</c> / <c>check.sh</c>.
/// Таблица режимов собрана в одном месте, а не цепочкой проверок в точке входа: у каждого режима
/// названы и ключ, и то, что он проверяет, поэтому новый режим нельзя добавить, забыв описание,
/// а точка входа остаётся тем, чем называется, — запуском приложения.
/// </para>
/// <para>
/// Разбор аргументов намеренно мягкий, как и был: режим ищется среди аргументов, а не обязан быть
/// первым. Файловые режимы (рендеры) требуют, чтобы ключ стоял первым, а за ним шёл путь: путь
/// с ведущими дефисами иначе не отличить от ключа.
/// </para>
/// </remarks>
internal static class ModeRunner
{
    /// <summary>Режим проверки: ключ, описание для справки и действие.</summary>
    /// <param name="Key">Ключ командной строки.</param>
    /// <param name="Description">Что режим проверяет.</param>
    /// <param name="Run">Действие режима: возвращает код возврата.</param>
    private readonly record struct Mode(string Key, string Description, Func<int> Run);

    /// <summary>Режимы, которым путь к файлу не нужен.</summary>
    /// <remarks>
    /// Порядок проверок сохранён прежним: он не влияет на исход (ключи не пересекаются), но так
    /// проще сверять поведение с прежней цепочкой проверок.
    /// </remarks>
    private static readonly Mode[] PlainModes =
    [
        new(
            ModeArguments.Smoke,
            "точка входа: приложение собрано и запускается без графической сессии",
            () =>
            {
                Console.WriteLine("Go Engine: приложение собрано, точка входа работает.");

                return 0;
            }),
        new(ModeArguments.CrashTest, "лог падений: пишет события и намеренно падает", CheckModes.CrashTest),
        new(ModeArguments.Check, "попадание щелчка в точку доски", CheckModes.PointMapping),
        new(ModeArguments.Stress, "нагрузка: сто партий и прирост памяти", CheckModes.Stress),
        new(ModeArguments.EndToEnd, "полный сценарий партии", CheckModes.EndToEnd),
        new(ModeArguments.Animation, "кадры анимации камней", CheckModes.Animation),
        new(ModeArguments.Sgf, "запись и чтение партии в SGF", CheckModes.Sgf),
        new(ModeArguments.Settings, "запись и чтение настроек", CheckModes.Settings),
        new(ModeArguments.Problems, "режим задач: подписи и линия решения", CheckModes.Problems),
        new(ModeArguments.Result, "строки итога партии", CheckModes.ResultLines),
        new(ModeArguments.State, "панель статуса партии", CheckModes.ViewModel),
        new(ModeArguments.Territory, "подсчёт территории и пленных", TerritoryCheck.Run),
        new(ModeArguments.Clock, "часы партии: старт, пауза, отмена", CheckModes.Clock),
        new(ModeArguments.Update, "обновление: приглашение и ход загрузки без сети", CheckModes.Update)
    ];

    /// <summary>Запускает проверочный режим, если он запрошен.</summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <param name="exitCode">Код возврата режима или 0, если режим не запрошен.</param>
    /// <returns><c>true</c>, если режим найден и выполнен.</returns>
    public static bool TryRun(string[] args, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (var mode in PlainModes)
        {
            if (args.Contains(mode.Key))
            {
                exitCode = mode.Run();

                return true;
            }
        }

        // Файловые режимы: ключ первым, за ним путь. Проверяются в том же порядке, что и раньше.
        if (TryRender(args, ModeArguments.RenderAnimation, RenderSamples.AnimationFrame, "кадр анимации", out exitCode)
            || TryRender(args, ModeArguments.RenderTerritory, RenderSamples.TerritoryPng, "заключительная позиция", out exitCode)
            || TryRender(args, ModeArguments.Render, RenderSamples.ToPng, "доска", out exitCode))
        {
            return true;
        }

        exitCode = 0;

        return false;
    }

    /// <summary>Рисует проверочный кадр в файл, если запрошен соответствующий режим.</summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <param name="key">Ключ режима.</param>
    /// <param name="draw">Что рисовать: путь к файлу.</param>
    /// <param name="what">Название кадра для сообщения.</param>
    /// <param name="exitCode">Код возврата режима или 0, если режим не запрошен.</param>
    /// <returns><c>true</c>, если режим найден и выполнен.</returns>
    private static bool TryRender(string[] args, string key, Action<string> draw, string what, out int exitCode)
    {
        if (args is [var first, var path, ..] && string.Equals(first, key, StringComparison.Ordinal))
        {
            draw(path);
            Console.WriteLine($"Go Engine: {what} отрисована в {path}.");
            exitCode = 0;

            return true;
        }

        exitCode = 0;

        return false;
    }
}
