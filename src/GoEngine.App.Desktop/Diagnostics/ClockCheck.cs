using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoPoint = GoEngine.Core.Point;

namespace GoEngine.App.Diagnostics;

/// <summary>Проверка часов партии: старт, пауза, продолжение, отмена и запрет ходов на паузе.</summary>
/// <remarks>
/// Своя единица на свою проверку — как подсчёт территории в <see cref="TerritoryCheck"/>.
/// Проверка идёт по той же модели представления, что и панель: ходы на паузе обязаны
/// отклоняться, а не только выглядеть недоступными. Время берётся у системных часов, поэтому
/// «идут ли часы» проверяется короткими паузами и приростом точного времени, а не подделкой
/// времени: строка времени округляет до секунд, а прирост виден и за десятки миллисекунд.
/// </remarks>
internal static class ClockCheck
{
    /// <summary>Зерно проверочной партии: она должна повторяться от запуска к запуску.</summary>
    /// <remarks>То же зерно, что у остальных проверок: партия задана ходами, а зерно достаётся
    /// уровням AI, которые ходят сами.</remarks>
    private const int Seed = 20260926;

    /// <summary>Проверяет часы партии: старт, пауза, продолжение, отмена и запрет ходов на паузе.</summary>
    /// <returns>0, если время партии считается верно; иначе 1.</returns>
    public static int Run()
    {
        // Проба времени: часы считают с точностью до миллисекунд, поэтому «идут или стоят»
        // видно и за короткую задержку — ждать настоящую секунду проверке незачем.
        const int probeMilliseconds = 60;

        var failures = 0;
        var model = new MainViewModel(AppSettings.Default, new Random(Seed));

        failures += CheckModes.Expect(!model.IsGameStarted, "новая партия не начата");
        failures += CheckModes.Expect(model.ClockDisplay == "00:00", "время новой партии — 00:00");
        failures += CheckModes.Expect(!model.CanPauseClock && !model.CanResumeClock, "до старта часы не останавливают");
        failures += CheckModes.Expect(!model.CanAbortGame, "до старта партию не отменяют");

        model.StartGame();
        failures += CheckModes.Expect(model.IsGameStarted && model.IsGameRunning, "«Начать партию» начала партию");
        failures += CheckModes.Expect(model.GameTime < TimeSpan.FromSeconds(1), "часы начинают счёт с нуля");
        failures += CheckModes.Expect(model.CanPauseClock && model.CanAbortGame, "в идущей партии доступны пауза и отмена");

        Thread.Sleep(probeMilliseconds);
        var running = model.GameTime;
        failures += CheckModes.Expect(running > TimeSpan.Zero, $"часы идут: {model.ClockDisplay}");

        model.PauseGame();
        failures += CheckModes.Expect(model.IsGamePaused && !model.IsGameRunning, "пауза остановила часы");
        failures += CheckModes.Expect(model.CanResumeClock && !model.CanPauseClock, "на паузе доступно продолжение");

        var frozen = model.GameTime;
        Thread.Sleep(probeMilliseconds);
        failures += CheckModes.Expect(model.GameTime == frozen, "на паузе время не растёт");

        var board = model.Board;
        failures += CheckModes.Expect(!model.PlayMove(new GoPoint(4, 4)), "ход на паузе отклонён");
        failures += CheckModes.Expect(!model.Pass(), "пас на паузе отклонён");

        // Доска та же самая: отклонённый ход не меняет позицию, а не только не сообщает о ней.
        failures += CheckModes.Expect(ReferenceEquals(board, model.Board), "доска на паузе не изменилась");

        model.ResumeGame();
        failures += CheckModes.Expect(model.IsGameRunning && !model.IsGamePaused, "продолжение вернуло партию в игру");
        failures += CheckModes.Expect(model.PlayMove(new GoPoint(4, 4)), "после продолжения ход принят");

        model.AbortGame();
        failures += CheckModes.Expect(!model.IsGameStarted && !model.IsGameRunning, "отмена партии вернула «не начата»");
        failures += CheckModes.Expect(model.ClockDisplay == "00:00" && model.GameTime == TimeSpan.Zero, "отмена партии обнулила часы");
        failures += CheckModes.Expect(model.MoveNumber == "0" && !model.CanAbortGame, "отмена партии очистила ходы");

        Console.WriteLine(failures == 0
            ? "Go Engine: часы партии идут, пауза останавливает и время, и ходы, отмена возвращает начало."
            : $"Go Engine: ошибок часов партии — {failures}.");

        return failures == 0 ? 0 : 1;
    }
}
