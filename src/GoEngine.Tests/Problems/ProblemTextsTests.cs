using System.Text.RegularExpressions;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Тексты задачи: точная цель, отклик на ход и разбор решения.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-11: «когда задача решена — необходимо дать понятное разъяснение,
/// почему задача решена так, и как это повлияет на следующие гипотетические ходы. Я всё сделал
/// с подсказками, но так и не понял суть задачи». Проверки идут по всем задачам библиотеки:
/// цель называет группу точками, разбор объясняет жизнь группы, отклик называет сыгранный ход.
/// </remarks>
public sealed class ProblemTextsTests
{
    /// <summary>Подпись точки в тексте: буква столбца (без «I») и номер строки.</summary>
    private static readonly Regex PointPattern = new(@"(?<![A-Za-zА-Яа-я])(?<point>[A-HJ-T]\d{1,2})(?![0-9])", RegexOptions.Compiled);

    [Fact]
    public void Цель_Называет_Группу_Точками()
    {
        List<string> errors = [];

        foreach (var problem in ProblemLibrary.All)
        {
            var session = new ProblemSession(problem);
            var goal = ProblemTexts.Goal(problem, session.StartBoard);

            if (Points(goal).Count == 0)
            {
                errors.Add($"{problem.Id}: в цели нет координат — «{goal}»");
            }

            if (!goal.Contains("дыхани", StringComparison.Ordinal))
            {
                errors.Add($"{problem.Id}: в цели не сказано о дыханиях — «{goal}»");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void Цель_Не_Выдаёт_Ответ()
    {
        // Условие задачи называет её предмет, а не решение: точка первого хода в цели — утечка.
        List<string> errors = [];

        foreach (var problem in ProblemLibrary.All)
        {
            var session = new ProblemSession(problem);
            var goal = ProblemTexts.Goal(problem, session.StartBoard);
            if (session.AcceptedMoves.Count == 0 || session.AcceptedMoves[0].Type != MoveType.Play)
            {
                continue;
            }

            var answer = session.AcceptedMoves[0];

            var label = BoardCoordinates.Label(answer.Point, problem.Size);

            if (Points(goal).Contains(label))
            {
                errors.Add($"{problem.Id}: цель называет точку решения {label} — «{goal}»");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void Отклик_Называет_Сыгранный_Ход()
    {
        List<string> errors = [];

        foreach (var problem in ProblemLibrary.All)
        {
            var session = new ProblemSession(problem);

            if (session.AcceptedMoves.Count == 0)
            {
                continue;
            }

            var move = session.AcceptedMoves[0];
            var before = session.Board;
            var label = BoardCoordinates.Label(move.Point, problem.Size);

            _ = session.Play(move.Point);

            var feedback = ProblemTexts.Feedback(problem, before, move, session.Board);

            if (!feedback.StartsWith(label, StringComparison.Ordinal))
            {
                errors.Add($"{problem.Id}: отклик не начинается с хода {label} — «{feedback}»");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void Отклик_Говорит_Чем_Кончилось_Снятие()
    {
        // Отклик обязан назвать снятие, а не только точку. Позиция строится прямо здесь:
        // в библиотеке задач ходы со снятием на первом шаге — редкость, а объяснение нужно всем.
        var problem = ProblemLibrary.All[0];
        var size = BoardSize.Size9;
        var board = new Board(size);

        // Белый камень B1 в атари: чёрные A1 и C1, ход чёрных B2 закрывает последнее дыхание.
        board = board.ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black));
        board = board.ApplyMove(Move.Play(new Point(1, 0), StoneColor.White));
        board = board.ApplyMove(Move.Play(new Point(2, 0), StoneColor.Black));
        board = board.ApplyMove(Move.Play(new Point(8, 8), StoneColor.White));

        var move = Move.Play(new Point(1, 1), StoneColor.Black);
        var after = board.ApplyMove(move);
        var feedback = ProblemTexts.Feedback(problem, board, move, after);

        Assert.Contains("снято 1 камень", feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void Разбор_Объясняет_Жизнь_Группы_Точками()
    {
        List<string> errors = [];

        foreach (var problem in ProblemLibrary.All)
        {
            var session = new ProblemSession(problem);
            var guard = 0;

            while (session.State != ProblemState.Solved && guard++ < 30 && session.AcceptedMoves.Count > 0)
            {
                _ = session.Play(session.AcceptedMoves[0].Point);
            }

            var text = ProblemTexts.Solution(problem, session.Board, session.SolverMoves);

            if (Points(text).Count == 0)
            {
                errors.Add($"{problem.Id}: в разборе нет координат — «{text}»");
            }

            if (!text.Contains("дыхани", StringComparison.Ordinal))
            {
                errors.Add($"{problem.Id}: разбор не говорит о дыханиях — «{text}»");
            }

            if (!text.Contains("глаз", StringComparison.Ordinal) && !text.Contains("снята", StringComparison.Ordinal))
            {
                errors.Add($"{problem.Id}: разбор не говорит ни о глазах, ни о снятии — «{text}»");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void Разбор_Снятой_Группы_Говорит_О_Снятии()
    {
        // Целевой группы на доске нет — разбор обязан сказать, что её сняли и что это даёт дальше.
        // Позиция берётся пустая: цель задачи в ней отсутствует, ветка разбора — про снятие.
        var problem = ProblemLibrary.All[0];
        var empty = new Board(problem.Size);

        var text = ProblemTexts.Solution(problem, empty, [Move.Play(new Point(4, 4), problem.SolverColor)]);

        Assert.Contains("снята", text, StringComparison.Ordinal);
        Assert.Contains("очки", text, StringComparison.Ordinal);
        Assert.Contains("ход", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Каждая_Задача_Получает_Все_Три_Текста()
    {
        List<string> errors = [];

        foreach (var problem in ProblemLibrary.All)
        {
            var session = new ProblemSession(problem);
            var goal = ProblemTexts.Goal(problem, session.StartBoard);

            if (goal.Length < 20)
            {
                errors.Add($"{problem.Id}: цель слишком короткая — «{goal}»");
            }

            if (!goal.Contains(problem.Name.Split(' ')[0], StringComparison.OrdinalIgnoreCase) && goal.Length < 40)
            {
                errors.Add($"{problem.Id}: цель не говорит о задаче — «{goal}»");
            }

            var solution = ProblemTexts.Solution(problem, session.StartBoard, []);

            if (solution.Length < 40)
            {
                errors.Add($"{problem.Id}: разбор слишком короткий — «{solution}»");
            }
        }

        Assert.Empty(errors);
    }

    /// <summary>Находит в тексте подписи точек.</summary>
    /// <param name="text">Текст.</param>
    /// <returns>Найденные подписи.</returns>
    private static IReadOnlyList<string> Points(string text) =>
        PointPattern.Matches(text).Select(match => match.Groups["point"].Value).ToList();
}
