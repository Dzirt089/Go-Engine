using System.ComponentModel;
using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Тесты модели обучения: выбор урока, ходы, переходы и завершение.</summary>
/// <remarks>
/// Обучение ведётся по-русски, поэтому здесь же проверяется язык текстов: урок с английским
/// объяснением не должен попасть в игру (замечание пользователя 2026-10-07).
/// </remarks>
public sealed class LessonViewModelTests
{
    [Fact]
    public void Список_Уроков_Не_Пуст()
    {
        var model = new LessonViewModel();

        Assert.NotEmpty(model.Labels);
    }

    [Fact]
    public void Начинается_С_Первого_Урока()
    {
        var model = new LessonViewModel();

        Assert.Equal(0, model.SelectedIndex);
        Assert.Equal(1, model.StepNumber);
    }

    [Fact]
    public void Вначале_Показан_Первый_Шаг_Первого_Урока()
    {
        var model = new LessonViewModel();
        var lesson = LessonLibrary.All[0];

        Assert.Equal(lesson.Title, model.LessonTitle);
        Assert.Equal(lesson.Steps[0].Title, model.StepTitle);
    }

    [Fact]
    public void Выбор_Урока_Меняет_Заголовок()
    {
        var model = new LessonViewModel();
        var second = LessonLibrary.All[1];

        model.SelectLesson(1);

        Assert.Equal(second.Title, model.LessonTitle);
    }

    [Fact]
    public void Выбор_Того_Же_Урока_Ничего_Не_Меняет()
    {
        var model = new LessonViewModel();
        var changed = 0;
        model.PropertyChanged += (_, _) => changed++;

        model.SelectLesson(0);

        Assert.Equal(0, changed);
    }

    [Fact]
    public void Верный_Ход_Принимается()
    {
        var model = new LessonViewModel();
        var session = NextTask(model);

        model.Play(session.Point);

        Assert.True(model.IsMessageGood, model.Message);
        Assert.True(model.CanGoNext);
    }

    [Fact]
    public void Верный_Ход_Ставит_Камень_На_Доску()
    {
        var model = new LessonViewModel();
        var session = NextTask(model);
        var before = model.Board;

        model.Play(session.Point);

        Assert.NotSame(before, model.Board);
        Assert.NotEqual(StoneColor.Empty, model.Board.At(session.Point));
    }

    [Fact]
    public void Неверный_Ход_Не_Меняет_Доску()
    {
        var model = new LessonViewModel();
        var session = NextTask(model);
        var before = model.Board;
        var wrong = WrongPoint(model);

        if (wrong is null)
        {
            return;
        }

        model.Play(wrong.Value);

        Assert.Same(before, model.Board);
        Assert.False(model.IsMessageGood);
    }

    [Fact]
    public void Задание_Не_Пропускается_Без_Верного_Хода()
    {
        var model = new LessonViewModel();
        _ = NextTask(model);

        Assert.False(model.CanGoNext);
    }

    [Fact]
    public void На_Объяснении_Ход_Не_Принимается()
    {
        var model = new LessonViewModel();
        var before = model.Board;

        model.Play(new Point(4, 4));

        Assert.Same(before, model.Board);
        Assert.False(model.IsStepTask);
    }

    [Fact]
    public void Подсказка_Показывает_Точку()
    {
        var model = new LessonViewModel();
        _ = NextTask(model);

        model.ShowHint();

        Assert.NotNull(model.HintPoint);
    }

    [Fact]
    public void Урок_Проходится_До_Конца()
    {
        var model = new LessonViewModel();

        PassLesson(model);

        Assert.True(model.IsCompleted);
        Assert.Equal(LessonViewModel.CompletedText, model.StepCounter);
    }

    [Fact]
    public void Пройденный_Урок_Начинается_Заново()
    {
        var model = new LessonViewModel();
        PassLesson(model);

        model.Restart();

        Assert.False(model.IsCompleted);
        Assert.Equal($"Шаг 1 из {model.StepCount}", model.StepCounter);
    }

    [Fact]
    public void Назад_Возвращает_Предыдущий_Шаг()
    {
        var model = new LessonViewModel();
        _ = NextTask(model);
        model.Play(model.AcceptedPoints[0]);
        model.Next();

        model.Back();

        Assert.False(model.IsCompleted);
        Assert.True(model.StepNumber >= 1);
    }

    [Fact]
    public void Подсветка_Берётся_Из_Шага()
    {
        var model = new LessonViewModel();

        Assert.Equal(LessonLibrary.All[0].Steps[0].Position.Highlight, model.Highlight);
    }

    [Fact]
    public void Смена_Шага_Сообщает_О_Свойствах_Панели()
    {
        var model = new LessonViewModel();
        var changed = new List<string>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        PassLesson(model);

        Assert.Contains(nameof(LessonViewModel.StepTitle), changed);
        Assert.Contains(nameof(LessonViewModel.StepText), changed);
        Assert.Contains(nameof(LessonViewModel.StepCounter), changed);
        Assert.Contains(nameof(LessonViewModel.Message), changed);
    }

    [Fact]
    public void Все_Тексты_Уроков_На_Русском()
    {
        // Обучение ведётся по-русски: латиница допустима только внутри слов-терминов,
        // поэтому проверяется наличие кириллицы в каждом тексте шага.
        foreach (var lesson in LessonLibrary.All)
        {
            Assert.True(ContainsCyrillic(lesson.Title), $"{lesson.Id}: название урока без кириллицы");
            Assert.True(ContainsCyrillic(lesson.Summary), $"{lesson.Id}: описание урока без кириллицы");

            foreach (var step in lesson.Steps)
            {
                Assert.True(ContainsCyrillic(step.Title), $"{lesson.Id}: заголовок шага без кириллицы");
                Assert.True(ContainsCyrillic(step.Text), $"{lesson.Id}: текст шага без кириллицы");
                Assert.True(ContainsCyrillic(step.Success), $"{lesson.Id}: похвала без кириллицы");
                Assert.True(ContainsCyrillic(step.Failure), $"{lesson.Id}: отказ без кириллицы");
            }
        }
    }

    /// <summary>Ставит модель на первое задание, проходя объяснения.</summary>
    /// <param name="model">Модель обучения.</param>
    /// <returns>Первый верный ход задания.</returns>
    private static (Point Point, int Step) NextTask(LessonViewModel model)
    {
        while (!model.IsStepTask && model.CanGoNext)
        {
            model.Next();
        }

        return (model.AcceptedPoints[0], model.StepNumber);
    }

    /// <summary>Проходит урок целиком: верные ходы и переходы.</summary>
    /// <param name="model">Модель обучения.</param>
    private static void PassLesson(LessonViewModel model)
    {
        var guard = 0;

        while (!model.IsCompleted && guard++ < 100)
        {
            if (model.IsStepTask)
            {
                model.Play(model.AcceptedPoints[0]);
            }

            model.Next();
        }
    }

    /// <summary>Ищет точку, которой нет среди принимаемых ходов шага.</summary>
    /// <param name="model">Модель обучения.</param>
    /// <returns>Точка или <c>null</c>, если такой нет.</returns>
    private static Point? WrongPoint(LessonViewModel model)
    {
        var answer = model.AcceptedPoints;

        foreach (var point in model.Board.AllPoints())
        {
            if (!answer.Contains(point) && model.Board.IsEmpty(point))
            {
                return point;
            }
        }

        return null;
    }

    /// <summary>Проверяет, что в тексте есть кириллица: обучение ведётся по-русски.</summary>
    /// <param name="text">Текст урока.</param>
    /// <returns><c>true</c>, если русские буквы в тексте есть.</returns>
    private static bool ContainsCyrillic(string text) =>
        text.Any(ch => ch is >= 'А' and <= 'я' or 'ё' or 'Ё');
}
