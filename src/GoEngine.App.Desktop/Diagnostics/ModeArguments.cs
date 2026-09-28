namespace GoEngine.App.Diagnostics;

/// <summary>Аргументы командной строки, включающие проверочные режимы приложения.</summary>
/// <remarks>
/// Проверочные режимы не открывают окно, поэтому работают на сборочном агенте — их и вызывает
/// `check.ps1` / `check.sh` (`PROJECT.md`, раздел «Как проверять проект»). Здесь только имена
/// аргументов: разбор и поведение — в `Program` и `CheckModes`.
/// </remarks>
internal static class ModeArguments
{
    /// <summary>Проверка запуска без окна: сборка и точка входа целы.</summary>
    public const string Smoke = "--smoke";

    /// <summary>Проверка попадания щелчка в точку доски.</summary>
    public const string Check = "--check";

    /// <summary>Проверка данных панели статуса.</summary>
    public const string State = "--state";

    /// <summary>Проверка записи и чтения настроек.</summary>
    public const string Settings = "--settings";

    /// <summary>Проверка записи и чтения партии в SGF.</summary>
    public const string Sgf = "--sgf";

    /// <summary>Проверка анимации камней.</summary>
    public const string Animation = "--animation";

    /// <summary>Финальный smoke-тест: полный сценарий партии.</summary>
    public const string EndToEnd = "--e2e";

    /// <summary>Проверка нагрузки: много партий подряд.</summary>
    public const string Stress = "--stress";

    /// <summary>Проверочный рендер доски в PNG.</summary>
    public const string Render = "--render";

    /// <summary>Проверочный рендер кадра анимации.</summary>
    public const string RenderAnimation = "--render-anim";

    /// <summary>Проверка лога падений: пишет события и намеренно падает.</summary>
    /// <remarks>
    /// Режим не входит в <c>check.ps1</c>: он завершается падением по замыслу, а проверка проекта
    /// требует нулевого кода возврата. Его запускают вручную, чтобы убедиться, что после сбоя
    /// появляются копия лога и файл-признак, а следующий запуск показывает сообщение.
    /// </remarks>
    public const string CrashTest = "--crash-test";
}
