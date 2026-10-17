using GoEngine.App.ViewModels;

namespace GoEngine.App.Tests;

/// <summary>Прогресс уроков: отметка пройденного урока не снимается при его открытии.</summary>
/// <remarks>
/// Находка QA 2026-10-17 (высокая): открытие пройденного урока снимало отметку и уменьшало счётчик
/// «Урок · пройдено N из 12» — игрок видел, что прогресс курса теряется.
/// </remarks>
public sealed class LessonProgressTests
{
    [Fact]
    public void Открытие_Пройденного_Урока_Не_Снимает_Отметку()
    {
        var model = new LessonViewModel();
        var guard = 0;

        // Проходим первый урок целиком: подсказка ведёт к верному ходу.
        while (!model.IsCompleted && guard++ < 300)
        {
            if (model.HintPoint is { } hint)
            {
                model.Play(hint);
                continue;
            }

            if (model.CanHint)
            {
                model.ShowHint();
                continue;
            }

            if (!model.CanGoNext)
            {
                break;
            }

            model.Next();
        }

        Assert.True(model.IsCompleted, "урок не пройден");
        var caption = model.ListCaption;

        // Открываем второй урок и возвращаемся в первый: счётчик пройденных не должен уменьшиться.
        model.NextLesson();
        model.PreviousLesson();

        Assert.Equal(caption, model.ListCaption);
    }

    [Fact]
    public void Пройденный_Урок_Остаётся_Отмеченным_После_Возврата()
    {
        var model = new LessonViewModel();
        var guard = 0;

        while (!model.IsCompleted && guard++ < 300)
        {
            if (model.HintPoint is { } hint)
            {
                model.Play(hint);
                continue;
            }

            if (model.CanHint)
            {
                model.ShowHint();
                continue;
            }

            if (!model.CanGoNext)
            {
                break;
            }

            model.Next();
        }

        Assert.True(model.IsCompleted, "урок не пройден");
        Assert.True(model.IsLessonCompleted, "урок не отмечен как пройденный");

        model.NextLesson();
        model.PreviousLesson();

        Assert.True(model.IsLessonCompleted, "отметка пройденного урока пропала после возврата");
    }
}
