using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.Services.Updates;
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

    /// <summary>Ответ на вопрос «есть ли модель для доски»: его даёт вызывающий.</summary>
    /// <remarks>
    /// По умолчанию читаются статические <c>App.Evaluator</c>/<c>App.ModelSizes</c> — так вид
    /// работает, когда его создал XAML. Настольное окно и мобильный вид передают ответ модели
    /// представления, чтобы наличие модели считалось в одном месте.
    /// </remarks>
    private Func<BoardSize, bool> _modelAvailable = DefaultModelAvailable;

    /// <summary>Служба обновления: создаётся один раз, <c>null</c> — голова её не настроила.</summary>
    private readonly UpdateService? _updates = global::GoEngine.App.App.CreateUpdateService();

    /// <summary>Найденное обновление: по нему работает кнопка установки.</summary>
    private UpdateCheck? _available;

    /// <summary>Кнопки, которые выключаются на время работы с обновлением.</summary>
    private static readonly string[] UpdateButtons = ["CheckUpdatesButton", "InstallUpdateButton"];

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

        if (this.FindControl<Button>("CheckUpdatesButton") is { } checkButton)
        {
            checkButton.Click += OnCheckUpdatesClick;
        }

        if (this.FindControl<Button>("InstallUpdateButton") is { } installButton)
        {
            installButton.Click += OnInstallUpdateClick;
        }

        if (this.FindControl<TextBlock>("VersionText") is { } versionText)
        {
            versionText.Text = $"Версия: {AppVersion.Current}";
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
    /// <param name="modelAvailable">
    /// Ответ на вопрос «есть ли модель для доски»; <c>null</c> — оставить прежний источник.
    /// </param>
    /// <remarks>
    /// Нужен и конструктору, и тому, кто показывает уже созданный вид повторно: XAML создаёт
    /// <see cref="SettingsView"/> без аргументов, поэтому настройки передаются отдельно.
    /// </remarks>
    public void Initialize(AppSettings current, Func<BoardSize, bool>? modelAvailable = null)
    {
        ArgumentNullException.ThrowIfNull(current);

        _modelAvailable = modelAvailable ?? _modelAvailable;
        Selected = current;

        if (this.FindControl<ComboBox>("SizeBox") is { } sizeBox)
        {
            sizeBox.ItemsSource = BoardSizes.Labels;
            sizeBox.SelectedIndex = Math.Max(0, BoardSizes.IndexOf(current.ToBoardSize()));
        }

        FillLevels(current.ToBoardSize(), current.ToDifficultyLevel());

        if (this.FindControl<ComboBox>("ColorBox") is { } colorBox)
        {
            colorBox.ItemsSource = StoneColorLabels.All;
            colorBox.SelectedIndex = StoneColorLabels.IndexOf(current.ToPlayerColor());
        }

        if (this.FindControl<NumericUpDown>("KomiBox") is { } komiBox)
        {
            komiBox.Value = (decimal)current.Komi;
        }
    }

    /// <summary>Наличие модели по статическим данным приложения: запасной источник ответа.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns><c>true</c>, если сеть загружена и для этого размера нашлась модель.</returns>
    private static bool DefaultModelAvailable(BoardSize size) =>
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
        _levels = LevelListView.ForSettings(size, _modelAvailable(size));

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
        var level = LevelChooser.Resolve(CurrentLevel(), size, _modelAvailable(size));
        var color = StoneColorLabels.At(SelectedIndex("ColorBox"));
        var komi = this.FindControl<NumericUpDown>("KomiBox")?.Value ?? (decimal)Selected.Komi;

        Selected = AppSettings.From(size, level, color, new Komi((double)komi));

        Accepted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Сообщает об отказе от изменений.</summary>
    /// <param name="sender">Кнопка «Отмена».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>Запускает проверку обновления.</summary>
    /// <param name="sender">Кнопка «Проверить обновления».</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Обработчик не <c>async void</c> (<c>AGENTS.md</c>, п. 11): задача запускается отдельно,
    /// а все исходы приходят значениями <see cref="Result{T}"/>, поэтому исключений не бывает.
    /// </remarks>
    private void OnCheckUpdatesClick(object? sender, RoutedEventArgs e) => _ = CheckUpdatesAsync();

    /// <summary>Проверяет обновление и показывает исход игроку.</summary>
    /// <returns>Задача проверки.</returns>
    private async Task CheckUpdatesAsync()
    {
        if (_updates is null)
        {
            ShowUpdateStatus("Проверка обновлений в этой сборке недоступна.");

            return;
        }

        SetUpdateBusy(true, "Проверяю обновление…");

        var result = await _updates.CheckAsync().ConfigureAwait(true);

        SetUpdateBusy(false, null);

        if (!result.IsSuccess)
        {
            _available = null;
            ShowUpdateStatus(result.Error!);

            return;
        }

        var check = result.Value;
        _available = check.IsAvailable ? check : null;

        if (this.FindControl<Button>("InstallUpdateButton") is { } installButton)
        {
            installButton.IsVisible = check.IsAvailable;
        }

        ShowUpdateStatus(check.IsAvailable
            ? $"Доступна версия {check.Version}, установлена {_updates.Current}."
            : $"Установлена самая свежая версия ({_updates.Current}).");
    }

    /// <summary>Скачивает и устанавливает найденное обновление.</summary>
    /// <param name="sender">Кнопка «Скачать и установить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnInstallUpdateClick(object? sender, RoutedEventArgs e) => _ = InstallUpdateAsync();

    /// <summary>Скачивает обновление, проверяет его и передаёт установщику платформы.</summary>
    /// <returns>Задача установки.</returns>
    private async Task InstallUpdateAsync()
    {
        if (_updates is null || _available is not { } check)
        {
            ShowUpdateStatus("Сначала проверьте обновления.");

            return;
        }

        SetUpdateBusy(true, $"Скачиваю обновление {check.Version}…");

        var result = await _updates.DownloadAndInstallAsync(check).ConfigureAwait(true);

        SetUpdateBusy(false, null);
        ShowUpdateStatus(result.IsSuccess ? result.Value! : result.Error!);
    }

    /// <summary>Выключает кнопки обновления на время работы и показывает ход дела.</summary>
    /// <param name="busy">Идёт работа.</param>
    /// <param name="status">Строка состояния или <c>null</c>, чтобы оставить прежнюю.</param>
    private void SetUpdateBusy(bool busy, string? status)
    {
        foreach (var name in UpdateButtons)
        {
            if (this.FindControl<Button>(name) is { } button)
            {
                button.IsEnabled = !busy;
            }
        }

        if (status is not null)
        {
            ShowUpdateStatus(status);
        }
    }

    /// <summary>Показывает строку состояния обновления.</summary>
    /// <param name="text">Текст для игрока.</param>
    private void ShowUpdateStatus(string text)
    {
        if (this.FindControl<TextBlock>("UpdateStatusText") is { } status)
        {
            status.Text = text;
        }
    }

    /// <summary>Возвращает выбранный индекс списка.</summary>
    /// <param name="name">Имя элемента управления.</param>
    /// <returns>Индекс выбранного элемента или 0.</returns>
    private int SelectedIndex(string name) => this.FindControl<ComboBox>(name)?.SelectedIndex ?? 0;
}
