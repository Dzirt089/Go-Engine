using System.ComponentModel;

namespace GoEngine.App.Controls;

/// <summary>Чип формы в полосе форм: номер, подпись, объяснение и признак выбора.</summary>
/// <remarks>
/// Отдельный тип, а не <c>ShapeItem</c>: признак выбора — дело полосы форм, а не модели.
/// Номер тот же, что на доске и в строке разбора, поэтому форма узнаётся без объяснений.
/// Признак выбора меняется на месте и уведомляет привязку: пересоздавать список чипов
/// на каждый клик нельзя — кнопка, по которой нажали, исчезала бы из визуального дерева
/// вместе с фокусом (находка ревью D-116).
/// </remarks>
public sealed class ShapeChip : INotifyPropertyChanged
{
    private bool _isSelected;

    /// <summary>Создаёт чип формы.</summary>
    /// <param name="number">Номер формы с единицы: по нему приходит выбор.</param>
    /// <param name="label">Подпись «1) пасть тигра».</param>
    /// <param name="why">Чем форма хороша или плоха: подсказка чипа.</param>
    public ShapeChip(int number, string label, string why)
    {
        Number = number;
        Label = label;
        Why = why;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Номер формы с единицы: по нему приходит выбор.</summary>
    public int Number { get; }

    /// <summary>Подпись «1) пасть тигра».</summary>
    public string Label { get; }

    /// <summary>Чем форма хороша или плоха: подсказка чипа.</summary>
    public string Why { get; }

    /// <summary>Выбрана ли форма: выбранный чип подсвечивается.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
