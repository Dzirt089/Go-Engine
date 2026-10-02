using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Diagnostics;

/// <summary>Проверка подсчёта территории и пленных: числа сходятся арифметикой, а не на глаз.</summary>
/// <remarks>
/// <para>
/// Режим без окна. Проверяются две позиции: идущая партия с запрошенной оценкой (игрок нажал
/// «Территория» — программа сама помечает мёртвую группу) и та же позиция, завершённая двумя
/// пасами (идёт согласование). Печатаются разметка территории, строка пленных, счёт и разбор
/// обеих систем — ровно то, что видит игрок на панели.
/// </para>
/// <para>
/// Числа проверяются независимой арифметикой: территория и нейтраль пересчитываются по владению
/// точками, камни — по доске без мёртвых, пленные складываются из счётчиков партии и пометок.
/// Строки панели при этом не разбираются: сверяются величины, а не текст, поэтому переформулировка
/// подписи не ломает проверку. Японская величина = территория + пленные + коми,
/// китайская = камни + территория (D-064).
/// </para>
/// <para>
/// Сумма «территория + камни + нейтраль» обязана равняться площади доски: владение покрывает
/// доску ровно один раз. Проверка 2026-10-02 искала расхождение состава на доске подсчёта, поэтому
/// та же арифметика сверяется ещё и на 13×13 и 19×19 — там другое коми и другая площадь.
/// </para>
/// <para>
/// Отдельно проверяется доступность переключателя «Территория»: до первого хода его нет
/// (на пустой доске территории не бывает), в идущей партии он есть, и после конца партии тоже.
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

    /// <summary>Проверяет подсчёт территории и пленных на идущей и завершённой партии.</summary>
    /// <returns>0, если все числа сошлись; иначе 1.</returns>
    /// <remarks>Оценка позиции постоянная: мёртвых считает программа, и нажимать ничего не нужно.</remarks>
    public static int Run()
    {
        var failures = CountInProgress();
        failures += CountFinished();
        failures += CheckOtherSizes();
        failures += CheckToggleAvailability();

        Console.WriteLine(failures == 0
            ? "Go Engine: территория и пленные считаются верно."
            : $"Go Engine: ошибок подсчёта территории и пленных — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Проверяет подсчёт идущей партии: оценка постоянная, нажатий не требуется.</summary>
    /// <returns>Число невыполненных проверок.</returns>
    /// <remarks>
    /// Игрок ничего не нажимает (D-070): разметка включена с запуска, мёртвые группы помечает
    /// программа, и счёт считает их с первого чтения. Переключатель только скрывает разметку
    /// на экране — на оценку позиции он не влияет.
    /// </remarks>
    private static int CountInProgress()
    {
        var failures = 0;
        var model = CreateModel();
        _ = model.LoadGame(DeadCornerGame(finished: false));

        var expected = ExpectedNumbers(model, GameKomi);

        Console.WriteLine("— идущая партия без единого нажатия —");
        Console.WriteLine($"Пометки мёртвых: {model.DeadPoints.Count} — {string.Join(", ", model.DeadPoints)}");
        Console.WriteLine($"Разметка территории: {model.ScoreBreakdown}");
        Console.WriteLine($"Пленные: {model.PrisonersLine}");
        Console.WriteLine($"Счёт: {model.Score}");
        Console.WriteLine($"Разбор: {model.ScoreDetail}");
        Console.WriteLine($"Предохранитель перебора: {model.DeadProposalBudget} узлов");

        failures += Check(model.HasTerritory, "разметка показана сразу, без нажатий");
        failures += Check(model.CanToggleTerritory, "переключатель территории доступен в идущей партии");
        failures += Check(model.DeadPoints.Count > 0, "программа сама пометила мёртвую группу в идущей партии");
        failures += Check(
            model.DeadPoints.SequenceEqual(Endgame.ProposeDead(model.Board)),
            "пометки идущей партии совпадают с перебором ядра");
        failures += Counts(model, expected);
        failures += Check(model.DeadProposalCount == 1, "предложение мёртвых считается один раз на позицию");
        failures += Check(
            model.DeadProposalBudget == Endgame.EstimateNodesPerPosition,
            $"в идущей партии действует предохранитель оценки: ожидалось {Endgame.EstimateNodesPerPosition}, получено {model.DeadProposalBudget}");

        model.ShowTerritory = false;

        Console.WriteLine($"— разметка выключена: пометки {model.DeadPoints.Count}, переборов {model.DeadProposalCount} —");

        failures += Check(model.DeadPoints.Count > 0, "скрытая разметка не отменяет пометок: их считает программа");
        failures += Check(model.DeadProposalCount == 1, "выключение разметки не запускает перебор заново");
        failures += Check(model.Score == expected.JapaneseLine, "скрытая разметка не меняет счёт");

        return failures;
    }

    /// <summary>Проверяет подсчёт заключительной позиции: итог называется сразу.</summary>
    /// <returns>Число невыполненных проверок.</returns>
    /// <remarks>
    /// Шага подтверждения нет (D-070): после двух пасов программа сама называет итог, кнопки
    /// «Посчитать» не существует, а правка пометки кликом меняет уже названный счёт.
    /// </remarks>
    private static int CountFinished()
    {
        var failures = 0;
        var model = CreateModel();
        _ = model.LoadGame(DeadCornerGame(finished: true));

        Console.WriteLine();
        Console.WriteLine("— конец партии —");
        Console.WriteLine($"Пометки мёртвых: {model.DeadPoints.Count} — {string.Join(", ", model.DeadPoints)}");
        Console.WriteLine($"Территория: {model.ScoreBreakdown}");
        Console.WriteLine($"Пленные: {model.PrisonersLine}");
        Console.WriteLine($"Счёт: {model.Score}");
        Console.WriteLine($"Разбор: {model.ScoreDetail}");
        Console.WriteLine(
            $"Строка состояния: {GameStatusLines.Headline(model.HasOutcome, model.Status, model.ToMove)}"
            + $" · {GameStatusLines.Detail(model.HasOutcome, model.Outcome, model.MoveNumber)}");

        var expected = ExpectedNumbers(model, GameKomi);

        failures += Check(
            model.DeadProposalBudget == Endgame.MaxNodesPerPosition,
            $"в конце партии действует окончательный предохранитель: ожидалось {Endgame.MaxNodesPerPosition}, получено {model.DeadProposalBudget}");
        failures += Check(model.DeadPoints.Count > 0, "перебор предложил мёртвую группу, и она видна на доске");
        failures += Counts(model, expected);
        failures += Check(
            GameStatusLines.ShowOutcome(model.HasOutcome),
            "итог показывается сразу после двух пасов");
        failures += Check(
            model.ResultHeadline.StartsWith("Вы ", StringComparison.Ordinal),
            $"победитель назван словами игрока: «{model.ResultHeadline}»");
        failures += Check(
            model.ResultDetail == model.Outcome,
            "подпись баннера называет исход словами и не повторяет счёт");
        failures += Check(model.Score == expected.JapaneseLine, "счёт посчитан по доске без мёртвых камней");

        // Правка пометки после конца партии — право игрока: счёт пересчитывается сразу.
        var before = model.Score;

        _ = model.ToggleDeadAt(new Point(0, 0));

        Console.WriteLine($"— клик по мёртвой группе: счёт {model.Score} —");

        failures += Check(model.Score != before, "клик после конца партии меняет итог");

        // Предложение мёртвых — перебор по каждой группе-кандидату: он обязан считаться один раз
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

        return failures;
    }

    /// <summary>Проверяет ту же арифметику на других размерах доски: 13×13 и 19×19.</summary>
    /// <returns>Число невыполненных проверок.</returns>
    /// <remarks>
    /// Сумма «территория + камни + нейтраль» обязана сходиться на любой доске, а не только
    /// на проверочной девятке: расхождение означало бы, что одно из чисел панели посчитано
    /// по другой позиции (проверка 2026-10-02 искала ровно это).
    /// </remarks>
    private static int CheckOtherSizes()
    {
        var failures = 0;

        foreach (var size in new[] { BoardSize.Size13, BoardSize.Size19 })
        {
            var model = CreateModel(size);
            _ = model.LoadGame(SmallGame(size));

            model.ShowTerritory = true;

            var expected = ExpectedNumbers(model, Komi.For(size));

            Console.WriteLine();
            Console.WriteLine($"— доска {size.Value}×{size.Value}: камней на доске чёрных {model.Board.OccupiedPoints(StoneColor.Black).Count()}, белых {model.Board.OccupiedPoints(StoneColor.White).Count()} —");
            Console.WriteLine($"Разбор: {model.ScoreBreakdown.Replace(Environment.NewLine, " · ")}");

            failures += Check(model.DeadPoints.Count > 0, $"на доске {size.Value}×{size.Value} мёртвая группа помечена");
            failures += Counts(model, expected);
        }

        return failures;
    }

    /// <summary>Проверяет доступность переключателя «Территория» в трёх состояниях партии.</summary>
    /// <returns>Число невыполненных проверок.</returns>
    /// <remarks>
    /// До первого хода территории нет: все точки нейтральны, и переключать нечего. После первого
    /// хода он появляется и остаётся доступным, когда партия завершена: разметку можно скрыть.
    /// </remarks>
    private static int CheckToggleAvailability()
    {
        var failures = 0;
        var fresh = CreateModel();

        Console.WriteLine();
        Console.WriteLine($"— переключатель: до первого хода {fresh.CanToggleTerritory} —");

        failures += Check(!fresh.CanToggleTerritory, "до первого хода переключателя территории нет");

        var playing = CreateModel();
        _ = playing.LoadGame(DeadCornerGame(finished: false));

        failures += Check(playing.CanToggleTerritory, "в идущей партии переключатель доступен");

        var finished = CreateModel();
        _ = finished.LoadGame(DeadCornerGame(finished: true));

        failures += Check(finished.CanToggleTerritory, "после конца партии переключатель доступен");

        return failures;
    }

    /// <summary>Сверяет числа панели с независимо посчитанными величинами.</summary>
    /// <param name="model">Модель представления проверяемой позиции.</param>
    /// <param name="expected">Ожидаемые величины, посчитанные по доске без мёртвых камней.</param>
    /// <returns>Число невыполненных проверок.</returns>
    /// <remarks>
    /// Сверяются величины, а не строки: в модели нет публичного доступа к <c>FinalScore</c>,
    /// но есть разметка, пленные, счёт и перевес — этого довольно, чтобы проверить каждое число.
    /// </remarks>
    private static int Counts(MainViewModel model, Expected expected)
    {
        var failures = 0;

        failures += Check(
            model.Score == expected.JapaneseLine,
            $"японская величина = территория + пленные + коми: ожидалось «{expected.JapaneseLine}», получено «{model.Score}»");
        failures += Check(
            model.Territory is { } territory && SameOwnership(territory, expected.Ownership),
            "разметка территории совпадает с владением по доске без мёртвых камней");
        failures += Check(
            model.PrisonersLine == expected.PrisonersLine,
            $"строка пленных: ожидалось «{expected.PrisonersLine}», получено «{model.PrisonersLine}»");
        failures += Check(
            Near(model.Margin, expected.JapaneseMargin),
            $"перевес по японской системе: ожидалось {expected.JapaneseMargin}, получено {model.Margin}");
        failures += Check(
            model.ScoreBreakdown.Contains(expected.AreaLine, StringComparison.Ordinal),
            $"разбор площади: ожидалось «{expected.AreaLine}», получено «{model.ScoreBreakdown}»");
        failures += Check(
            model.ScoreDetail.Contains("Китайская:", StringComparison.Ordinal)
            && model.ScoreDetail.Contains("Мёртвые группы:", StringComparison.Ordinal),
            "разбор счёта называет обе системы и мёртвые группы");
        failures += Check(
            expected.Total == model.Board.Size.Area,
            $"сумма территории, камней и нейтрали равна площади доски: {expected.Total} против {model.Board.Size.Area}");

        return failures;
    }

    /// <summary>Считает ожидаемые числа по доске без мёртвых камней — независимо от модели.</summary>
    /// <param name="model">Модель с проверяемой позицией.</param>
    /// <param name="komi">Коми партии: у 19×19 оно другое, чем у 9×9.</param>
    /// <returns>Ожидаемые величины: разметка, пленные, счёт и разбор площади.</returns>
    private static Expected ExpectedNumbers(MainViewModel model, Komi komi)
    {
        var board = model.Board;
        var dead = model.DeadPoints;
        var cleared = Endgame.ClearedBoard(board, dead);
        var ownership = Scorer.Ownership(cleared);

        var blackTerritory = 0;
        var whiteTerritory = 0;
        var neutral = 0;

        // Нейтраль считается по владению, а не вычитанием из площади: только тогда сумма
        // «территория + камни + нейтраль» что-то доказывает — покрывает доску ровно один раз.
        // Камни в территорию не попадают: у них своя строка разбора.
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
            else
            {
                neutral++;
            }
        }

        var deadBlack = dead.Count(point => board.At(point) == StoneColor.Black);
        var deadWhite = dead.Count - deadBlack;

        // Пленные называются по тому, кто их взял: чёрные — снятые ими белые камни (D-064).
        var blackPrisoners = model.CapturedWhite + deadWhite;
        var whitePrisoners = model.CapturedBlack + deadBlack;

        var blackStones = cleared.OccupiedPoints(StoneColor.Black).Count();
        var whiteStones = cleared.OccupiedPoints(StoneColor.White).Count();

        var japaneseLine = $"Чёрные {blackTerritory + blackPrisoners} : {whiteTerritory + whitePrisoners + komi.Value} Белые";
        var japaneseMargin = Math.Abs(blackTerritory + blackPrisoners - (whiteTerritory + whitePrisoners + komi.Value));

        // Пока пометок нет, слагаемое одно и скобки не нужны — строка собирается так же, как в модели.
        var prisonersLine = dead.Count == 0
            ? $"Пленные: чёрные {blackPrisoners} : {whitePrisoners} белые"
            : $"Пленные: чёрные {blackPrisoners} : {whitePrisoners} белые "
                + $"(в партии {model.CapturedWhite} : {model.CapturedBlack} · мертвыми {deadWhite} : {deadBlack})";

        var areaLine = $"Территория — чёрные {blackTerritory} · белые {whiteTerritory} · нейтрально {neutral}"
            + Environment.NewLine
            + $"Камни — чёрные {blackStones} · белые {whiteStones}";

        return new Expected(japaneseLine, prisonersLine, areaLine, ownership, japaneseMargin);
    }

    /// <summary>Сравнивает две разметки владения поточечно.</summary>
    /// <param name="actual">Разметка модели представления.</param>
    /// <param name="expected">Разметка, посчитанная проверкой.</param>
    /// <returns><c>true</c>, если владелец каждой точки совпал.</returns>
    private static bool SameOwnership(IReadOnlyList<StoneColor> actual, IReadOnlyList<StoneColor> expected) =>
        actual.Count == expected.Count && actual.SequenceEqual(expected);

    /// <summary>Сравнивает два перевеса с точностью до половины очка.</summary>
    /// <param name="actual">Перевес модели.</param>
    /// <param name="expected">Перевес, посчитанный проверкой.</param>
    /// <returns><c>true</c>, если числа совпали.</returns>
    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 0.001;

    /// <summary>Создаёт партию с доказанно мёртвой белой группой в углу.</summary>
    /// <param name="finished">Завершить партию двумя пасами.</param>
    /// <returns>Модель представления проверяемой позиции.</returns>
    /// <remarks>
    /// Позиция: чёрные закрыли угол (2,0), (2,1), (0,2), (1,2), внутри остались два белых камня.
    /// Ход белых в любую из двух пустых точек угла проигрывает: чёрные отвечают и снимают группу.
    /// Живая белая пара (8,8), (8,7) стоит вдали и мёртвой не объявляется.
    /// </remarks>
    private static MainViewModel CreateModel(BoardSize? size = null) =>
        new(
            AppSettings.From(size ?? Size, DifficultyLevel.Kyu20, StoneColor.Black, GameKomi),
            new Random(Seed));

    /// <summary>Строит проверочную партию: с пасами или без них.</summary>
    /// <param name="finished">Добавить два паса и завершить партию.</param>
    /// <returns>Партия для загрузки в модель представления.</returns>
    private static SgfGame DeadCornerGame(bool finished)
    {
        List<Move> moves =
        [
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(2, 1), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White),
            Move.Play(new Point(1, 2), StoneColor.Black),
            Move.Play(new Point(8, 7), StoneColor.White)
        ];

        if (finished)
        {
            moves.Add(Move.Pass(StoneColor.Black));
            moves.Add(Move.Pass(StoneColor.White));
        }

        return new SgfGame(Size, GameKomi, moves, null);
    }

    /// <summary>Строит партию для другой доски: мёртвая группа в углу и живые камни вдали.</summary>
    /// <param name="size">Сторона доски.</param>
    /// <returns>Партия без пасов: последним ходят белые, поэтому соперник после загрузки не ходит.</returns>
    /// <remarks>
    /// Позиция та же, что на девятке, только живые белые камни отодвинуты по размеру доски:
    /// так проверка не зависит от размера, а порядок ходов оставляет очередь игроку.
    /// </remarks>
    private static SgfGame SmallGame(BoardSize size)
    {
        var far = (byte)(size.Value - 1);
        var near = (byte)(size.Value - 2);

        return new SgfGame(
            size,
            Komi.For(size),
            [
                Move.Play(new Point(2, 0), StoneColor.Black),
                Move.Play(new Point(0, 0), StoneColor.White),
                Move.Play(new Point(2, 1), StoneColor.Black),
                Move.Play(new Point(1, 0), StoneColor.White),
                Move.Play(new Point(0, 2), StoneColor.Black),
                Move.Play(new Point(far, far), StoneColor.White),
                Move.Play(new Point(1, 2), StoneColor.Black),
                Move.Play(new Point(near, far), StoneColor.White)
            ],
            null);
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

    /// <summary>Ожидаемые величины проверки, собранные из независимо посчитанных чисел.</summary>
    /// <param name="JapaneseLine">Счёт по японской системе: территория + пленные + коми.</param>
    /// <param name="PrisonersLine">Строка пленных с разложением на партию и мёртвых.</param>
    /// <param name="AreaLine">Разбор площади: территория и камни по сторонам с нейтралью.</param>
    /// <param name="Ownership">Владение точками по доске без мёртвых камней.</param>
    /// <param name="JapaneseMargin">Перевес по японской системе без знака.</param>
    private readonly record struct Expected(
        string JapaneseLine,
        string PrisonersLine,
        string AreaLine,
        IReadOnlyList<StoneColor> Ownership,
        double JapaneseMargin)
    {
        /// <summary>Все точки доски: владение покрывает её ровно один раз — камни и территория.</summary>
        public int Total => Ownership.Count;
    }
}
