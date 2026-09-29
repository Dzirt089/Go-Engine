using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Прохождение задач в модели представления: вердикты, подсказка на доске и честные пометки.</summary>
/// <remarks>
/// Проверяется весь набор задач библиотеки, а не одна выбранная: набор растёт файлами
/// <c>Problems/*.sgf</c>, и новая задача обязана работать без правок кода. Подсказка проверяется
/// вместе с точкой на доске (<see cref="ProblemViewModel.HintPoint"/>): игрок должен видеть ход,
/// а не искать пересечение по названию координаты.
/// </remarks>
public sealed class ProblemFlowTests
{
    [Fact]
    public void Каждая_Задача_Решается_ПоПодсказке_И_Отвергает_Посторонний_Ход()
    {
        var model = new ProblemViewModel();

        for (var index = 0; index < model.Problems.Count; index++)
        {
            model.Select(index);
            var id = model.Current.Id;

            Assert.Equal(string.Empty, model.Verdict);
            Assert.Null(model.HintPoint);

            // Посторонний ход: пустая легальная точка, которой нет среди принимаемых.
            var foreign = ForeignMove(model);
            model.Play(foreign);

            Assert.Equal(ProblemViewModel.WrongText, model.Verdict);
            Assert.False(model.IsSolved);

            var steps = 0;

            while (!model.IsSolved && steps < 16)
            {
                steps++;

                Assert.True(model.CanHint, $"{id}: задача не решена, а принимаемых ходов нет");
                model.ShowHint();

                Assert.NotNull(model.HintPoint);
                Assert.StartsWith("Подсказка:", model.Verdict, StringComparison.Ordinal);

                model.Play(model.HintPoint!.Value);
            }

            Assert.True(model.IsSolved, $"{id}: задача не решена по собственной подсказке");
            Assert.Equal(ProblemViewModel.SolvedText, model.Verdict);
            Assert.Null(model.HintPoint);
            Assert.False(model.CanHint);
        }
    }

    [Fact]
    public void Пометка_Об_Источнике_Меняется_Вместе_С_Задачей()
    {
        // Регрессия: панель показывала источник первой задачи после перехода ко второй —
        // SourceText не попадал в список изменившихся свойств, и надпись застывала.
        var model = new ProblemViewModel();

        for (var index = 0; index < model.Problems.Count; index++)
        {
            model.Select(index);

            if (model.Current.Goal != ProblemGoal.Reference)
            {
                Assert.False(model.HasSource);
                continue;
            }

            Assert.True(model.HasSource, $"{model.Current.Id}: нет пометки о происхождении решения");
            Assert.Contains(model.Current.Source, model.SourceText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Переход_К_Задаче_Сообщает_ОбИзменении_Пометки_И_Подсказки()
    {
        var model = new ProblemViewModel();
        List<string> changed = [];

        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        model.Next();

        Assert.Contains(nameof(ProblemViewModel.SourceText), changed);
        Assert.Contains(nameof(ProblemViewModel.HintPoint), changed);
        Assert.Contains(nameof(ProblemViewModel.Description), changed);
    }

    [Fact]
    public void Условие_Задачи_Показывается_Игроку()
    {
        var model = new ProblemViewModel();

        Assert.True(model.HasDescription, "у задачи нет условия для игрока");
        Assert.False(string.IsNullOrWhiteSpace(model.Description));

        model.Next();

        Assert.Equal(model.Current.Description, model.Description);
    }

    [Fact]
    public void Подсказка_Ставит_Точку_На_Доске_И_Снимается_Ходом_Отменой_И_Сбросом()
    {
        var model = new ProblemViewModel([ProblemLibrary.ById("ts-001")!, ProblemLibrary.ById("ts-002")!]);
        var hint = model.AcceptedMoves[0].Point;

        Assert.Null(model.HintPoint);

        model.ShowHint();
        Assert.Equal(hint, model.HintPoint);

        model.Play(hint);
        Assert.Null(model.HintPoint);

        model.ShowHint();
        Assert.NotNull(model.HintPoint);

        model.Back();
        Assert.Null(model.HintPoint);

        model.ShowHint();
        Assert.NotNull(model.HintPoint);

        model.Reset();
        Assert.Null(model.HintPoint);

        model.ShowHint();
        Assert.NotNull(model.HintPoint);

        model.Next();
        Assert.Null(model.HintPoint);
    }

    [Fact]
    public void Промах_По_Занятой_Точке_Подсказку_Не_Снимает()
    {
        var model = new ProblemViewModel([ProblemLibrary.ById("ts-001")!]);
        var occupied = model.Board.OccupiedPoints(StoneColor.Black).First();

        model.ShowHint();
        var hint = model.HintPoint;
        var verdict = model.Verdict;

        Assert.NotNull(hint);

        model.Play(occupied);

        Assert.Equal(hint, model.HintPoint);
        Assert.Equal(verdict, model.Verdict);
    }

    [Fact]
    public void После_Решения_Доска_Ходов_Не_Принимает()
    {
        // Решённая задача — не поле для ходов: щелчок по пустой точке раньше «ставил» метку
        // последнего хода на произвольное пересечение, и это выглядело как сделанный ход.
        var model = new ProblemViewModel([ProblemLibrary.ById("ts-001")!]);
        var steps = 0;

        while (!model.IsSolved && steps < 16)
        {
            steps++;
            model.Play(model.AcceptedMoves[0].Point);
        }

        Assert.True(model.IsSolved);

        var board = Snapshot(model.Board);
        var lastMove = model.LastMove;
        var free = model.Board.EmptyPoints().First();

        model.Play(free);

        Assert.Equal(ProblemViewModel.SolvedText, model.Verdict);
        Assert.Equal(board, Snapshot(model.Board));
        Assert.Equal(lastMove, model.LastMove);
    }

    /// <summary>Ищет легальный ход в пустую точку, которой нет среди принимаемых.</summary>
    /// <param name="model">Модель задачи.</param>
    /// <returns>Точка постороннего хода.</returns>
    private static Point ForeignMove(ProblemViewModel model)
    {
        var accepted = model.AcceptedMoves;

        foreach (var point in model.Board.AllPoints())
        {
            if (!model.Board.IsEmpty(point))
            {
                continue;
            }

            if (!model.Board.IsLegal(Move.Play(point, model.Current.SolverColor)).IsSuccess)
            {
                continue;
            }

            if (accepted.All(move => move.Point != point))
            {
                return point;
            }
        }

        throw new InvalidOperationException("В задаче нет постороннего легального хода: проверять нечего.");
    }

    /// <summary>Снимок позиции: камни по всем точкам — сравнение «до и после».</summary>
    /// <param name="board">Доска.</param>
    /// <returns>Строка состояния доски.</returns>
    private static string Snapshot(Board board) =>
        string.Join('|', board.AllPoints().Select(point => $"{point.X},{point.Y}:{board.At(point).Id}"));
}
