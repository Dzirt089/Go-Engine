using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Выбор формы в разборе партии: подсветка переключается и не откатывается.</summary>
/// <remarks>
/// Регрессия 2026-11-06: <c>SelectShape</c> заново считал формы, а пересчёт сбрасывал выбор
/// на первую форму — нажатие чипа не меняло подсветку даже после починки перерисовки доски.
/// </remarks>
public sealed class ReviewShapeSelectionTests
{
    [Fact]
    public void Выбор_Формы_В_Партии_Меняет_Камни_Подсветки()
    {
        var (model, _) = ShapeScenarios.ReviewWithSeveralShapes();

        model.SelectShape(1);

        Assert.Equal(model.Shapes[1].Points, model.ShapePoints);
    }

    [Fact]
    public void Выбранная_Форма_Номерует_Подсветку()
    {
        var (model, _) = ShapeScenarios.ReviewWithSeveralShapes();

        model.SelectShape(1);

        Assert.Equal(2, model.SelectedShape?.Number);
    }

    [Fact]
    public void Выбор_Формы_Не_Сообщает_О_Смене_Хода()
    {
        // Выбор формы не должен поднимать LastMove: вид на него перезапускает анимацию камня.
        var (model, _) = ShapeScenarios.ReviewWithSeveralShapes();
        List<string?> names = [];
        model.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        model.SelectShape(1);

        Assert.DoesNotContain(nameof(ReviewViewModel.LastMove), names);
    }

    [Fact]
    public void Продолжение_Разбора_Показывает_Формы_Сразу()
    {
        // Регрессия живой проверки D-116: после перезапуска приложения разбор открывался
        // на сохранённом ходе без чипов и подсветки, пока не нажмёшь «Далее»/«Назад».
        var (model, game) = ShapeScenarios.ReviewWithSeveralShapes();
        var progress = StudyProgress.Empty;
        progress.SaveGame(game.Id, model.MoveNumber, false);

        var resumed = new ReviewViewModel(ReviewLibrary.All, progress);

        Assert.True(resumed.HasSeveralShapes);
    }

    [Fact]
    public void Новый_Ход_Начинает_Выбор_С_Первого_Номера()
    {
        // Прежняя проверка через Restart была пустой: после него форм нет, и ноль получался
        // сам собой. Здесь позиция меняется ходом или ответом на вопрос, и выбор обязан
        // вернуться к первому номеру (находка ревью D-116).
        var (model, game) = ShapeScenarios.ReviewWithSeveralShapes();
        model.SelectShape(model.Shapes.Count - 1);

        if (model.IsQuiz)
        {
            model.Play(game.Moves[model.MoveNumber].Point);
        }
        else
        {
            model.Next();
        }

        Assert.Equal(0, model.SelectedShapeIndex);
    }
}
