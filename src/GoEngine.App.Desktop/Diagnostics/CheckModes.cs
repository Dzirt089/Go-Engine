using System.Globalization;
using GoEngine.AI;
using GoEngine.App.Rendering;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoPoint = GoEngine.Core.Point;

namespace GoEngine.App.Diagnostics;

/// <summary>Проверочные режимы: приложение проверяет себя без окна и возвращает код возврата.</summary>
/// <remarks>
/// Каждый режим печатает результат и возвращает 0 при успехе. Проверки идут на тех же классах,
/// что и игра: рендерер, модель представления, хранилища. Режимы собраны здесь, а не в точке
/// входа, чтобы `Program` оставался тем, чем называется, — запуском приложения.
/// </remarks>
internal static class CheckModes
{
    /// <summary>Зерно проверок: они должны повторяться от запуска к запуску.</summary>
    private const int SeedForChecks = 20260926;

    /// <summary>Сколько партий играет проверка нагрузки.</summary>
    private const int StressGames = 100;

    /// <summary>Сколько ходов должна выдержать проверка нагрузки.</summary>
    private const int StressMoves = 10000;

    /// <summary>Сколько ходов игрока играет финальный smoke-тест.</summary>
    private const int SmokeMoves = 20;

    /// <summary>Проверяет, что щелчок по центру точки попадает в неё на всех размерах доски.</summary>
    /// <returns>0, если попадание точное; иначе 1.</returns>
    public static int PointMapping()
    {
        var failures = 0;

        foreach (var size in new[] { BoardSize.Size9, BoardSize.Size13, BoardSize.Size19 })
        {
            var geometry = BoardGeometry.Fit(size, RenderSamples.Width, RenderSamples.Height);

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

    /// <summary>Играет много партий подряд и смотрит на прирост памяти.</summary>
    /// <returns>0, если движок выдержал нагрузку; иначе 1.</returns>
    public static int Stress()
    {
        var random = new Random(SeedForChecks);
        var moves = 0;

        GC.Collect();
        var before = GC.GetTotalMemory(forceFullCollection: true);

        for (var number = 0; number < StressGames; number++)
        {
            var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

            while (game.Status == GameStatus.InProgress)
            {
                var legal = LegalMoves.For(game.Board, game.ToMove);
                var move = legal.Count == 0 ? Move.Pass(game.ToMove) : legal[random.Next(legal.Count)];

                if (!game.Play(move).IsSuccess)
                {
                    break;
                }

                moves++;
            }
        }

        GC.Collect();
        var growth = (GC.GetTotalMemory(forceFullCollection: true) - before) / (1024.0 * 1024.0);

        Console.WriteLine($"Go Engine: {moves} ходов в {StressGames} партиях, прирост памяти {growth:F1} МБ.");

        return moves >= StressMoves && growth < 100 ? 0 : 1;
    }

    /// <summary>Прогоняет полный сценарий партии без окна.</summary>
    /// <returns>0, если все шаги сценария прошли; иначе 1.</returns>
    public static int EndToEnd()
    {
        var failures = 0;
        var path = Path.Combine(Path.GetTempPath(), $"go-engine-smoke-{Guid.NewGuid():N}.sgf");

        try
        {
            var settings = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu10, StoneColor.Black, Komi.For9x9);
            var model = new MainViewModel(settings, new Random(SeedForChecks));

            failures += Expect(model.MoveNumber == "0", "новая партия 9×9 начата");
            failures += Expect(model.Level == "10 кю", "соперник — 10 кю");

            var played = 0;

            foreach (var point in model.Board.AllPoints())
            {
                if (played >= SmokeMoves)
                {
                    break;
                }

                if (model.PlayMove(point))
                {
                    played++;
                }
            }

            failures += Expect(played == SmokeMoves, $"сыграно ходов игрока: {played}");
            failures += Expect(model.MoveNumber == (SmokeMoves * 2).ToString(CultureInfo.InvariantCulture), "AI ответил на каждый ход");

            failures += Expect(SgfStore.Save(model.ToSgfGame(), path).IsSuccess, "партия сохранена");

            var loaded = SgfStore.Load(path);
            failures += Expect(loaded.IsSuccess, "партия прочитана");

            var moves = model.MoveNumber;
            failures += Expect(model.LoadGame(loaded.Value).IsSuccess, "партия загружена в игру");
            failures += Expect(model.MoveNumber == moves, "после загрузки ходов столько же");

            failures += Expect(model.Undo(), "ход отменён");
            failures += Expect(model.Redo(), "ход возвращён");

            failures += Expect(model.Undo() && model.PlayMove(model.Board.EmptyPoints().First()), "после отмены можно ходить снова");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        Console.WriteLine(failures == 0
            ? "Go Engine: финальный smoke-тест пройден — v1 готова."
            : $"Go Engine: ошибок smoke-теста — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Проверяет арифметику кадров анимации.</summary>
    /// <returns>0, если кадры считаются верно; иначе 1.</returns>
    public static int Animation()
    {
        var failures = 0;
        var start = new StoneAnimation(new GoPoint(4, 4), [new GoPoint(0, 0)], StoneColor.Black, 0);

        failures += Expect(start.IsActive, "анимация началась");
        failures += Expect(start.AppearanceAlpha == 0, "камень появляется с нулевой прозрачности");
        failures += Expect(start.DisappearanceAlpha == 255, "снятый камень сначала непрозрачен");

        var middle = start.Advance(StoneAnimation.DurationSeconds / 2);
        failures += Expect(middle.AppearanceAlpha > 100 && middle.AppearanceAlpha < 160, "к середине камень наполовину виден");
        failures += Expect(middle.AppearanceScale < 1, "к середине камень меньше полного");

        var finished = middle.Advance(StoneAnimation.DurationSeconds);
        failures += Expect(!finished.IsActive, "анимация завершилась");
        failures += Expect(finished.AppearanceAlpha == 255, "камень стал полностью виден");
        failures += Expect(finished.DisappearanceAlpha == 0, "снятый камень исчез");
        failures += Expect(!StoneAnimation.None.IsActive, "без хода анимации нет");

        Console.WriteLine(failures == 0
            ? "Go Engine: кадры анимации считаются верно."
            : $"Go Engine: ошибок анимации — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Проверяет запись и чтение партии в SGF через файл.</summary>
    /// <returns>0, если партия сохраняется и читается без потерь; иначе 1.</returns>
    public static int Sgf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"go-engine-game-{Guid.NewGuid():N}.sgf");
        var failures = 0;

        try
        {
            var model = new MainViewModel(AppSettings.Default, new Random(SeedForChecks));
            _ = model.PlayMove(new GoPoint(4, 4));
            var moves = model.ToSgfGame().Moves.Count;

            failures += Expect(SgfStore.Save(model.ToSgfGame(), path).IsSuccess, "партия записана");

            var loaded = SgfStore.Load(path);
            failures += Expect(loaded.IsSuccess, "партия прочитана");
            failures += Expect(loaded.Value.Moves.Count == moves, "число ходов совпало");
            failures += Expect(loaded.Value.Size == BoardSize.Size9, "размер доски совпал");
            failures += Expect(loaded.Value.Komi == Komi.For9x9, "коми совпало");
            failures += Expect(model.LoadGame(loaded.Value).IsSuccess, "партия восстановлена в игре");
            failures += Expect(!SgfStore.Load(path + ".missing").IsSuccess, "отсутствующий файл — отказ");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        Console.WriteLine(failures == 0
            ? "Go Engine: партия сохраняется и читается в SGF."
            : $"Go Engine: ошибок SGF — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Проверяет запись и чтение настроек партии.</summary>
    /// <returns>0, если настройки сохраняются и читаются; иначе 1.</returns>
    public static int Settings()
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
    public static int ViewModel()
    {
        var model = new MainViewModel(AppSettings.Default, new Random(SeedForChecks));
        var failures = 0;

        failures += Expect(model.ToMove == StoneColorLabels.Label(StoneColor.Black), "игрок чёрными ходит первым");
        failures += Expect(model.MoveNumber == "0", "номер хода в начале равен нулю");
        failures += Expect(model.Status == "Идёт", "партия идёт");
        failures += Expect(model.LastMove is null, "последнего хода ещё нет");
        failures += Expect(model.Level == "20 кю", "уровень AI из настроек");

        failures += Expect(model.PlayMove(new GoPoint(4, 4)), "ход игрока принят");
        failures += Expect(model.MoveNumber == "2", "AI ответил своим ходом");
        failures += Expect(model.ToMove == StoneColorLabels.Label(StoneColor.Black), "после ответа AI снова ход игрока");
        failures += Expect(model.LastMove is not null, "последний ход виден");
        failures += Expect(!model.PlayMove(new GoPoint(4, 4)), "ход в занятую точку отклонён");
        failures += Expect(
            model.Score.Contains(StoneColorLabels.Label(StoneColor.Black), StringComparison.Ordinal),
            "счёт посчитан");
        failures += Expect(model.Board.At(new GoPoint(4, 4)) == StoneColor.Black, "камень стоит на доске");

        // Отмена убирает ход игрока вместе с ответом AI, возврат — восстанавливает.
        failures += Expect(model.Undo(), "ход отменён");
        failures += Expect(model.MoveNumber == "0", "после отмены ходов нет");
        failures += Expect(model.Board.At(new GoPoint(4, 4)) == StoneColor.Empty, "доска очищена");
        failures += Expect(model.Redo(), "ход возвращён");
        failures += Expect(model.MoveNumber == "2", "после возврата ходов снова два");
        failures += Expect(model.Board.At(new GoPoint(4, 4)) == StoneColor.Black, "камень снова на доске");
        failures += Expect(!model.Undo() || !model.Undo(), "повторная отмена не ломает партию");

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
}
