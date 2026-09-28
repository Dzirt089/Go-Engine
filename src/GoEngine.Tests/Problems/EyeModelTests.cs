using GoEngine.AI;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Модель жизни «два глаза за горизонт»: критерий глаза и его границы.</summary>
/// <remarks>
/// Критерий глаза здесь строгий по границе: пустая область считается глазом, когда <b>все</b>
/// соседние с ней камни — цвета группы. Край доски границу завершает. Критерий шире
/// <see cref="EyeDetector"/> из слоя AI (тот не считает глазом дамэ с пустыми диагоналями),
/// и у него есть честная граница: ложный глаз редких форм может сойти за настоящий — тест
/// <see cref="Ложный_Глаз_Редких_Форм_Критерий_Принимает"/> фиксирует это как известное свойство.
/// </remarks>
public sealed class EyeModelTests
{
    [Fact]
    public void Глаз_По_Границе_Считается_Глазом()
    {
        // Угловое дамэ: соседи — свои камни, край замыкает границу. Диагональ (1,1) пуста,
        // и именно такие глаза EyeDetector из слоя AI настоящими не считает.
        var board = Build(
        [
            new(new Point(1, 0), StoneColor.Black),
            new(new Point(0, 1), StoneColor.Black),
        ]);

        Assert.True(ProblemLife.IsEye(board, new Point(0, 0), StoneColor.Black));
    }

    [Fact]
    public void Чужой_Камень_На_Границе_Глазом_Не_Делает()
    {
        var board = Build(
        [
            new(new Point(1, 0), StoneColor.Black),
            new(new Point(0, 1), StoneColor.White),
        ]);

        Assert.False(ProblemLife.IsEye(board, new Point(0, 0), StoneColor.Black));
    }

    [Fact]
    public void Два_Глаза_Сразу_Дают_Жизнь()
    {
        var board = TwoEyesBoard();
        var group = Stones(board);

        Assert.Equal(2, ProblemLife.CountEyes(board, StoneColor.Black, group));

        var life = ProblemLife.CanForceTwoEyes(board, StoneColor.Black, group, depth: 3, radius: 2);

        Assert.True(life.Forced);
        Assert.False(life.LimitReached);
    }

    [Fact]
    public void Один_Глаз_Жизнью_Не_Считается()
    {
        var board = Build(
        [
            new(new Point(0, 0), StoneColor.Black),
            new(new Point(2, 0), StoneColor.Black),
            new(new Point(0, 1), StoneColor.Black),
            new(new Point(1, 1), StoneColor.Black),
            new(new Point(2, 1), StoneColor.Black),
        ]);

        Assert.Equal(1, ProblemLife.CountEyes(board, StoneColor.Black, Stones(board)));
    }

    [Fact]
    public void Ложный_Глаз_Критерий_Не_Принимает()
    {
        // Камень (1,0) на границе глаза стоит в атари: белые входят в точку (1,1) и снимают его,
        // значит вход законен, а глаз ложный. Критерий это видит — иначе задача считалась бы решённой
        // по ложной жизни.
        var board = Build(
        [
            new(new Point(1, 0), StoneColor.Black),
            new(new Point(0, 1), StoneColor.Black),
            new(new Point(2, 0), StoneColor.White),
            new(new Point(1, 1), StoneColor.White),
        ]);

        // Угловое дамэ (0,0) занято быть глазом не может: у чёрного камня (1,0) единственное дамэ —
        // это же (0,0), и белые, войдя туда, снимают камень.
        Assert.False(ProblemLife.IsEye(board, new Point(0, 0), StoneColor.Black));
    }

    [Fact]
    public void Большое_Глазное_Пространство_Жизнью_Не_Признаётся()
    {
        // Граница критерия в другую сторону: пространство из нескольких точек, куда соперник может
        // войти, глазом не считается, хотя по правилам такое пространство часто уже живо.
        // Критерий намеренно осторожен: ложная жизнь хуже неподтверждённой.
        var board = Build(
        [
            new(new Point(2, 0), StoneColor.Black),
            new(new Point(2, 1), StoneColor.Black),
            new(new Point(2, 2), StoneColor.Black),
            new(new Point(1, 2), StoneColor.Black),
            new(new Point(0, 2), StoneColor.Black),
        ]);

        Assert.False(ProblemLife.IsEye(board, new Point(1, 1), StoneColor.Black));
        Assert.Equal(0, ProblemLife.CountEyes(board, StoneColor.Black, Stones(board)));
    }

    [Fact]
    public void Упор_В_Предел_Узлов_Не_Даёт_Приговора()
    {
        // Одинокий камень: двух глаз нет и быстрым ходом не появится, поэтому перебор обязан
        // упереться в предел и не выносить приговор.
        var board = Build([new(new Point(4, 4), StoneColor.Black)]);
        var group = Stones(board);

        var life = ProblemLife.CanForceTwoEyes(board, StoneColor.Black, group, depth: 9, radius: 3, maxNodes: 1);

        Assert.False(life.Forced);
        Assert.True(life.LimitReached);
    }

    [Fact]
    public void Спасение_Группы_С_Дамэ_Подтверждается()
    {
        // Одинокий камень с четырьмя дамэ: за три полухода снять его нельзя.
        var board = Build([new(new Point(4, 4), StoneColor.Black)]);
        var survive = ProblemLife.CanSurvive(board, StoneColor.Black, [new Point(4, 4)], depth: 3, radius: 2);

        Assert.True(survive.Forced);
        Assert.False(survive.LimitReached);
    }

    [Fact]
    public void Захват_Группы_В_Атари_Виден()
    {
        // Регрессия: в первой версии перебора кванторы сторон были перепутаны, и атакующий требовал
        // удачи от всех своих ходов сразу — «захват не форсирован» выходило на любой позиции.
        var board = Build(
        [
            new(new Point(4, 4), StoneColor.Black),
            new(new Point(3, 4), StoneColor.White),
            new(new Point(5, 4), StoneColor.White),
            new(new Point(4, 3), StoneColor.White),
        ]);

        var survive = ProblemLife.CanSurvive(board, StoneColor.Black, [new Point(4, 4)], depth: 3, radius: 2);

        Assert.False(survive.Forced);
        Assert.False(survive.LimitReached);
    }

    [Fact]
    public void Цель_ДваГлаза_Считается_Перебором_По_Группе()
    {
        // Цель Eyes считается перебором, а не детектором AI. Проверяется на полной группе: так
        // критерий не зависит от того, какие камни попали в Problem.TargetPoints синтетической задачи
        // (это отдельный вопрос к конструктору задачи, а не к модели глаз).
        var board = TwoEyesBoard();
        var group = Stones(board);

        Assert.True(ProblemLife.CanForceTwoEyes(board, StoneColor.Black, group, depth: 3, radius: 2).Forced);

        var single = OneEyeBoard();

        Assert.Equal(1, ProblemLife.CountEyes(single, StoneColor.Black, Stones(single)));
    }

    /// <summary>Доска с двумя настоящими глазами: две пустые точки, окружённые своими камнями.</summary>
    /// <returns>Доска с двумя глазами.</returns>
    /// <remarks>Цепочка связная: камни соединены по нижнему ряду, поэтому жизнь — у одной группы.</remarks>
    private static Board TwoEyesBoard() =>
        Build(
        [
            new(new Point(0, 0), StoneColor.Black),
            new(new Point(2, 0), StoneColor.Black),
            new(new Point(3, 0), StoneColor.Black),
            new(new Point(5, 0), StoneColor.Black),
            new(new Point(0, 1), StoneColor.Black),
            new(new Point(1, 1), StoneColor.Black),
            new(new Point(2, 1), StoneColor.Black),
            new(new Point(3, 1), StoneColor.Black),
            new(new Point(4, 1), StoneColor.Black),
            new(new Point(5, 1), StoneColor.Black),
        ]);

    /// <summary>Доска с одним глазом.</summary>
    /// <returns>Доска с одним глазом.</returns>
    private static Board OneEyeBoard() =>
        Build(
        [
            new(new Point(0, 0), StoneColor.Black),
            new(new Point(2, 0), StoneColor.Black),
            new(new Point(0, 1), StoneColor.Black),
            new(new Point(1, 1), StoneColor.Black),
            new(new Point(2, 1), StoneColor.Black),
        ]);

    private static List<Point> Stones(Board board)
    {
        List<Point> stones = [];

        foreach (var point in board.AllPoints())
        {
            if (board.At(point) == StoneColor.Black)
            {
                stones.Add(point);
            }
        }

        return stones;
    }

    private static Board Build(IReadOnlyList<ProblemStone> stones)
    {
        var built = ProblemSetup.Build(BoardSize.Size9, stones);

        Assert.True(built.IsSuccess, built.Error);

        return built.Value!;
    }

    private static Problem ProblemFor(Board board, ProblemGoal goal)
    {
        List<ProblemStone> stones = [];

        foreach (var point in board.AllPoints())
        {
            if (board.At(point) != StoneColor.Empty)
            {
                stones.Add(new ProblemStone(point, board.At(point)));
            }
        }

        var move = board.AllPoints().Select(p => Move.Play(p, StoneColor.White)).First(m => board.IsLegal(m).IsSuccess);

        return new Problem(
            "eyes-test",
            "глаза-тест",
            BoardSize.Size9,
            new Komi(5.5),
            StoneColor.Black,
            goal,
            stones,
            new Point(1, 1),
            20,
            "проверка модели глаз",
            ProblemNode.One(move, ProblemNode.Leaf),
            checkDepth: 3,
            windowRadius: 2);
    }
}
