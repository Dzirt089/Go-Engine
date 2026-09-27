using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Прохождение задачи: вердикты, подсказка, отмена и детерминизм.</summary>
public sealed class ProblemSessionTests
{
    [Fact]
    public void Верный_Ход_РешаетЗадачу()
    {
        var problem = ProblemLibrary.ById("cg-001")!;
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
        var session = new ProblemSession(ProblemLibrary.ById("cg-001")!);
        var before = Fingerprint(session.Board);

        var verdict = session.Play(new Point(5, 5));

        Assert.Equal(ProblemVerdict.Wrong, verdict);
        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Equal(before, Fingerprint(session.Board));
        Assert.Equal(0, session.SolverMoveCount);
    }

    [Fact]
    public void Подсказка_СовпадаетСПринимаемымХодом()
    {
        var problem = ProblemLibrary.ById("lf-001")!;
        var session = new ProblemSession(problem);

        Assert.Equal(problem.Hint!.Value.Point, session.Hint!.Value.Point);
        Assert.Single(session.AcceptedMoves);
    }

    [Fact]
    public void Отмена_ВозвращаетНачальнуюПозицию()
    {
        var session = new ProblemSession(ProblemLibrary.ById("cg-002")!);
        var start = Fingerprint(session.Board);

        Assert.Equal(ProblemVerdict.Solved, session.Play(new Point(6, 1)));
        Assert.True(session.CanBack);
        Assert.True(session.Back());

        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Equal(start, Fingerprint(session.Board));
        Assert.False(session.CanBack);
    }

    [Fact]
    public void Перезапуск_ВозвращаетНачальнуюПозицию()
    {
        var session = new ProblemSession(ProblemLibrary.ById("lf-002")!);
        var start = Fingerprint(session.Board);

        Assert.Equal(ProblemVerdict.Solved, session.Play(new Point(1, 0)));

        session.Reset();

        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Equal(start, Fingerprint(session.Board));
        Assert.Equal(0, session.SolverMoveCount);
    }

    [Fact]
    public void Повтор_ДаётТотЖеРезультат()
    {
        var problem = ProblemLibrary.ById("cg-003")!;
        var first = new ProblemSession(problem);
        var second = new ProblemSession(problem);

        var firstVerdict = first.Play(new Point(5, 5));
        var secondVerdict = second.Play(new Point(5, 5));

        Assert.Equal(firstVerdict, secondVerdict);
        Assert.Equal(Fingerprint(first.Board), Fingerprint(second.Board));
    }

    [Fact]
    public void Ход_ЗаСоперника_Отклоняется()
    {
        var session = new ProblemSession(ProblemLibrary.ById("cg-001")!);
        var before = Fingerprint(session.Board);

        // Цвет хода берётся из очереди: играть за белых в задаче нельзя.
        var verdict = session.Play(new Point(2, 0));

        Assert.Equal(ProblemVerdict.Solved, verdict);
        Assert.NotEqual(before, Fingerprint(session.Board));
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
