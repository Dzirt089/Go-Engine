using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Прохождение задачи: вердикты, подсказка, отмена и детерминизм.</summary>
/// <remarks>
/// Задачи с доказанной целью берутся из образцов (<see cref="EngineFixtures"/>), задачи по решению
/// источника — из библиотеки: у первых вердикт выносит движок, у вторых — линия источника.
/// </remarks>
public sealed class ProblemSessionTests
{
    [Fact]
    public void Верный_Ход_РешаетЗадачу()
    {
        var problem = EngineFixtures.Get("capture-in-corner");
        var session = new ProblemSession(problem);

        var verdict = session.Play(new Point(2, 0));

        Assert.Equal(ProblemVerdict.Solved, verdict);
        Assert.Equal(ProblemState.Solved, session.State);
        Assert.Equal(StoneColor.Empty, session.Board.At(new Point(0, 0)));
        Assert.Equal(StoneColor.Empty, session.Board.At(new Point(1, 0)));
        Assert.True(ProblemGoalCheck.IsAchieved(problem, session.Board));
    }

    [Fact]
    public void Неверный_Ход_НеМеняетПозицию()
    {
        var session = new ProblemSession(ProblemLibrary.ById("ts-001")!);
        var before = Fingerprint(session.Board);

        var verdict = session.Play(new Point(5, 1));

        Assert.Equal(ProblemVerdict.Wrong, verdict);
        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Equal(before, Fingerprint(session.Board));
        Assert.Equal(0, session.SolverMoveCount);
    }

    [Fact]
    public void Подсказка_СовпадаетСПринимаемымХодом()
    {
        var problem = EngineFixtures.Get("two-eyes");
        var session = new ProblemSession(problem);

        Assert.Equal(problem.Hint!.Value.Point, session.Hint!.Value.Point);
        Assert.Single(session.AcceptedMoves);
    }

    [Fact]
    public void Отмена_ВозвращаетНачальнуюПозицию()
    {
        // Одноходовая задача: отмена единственного хода возвращает начальную позицию.
        var problem = EngineFixtures.Get("two-eyes");
        var session = new ProblemSession(problem);
        var start = Fingerprint(session.Board);

        Assert.Equal(ProblemVerdict.Solved, session.Play(problem.AcceptedFirstMoves[0].Point));
        Assert.True(session.CanBack);
        Assert.True(session.Back());

        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Equal(start, Fingerprint(session.Board));
        Assert.False(session.CanBack);
    }

    [Fact]
    public void Отмена_Многоходовой_СнимаетХодИОтветСоперника()
    {
        // Отмена снимает ход решающего вместе с ответом соперника — так позиция не расходится
        // с деревом. После отмены остаётся предыдущий ход решающего, а не начало задачи.
        var problem = EngineFixtures.Get("capture-two-liberties");
        var session = new ProblemSession(problem);

        Solve(session, problem);

        Assert.Equal(ProblemState.Solved, session.State);
        Assert.Equal(2, session.SolverMoveCount);

        Assert.True(session.Back());

        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Equal(1, session.SolverMoveCount);
        Assert.False(session.Back() && session.SolverMoveCount == 1);
    }

    [Fact]
    public void Перезапуск_ВозвращаетНачальнуюПозицию()
    {
        var problem = EngineFixtures.Get("bent-four");
        var session = new ProblemSession(problem);
        var start = Fingerprint(session.Board);

        Solve(session, problem);

        Assert.Equal(ProblemState.Solved, session.State);

        session.Reset();

        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Equal(start, Fingerprint(session.Board));
        Assert.Equal(0, session.SolverMoveCount);
    }

    [Fact]
    public void Повтор_ДаётТотЖеРезультат()
    {
        var problem = EngineFixtures.Get("nakade");
        var first = new ProblemSession(problem);
        var second = new ProblemSession(problem);

        var firstVerdict = first.Play(new Point(5, 5));
        var secondVerdict = second.Play(new Point(5, 5));

        Assert.Equal(firstVerdict, secondVerdict);
        Assert.Equal(Fingerprint(first.Board), Fingerprint(second.Board));
    }

    [Fact]
    public void Очередь_Хода_ВсегдаУРешающего()
    {
        var problem = ProblemLibrary.ById("ts-008")!;
        var session = new ProblemSession(problem);
        var node = problem.Solution;

        Assert.Equal(problem.SolverColor, session.ToMove);

        while (!node.IsLeaf)
        {
            var verdict = session.Play(node.Moves[0].Move);

            if (verdict == ProblemVerdict.Solved)
            {
                break;
            }

            Assert.Equal(problem.SolverColor, session.ToMove);

            node = node.Moves[0].Next;
            node = node.IsLeaf ? node : node.Moves[0].Next;
        }

        Assert.Equal(ProblemState.Solved, session.State);
    }

    [Fact]
    public void РешениеИсточника_ХодВнеЛинии_НеМеняетПозицию()
    {
        var problem = ProblemLibrary.ById("ts-002")!;
        var session = new ProblemSession(problem);
        var before = Fingerprint(session.Board);

        // Первый ход линии известен, но берём заведомо постороннюю точку: у задачи по решению
        // источника второго основания accept нет, поэтому вердикт — «не решает задачу».
        var verdict = session.Play(new Point(7, 7));

        Assert.Equal(ProblemVerdict.Wrong, verdict);
        Assert.Equal(before, Fingerprint(session.Board));
    }

    /// <summary>Проходит линию задачи до конца.</summary>
    /// <param name="session">Сессия.</param>
    /// <param name="problem">Задача.</param>
    private static void Solve(ProblemSession session, Problem problem)
    {
        var node = problem.Solution;

        while (!node.IsLeaf)
        {
            var verdict = session.Play(node.Moves[0].Move);

            if (verdict == ProblemVerdict.Solved)
            {
                return;
            }

            Assert.Equal(ProblemVerdict.Correct, verdict);

            node = node.Moves[0].Next;
            node = node.IsLeaf ? node : node.Moves[0].Next;
        }
    }

    private static string Fingerprint(Board board)
    {
        var hash = 14695981039346656037UL;

        foreach (var point in board.AllPoints())
        {
            hash = (hash ^ (ulong)board.At(point).Id) * 1099511628211UL;
        }

        return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }
}
