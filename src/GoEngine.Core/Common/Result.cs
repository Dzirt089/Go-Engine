namespace GoEngine.Core;

/// <summary>Исход операции без значения.</summary>
/// <remarks>
/// Ожидаемые исходы — нелегальный ход, нарушение ко, отказ правил — возвращаются значением,
/// а не исключением (<c>AGENTS.md</c>, п. 4). Исключением остаётся только нарушение инварианта
/// движка — <see cref="DomainException"/>.
/// </remarks>
public readonly struct Result
{
    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Признак успешного исхода.</summary>
    public bool IsSuccess { get; }

    /// <summary>Причина отказа на русском языке или <c>null</c> при успехе.</summary>
    public string? Error { get; }

    /// <summary>Создаёт успешный исход.</summary>
    /// <returns>Результат без ошибки.</returns>
    public static Result Ok() => new(true, null);

    /// <summary>Создаёт неуспешный исход.</summary>
    /// <param name="error">Причина отказа, на русском языке.</param>
    /// <returns>Результат с причиной отказа.</returns>
    /// <exception cref="ArgumentException">Причина пуста или состоит из пробелов.</exception>
    public static Result Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new(false, error);
    }

    /// <inheritdoc />
    public override string ToString() => IsSuccess ? "Ok" : $"Fail: {Error}";
}

/// <summary>Исход операции со значением.</summary>
/// <typeparam name="T">Тип значения при успехе.</typeparam>
/// <remarks>
/// При отказе <see cref="Value"/> равно <c>default</c>: значение имеет смысл только вместе
/// с <see cref="IsSuccess"/>.
/// </remarks>
public readonly struct Result<T>
{
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>Признак успешного исхода.</summary>
    public bool IsSuccess { get; }

    /// <summary>Значение при успехе или <c>default</c> при отказе.</summary>
    public T? Value { get; }

    /// <summary>Причина отказа на русском языке или <c>null</c> при успехе.</summary>
    public string? Error { get; }

    /// <summary>Создаёт успешный исход со значением.</summary>
    /// <param name="value">Значение успешного исхода.</param>
    /// <returns>Результат со значением.</returns>
    public static Result<T> Ok(T value) => new(true, value, null);

    /// <summary>Создаёт неуспешный исход.</summary>
    /// <param name="error">Причина отказа, на русском языке.</param>
    /// <returns>Результат с причиной отказа.</returns>
    /// <exception cref="ArgumentException">Причина пуста или состоит из пробелов.</exception>
    public static Result<T> Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new(false, default, error);
    }

    /// <inheritdoc />
    public override string ToString() => IsSuccess ? $"Ok: {Value}" : $"Fail: {Error}";
}
