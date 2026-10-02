using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Предложение мёртвых в любой позиции: перебор работает и в идущей партии.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-02: «определять мёртвую группу должна программа, а не человек» —
/// и не только после двух пасов, но и в игре, чтобы игрок видел перспективу партии. Поэтому
/// перебор <see cref="Endgame.ProposeDead(Board)"/> обязан находить доказанно мёртвые группы
/// в незавершённой партии и молчать там, где группы живы.
/// </remarks>
public sealed class DeadProposalTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Предложение_В_Идущей_Партии_Находит_Мёртвую_Группу()
    {
        // Партия идёт: два паса не сделаны, статус — «идёт». Угловая группа белых всё равно мертва.
        var game = DeadCornerGame();

        Assert.Equal([new Point(0, 0), new Point(1, 0)], Endgame.ProposeDead(game.Board));
    }

    [Fact]
    public void Предложение_В_Идущей_Партии_Не_Меняет_Статус()
    {
        var game = DeadCornerGame();

        _ = Endgame.ProposeDead(game.Board);

        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void Предложение_На_Живой_Позиции_Ничего_Не_Помечает()
    {
        // Регрессия ложного «мёртвая»: у обеих групп по три и больше дамэ, захват не доказан.
        // Перебор обязан промолчать, иначе живая группа ушла бы в пленные и испортила счёт молча.
        var game = LiveGame();

        Assert.Empty(Endgame.ProposeDead(game.Board));
    }

    [Fact]
    public void Предложение_Не_Помечает_Целевую_Группу_Задачи_На_Спасение()
    {
        // ts-001 — «спасти пять камней»: целевая группа живёт бегством, и объявлять её мёртвой
        // нельзя. Перебор помечает в этой позиции только жертвенные два камня в углу.
        var problem = ProblemLibrary.ById("ts-001")!;
        var board = ProblemSetup.Build(problem.Size, problem.Stones).Value!;

        var dead = Endgame.ProposeDead(board);

        Assert.DoesNotContain(dead, point => problem.TargetPoints.Contains(point));
    }

    [Fact]
    public void Предложение_Помечает_Группу_Задачи_Без_Спасения()
    {
        // Обратная сторона: группа, которую защита не спасает, обязана быть помечена — иначе
        // программа «не определяет мёртвых» вовсе и жалоба не закрыта.
        var problem = ProblemLibrary.ById("ts-010")!;
        var board = ProblemSetup.Build(problem.Size, problem.Stones).Value!;

        Assert.Contains(new Point(7, 6), Endgame.ProposeDead(board));
    }

    /// <summary>Строит идущую партию 9×9 с доказанно мёртвой белой группой в углу.</summary>
    /// <returns>Незавершённая партия: чёрные закрыли угол, внутри остались два белых камня.</returns>
    /// <remarks>
    /// Ходы те же, что в тестах конца партии, но без двух пасов: партия остаётся идущей,
    /// а позиция — той же, на которой перебор доказывает смерть группы.
    /// </remarks>
    private static GameState DeadCornerGame()
    {
        var game = GameState.NewGame(Size, Komi.For9x9);

        foreach (var move in new[]
        {
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(2, 1), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White),
            Move.Play(new Point(1, 2), StoneColor.Black),
            Move.Play(new Point(8, 7), StoneColor.White)
        })
        {
            _ = game.Play(move);
        }

        return game;
    }

    /// <summary>Строит идущую партию 9×9 без доказуемо мёртвых групп.</summary>
    /// <returns>Незавершённая партия: три чёрных камня в углу и два белых вдали, у всех по три дамэ.</returns>
    /// <remarks>
    /// Позиция взята у тестов разметки территории: угловая группа чёрных держит три дамэ, белая
    /// пара на краю — три, поэтому перебор обязан промолчать и разметка остаётся нетронутой.
    /// </remarks>
    private static GameState LiveGame()
    {
        var game = GameState.NewGame(Size, Komi.For9x9);

        foreach (var move in new[]
        {
            Move.Play(new Point(1, 0), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White),
            Move.Play(new Point(0, 1), StoneColor.Black),
            Move.Play(new Point(7, 8), StoneColor.White),
            Move.Play(new Point(1, 1), StoneColor.Black)
        })
        {
            _ = game.Play(move);
        }

        return game;
    }
}
