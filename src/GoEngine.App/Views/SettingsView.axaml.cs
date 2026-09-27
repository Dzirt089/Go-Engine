using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Views;

/// <summary>Настройки партии: размер доски, уровень AI, цвет игрока и коми.</summary>
/// <remarks>
/// Вид не хранит настройки сам: он собирает их из полей и сообщает о согласии событием
/// <see cref="Accepted"/> (готовые значения — в <see cref="Selected"/>). Кто показывает этот
/// вид — решает вызывающий: настольное окно <see cref="SettingsWindow"/> или мобильный вид
/// <see cref="BoardView"/> поверх доски. Логика выбора доски и уровня одна на всех.
/// </remarks>
public sealed partial class SettingsView : UserControl
{
    /// <summary>Уровни, показанные сейчас: зависят от доски и наличия модели (D-038).</summary>
    private LevelListView _levels = LevelListView.ForSettings(BoardSize.Size9, false);

    /// <summary>Создаёт вид настроек со значениями по умолчанию.</summary>
    public SettingsView() : this(AppSettings.Default)
    {
    }

    /// <summary>Создаёт вид настроек с текущими значениями.</summary>
    /// <param name="current">Текущие настройки партии.</param>
    public SettingsView(AppSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);

        AvaloniaXamlLoader.Load(this);

        if (this.FindControl<ComboBox>("SizeBox") is { } sizeBox)
        {
            sizeBox.SelectionChanged += OnSizeChanged;
        }

        if (this.FindControl<Button>("StartButton") is { } startButton)
        {
            startButton.Click += OnStartClick;
        }

        if (this.FindControl<Button>("CancelButton") is { } cancelButton)
        {
            cancelButton.Click += OnCancelClick;
        }

        Initialize(current);
    }

    /// <summary>Настройки, выбранные в виде.</summary>
    public AppSettings Selected { get; private set; } = AppSettings.Default;

    /// <summary>Игрок согласился с настройками.</summary>
    public event EventHandler? Accepted;

    /// <summary>Игрок отказался от изменений.</summary>
    public event EventHandler? Cancelled;

    /// <summary>Заполняет поля значениями настроек.</summary>
    /// <param name="current">Настройки партии.</param>
    /// <remarks>
    /// Нужен и конструктору, и тому, кто показывает уже созданный вид повторно: XAML создаёт
    /// <see cref="SettingsView"/> без аргументов, поэтому настройки передаются отдельно.
    /// </remarks>
    public void Initialize(AppSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);

        Selected = current;

        if (this.FindControl<ComboBox>("SizeBox") is { } sizeBox)
        {
            sizeBox.ItemsSource = BoardSizes.Labels;
            sizeBox.SelectedIndex = Math.Max(0, BoardSizes.IndexOf(current.ToBoardSize()));
        }

        FillLevels(current.ToBoardSize(), current.ToDifficultyLevel());

        if (this.FindControl<ComboBox>("ColorBox") is { } colorBox)
        {
            colorBox.ItemsSource = new List<string> { "Чёрные", "Белые" };
            colorBox.SelectedIndex = current.ToPlayerColor() == StoneColor.Black ? 0 : 1;
        }

        if (this.FindControl<NumericUpDown>("KomiBox") is { } komiBox)
        {
            komiBox.Value = (decimal)current.Komi;
        }
    }

    /// <summary>Есть ли модель для доски: без неё уровни Дан не предлагаются.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns><c>true</c>, если сеть загружена и для этого размера нашлась модель.</returns>
    private static bool ModelAvailableFor(BoardSize size) =>
        global::GoEngine.App.App.Evaluator is not null && global::GoEngine.App.App.ModelSizes.Contains(size.Value);

    /// <summary>Перестраивает список уровней и подставляет правило коми при смене размера доски.</summary>
    /// <param name="sender">Список размеров.</param>
    /// <param name="e">Событие смены выбора.</param>
    /// <remarks>
    /// Если выбранный уровень для новой доски недоступен (уровень Дан без модели), выбор
    /// сбрасывается на 10 кю: партия не должна начинаться с уровня, который не сможет играть.
    /// Коми пересчитывается по правилу для новой доски: раньше смена размера его не трогала,
    /// и в настройки уезжала пара «9×9 + 7.5» от 19×19 (D-051).
    /// </remarks>
    private void OnSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        var size = SelectedSize();

        FillLevels(size, CurrentLevel());
        ApplyKomiRule(size);
    }

    /// <summary>Подставляет правило коми для выбранного размера доски.</summary>
    /// <param name="size">Размер доски.</param>
    /// <remarks>
    /// Вызывается только на смену размера игроком. При открытии окна поле коми заполняется
    /// из сохранённых настроек (см. <see cref="Initialize"/>) — иначе осознанно выставленное
    /// игроком значение затиралось бы правилом.
    /// </remarks>
    private void ApplyKomiRule(BoardSize size)
    {
        if (this.FindControl<NumericUpDown>("KomiBox") is { } komiBox)
        {
            komiBox.Value = (decimal)BoardSizes.KomiFor(size).Value;
        }
    }

    /// <summary>Заполняет список уровней для доски.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="preferred">Желаемый уровень; недоступный заменяется на уровень сброса.</param>
    private void FillLevels(BoardSize size, DifficultyLevel preferred)
    {
        _levels = LevelListView.ForSettings(size, ModelAvailableFor(size));

        if (this.FindControl<ComboBox>("LevelBox") is not { } levelBox)
        {
            return;
        }

        levelBox.ItemsSource = _levels.Labels;
        levelBox.SelectedIndex = _levels.IndexOfAvailable(preferred);
    }

    /// <summary>Возвращает выбранный сейчас уровень.</summary>
    /// <returns>Уровень из списка или уровень сброса.</returns>
    private DifficultyLevel CurrentLevel() => _levels.At(SelectedIndex("LevelBox"));

    /// <summary>Возвращает выбранный размер доски.</summary>
    /// <returns>Размер доски.</returns>
    private BoardSize SelectedSize() => BoardSizes.At(SelectedIndex("SizeBox"));

    /// <summary>Собирает настройки из полей и сообщает о согласии.</summary>
    /// <param name="sender">Кнопка «Начать партию».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        var size = SelectedSize();
        var level = LevelChooser.Resolve(CurrentLevel(), size, ModelAvailableFor(size));
        var color = SelectedIndex("ColorBox") == 1 ? StoneColor.White : StoneColor.Black;
        var komi = this.FindControl<NumericUpDown>("KomiBox")?.Value ?? (decimal)Selected.Komi;

        Selected = AppSettings.From(size, level, color, new Komi((double)komi));

        Accepted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Сообщает об отказе от изменений.</summary>
    /// <param name="sender">Кнопка «Отмена».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>Возвращает выбранный индекс списка.</summary>
    /// <param name="name">Имя элемента управления.</param>
    /// <returns>Индекс выбранного элемента или 0.</returns>
    private int SelectedIndex(string name) => this.FindControl<ComboBox>(name)?.SelectedIndex ?? 0;
}
