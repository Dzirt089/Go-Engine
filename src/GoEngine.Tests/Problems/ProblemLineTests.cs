using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Линия решения задачи: каждая задача библиотеки проходится до конца и отвергает посторонний ход.</summary>
/// <remarks>
/// Проверка идёт по всей библиотеке, а не по выбранным задачам: набор растёт файлами
/// <c>Problems/*.sgf</c>, и новая задача обязана проверяться тем же тестом без правок.
/// Ходы берутся из дерева решения — из того же места, откуда их берёт интерфейс.
/// </remarks>
public sealed class ProblemLineTests
{
    /// <summary>Идентификаторы задач библиотеки: данные теории берутся из набора, а не из списка в тесте.</summary>
    /// <returns>Идентификаторы задач.</returns>
    public static TheoryData<string> LibraryIds()
    {
        TheoryData<string> data = [];

        foreach (var problem in ProblemLibrary.All)
        {
            data.Add(problem.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LibraryIds))]
    public void Задача_Решается_ПоСвоейЛинии(string id)
    {
        var problem = ProblemLibrary.ById(id);
        Assert.NotNull(problem);

        var session = new ProblemSession(problem);
        var node = problem.Solution;
        var steps = 0;

        while (!node.IsLeaf)
        {
            steps++;
            Assert.True(steps <= 16, $"{id}: линия длиннее шестнадцати полуходов — обход не сошёлся");

            var verdict = session.Play(node.Moves[0].Move);

            if (verdict == ProblemVerdict.Solved)
            {
                break;
            }

            Assert.Equal(ProblemVerdict.Correct, verdict);

            node = node.Moves[0].Next;
            node = node.IsLeaf ? node : node.Moves[0].Next;
        }

        Assert.Equal(ProblemState.Solved, session.State);
        Assert.True(session.SolverMoveCount > 0, $"{id}: задача «решена» без единого хода решающего");
    }

    [Theory]
    [MemberData(nameof(LibraryIds))]
    public void Посторонний_Ход_Не_Решает_Задачу(string id)
    {
        var problem = ProblemLibrary.ById(id);
        Assert.NotNull(problem);

        // Второго основания accept у задачи из источника нет: принимается ровно линия,
        // поэтому любой легальный ход вне набора принимаемых — «не решает задачу».
        Assert.Equal(ProblemGoal.Reference, problem.Goal);

        var session = new ProblemSession(problem);
        var before = Fingerprint(session.Board);
        var foreign = ForeignMove(session);
        var verdict = session.Play(foreign);

        Assert.Equal(ProblemVerdict.Wrong, verdict);
        Assert.Equal(ProblemState.InProgress, session.State);
        Assert.Equal(before, Fingerprint(session.Board));
        Assert.Equal(0, session.SolverMoveCount);
    }

    /// <summary>Ищет легальный ход в пустую точку, которой нет среди принимаемых.</summary>
    /// <param name="session">Сессия задачи.</param>
    /// <returns>Посторонний ход решающего.</returns>
    /// <remarks>
    /// Перебор идёт от начала доски: точка обязана быть пустой, легальной и не принимаемой —
    /// иначе вердикт был бы не про посторонний ход, а про правило или про дерево.
    /// </remarks>
    private static Move ForeignMove(ProblemSession session)
    {
        foreach (var point in session.Board.AllPoints())
        {
            if (!session.Board.IsEmpty(point))
            {
                continue;
            }

            var move = Move.Play(point, session.ToMove);

            if (!session.Board.IsLegal(move).IsSuccess)
            {
                continue;
            }

            var accepted = false;

            foreach (var candidate in session.AcceptedMoves)
            {
                accepted |= candidate.Point == point;
            }

            if (!accepted)
            {
                return move;
            }
        }

        throw new DomainException("В задаче нет постороннего легального хода: проверять нечего.");
    }

    private static string Fingerprint(Board board) =>
        string.Join('|', board.AllPoints().Select(point => $"{point.X},{point.Y}:{board.At(point).Id}"));
}
