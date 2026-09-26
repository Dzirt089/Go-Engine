namespace GoEngine.AI;

/// <summary>Нарушение инварианта слоя AI: селектор вернул невозможный ход, бюджет исчерпан и подобное.</summary>
/// <remarks>
/// Ожидаемые исходы (нелегальный ход, отсутствие ходов) исключениями не оформляются:
/// для них есть <see cref="GoEngine.Core.Result{T}"/> и пас. Сообщения — на русском языке.
/// </remarks>
public sealed class AiException : Exception
{
    /// <summary>Создаёт исключение с описанием нарушения.</summary>
    /// <param name="message">Описание нарушения инварианта, на русском языке.</param>
    public AiException(string message) : base(message)
    {
    }

    /// <summary>Создаёт исключение с описанием нарушения и вложенной причиной.</summary>
    /// <param name="message">Описание нарушения инварианта, на русском языке.</param>
    /// <param name="innerException">Причина нарушения.</param>
    public AiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
