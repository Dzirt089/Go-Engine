using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Тесты модели представления режима задач: вердикты, подсказка, отмена, переходы.</summary>
/// <remarks>
/// Проверки идут по встроенной библиотеке задач: режим должен работать с настоящими задачами,
/// а не с выдуманными в тесте. Верный ход берётся из принимаемых ходов текущей позиции — тот же
/// набор, на который опирается подсказка (правило согласованности, <c>PROBLEMS.md</c>).
/// </remarks>
public sealed class ProblemViewModelTests
{
    [Fact]
    public void Библиотека_Задач_Не_Пуста_И_Модель_Её_Показывает()
    {
        var model = new ProblemViewModel();

        Assert.NotEmpty(model.Problems);
        Assert.Equal(model.Problems.Count, model.Labels.Count);
        Assert.Equal(0, model.SelectedIndex);
        Assert.Equal(model.Problems[0].Id, model.Current.Id);
        Assert.Equal($"Задача 1 из {model.Problems.Count}", model.StatusText);
    }

    [Fact]
    public void Пустой_Список_Задач_Отвергается()
    {
        Assert.Throws<ArgumentException>(() => new ProblemViewModel([]));
    }

    [Fact]
    public void Промах_По_Занятой_Точке_Ничего_Не_Меняет()
    {
        var model = new ProblemViewModel();
        var occupied = model.Board.OccupiedPoints(StoneColor.Black).First();
        var before = Snapshot(model.Board);

        model.Play(occupied);

        Assert.Equal(before, Snapshot(model.Board));
        Assert.Equal(string.Empty, model.Verdict);
        Assert.Null(model.LastMove);
    }

    [Fact]
    public void Верный_Ход_Принимается_И_Задача_Доводится_До_Конца()
    {
        var model = new ProblemViewModel();
        var solved = false;

        for (var step = 0; step < 8 && !solved; step++)
        {
            Assert.True(model.CanHint, "У задачи нет принимаемого хода — библиотека повреждена.");

            model.ShowHint();
            Assert.StartsWith("Подсказка:", model.Verdict, StringComparison.Ordinal);

            var accepted = model.AcceptedMoves[0];
            model.Play(accepted.Point);

            solved = model.IsSolved;
            Assert.True(
                model.Verdict is ProblemViewModel.SolvedText or ProblemViewModel.CorrectText,
                $"Неожиданный вердикт после верного хода: {model.Verdict}");
        }

        Assert.True(solved, "Задача не доведена до конца по собственной подсказке.");
        Assert.Equal(ProblemViewModel.SolvedText, model.Verdict);
        Assert.False(model.CanHint);
    }

    [Fact]
    public void Неверный_Ход_Не_Меняет_Позицию_И_Объясняет_Себя()
    {
        var model = new ProblemViewModel();
        var before = Snapshot(model.Board);
        var far = FarEmptyPoint(model);

        model.Play(far);

        Assert.Equal(ProblemViewModel.WrongText, model.Verdict);
        Assert.Equal(before, Snapshot(model.Board));
        Assert.Null(model.LastMove);
        Assert.False(model.IsSolved);
    }

    [Fact]
    public void Подсказка_Называет_Первый_Принимаемый_Ход()
    {
        var model = new ProblemViewModel();
        var accepted = model.AcceptedMoves[0];

        Assert.True(model.CanHint);

        model.ShowHint();

        var label = BoardCoordinates.Label(accepted.Point, model.Current.Size);
        Assert.Equal($"Подсказка: {label}", model.Verdict);
    }

    [Fact]
    public void Отмена_Возвращает_Начальную_Позицию()
    {
        var model = new ProblemViewModel();
        var start = Snapshot(model.Board);

        model.Play(model.AcceptedMoves[0].Point);
        Assert.True(model.CanBack);

        model.Back();

        Assert.Equal(start, Snapshot(model.Board));
        Assert.False(model.CanBack);
        Assert.Equal(string.Empty, model.Verdict);
        Assert.Null(model.LastMove);
    }

    [Fact]
    public void Заново_Возвращает_Задачу_В_Начало()
    {
        var model = new ProblemViewModel();
        var start = Snapshot(model.Board);

        model.Play(model.AcceptedMoves[0].Point);
        model.ShowHint();

        model.Reset();

        Assert.Equal(start, Snapshot(model.Board));
        Assert.Equal(string.Empty, model.Verdict);
        Assert.False(model.CanBack);
        Assert.False(model.IsSolved);
    }

    [Fact]
    public void Переходы_По_Задачам_Меняют_Позицию_И_Доступность_Кнопок()
    {
        var model = new ProblemViewModel();

        Assert.False(model.CanGoPrevious);
        Assert.True(model.CanGoNext);

        var first = Snapshot(model.Board);

        model.Next();

        Assert.Equal(1, model.SelectedIndex);
        Assert.True(model.CanGoPrevious);
        Assert.NotEqual(model.Problems[0].Id, model.Current.Id);
        Assert.NotEqual(first, Snapshot(model.Board));

        model.Previous();

        Assert.Equal(0, model.SelectedIndex);
        Assert.False(model.CanGoPrevious);
        Assert.Equal(first, Snapshot(model.Board));

        model.Select(model.Problems.Count - 1);

        Assert.False(model.CanGoNext);
        Assert.Equal(model.Problems.Count - 1, model.SelectedIndex);
    }

    [Fact]
    public void Выбор_Задачи_В_Списке_Начинает_Её_Сначала()
    {
        var model = new ProblemViewModel();

        model.Play(model.AcceptedMoves[0].Point);
        Assert.True(model.CanBack);

        model.SelectedIndex = 2;

        Assert.Equal(2, model.SelectedIndex);
        Assert.False(model.CanBack);
        Assert.Equal(string.Empty, model.Verdict);
        Assert.Equal(model.Problems[2].Id, model.Current.Id);
    }

    [Fact]
    public void Смена_Задачи_Сообщает_Об_Изменениях()
    {
        var model = new ProblemViewModel();
        List<string> changed = [];

        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        model.Next();

        Assert.Contains(nameof(ProblemViewModel.Title), changed);
        Assert.Contains(nameof(ProblemViewModel.Board), changed);
        Assert.Contains(nameof(ProblemViewModel.SelectedIndex), changed);
        Assert.Contains(nameof(ProblemViewModel.StatusText), changed);
    }

    /// <summary>Возвращает самый дальний от цели пустой ход: он заведомо не решает задачу.</summary>
    /// <param name="model">Модель задачи.</param>
    /// <returns>Точка хода.</returns>
    private static Point FarEmptyPoint(ProblemViewModel model)
    {
        var target = model.Current.Target;
        var best = default(Point);
        var bestDistance = -1;

        foreach (var point in model.Board.EmptyPoints())
        {
            var distance = Math.Abs(point.X - target.X) + Math.Abs(point.Y - target.Y);

            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }

        return best;
    }

    /// <summary>Снимок позиции: камни по всем точкам — сравнение «до и после».</summary>
    /// <param name="board">Доска.</param>
    /// <returns>Строка состояния доски.</returns>
    private static string Snapshot(Board board) =>
        string.Join('|', board.AllPoints().Select(point => $"{point.X},{point.Y}:{board.At(point).Name}"));
}
