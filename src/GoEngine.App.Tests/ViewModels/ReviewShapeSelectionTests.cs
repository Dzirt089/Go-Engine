using GoEngine.App.ViewModels;

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
        var model = ShapeScenarios.ReviewWithSeveralShapes();

        model.SelectShape(1);

        Assert.Equal(model.Shapes[1].Points, model.ShapePoints);
    }

    [Fact]
    public void Выбранная_Форма_Номерует_Подсветку()
    {
        var model = ShapeScenarios.ReviewWithSeveralShapes();

        model.SelectShape(1);

        Assert.Equal(2, model.SelectedShape?.Number);
    }

    [Fact]
    public void Выбор_Формы_Не_Сообщает_О_Смене_Хода()
    {
        // Выбор формы не должен поднимать LastMove: вид на него перезапускает анимацию камня.
        var model = ShapeScenarios.ReviewWithSeveralShapes();
        List<string?> names = [];
        model.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        model.SelectShape(1);

        Assert.DoesNotContain(nameof(ReviewViewModel.LastMove), names);
    }

    [Fact]
    public void Начать_Разбор_Заново_Выбирает_Первую_Форму()
    {
        var model = ShapeScenarios.ReviewWithSeveralShapes();
        model.SelectShape(1);

        model.Restart();

        Assert.Equal(0, model.SelectedShapeIndex);
    }
}
