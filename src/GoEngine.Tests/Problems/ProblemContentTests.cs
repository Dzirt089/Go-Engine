using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Содержание исправленных задач: финал линии подтверждает заявленную цель.</summary>
/// <remarks>
/// Ревизия содержания 2026-10-17 нашла задачи, где линия играла не у целевой группы, а название
/// обещало захват или жизнь, которых в позиции нет. Цель исправлена по финалу линии, и эти
/// проверки держат соответствие: число дыханий, глаза и соединение считаются по позиции, а не
/// берутся из текста. Ошибка в линии или в названии снова роняет тест.
/// </remarks>
public sealed class ProblemContentTests
{
    /// <summary>Ожидаемый финал критических задач: название, дыхания и глаза целевой группы.</summary>
    private static readonly (string Id, string NamePart, int Liberties, int Eyes)[] Fixed =
    [
        ("ts-001", "атари", 1, 0),
        ("ts-004", "Зажать", 2, 0),
        ("ts-010", "атари", 2, 0),
        ("ts-012", "атари", 1, 0),
        ("ts-015", "Зажать", 2, 0),
        ("ts-018", "Спасти", 3, 0),
        ("ts-026", "Разрушить", 5, 1),
        ("ts-027", "Зажать", 2, 0),
        ("ts-030", "Обеспечить", 7, 0),
        ("ts-031", "ко", 2, 2),
        ("ts-033", "Обеспечить", 8, 0),
    ];

    [Fact]
    public void КритическиеЗадачи_ФиналЛинииСовпадаетСНазванием()
    {
        List<string> errors = [];

        foreach (var (id, namePart, liberties, eyes) in Fixed)
        {
            var problem = ProblemLibrary.ById(id);
            Assert.NotNull(problem);

            var group = FinalGroup(problem!);

            if (!problem!.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{id}: в названии нет «{namePart}» — «{problem.Name}»");
            }

            if (group.Liberties.Count != liberties)
            {
                errors.Add($"{id}: у целевой группы {group.Liberties.Count} дыханий, а названо {liberties}");
            }

            var count = ProblemLife.CountEyes(group.Position, group.Color, group.Stones);

            if (count != eyes)
            {
                errors.Add($"{id}: у целевой группы {count} глаз, а названо {eyes}");
            }
        }

        Assert.Empty(errors);
    }

    /// <summary>Соединение целевой группы с точкой, которую ставит исправленная линия.</summary>
    private static readonly (string Id, byte X, byte Y)[] Connected =
    [
        ("ts-030", 4, 11),
        ("ts-033", 4, 7),
    ];

    [Fact]
    public void ИсправленнаяЛиния_СоединяетЦелевуюГруппу()
    {
        foreach (var (id, x, y) in Connected)
        {
            var problem = ProblemLibrary.ById(id);
            Assert.NotNull(problem);

            var group = FinalGroup(problem!);

            Assert.Contains(new Point(x, y), group.Stones);
        }
    }

    [Fact]
    public void Отклик_ПишетДваКамня()
    {
        var problem = ProblemLibrary.All[0];
        var board = new Board(BoardSize.Size9);
        board = board.ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black));
        board = board.ApplyMove(Move.Play(new Point(1, 0), StoneColor.White));
        board = board.ApplyMove(Move.Play(new Point(3, 0), StoneColor.Black));
        board = board.ApplyMove(Move.Play(new Point(2, 0), StoneColor.White));
        board = board.ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black));
        board = board.ApplyMove(Move.Play(new Point(8, 8), StoneColor.White));

        // C2 закрывает последнее дыхание пары B1–C1: снимаются два белых камня.
        var move = Move.Play(new Point(2, 1), StoneColor.Black);
        var after = board.ApplyMove(move);
        var feedback = ProblemTexts.Feedback(problem, board, move, after);

        Assert.Contains("снято 2 камня", feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void Разбор_ЗадачиИсточника_НеОбещаетНеубиваемость()
    {
        var problem = ProblemLibrary.ById("ts-001")!;
        var session = new ProblemSession(problem);
        var guard = 0;

        while (session.State != ProblemState.Solved && guard++ < 20 && session.AcceptedMoves.Count > 0)
        {
            _ = session.Play(session.AcceptedMoves[0].Point);
        }

        var text = ProblemTexts.Solution(problem, session.Board, session.SolverMoves, session.Replies);

        Assert.DoesNotContain("убить её нельзя", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ВсеЗадачи_ОбъявляютОкноПоиска()
    {
        Assert.All(
            ProblemLibrary.All,
            problem => Assert.Equal(Problem.DefaultWindowRadius, problem.WindowRadius));
    }

    [Fact]
    public void Урок_Тоби_НеНазываетПрыжокСоединением()
    {
        var lesson = LessonLibrary.ById("go-10")!;
        var step = lesson.Steps.First(candidate => candidate.Title.Contains("Тоби", StringComparison.Ordinal));

        Assert.DoesNotContain("связь сохранится", step.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ИтоговаяЗаметка_Партии02_СовпадаетСРезультатом()
    {
        var game = ReviewLibrary.ById("game-02")!;
        var note = game.NoteBefore(80);

        Assert.NotNull(note);
        Assert.Contains("+14.5", note!.Title, StringComparison.Ordinal);
    }

    /// <summary>Проходит линию задачи и возвращает целевую группу в финале.</summary>
    /// <param name="problem">Задача.</param>
    /// <returns>Группа в финальной позиции.</returns>
    private static FinalTargetGroup FinalGroup(Problem problem)
    {
        var session = new ProblemSession(problem);
        var guard = 0;

        while (session.State != ProblemState.Solved && guard++ < 20 && session.AcceptedMoves.Count > 0)
        {
            _ = session.Play(session.AcceptedMoves[0].Point);
        }

        var group = GroupTracker.FindGroup(session.Board, problem.Target);

        return new FinalTargetGroup(session.Board, group.Color, group.Stones, group.Liberties);
    }

    /// <summary>Финальная группа цели: доска, цвет, камни и дыхания.</summary>
    private sealed record FinalTargetGroup(
        Board Position,
        StoneColor Color,
        IReadOnlyList<Point> Stones,
        IReadOnlySet<Point> Liberties);
}
