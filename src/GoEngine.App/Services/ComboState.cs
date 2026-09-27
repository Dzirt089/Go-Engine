namespace GoEngine.App.Services;

/// <summary>Состояние списка выбора: что в нём показано и чей это выбор.</summary>
/// <remarks>
/// Список выбора сбрасывает <c>SelectedIndex</c>, когда меняется <c>ItemsSource</c>, и сообщает
/// об этом как о выборе игрока. Без этого признака сброс попадал бы в модель представления и
/// пересоздавал партию с чужим уровнем — регрессия «выбрано 25 кю, играет 30 кю».
/// Логика вынесена из вида: у самого списка её тестами не проверить, а здесь — можно.
/// </remarks>
public sealed class ComboState
{
    private IReadOnlyList<string> _labels = [];
    private int _index = -1;

    /// <summary>Идёт программное обновление: изменения списка в это время игнорируются.</summary>
    public bool IsUpdating { get; private set; }

    /// <summary>Подписи, показанные в списке.</summary>
    public IReadOnlyList<string> Labels => _labels;

    /// <summary>Индекс, показанный в списке.</summary>
    public int Index => _index;

    /// <summary>Начинает программное обновление списка.</summary>
    /// <param name="labels">Подписи, которые должна показать модель.</param>
    /// <param name="index">Индекс, который должна показать модель.</param>
    /// <returns><c>true</c>, если подписи надо присвоить списку: тот же экземпляр не пересоздаём.</returns>
    public bool BeginUpdate(IReadOnlyList<string> labels, int index)
    {
        ArgumentNullException.ThrowIfNull(labels);

        var replace = !ReferenceEquals(_labels, labels);

        _labels = labels;
        _index = index;
        IsUpdating = true;

        return replace;
    }

    /// <summary>Завершает программное обновление.</summary>
    public void EndUpdate() => IsUpdating = false;

    /// <summary>Принимает изменение списка.</summary>
    /// <param name="index">Индекс, выбранный в списке.</param>
    /// <returns><c>true</c>, если это выбор игрока; <c>false</c> — следствие программного обновления.</returns>
    public bool TryAccept(int index)
    {
        if (IsUpdating)
        {
            return false;
        }

        _index = index;

        return true;
    }
}
