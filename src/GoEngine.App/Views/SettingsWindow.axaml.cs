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

    /// <summary>Уровни AI в порядке списка выбора.</summary>
    private static readonly DifficultyLevel[] Levels =
    [
        DifficultyLevel.Kyu30,
        DifficultyLevel.Kyu25,
        DifficultyLevel.Kyu20,
        DifficultyLevel.Kyu15,
        DifficultyLevel.Kyu10,
        DifficultyLevel.Kyu8,
        DifficultyLevel.Kyu5
    ];

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
        }

        if (levelBox is not null)
        {
            levelBox.ItemsSource = Levels.Select(level => $"{level.RankKyu} кю").ToList();
            levelBox.SelectedIndex = Math.Max(0, Array.IndexOf(Levels, current.ToDifficultyLevel()));
        }

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

    /// <summary>Собирает настройки из полей и закрывает диалог.</summary>
    /// <param name="sender">Кнопка «Начать партию».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        var sizeValue = Sizes[Math.Clamp(SelectedIndex("SizeBox"), 0, Sizes.Length - 1)];
        var level = Levels[Math.Clamp(SelectedIndex("LevelBox"), 0, Levels.Length - 1)];
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
