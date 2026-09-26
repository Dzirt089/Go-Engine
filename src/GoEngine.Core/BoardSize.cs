namespace GoEngine.Core;

/// <summary>Размер доски. Допустимы только 9×9, 13×13 и 19×19.</summary>
/// <remarks>
/// Позиционная запись не может валидировать значение, поэтому конструктор объявлен явно,
/// а <see cref="Value"/> доступно только для чтения: недопустимый размер нельзя получить
/// ни через <c>new</c>, ни через <c>with</c>.
/// </remarks>
public readonly record struct BoardSize
{
    /// <summary>Допустимая сторона 9.</summary>
    private const byte SmallValue = 9;

    /// <summary>Допустимая сторона 13.</summary>
    private const byte MediumValue = 13;

    /// <summary>Допустимая сторона 19.</summary>
    private const byte LargeValue = 19;

    /// <summary>Создаёт размер доски.</summary>
    /// <param name="value">Сторона доски: 9, 13 или 19.</param>
    /// <exception cref="DomainException">Размер не входит в набор допустимых.</exception>
    public BoardSize(byte value)
    {
        if (value != SmallValue && value != MediumValue && value != LargeValue)
        {
            throw new DomainException(
                $"Недопустимый размер доски: {value}. Разрешены только {SmallValue}, {MediumValue} и {LargeValue}.");
        }

        Value = value;
    }

    /// <summary>Сторона доски.</summary>
    public byte Value { get; }

    /// <summary>Размер 9×9.</summary>
    public static BoardSize Size9 { get; } = new(SmallValue);

    /// <summary>Размер 13×13.</summary>
    public static BoardSize Size13 { get; } = new(MediumValue);

    /// <summary>Размер 19×19.</summary>
    public static BoardSize Size19 { get; } = new(LargeValue);

    /// <summary>Количество точек на доске.</summary>
    public int Area => Value * Value;

    /// <summary>Лимит ходов партии по умолчанию: два хода на каждую точку доски.</summary>
    /// <remarks>Для доски 9×9 это 162 хода — ограничение из <c>GO_RULES.md</c>, п. 8 для партий AI.</remarks>
    public int DefaultMoveLimit => 2 * Area;

    /// <summary>Возвращает размер в виде «9×9».</summary>
    /// <returns>Сторона доски, повторённая дважды через знак умножения.</returns>
    public override string ToString() => $"{Value}×{Value}";
}
