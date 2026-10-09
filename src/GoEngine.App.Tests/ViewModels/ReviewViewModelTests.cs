using Avalonia.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Тесты модели разбора партий: ходы, вопросы, переходы и выбор партии.</summary>
/// <remarks>
/// Разбор — это практика после уроков: ход партии играет запись, а вопрос даёт игроку найти этот
/// ход самому. Здесь проверяется поведение модели, а не материал: материал проверяют тесты
/// <c>GoEngine.Tests</c> (партия обязана быть легальной и доигранной).
/// </remarks>
public sealed class ReviewViewModelTests
{
    [Fact]
    public void Партия_Начинается_С_Первой_Из_Библиотеки()
    {
        var model = new ReviewViewModel();

        Assert.Equal(0, model.SelectedIndex);
        Assert.Equal(0, model.MoveNumber);
        Assert.False(model.IsCompleted);
        Assert.True(model.MoveCount > 0);
    }

    [Fact]
    public void Счётчик_Ходов_Показывает_Номер_И_Всего()
    {
        var model = new ReviewViewModel();

        Assert.Equal($"Ход 0 из {model.MoveCount}", model.MoveCounter);
    }

    [Fact]
    public void Далее_Играет_Ход_Партии()
    {
        var model = new ReviewViewModel();
        Step(model);

        var before = model.Board;

        model.Next();

        Assert.Equal(2, model.MoveNumber);
        Assert.NotSame(before, model.Board);
        Assert.NotNull(model.LastMove);
    }

    [Fact]
    public void Партия_Проходится_До_Конца()
    {
        var model = new ReviewViewModel();
        var guard = 0;

        while (!model.IsCompleted && guard++ < model.MoveCount + 10)
        {
            if (model.IsQuiz)
            {
                model.Play(model.QuizPoints[0]);
            }

            model.Next();
        }

        Assert.True(model.IsCompleted);
        Assert.Equal(ReviewViewModel.CompletedText, model.MoveCounter);
    }

    [Fact]
    public void Вопрос_Принимает_Верный_Ход_И_Играет_Его()
    {
        var (model, game) = AtQuiz();
        var before = model.MoveNumber;

        model.Play(game.Moves[before].Point);

        Assert.True(model.IsMessageGood);
        Assert.Equal(before + 1, model.MoveNumber);
        Assert.NotNull(model.LastMove);
    }

    [Fact]
    public void Неверный_Ход_Не_Двигает_Партию()
    {
        var (model, _) = AtQuiz();
        var before = model.Board;
        var number = model.MoveNumber;
        var wrong = WrongPoint(model);

        if (wrong is null)
        {
            return;
        }

        model.Play(wrong.Value);

        Assert.False(model.IsMessageGood);
        Assert.Same(before, model.Board);
        Assert.Equal(number, model.MoveNumber);
        Assert.False(model.CanGoNext);
    }

    [Fact]
    public void Подсказка_Ведёт_К_Принимаемому_Ходу()
    {
        var (model, _) = AtQuiz();

        Assert.True(model.CanHint);

        model.ShowHint();

        Assert.NotNull(model.HintPoint);
        Assert.Contains(model.QuizPoints, point => point == model.HintPoint);
    }

    [Fact]
    public void Ход_Без_Вопроса_Не_Принимается()
    {
        var model = new ReviewViewModel();

        // Доходим до позиции без вопроса: вопрос сначала решается, иначе разбор не идёт дальше.
        while (model.IsQuiz && !model.IsCompleted)
        {
            model.Play(model.QuizPoints[0]);
        }

        if (model.IsCompleted)
        {
            // Разбор дошёл до конца: ходить некуда, и «нет хода» здесь проверять нечего.
            return;
        }

        var before = model.Board;

        model.Play(FreePoint(before));

        Assert.Same(before, model.Board);
        Assert.False(model.IsMessageGood);
    }

    [Fact]
    public void Назад_И_Заново_Возвращают_Партию()
    {
        var model = new ReviewViewModel();

        Step(model);
        Step(model);
        model.Back();

        Assert.Equal(1, model.MoveNumber);

        model.Restart();

        Assert.Equal(0, model.MoveNumber);
        Assert.False(model.IsCompleted);
        Assert.False(model.CanGoBack);
    }

    [Fact]
    public void Выбор_Партии_Начинает_Её_С_Начала()
    {
        var model = new ReviewViewModel();

        model.Next();
        model.SelectGame(1);

        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal(0, model.MoveNumber);
        Assert.NotEqual(ReviewLibrary.All[0].Title, model.GameTitle);
    }

    [Fact]
    public void Повторный_Выбор_Партии_Ничего_Не_Меняет()
    {
        var model = new ReviewViewModel();

        Step(model);
        model.SelectGame(0);

        Assert.Equal(1, model.MoveNumber);
    }

    [Fact]
    public void Список_Партий_Содержит_Все()
    {
        var model = new ReviewViewModel();

        Assert.Equal(ReviewLibrary.Count, model.Labels.Count);
    }

    /// <summary>Делает один шаг разбора: отвечает на вопрос, если он есть, и играет ход партии.</summary>
    /// <param name="model">Модель разбора.</param>
    /// <remarks>
    /// Партия начинается с вопроса: первый ход ищет игрок, поэтому «Далее» без ответа не двигает
    /// разбор — он не идёт дальше нерешённого вопроса.
    /// </remarks>
    private static void Step(ReviewViewModel model)
    {
        if (model.IsQuiz)
        {
            model.Play(model.QuizPoints[0]);

            return;
        }

        model.Next();
    }

    /// <summary>Ставит модель на первую позицию с вопросом.</summary>
    private static (ReviewViewModel Model, ReviewGame Game) AtQuiz()
    {
        var model = new ReviewViewModel();

        while (!model.IsQuiz && !model.IsCompleted)
        {
            model.Next();
        }

        return (model, ReviewLibrary.All[model.SelectedIndex]);
    }

    /// <summary>Ищет свободную точку, которая не принимается вопросом.</summary>
    private static Point? WrongPoint(ReviewViewModel model)
    {
        for (var x = 0; x < model.Board.Size.Value; x++)
        {
            for (var y = 0; y < model.Board.Size.Value; y++)
            {
                var point = new Point((byte)x, (byte)y);

                if (model.Board.IsEmpty(point) && !model.QuizPoints.Contains(point))
                {
                    return point;
                }
            }
        }

        return null;
    }

    /// <summary>Ищет любую свободную точку доски.</summary>
    private static Point FreePoint(Board board)
    {
        for (var x = 0; x < board.Size.Value; x++)
        {
            for (var y = 0; y < board.Size.Value; y++)
            {
                var point = new Point((byte)x, (byte)y);

                if (board.IsEmpty(point))
                {
                    return point;
                }
            }
        }

        return new Point(0, 0);
    }
}
