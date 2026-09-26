namespace GoEngine.Core;

/// <summary>
/// Нарушение инварианта движка: недопустимый размер доски, ход в занятую точку,
/// рассинхрон групп и другие состояния, невозможные в корректной партии.
/// </summary>
/// <remarks>
/// Ожидаемые исходы (нелегальный ход, ко) исключениями не оформляются:
/// для них предназначены <c>Result</c> и <c>Result&lt;T&gt;</c>.
/// </remarks>
public sealed class DomainException : Exception
{
    /// <summary>Создаёт исключение с описанием нарушения.</summary>
    /// <param name="message">Описание нарушения инварианта, на русском языке.</param>
    public DomainException(string message) : base(message)
    {
    }

    /// <summary>Создаёт исключение с описанием нарушения и вложенной причиной.</summary>
    /// <param name="message">Описание нарушения инварианта, на русском языке.</param>
    /// <param name="innerException">Причина нарушения.</param>
    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
