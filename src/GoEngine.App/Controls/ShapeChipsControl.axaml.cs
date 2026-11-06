using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Controls;

/// <summary>Полоса форм хода: заголовок, чипы форм и объяснение выбранной формы.</summary>
/// <remarks>
/// <para>
/// Один элемент на задачи, уроки и разбор партий: до этого разметка чипов и обработчик нажатия
/// были скопированы в три вида, и правка в одном разделе не доходила до остальных (D-116).
/// </para>
/// <para>
/// Формы приходят из модели (<see cref="Shapes"/>), выбор уходит обратно двусторонней привязкой
/// (<see cref="SelectedIndex"/>): контрол не знает, задача это, урок или партия.
/// </para>
/// </remarks>
public sealed partial class ShapeChipsControl : UserControl
{
    /// <summary>Формы для показа: номерами, от крупной к мелкой.</summary>
    public static readonly StyledProperty<IReadOnlyList<ShapeItem>?> ShapesProperty =
        AvaloniaProperty.Register<ShapeChipsControl, IReadOnlyList<ShapeItem>?>(nameof(Shapes));

    /// <summary>Номер выбранной формы, начиная с нуля; привязка по умолчанию двусторонняя.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<ShapeChipsControl, int>(
            nameof(SelectedIndex),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Заголовок полосы: «Формы решения» или «Формы хода».</summary>
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<ShapeChipsControl, string>(nameof(Header), defaultValue: "Формы хода");

    /// <summary>Показывать ли объяснение выбранной формы: в разборе партий его нет.</summary>
    public static readonly StyledProperty<bool> ShowWhyProperty =
        AvaloniaProperty.Register<ShapeChipsControl, bool>(nameof(ShowWhy), defaultValue: true);

    private readonly ItemsControl? _chips;
    private readonly TextBlock? _header;
    private readonly TextBlock? _why;

    /// <summary>Чипы, показанные сейчас: список пересобирается только при смене форм.</summary>
    private IReadOnlyList<ShapeChip> _chipItems = [];

    /// <summary>Формы, по которым собраны <see cref="_chipItems"/>: ссылка, а не содержимое.</summary>
    /// <remarks>
    /// Модель отдаёт новый список только после пересчёта форм; смена выбранной формы список
    /// не меняет. Поэтому сравнение по ссылке дешево и не путает выбор с новым ходом.
    /// </remarks>
    private IReadOnlyList<ShapeItem>? _lastShapes;

    /// <summary>Создаёт полосу форм.</summary>
    public ShapeChipsControl()
    {
        AvaloniaXamlLoader.Load(this);

        _chips = this.FindControl<ItemsControl>("Chips");
        _header = this.FindControl<TextBlock>("HeaderText");
        _why = this.FindControl<TextBlock>("WhyText");

        Refresh();
    }

    /// <summary>Формы для показа: номерами, от крупной к мелкой.</summary>
    public IReadOnlyList<ShapeItem>? Shapes
    {
        get => GetValue(ShapesProperty);
        set => SetValue(ShapesProperty, value);
    }

    /// <summary>Номер выбранной формы, начиная с нуля.</summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Заголовок полосы: «Формы решения» или «Формы хода».</summary>
    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Показывать ли объяснение выбранной формы.</summary>
    public bool ShowWhy
    {
        get => GetValue(ShowWhyProperty);
        set => SetValue(ShowWhyProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);

        base.OnPropertyChanged(change);

        if (change.Property == ShapesProperty || change.Property == SelectedIndexProperty
            || change.Property == HeaderProperty || change.Property == ShowWhyProperty)
        {
            Refresh();
        }
    }

    /// <summary>Перестраивает заголовок, чипы и объяснение по текущим свойствам.</summary>
    private void Refresh()
    {
        var shapes = Shapes ?? [];
        var selected = Math.Clamp(SelectedIndex, 0, Math.Max(0, shapes.Count - 1));

        if (selected != SelectedIndex)
        {
            // Форм стало меньше, и старый номер вышел за границы. Зажимаем выбор: визуально
            // и в привязке должен быть один и тот же номер (находка ревью D-116).
            SetCurrentValue(SelectedIndexProperty, selected);

            return;
        }

        if (_header is not null)
        {
            // Одна форма в заголовке не нуждается: «Формы хода» появляется, когда форм больше одной.
            _header.Text = Header;
            _header.IsVisible = shapes.Count > 1;
        }

        if (_chips is not null)
        {
            if (!ReferenceEquals(shapes, _lastShapes))
            {
                _lastShapes = shapes;
                _chipItems = [.. shapes.Select(shape => new ShapeChip(shape.Number, shape.Label, shape.Why))];
                _chips.ItemsSource = _chipItems;
            }

            for (var index = 0; index < _chipItems.Count; index++)
            {
                // Признак меняется на месте: кнопка, по которой нажали, остаётся в дереве,
                // и фокус не теряется (находка ревью D-116).
                _chipItems[index].IsSelected = index == selected;
            }
        }

        if (_why is not null)
        {
            var why = ShowWhy && Selected(shapes) is { } shape ? shape.Why : string.Empty;

            _why.Text = why;
            _why.IsVisible = why.Length > 0;
        }
    }

    /// <summary>Выбранная форма или <c>null</c>, когда форм нет.</summary>
    /// <param name="shapes">Формы позиции.</param>
    /// <returns>Форма, на которую показывает выбор.</returns>
    private ShapeItem? Selected(IReadOnlyList<ShapeItem> shapes) =>
        shapes.Count == 0 ? null : shapes[Math.Clamp(SelectedIndex, 0, shapes.Count - 1)];

    /// <summary>Выбирает форму по номеру чипа.</summary>
    /// <param name="sender">Кнопка формы.</param>
    /// <param name="e">Аргументы события.</param>
    private void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int number })
        {
            SelectedIndex = number - 1;
        }
    }
}
