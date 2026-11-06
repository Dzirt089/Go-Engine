using System.Reflection;
using Avalonia;
using GoEngine.App.Controls;
using GoEngine.App.Rendering;
using GoPoint = GoEngine.Core.Point;

namespace GoEngine.App.Tests;

/// <summary>Свойства, которые доска рисует, обязаны перерисовывать её при изменении.</summary>
/// <remarks>
/// Регрессия 2026-11-06: подсветка форм не переключалась. Модель меняла камни выбранной формы,
/// но свойства формы не были зарегистрированы в <c>AffectsRender</c> — доска оставалась прежней,
/// и выбор чипа был виден только в тексте. Список сверяется со всеми свойствами элемента,
/// поэтому новое свойство отрисовки без регистрации валит проверку, а не ждёт живого прогона.
/// </remarks>
public sealed class BoardControlRenderRegistrationTests
{
    [Fact]
    public void Все_Свойства_Доски_Зарегистрированы_Для_Перерисовки()
    {
        var missing = Properties()
            .Where(property => !BoardControl.DrawingProperties.Contains(property))
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Свойства_Формы_Перерисовывают_Доску()
    {
        AvaloniaProperty[] shapeProperties =
        [
            BoardControl.ShapePointsProperty,
            BoardControl.ShapeBadgesProperty,
            BoardControl.ShapeIsGoodProperty
        ];

        Assert.All(shapeProperties, property => Assert.Contains(property, BoardControl.DrawingProperties));
    }

    [Fact]
    public void Смена_Свойств_Формы_Запрашивает_Перерисовку()
    {
        // Проверка идёт через счётчик обработчика: вернуть старый AffectsRender без свойств формы
        // и оставить DrawingProperties полным тест не даст (находка ревью D-116).
        var control = new BoardControl();
        var before = control.RenderRequests;

        control.ShapePoints = [new GoPoint(1, 1)];
        control.ShapeBadges = [new ShapeBadge(new GoPoint(1, 1), 1)];
        control.ShapeIsGood = false;

        Assert.Equal(before + 3, control.RenderRequests);
    }

    /// <summary>Все свойства, объявленные в <see cref="BoardControl"/>.</summary>
    /// <returns>Свойства элемента в порядке объявления.</returns>
    private static IEnumerable<AvaloniaProperty> Properties() =>
        typeof(BoardControl)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => typeof(AvaloniaProperty).IsAssignableFrom(field.FieldType))
            .Select(field => (AvaloniaProperty)field.GetValue(null)!);
}
