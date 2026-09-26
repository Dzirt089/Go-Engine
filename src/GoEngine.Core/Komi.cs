using System.Globalization;

namespace GoEngine.Core;

/// <summary>Коми — компенсация белым за второй ход (<c>GO_RULES.md</c>, п. 10).</summary>
/// <remarks>
/// Позиционная запись не может валидировать значение, поэтому конструктор объявлен явно,
/// а <see cref="Value"/> доступно только для чтения: отрицательное коми нельзя получить
/// ни через <c>new</c>, ни через <c>with</c>.
/// </remarks>
public readonly record struct Komi
{
    /// <summary>Коми для доски 19×19.</summary>
    private const double LargeValue = 7.5;

    /// <summary>Коми для доски 13×13.</summary>
    private const double MediumValue = 5.5;

    /// <summary>Коми для доски 9×9.</summary>
    private const double SmallValue = 5.5;

    /// <summary>Создаёт коми.</summary>
    /// <param name="value">Значение коми: неотрицательное число, обычно с половиной очка.</param>
    /// <exception cref="DomainException">Значение отрицательное или не является числом.</exception>
    public Komi(double value)
    {
        if (double.IsNaN(value) || value < 0)
        {
            throw new DomainException($"Недопустимое коми: {value}. Коми не может быть отрицательным.");
        }

        Value = value;
    }

    /// <summary>Значение коми.</summary>
    public double Value { get; }

    /// <summary>Коми для доски 19×19: 7.5.</summary>
    public static Komi For19x19 { get; } = new(LargeValue);

    /// <summary>Коми для доски 13×13: 5.5.</summary>
    public static Komi For13x13 { get; } = new(MediumValue);

    /// <summary>Коми для доски 9×9: 5.5.</summary>
    public static Komi For9x9 { get; } = new(SmallValue);

    /// <summary>Возвращает коми стандартным для доски значением.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns>7.5 для 19×19, 5.5 для 13×13 и 9×9.</returns>
    public static Komi For(BoardSize size) => size.Value switch
    {
        var value when value == BoardSize.Size19.Value => For19x19,
        var value when value == BoardSize.Size13.Value => For13x13,
        _ => For9x9
    };

    /// <summary>Возвращает значение коми в инвариантном формате «7.5».</summary>
    /// <returns>Значение без учёта текущей культуры: разделитель — точка.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
