using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Views;

/// <summary>Диалог настроек партии: размер доски, уровень AI, цвет игрока и коми.</summary>
/// <remarks>
/// Диалог не хранит настройки сам: он собирает их из полей и возвращает окну-владельцу
/// (<see cref="Selected"/>), а запись в файл делает <see cref="SettingsStore"/>.
/// </remarks>
public sealed partial class SettingsWindow : Window
{
    /// <summary>Стороны доски в порядке списка выбора.</summary>
    private static readonly byte[] Sizes = [9, 13, 19];

    /// <summary>Уровни, показанные сейчас: зависят от доски и наличия модели (D-038).</summary>
    private IReadOnlyList<DifficultyLevel> _levels = LevelChooser.Available(BoardSize.Size9, false);

    /// <summary>Создаёт диалог настроек.</summary>
    public SettingsWindow() : this(AppSettings.Default)
    {
    }

    /// <summary>Создаёт диалог настроек с текущими значениями.</summary>
    /// <param name="current">Текущие настройки партии.</param>
    public SettingsWindow(AppSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);

        AvaloniaXamlLoader.Load(this);

        Selected = current;

        var sizeBox = this.FindControl<ComboBox>("SizeBox");
        var levelBox = this.FindControl<ComboBox>("LevelBox");
        var colorBox = this.FindControl<ComboBox>("ColorBox");
        var komiBox = this.FindControl<NumericUpDown>("KomiBox");
        var startButton = this.FindControl<Button>("StartButton");
        var cancelButton = this.FindControl<Button>("CancelButton");

        if (sizeBox is not null)
        {
            sizeBox.ItemsSource = Sizes.Select(size => $"{size}×{size}").ToList();
            sizeBox.SelectedIndex = Math.Max(0, Array.IndexOf(Sizes, current.ToBoardSize().Value));
            sizeBox.SelectionChanged += OnSizeChanged;
        }

        FillLevels(current.ToBoardSize(), current.ToDifficultyLevel());

        if (colorBox is not null)
        {
            colorBox.ItemsSource = new List<string> { "Чёрные", "Белые" };
            colorBox.SelectedIndex = current.ToPlayerColor() == StoneColor.Black ? 0 : 1;
        }

        if (komiBox is not null)
        {
            komiBox.Value = (decimal)current.Komi;
        }

        if (startButton is not null)
        {
            startButton.Click += OnStartClick;
        }

        if (cancelButton is not null)
        {
            cancelButton.Click += OnCancelClick;
        }
    }

    /// <summary>Настройки, выбранные в диалоге.</summary>
    public AppSettings Selected { get; private set; }

    /// <summary>Загружена ли модель нейросети: без неё уровни Дан не предлагаются.</summary>
    private static bool NetworkAvailable => global::GoEngine.App.App.Evaluator is not null;

    /// <summary>Перестраивает список уровней при смене размера доски.</summary>
    /// <param name="sender">Список размеров.</param>
    /// <param name="e">Событие смены выбора.</param>
    /// <remarks>
    /// Если выбранный уровень для новой доски недоступен (уровень Дан на 9×9), выбор
    /// сбрасывается на 10 кю: партия не должна начинаться с уровня, который не сможет играть.
    /// </remarks>
    private void OnSizeChanged(object? sender, SelectionChangedEventArgs e) =>
        FillLevels(SelectedSize(), CurrentLevel());

    /// <summary>Заполняет список уровней для доски.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="preferred">Желаемый уровень; недоступный заменяется на уровень сброса.</param>
    private void FillLevels(BoardSize size, DifficultyLevel preferred)
    {
        _levels = LevelChooser.Available(size, NetworkAvailable);

        if (this.FindControl<ComboBox>("LevelBox") is not { } levelBox)
        {
            return;
        }

        levelBox.ItemsSource = _levels.Select(LevelChooser.Label).ToList();
        levelBox.SelectedIndex = Math.Max(0, IndexOf(LevelChooser.Resolve(preferred, size, NetworkAvailable)));
    }

    /// <summary>Возвращает выбранный сейчас уровень.</summary>
    /// <returns>Уровень из списка или уровень сброса.</returns>
    private DifficultyLevel CurrentLevel()
    {
        var index = SelectedIndex("LevelBox");

        return index >= 0 && index < _levels.Count ? _levels[index] : LevelChooser.Fallback;
    }

    /// <summary>Возвращает выбранный размер доски.</summary>
    /// <returns>Размер доски.</returns>
    private BoardSize SelectedSize() => new(Sizes[Math.Clamp(SelectedIndex("SizeBox"), 0, Sizes.Length - 1)]);

    /// <summary>Ищет уровень в текущем списке.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>Индекс уровня или 0.</returns>
    private int IndexOf(DifficultyLevel level)
    {
        for (var index = 0; index < _levels.Count; index++)
        {
            if (_levels[index] == level)
            {
                return index;
            }
        }

        return 0;
    }

    /// <summary>Собирает настройки из полей и закрывает диалог.</summary>
    /// <param name="sender">Кнопка «Начать партию».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        var sizeValue = Sizes[Math.Clamp(SelectedIndex("SizeBox"), 0, Sizes.Length - 1)];
        var level = LevelChooser.Resolve(CurrentLevel(), new BoardSize(sizeValue), NetworkAvailable);
        var color = SelectedIndex("ColorBox") == 1 ? StoneColor.White : StoneColor.Black;
        var komi = this.FindControl<NumericUpDown>("KomiBox")?.Value ?? (decimal)Selected.Komi;

        Selected = AppSettings.From(new BoardSize(sizeValue), level, color, new Komi((double)komi));

        Close(true);
    }

    /// <summary>Закрывает диалог без изменений.</summary>
    /// <param name="sender">Кнопка «Отмена».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    /// <summary>Возвращает выбранный индекс списка.</summary>
    /// <param name="name">Имя элемента управления.</param>
    /// <returns>Индекс выбранного элемента или 0.</returns>
    private int SelectedIndex(string name) => this.FindControl<ComboBox>(name)?.SelectedIndex ?? 0;
}
