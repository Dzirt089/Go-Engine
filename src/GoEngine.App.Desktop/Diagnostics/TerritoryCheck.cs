using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Diagnostics;

/// <summary>Проверка подсчёта территории и пленных: числа сходятся арифметикой, а не на глаз.</summary>
/// <remarks>
/// <para>
/// Режим без окна. Позиция та же, что в тестах конца партии: белая группа в углу доказанно мертва,
/// партия завершена двумя пасами. Режим печатает разметку территории, строку пленных, счёт и разбор
/// обеих систем до подтверждения и после него — ровно то, что видит игрок на панели.
/// </para>
/// <para>
/// Числа проверяются независимой арифметикой: территория пересчитывается по владению точками,
/// пленные складываются из счётчиков партии и пометок, и уже из них собираются ожидаемые строки.
/// Так проверка не повторяет подсчёт модели сама собой. Японская величина = территория + пленные
/// + коми, китайская = камни + территория (D-064).
/// </para>
/// </remarks>
internal static class TerritoryCheck
{
    /// <summary>Сторона доски проверки.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    /// <summary>Коми проверочной партии: стандартное для 9×9.</summary>
    private static readonly Komi GameKomi = Komi.For9x9;

    /// <summary>Зерно проверки: партия должна повторяться от запуска к запуску.</summary>
    private const int Seed = 20260926;

    /// <summary>Сколько раз режим читает свойства, чтобы убедиться в кэше предложения мёртвых.</summary>
    private const int ReadRepeats = 50;

    /// <summary>Проверяет подсчёт территории и пленных на заключительной позиции.</summary>
    /// <returns>0, если все числа сошлись; иначе 1.</returns>
    public static int Run()
    {
        var failures = 0;
        var model = CreateCountingModel();

        Console.WriteLine($"Согласование: {model.IsCounting}, кнопка «Посчитать» доступна: {model.CanConfirmScore}");
        Console.WriteLine($"Пометки мёртвых: {model.DeadPoints.Count} — {string.Join(", ", model.DeadPoints)}");
        Console.WriteLine($"Территория: {model.ScoreBreakdown}");
        Console.WriteLine($"Пленные: {model.PrisonersLine}");
        Console.WriteLine($"Счёт до подтверждения: {model.Score}");
        Console.WriteLine($"Разбор до подтверждения: {model.ScoreDetail}");
        Console.WriteLine(
            $"Строка состояния: {GameStatusLines.Headline(model.IsCounting, model.HasOutcome, model.Status, model.ToMove)}"
            + $" · {GameStatusLines.Detail(model.IsCounting, model.HasOutcome, model.Outcome, model.MoveNumber)}");

        var expected = ExpectedNumbers(model);

        failures += Check(model.IsCounting, "после двух пасов идёт согласование");
        failures += Check(model.CanConfirmScore, "кнопка «Посчитать» доступна сразу");
        failures += Check(model.DeadPoints.Count > 0, "перебор предложил мёртвую группу, и она видна на доске");
        failures += Check(
            model.Score == expected.JapaneseLine,
            $"японская величина = территория + пленные + коми: ожидалось «{expected.JapaneseLine}», получено «{model.Score}»");
        failures += Check(
            model.ScoreDetail.Contains(expected.ChineseLine, StringComparison.Ordinal),
            $"китайская величина = камни + территория: ожидалось «{expected.ChineseLine}», получено «{model.ScoreDetail}»");
        failures += Check(
            model.PrisonersLine == expected.PrisonersLine,
            $"строка пленных: ожидалось «{expected.PrisonersLine}», получено «{model.PrisonersLine}»");
        failures += Check(
            !GameStatusLines.ShowOutcome(model.IsCounting, model.HasOutcome),
            "до подтверждения победитель не назван");

        model.ConfirmScore();

        Console.WriteLine($"Счёт после подтверждения: {model.Score}");
        Console.WriteLine($"Итог: {model.ResultHeadline} · {model.ResultDetail}");

        failures += Check(!model.IsCounting, "после подтверждения согласование закончилось");
        failures += Check(
            GameStatusLines.ShowOutcome(model.IsCounting, model.HasOutcome),
            "после подтверждения итог показывается");
        failures += Check(
            model.ResultHeadline.StartsWith("Вы ", StringComparison.Ordinal),
            $"после подтверждения победитель назван словами игрока: «{model.ResultHeadline}»");
        failures += Check(
            model.Score == expected.JapaneseLine,
            "подтверждение не изменило числа: пометки и счёт были верны и до него");

        // Предложение мёртвых — перебор до 20 000 позиций на группу: он обязан считаться один раз
        // на позицию, а не на каждое чтение счёта, территории или пленных.
        var proposals = model.DeadProposalCount;

        for (var index = 0; index < ReadRepeats; index++)
        {
            _ = model.Score;
            _ = model.ScoreDetail;
            _ = model.ScoreBreakdown;
            _ = model.Territory;
            _ = model.PrisonersLine;
            _ = model.Outcome;
            _ = model.Margin;
            _ = model.DeadPoints;
        }

        failures += Check(
            model.DeadProposalCount == proposals,
            $"предложение мёртвых не считается повторно на чтение: было {proposals}, стало {model.DeadProposalCount}");

        Console.WriteLine(failures == 0
            ? "Go Engine: территория и пленные считаются верно."
            : $"Go Engine: ошибок подсчёта территории и пленных — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Считает ожидаемые числа по доске без мёртвых камней — независимо от модели.</summary>
    /// <param name="model">Модель с заключительной позицией.</param>
    /// <returns>Ожидаемые строки счёта, разбора и пленных.</returns>
    private static Expected ExpectedNumbers(MainViewModel model)
    {
        var board = model.Board;
        var dead = model.DeadPoints;
        var cleared = Endgame.ClearedBoard(board, dead);
        var ownership = Scorer.Ownership(cleared);

        var blackTerritory = 0;
        var whiteTerritory = 0;

        foreach (var point in cleared.AllPoints())
        {
            if (cleared.At(point) != StoneColor.Empty)
            {
                continue;
            }

            var owner = ownership[(point.Y * cleared.Size.Value) + point.X];

            if (owner == StoneColor.Black)
            {
                blackTerritory++;
            }
            else if (owner == StoneColor.White)
            {
                whiteTerritory++;
            }
        }

        var deadBlack = dead.Count(point => board.At(point) == StoneColor.Black);
        var deadWhite = dead.Count - deadBlack;

        // Пленные называются по тому, кто их взял: чёрные — снятые ими белые камни (D-064).
        var blackPrisoners = model.CapturedWhite + deadWhite;
        var whitePrisoners = model.CapturedBlack + deadBlack;

        var blackStones = cleared.OccupiedPoints(StoneColor.Black).Count();
        var whiteStones = cleared.OccupiedPoints(StoneColor.White).Count();

        var japaneseLine = $"Чёрные {blackTerritory + blackPrisoners} : {whiteTerritory + whitePrisoners + GameKomi.Value} Белые";
        var chineseLine = $"Китайская: чёрные {blackStones + blackTerritory} : {whiteStones + whiteTerritory + GameKomi.Value} белые";

        // Пока пометок нет, слагаемое одно и скобки не нужны — строка собирается так же, как в модели.
        var prisonersLine = dead.Count == 0
            ? $"Пленные: чёрные {blackPrisoners} : {whitePrisoners} белые"
            : $"Пленные: чёрные {blackPrisoners} : {whitePrisoners} белые "
                + $"(в партии {model.CapturedWhite} : {model.CapturedBlack} · мертвыми {deadWhite} : {deadBlack})";

        return new Expected(japaneseLine, chineseLine, prisonersLine);
    }

    /// <summary>Создаёт партию с доказанно мёртвой белой группой, завершённую двумя пасами.</summary>
    /// <returns>Модель с идущим согласованием мёртвых групп.</returns>
    /// <remarks>
    /// Позиция: чёрные закрыли угол (2,0), (2,1), (0,2), (1,2), внутри остались два белых камня.
    /// Ход белых в любую из двух пустых точек угла проигрывает: чёрные отвечают и снимают группу.
    /// </remarks>
    private static MainViewModel CreateCountingModel()
    {
        var model = new MainViewModel(
            AppSettings.From(Size, DifficultyLevel.Kyu20, StoneColor.Black, GameKomi),
            new Random(Seed));

        var moves = new[]
        {
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(2, 1), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White),
            Move.Play(new Point(1, 2), StoneColor.Black),
            Move.Play(new Point(8, 7), StoneColor.White),
            Move.Pass(StoneColor.Black),
            Move.Pass(StoneColor.White)
        };

        var loaded = model.LoadGame(new SgfGame(Size, GameKomi, moves, null));

        if (!loaded.IsSuccess)
        {
            throw new DomainException($"Проверочная партия не загрузилась: {loaded.Error}");
        }

        return model;
    }

    /// <summary>Проверяет условие и печатает ошибку, если оно не выполнено.</summary>
    /// <param name="condition">Условие проверки.</param>
    /// <param name="message">Что проверялось — словами, для разбора прогона.</param>
    /// <returns>1, если проверка не прошла; иначе 0.</returns>
    private static int Check(bool condition, string message)
    {
        if (condition)
        {
            return 0;
        }

        Console.WriteLine($"  ошибка: {message}");

        return 1;
    }

    /// <summary>Ожидаемые строки проверки, собранные из независимо посчитанных чисел.</summary>
    /// <param name="JapaneseLine">Счёт по японской системе: территория + пленные + коми.</param>
    /// <param name="ChineseLine">Строка китайской системы: камни + территория + коми.</param>
    /// <param name="PrisonersLine">Строка пленных с разложением на партию и мёртвых.</param>
    private readonly record struct Expected(string JapaneseLine, string ChineseLine, string PrisonersLine);
}
