using System.Text;
using GoEngine.Core;

namespace GoEngine.Problems;

using System.Globalization;

/// <summary>Тексты задачи: точная цель, отклик на ход и разбор решения.</summary>
/// <remarks>
/// <para>
/// Замечание пользователя 2026-10-11: «когда задача решена — необходимо дать понятное разъяснение,
/// почему задача решена так, и как это повлияет на следующие гипотетические ходы. Я всё сделал
/// с подсказками, но так и не понял суть задачи. А задача должна в конце дать этот ответ».
/// </para>
/// <para>
/// Ничего не выдумывается: тексты собираются по правилам и позиции — целевая группа и её точки,
/// дыхания до и после хода, глаза, снятия. Формулировки только называют то, что посчитано,
/// поэтому разбор честен и на чужой задаче.
/// </para>
/// </remarks>
public static class ProblemTexts
{
    /// <summary>Точная цель: какую группу решают, где она стоит и сколько у неё дыханий.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="board">Начальная позиция задачи.</param>
    /// <returns>Строку вида «Цель: снять белую группу из 3 камней (E5, F5, G5) на верхней стороне — у неё 3 дыхания».</returns>
    /// <remarks>
    /// Точка решения не называется: цель описывает <b>предмет</b> задачи, а не ответ. Раньше в цели
    /// стояло «убить белую группу» без единой координаты, и игрок не понимал, о какой группе речь
    /// (жалоба пользователя 2026-10-11).
    /// </remarks>
    public static string Goal(Problem problem, Board board)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(board);

        var group = TargetGroup(problem, board);

        if (group.Count == 0)
        {
            return Action(problem.Goal);
        }

        var liberties = Liberties(board, group);

        // Формулировка автора остаётся главной: в ней задача названа целиком, а группа у опорной
        // точки — это то, с чего начинают смотреть. Так строка не спорит с названием задачи,
        // даже если камней в ней больше, чем в одной группе (например «зажать группу», а у точки
        // стоят два камня).
        var subject = $"Ключевая группа: {Group(board, group)}, у неё {Breaths(liberties)}";

        return problem.Goal == ProblemGoal.Reference
            ? char.ToUpperInvariant(subject[0]) + subject[1..]
            : $"{Action(problem.Goal)}: {subject}";
    }

    /// <summary>Отклик на ход: координата, что ход сделал и как изменилась целевая группа.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="before">Позиция до хода.</param>
    /// <param name="move">Ход игрока.</param>
    /// <param name="after">Позиция после хода.</param>
    /// <returns>Строку вида «D5 — снято 2 камня; у белой группы осталось 2 дыхания».</returns>
    public static string Feedback(Problem problem, Board before, Move move, Board after)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(after);

        if (move.Type == MoveType.Pass)
        {
            return "Пас";
        }

        var point = move.Point;
        var facts = MoveDescriber.FactsOf(before, after, move, 1);
        List<string> parts = [];

        // Сколько камней соперника действительно исчезло с доски: число берётся из разницы позиций,
        // а не из счётчика снятий. Иначе строка говорила «снято 1 камень», а на доске ничего
        // не менялось (нюанс из отчёта QA 2026-10-17).
        var removed = Removed(before, after, move.Color.Opponent());

        if (removed > 0)
        {
            parts.Add($"снято {Stones(removed)}");
        }
        else if (facts.Atari.Count > 0)
        {
            parts.Add($"ставлю {Accusative(facts.Atari[0].Color)} группу в атари: у неё одно дыхание");
        }

        if (facts.Connects)
        {
            parts.Add("соединяю свои группы");
        }

        if (facts.Cuts)
        {
            parts.Add("разрезаю группы соперника");
        }

        // Что стало с целевой группой: дыхания до и после — главное, что видит решающий.
        var beforeGroup = TargetGroup(problem, before);
        var afterGroup = TargetGroup(problem, after);

        if (beforeGroup.Count > 0 && afterGroup.Count == 0)
        {
            parts.Add($"{Name(before.At(beforeGroup[0]))} группа снята: у неё не осталось дыханий");
        }
        else if (afterGroup.Count > 0)
        {
            var was = Liberties(before, beforeGroup);
            var now = Liberties(after, afterGroup);


            if (now != was)
            {
                parts.Add($"у {NameOf(after.At(afterGroup[0]))} группы {Breaths(now)} (было {was})");
            }
        }

        var label = BoardCoordinates.Label(point, after.Size);

        return parts.Count == 0 ? label : $"{label} — {string.Join("; ", parts)}";
    }

    /// <summary>Разбор решения: почему оно работает и что это даёт в дальнейших ходах.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="board">Позиция после решения.</param>
    /// <param name="line">Ходы решающего по порядку.</param>
    /// <param name="replies">Ответы соперника по порядку.</param>
    /// <returns>Разбор: что дало решение, чем ответил соперник, что это меняет.</returns>
    /// <remarks>
    /// Разбор отвечает на вопрос «почему это работает»: чем группа жива теперь и что придётся
    /// делать сопернику дальше. Это ответ на жалобу пользователя 2026-10-11: задача, решённая
    /// с подсказками, оставляла без понимания сути.
    /// </remarks>
    public static string Solution(Problem problem, Board board, IReadOnlyList<Move> line, IReadOnlyList<Move> replies)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(replies);

        var group = TargetGroup(problem, board);
        StringBuilder text = new();
        var moves = string.Join(" и ", line.Where(move => move.Type == MoveType.Play).Select(move => BoardCoordinates.Label(move.Point, board.Size)));

        if (group.Count == 0)
        {
            var who = IsOwn(problem, problem.SolverColor.Opponent()) ? "Ваша группа" : "Группа соперника";

            text.Append($"{who} снята: у неё не осталось дыханий.")
                .Append(moves.Length > 0 ? $" Последние камни ушли после {moves}." : string.Empty);
            text.Append(" Дальше эта часть доски свободна: вернуться сюда можно только с нуля, ")
                .Append("а очки за снятые камни уже в счёте.");

            return text.ToString();
        }

        // Первое, что нужно игроку, — что дало решение: чем стали его камни и что закрыто.
        var where = Group(board, group);
        text.Append(moves.Length > 0
            ? $"После {moves}: {where}, {Breaths(Liberties(board, group))}."
            : $"{char.ToUpperInvariant(where[0])}{where[1..]}: {Breaths(Liberties(board, group))}.");

        // Вердикт перебора: жива группа или её ещё можно убить. Это и есть ответ «что я выиграл».
        var verdict = LifeVerdict(problem, board, group);

        if (verdict.Length > 0)
        {
            text.Append(' ').Append(verdict);
        }

        var played = line.Where(move => move.Type == MoveType.Play).ToList();

        if (played.Count > 0)
        {
            var shape = MoveShape.Of(board, played[^1]);

            text.Append($" Форма: {shape.Name} — {shape.Why}");
        }

        if (replies.Count > 0)
        {
            var answered = string.Join(", ", replies.Where(move => move.Type == MoveType.Play).Select(move => BoardCoordinates.Label(move.Point, board.Size)));

            if (answered.Length > 0)
            {
                text.Append($" Противодействие: соперник отвечает {answered}.");
            }
        }

        var eyes = EyePoints(board, group);

        if (eyes.Count >= 2)
        {
            var eyeLabels = string.Join(" и ", eyes.Take(2).Select(point => BoardCoordinates.Label(point, board.Size)));

            text.Append($" Два глаза — {eyeLabels} — делают группу неубиваемой: соперник закроет только один, ")
                .Append("а на вторую угрозу вы отвечаете.");
        }

        // «Что это меняет» — смысл, написанный по задаче. Общая формулировка здесь бесполезна:
        // игрок и так видит дыхания и глаза, ему нужен вывод (жалоба пользователя 2026-10-11:
        // «по сути пустой текст, который и так понятен»).
        var meaning = ProblemStrategy.Of(problem);

        if (meaning.Length > 0)
        {
            text.Append(' ').Append(meaning);
        }

        return text.ToString();
    }

    /// <summary>Камни целевой группы в позиции.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="board">Позиция.</param>
    /// <returns>Точки группы; пустой список — группы на доске нет.</returns>
    private static IReadOnlyList<Point> TargetGroup(Problem problem, Board board)
    {
        if (!ProblemGoalCheck.TargetPresent(board, problem))
        {
            return [];
        }

        var group = GroupTracker.FindGroup(board, problem.Target);

        return group.Stones;
    }

    /// <summary>Свободные точки вокруг группы: её дыхания.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="group">Камни группы.</param>
    /// <returns>Число дыханий.</returns>
    private static int Liberties(Board board, IReadOnlyList<Point> group)
    {
        if (group.Count == 0)
        {
            return 0;
        }

        var tracked = GroupTracker.FindGroup(board, group[0]);

        return tracked.Liberties.Count;
    }

    /// <summary>Глаза группы: пустые точки, которые по строгому критерию принадлежат только ей.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="group">Камни группы.</param>
    /// <returns>Точки глаз по порядку обхода доски.</returns>
    private static IReadOnlyList<Point> EyePoints(Board board, IReadOnlyList<Point> group)
    {
        var colour = board.At(group[0]);
        List<Point> eyes = [];

        foreach (var point in group)
        {
            foreach (var neighbour in Neighbours(point, board.Size))
            {
                if (board.At(neighbour) == StoneColor.Empty
                    && !eyes.Contains(neighbour)
                    && ProblemLife.IsEye(board, neighbour, colour))
                {
                    eyes.Add(neighbour);
                }
            }
        }

        return eyes;
    }

    /// <summary>Соседние точки в пределах доски.</summary>
    /// <param name="point">Точка.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Соседей по линиям.</returns>
    private static IEnumerable<Point> Neighbours(Point point, BoardSize size)
    {
        if (point.X > 0)
        {
            yield return new Point((byte)(point.X - 1), point.Y);
        }

        if (point.X + 1 < size.Value)
        {
            yield return new Point((byte)(point.X + 1), point.Y);
        }

        if (point.Y > 0)
        {
            yield return new Point(point.X, (byte)(point.Y - 1));
        }

        if (point.Y + 1 < size.Value)
        {
            yield return new Point(point.X, (byte)(point.Y + 1));
        }
    }

    /// <summary>Жива ли группа: ответ о жизни, сказанный человеческим языком.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="board">Позиция после решения.</param>
    /// <param name="group">Камни целевой группы.</param>
    /// <returns>Строку о жизни группы; пустая — считать не удалось.</returns>
    /// <remarks>
    /// Считает движок, а не текст: «обеспечить жизнь» — это утверждение о позиции, и его обязан
    /// подтвердить поиск жизни. Задачи из источника цель не проверяют (Goal = Reference), поэтому
    /// единственный честный источник ответа — сама позиция. Формулировки — человеческие: игроку
    /// не нужно знать, как устроен поиск (замечание пользователя 2026-10-11 о «программистском жаргоне»).
    /// </remarks>
    private static string LifeVerdict(Problem problem, Board board, IReadOnlyList<Point> group)
    {
        var colour = board.At(group[0]);
        var own = IsOwn(problem, colour);
        var survive = ProblemLife.CanSurvive(board, colour, group, LifeDepth, LifeRadius, LifeNodes);

        if (survive.LimitReached)
        {
            // Для задачи из источника приговор не выносится, но и молчание не годится: игрок
            // должен видеть, что проверка упёрлась в свой предел, а не что группа мертва.
            return problem.Goal == ProblemGoal.Reference
                ? $"За {LifeDepth} полуходов снятие не найдено: группа держится, но это не доказанная жизнь."
                : string.Empty;
        }

        if (survive.Forced)
        {
            // У задачи из источника исход не доказывается: поиск ограничен горизонтом, и «убить
            // нельзя» было бы обещанием сильнее проверки (замечание ревизии 2026-10-17). Честная
            // формулировка называет ровно то, что проверено, — снятие за горизонт не найдено.
            if (problem.Goal == ProblemGoal.Reference)
            {
                return $"За {LifeDepth} полуходов снятие не найдено: группа держится, но это не доказанная жизнь.";
            }

            return own
                ? "Группа живая: убить её нельзя — на любую угрозу у вас найдётся ответ."
                : "Эта группа ещё держится: одним ходом её не убить, борьба продолжается.";
        }

        var eyes = EyePoints(board, group);

        if (problem.Goal == ProblemGoal.Reference)
        {
            return eyes.Count == 1
                ? "У группы один глаз, и этого мало: второго пока нет, но она ещё держится."
                : "Группа ещё держится: двух глаз у неё нет, и снятой она не считается.";
        }

        return own
            ? eyes.Count == 1
                ? "У группы один глаз, и этого мало: соперник закроет его и зажмёт группу — ищите второй глаз."
                : "Группа ещё не живая: двух глаз у неё нет, и соперник может её зажать."
            : "Группа обречена: второй глаз ей построить не дадут, доводите до конца.";
    }

    /// <summary>Горизонт перебора жизни в полуходах.</summary>
    private const int LifeDepth = 6;

    /// <summary>Радиус окна перебора жизни.</summary>
    private const int LifeRadius = 3;

    /// <summary>Предел узлов перебора жизни: разбор считается мгновенно.</summary>
    private const int LifeNodes = 20000;

    /// <summary>Описание группы: цвет, число камней, где стоит и её точки.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="group">Камни группы.</param>
    /// <returns>Строку вида «чёрная группа из 5 камней на нижней стороне (C1, D1, E1)».</returns>
    private static string Group(Board board, IReadOnlyList<Point> group) =>
        $"{Name(board.At(group[0]))} группа из {StonesOf(group.Count)} {Where(group[0], board.Size)} ({Points(group, board.Size)})";

    /// <summary>Где стоит группа: угол, сторона или центр.</summary>
    /// <param name="point">Точка группы.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Словами.</returns>
    private static string Where(Point point, BoardSize size)
    {
        var region = MoveDescriber.RegionOf(point, size.Value);

        return region switch
        {
            "угол" => "в углу",
            "сторона" => "на стороне",
            _ => "в центре"
        };
    }

    /// <summary>Точки группы подписями вида «E5, F5, G5».</summary>
    /// <param name="group">Камни группы.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Подписи через запятую.</returns>
    private static string Points(IReadOnlyList<Point> group, BoardSize size) =>
        string.Join(", ", group.Select(point => BoardCoordinates.Label(point, size)));

    /// <summary>Что нужно сделать с группой по цели задачи.</summary>
    /// <param name="goal">Цель задачи.</param>
    /// <returns>Начало фразы цели.</returns>
    private static string Action(ProblemGoal goal) => goal switch
    {
        var value when value == ProblemGoal.Capture => "Снять",
        var value when value == ProblemGoal.Dead => "Убить",
        var value when value == ProblemGoal.Live => "Сделать живой",
        var value when value == ProblemGoal.Survive => "Спасти",
        var value when value == ProblemGoal.Eyes => "Построить два глаза",
        var value when value == ProblemGoal.Ko => "Начать ко-борьбу",
        _ => "Решить задачу"
    };

    /// <summary>Сколько камней цвета исчезло с доски между позициями.</summary>
    /// <param name="before">Позиция до хода.</param>
    /// <param name="after">Позиция после хода.</param>
    /// <param name="colour">Цвет исчезнувших камней.</param>
    /// <returns>Разница в числе камней; 0 — ничего не снято.</returns>
    private static int Removed(Board before, Board after, StoneColor colour)
    {
        var size = before.Size.Value;
        var lost = 0;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var point = new Point((byte)x, (byte)y);

                if (before.At(point) == colour && after.At(point) != colour)
                {
                    lost++;
                }
            }
        }

        return lost;
    }

    /// <summary>Название цвета для текста.</summary>
    /// <param name="colour">Цвет.</param>
    /// <returns>«чёрная» или «белая».</returns>
    private static string Name(StoneColor colour) => colour == StoneColor.Black ? "чёрная" : "белая";

    /// <summary>Прилагательное цвета в винительном падеже: «чёрную» или «белую».</summary>
    /// <param name="colour">Цвет группы.</param>
    /// <returns>Слово для строки об атари.</returns>
    private static string Accusative(StoneColor colour) => colour == StoneColor.Black ? "чёрную" : "белую";

    /// <summary>Название цвета в родительном падеже: «у чёрной группы».</summary>
    /// <param name="colour">Цвет.</param>
    /// <returns>«чёрной» или «белой».</returns>
    private static string NameOf(StoneColor colour) => colour == StoneColor.Black ? "чёрной" : "белой";

    /// <summary>Чья это группа для решающего: своя или соперника.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="colour">Цвет группы.</param>
    /// <returns><c>true</c>, если группа принадлежит решающему.</returns>
    private static bool IsOwn(Problem problem, StoneColor colour) => colour == problem.SolverColor;

    /// <summary>Слово о числе камней.</summary>
    /// <param name="stones">Число камней.</param>
    /// <returns>«1 камень», «2 камня», «5 камней».</returns>
    private static string Stones(int stones) => stones switch
    {
        1 => "1 камень",
        >= 2 and <= 4 => $"{stones} камня",
        _ => $"{stones} камней"
    };

    /// <summary>Число камней после слова «из»: «из 1 камня», «из 5 камней».</summary>
    /// <param name="stones">Число камней.</param>
    /// <returns>Слова в родительном падеже.</returns>
    private static string StonesOf(int stones) => stones == 1 ? "1 камня" : $"{stones} камней";

    /// <summary>Слово о числе дыханий.</summary>
    /// <param name="liberties">Число дыханий.</param>
    /// <returns>«1 дыхание», «3 дыхания», «6 дыханий».</returns>
    private static string Breaths(int liberties) => liberties switch
    {
        1 => "1 дыхание",
        >= 2 and <= 4 => $"{liberties} дыхания",
        _ => $"{liberties} дыханий"
    };
}
