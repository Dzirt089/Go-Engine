using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Тесты прогресса обучения: продолжение с места, отметки в списках и события.</summary>
/// <remarks>
/// Прогресс нужен потому, что курс большой: 12 уроков и 4 партии на сотни ходов. Здесь
/// проверяется поведение моделей — продолжение, отметки и сообщения об изменениях; запись файла
/// проверяют тесты <see cref="ProgressStoreTests"/>.
/// </remarks>
public sealed class StudyProgressTests
{
    [Fact]
    public void Урок_Продолжается_С_Сохранённого_Шага()
    {
        var lessons = LessonLibrary.All;
        var progress = StudyProgress.Empty;
        progress.SaveLesson("go-01", 3, completed: false);

        var model = new LessonViewModel(lessons, progress);

        Assert.Equal(0, model.SelectedIndex);

        // Сессия не идёт дальше нерешённого задания: игрок возвращается к шагу 2 (первое задание),
        // а не в середину объяснения.
        Assert.Equal(2, model.StepNumber);
    }

    [Fact]
    public void Открывается_Последний_Урок()
    {
        var progress = StudyProgress.Empty;
        progress.SaveLesson("go-05", 1, completed: false);

        var model = new LessonViewModel(LessonLibrary.All, progress);

        Assert.Equal("go-05", LessonLibrary.All[model.SelectedIndex].Id);
    }

    [Fact]
    public void Начатый_Урок_Отмечается_В_Списке()
    {
        var model = new LessonViewModel(LessonLibrary.All, StudyProgress.Empty);

        model.Next();

        Assert.StartsWith("▸", model.Labels[0], StringComparison.Ordinal);
        Assert.Equal($"Урок · пройдено 0 из {LessonLibrary.Count}", model.ListCaption);
    }

    [Fact]
    public void Пройденный_Урок_Отмечается_Галочкой()
    {
        var model = new LessonViewModel(LessonLibrary.All, StudyProgress.Empty);
        var guard = 0;

        while (!model.IsCompleted && guard++ < 100)
        {
            if (model.IsStepTask)
            {
                model.Play(model.AcceptedPoints[0]);
            }

            model.Next();
        }

        Assert.True(model.IsLessonCompleted);
        Assert.StartsWith("✓", model.Labels[0], StringComparison.Ordinal);
        Assert.Equal($"Урок · пройдено 1 из {LessonLibrary.Count}", model.ListCaption);
    }

    [Fact]
    public void Шаг_Урока_Сообщает_О_Прогрессе()
    {
        var progress = StudyProgress.Empty;
        var model = new LessonViewModel(LessonLibrary.All, progress);
        var raised = 0;
        model.ProgressChanged += (_, _) => raised++;

        model.Next();

        Assert.Equal(1, raised);
        Assert.Equal(model.StepNumber, progress.ForLesson("go-01")?.Step);
    }

    [Fact]
    public void Партия_Продолжается_С_Сохранённого_Хода()
    {
        var games = ReviewLibrary.All;
        var progress = StudyProgress.Empty;
        progress.SaveGame("game-02", 12, completed: false);

        var model = new ReviewViewModel(games, progress);

        Assert.Equal("game-02", games[model.SelectedIndex].Id);
        Assert.Equal(12, model.MoveNumber);
    }

    [Fact]
    public void Разобранная_Партия_Отмечается_Галочкой()
    {
        var model = new ReviewViewModel(ReviewLibrary.All, StudyProgress.Empty);
        var guard = 0;

        while (!model.IsCompleted && guard++ < model.MoveCount + 10)
        {
            if (model.IsQuiz)
            {
                model.Play(model.QuizPoints[0]);
            }

            model.Next();
        }

        Assert.True(model.IsGameCompleted);
        Assert.StartsWith("✓", model.Labels[0], StringComparison.Ordinal);
        Assert.Equal($"Партия · разобрано 1 из {ReviewLibrary.Count}", model.ListCaption);
    }

    [Fact]
    public void Ход_Разбора_Сообщает_О_Прогрессе()
    {
        var progress = StudyProgress.Empty;
        var model = new ReviewViewModel(ReviewLibrary.All, progress);
        var raised = 0;
        model.ProgressChanged += (_, _) => raised++;

        model.Play(model.QuizPoints[0]);

        Assert.Equal(1, raised);
        Assert.Equal(model.MoveNumber, progress.ForGame("game-01")?.Step);
    }

    [Fact]
    public void Оболочка_Передаёт_Прогресс_В_Модели()
    {
        var progress = StudyProgress.Empty;
        progress.SaveLesson("go-04", 1, completed: true);
        progress.SaveGame("game-03", 5, completed: false);

        var shell = ShellViewModel.Create(
            AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9),
            null,
            null,
            progress: progress);

        Assert.Same(progress, shell.Progress);
        Assert.StartsWith("✓", shell.Lessons.Labels[3], StringComparison.Ordinal);
        Assert.Equal(5, shell.Reviews.MoveNumber);
    }

    [Fact]
    public void Оболочка_Сообщает_О_Прогрессе_Обучения()
    {
        var shell = ShellViewModel.Create(
            AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9),
            null,
            null);
        var raised = 0;
        shell.ProgressChanged += (_, _) => raised++;

        shell.Lessons.Next();

        Assert.Equal(1, raised);
    }
}
