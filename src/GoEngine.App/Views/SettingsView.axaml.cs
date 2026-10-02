using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.AI;
using GoEngine.App.Rendering;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.App.Services.Updates;
using GoEngine.App.ViewModels;
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
    /// <summary>Системы подсчёта в порядке показа: основная (японская) и китайская.</summary>
    /// <remarks>
    /// Список явный: элементы <see cref="Enumeration"/> публично не перечисляются, а порядок
    /// показа должен быть задан здесь, а не зависеть от порядка свойств в типе.
    /// </remarks>
    private static readonly ScoringRule[] ScoringRules = [ScoringRule.Japanese, ScoringRule.Chinese];

    /// <summary>Подписи систем подсчёта для списка выбора: описания берутся у самих правил.</summary>
    private static readonly IReadOnlyList<string> ScoringLabels =
        ScoringRules.Select(rule => rule.Descriptions ?? rule.Name).ToList().AsReadOnly();

    /// <summary>Уровни, показанные сейчас: зависят от доски и наличия модели (D-038).</summary>
    private LevelListView _levels = LevelListView.ForSettings(BoardSize.Size9, false);

    /// <summary>Ответ на вопрос «есть ли модель для доски»: его даёт вызывающий.</summary>
    /// <remarks>
    /// По умолчанию читаются статические <c>App.Evaluator</c>/<c>App.ModelSizes</c> — так вид
    /// работает, когда его создал XAML. Настольное окно и мобильный вид передают ответ модели
    /// представления, чтобы наличие модели считалось в одном месте.
    /// </remarks>
    private Func<BoardSize, bool> _modelAvailable = DefaultModelAvailable;

    /// <summary>Модель обновления: та же, что у приглашения в оболочке.</summary>
    /// <remarks>
    /// Одна на приложение (<c>App.Updates</c>): проверка из настроек и приглашение при запуске
    /// говорят об одном и том же обновлении, а скачивание идёт одно, а не два.
    /// </remarks>
    private readonly UpdateViewModel _updates = global::GoEngine.App.App.Updates;

    /// <summary>Кнопки, которые выключаются на время проверки, скачивания и установки.</summary>
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

        if (this.FindControl<ComboBox>("ScoringBox") is { } scoringBox)
        {
            scoringBox.SelectionChanged += OnScoringChanged;
        }

        if (this.FindControl<CheckBox>("SoundBox") is { } soundBox)
        {
            soundBox.IsCheckedChanged += OnSoundChanged;
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

        if (this.FindControl<Button>("CancelUpdateButton") is { } cancelUpdateButton)
        {
            cancelUpdateButton.Click += OnCancelUpdateClick;
        }

        if (this.FindControl<Button>("OpenLogsButton") is { } logsButton)
        {
            logsButton.Click += OnOpenLogsClick;
        }

        if (this.FindControl<TextBlock>("VersionText") is { } versionText)
        {
            // Показываем версию без хвоста коммита, а полную строку — в подсказке: она нужна
            // для диагностики, но не для игрока. Локальная сборка помечается словом, иначе
            // заводское «1.0.0» выглядит как номер выпуска.
            versionText.Text = $"Версия: {AppVersion.Display}";
            ToolTip.SetTip(versionText, $"Сборка: {AppVersion.InformationalVersion}");
        }

        ShowModels();
        ShowLogs();
        Initialize(current);
    }

    /// <summary>Настройки, выбранные в виде.</summary>
    public AppSettings Selected { get; private set; } = AppSettings.Default;

    /// <summary>Игрок согласился с настройками.</summary>
    public event EventHandler? Accepted;

    /// <summary>Игрок отказался от изменений.</summary>
    public event EventHandler? Cancelled;

    /// <summary>Показывает каталог логов, сообщение о сбое прошлого запуска и кнопку открытия папки.</summary>
    /// <remarks>
    /// Кнопка открытия видна только там, где голова задала обработчик: на Android открывать
    /// нечего, и там игрок видит путь к файлам, который можно переписать или переслать.
    /// Сообщение о сбое живёт до конца сеанса: файл-признак уже прочитан и удалён, поэтому
    /// вечно оно не показывается, а копия лога остаётся в каталоге.
    /// </remarks>
    private void ShowLogs()
    {
        if (this.FindControl<TextBlock>("LogsText") is { } logsText)
        {
            logsText.Text = AppLog.IsFileLogging
                ? $"Логи: {AppLog.Directory}"
                : "Логи: файл недоступен, игра работает без записи";
        }

        if (this.FindControl<TextBlock>("CrashText") is { } crashText && AppLog.PreviousCrash is { } crash)
        {
            crashText.Text = crash.Summary;
            crashText.IsVisible = true;
        }

        if (this.FindControl<Button>("OpenLogsButton") is { } openButton)
        {
            openButton.IsVisible = global::GoEngine.App.App.OpenLogsDirectory is not null;
        }
    }

    /// <summary>Открывает папку с логами средствами платформы.</summary>
    /// <param name="sender">Кнопка открытия.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnOpenLogsClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (global::GoEngine.App.App.OpenLogsDirectory is { } open && AppLog.Directory is { Length: > 0 } directory)
        {
            open(directory);
        }
    }

    /// <summary>Показывает состояние моделей, о котором сообщила голова платформы.</summary>
    /// <remarks>
    /// Состояние заполняет голова: на Android — после копирования моделей из пакета,
    /// на настольных системах — при поиске каталога models. Пусто — голова не сообщала.
    /// </remarks>
    private void ShowModels()
    {
        if (this.FindControl<TextBlock>("ModelsText") is { } modelsText)
        {
            modelsText.Text = global::GoEngine.App.App.ModelsStatus ?? ModelStatusText.Unknown;
        }
    }

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
        ShowModels();
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

        if (this.FindControl<ComboBox>("ScoringBox") is { } scoringBox)
        {
            scoringBox.ItemsSource = ScoringLabels;
            scoringBox.SelectedIndex = Math.Max(0, Array.IndexOf(ScoringRules, current.ToScoringRule()));
        }

        if (this.FindControl<CheckBox>("SoundBox") is { } soundBox)
        {
            soundBox.IsChecked = current.SoundEnabled;
        }

        ShowScoringHint();
    }

    /// <summary>Применяет выбор звука сразу и записывает его в файл настроек.</summary>
    /// <param name="sender">Переключатель «Звук ходов».</param>
    /// <param name="e">Событие смены состояния.</param>
    /// <remarks>
    /// Звук — не свойство партии, а выбор игрока, и действовать он должен сразу: игрок щёлкает
    /// переключатель, чтобы стало тихо, а не чтобы «стало тихо после начала новой партии».
    /// Поэтому выбор применяется здесь же и сразу сохраняется: без записи он терялся при выходе
    /// с экрана кнопкой «Назад» — на телефоне это единственный способ его закрыть, и настройка
    /// возвращалась включённой после перезапуска (проверено живым прогоном 2026-09-30, эмулятор).
    /// Партию это не трогает: применяются только те настройки, что уже действуют.
    /// </remarks>
    private void OnSoundChanged(object? sender, RoutedEventArgs e)
    {
        var enabled = this.FindControl<CheckBox>("SoundBox")?.IsChecked != false;

        Selected = Selected with { SoundEnabled = enabled };
        StoneSoundPlayer.SoundEnabled = enabled;

        // Неудачная запись не мешает играть: выбор уже действует в этой партии.
        _ = SettingsStore.Save(Selected);
    }

    /// <summary>Показывает пояснение к выбранной системе подсчёта.</summary>
    private void ShowScoringHint()
    {
        if (this.FindControl<TextBlock>("ScoringHintText") is { } hint)
        {
            hint.Text = ScoringRules[SelectedIndex("ScoringBox")].Descriptions ?? string.Empty;
        }
    }

    /// <summary>Обновляет пояснение при смене системы подсчёта.</summary>
    /// <param name="sender">Список систем.</param>
    /// <param name="e">Событие смены выбора.</param>
    private void OnScoringChanged(object? sender, SelectionChangedEventArgs e) => ShowScoringHint();

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

        var sound = this.FindControl<CheckBox>("SoundBox")?.IsChecked != false;

        Selected = AppSettings.From(
            size,
            level,
            color,
            new Komi((double)komi),
            ScoringRules[SelectedIndex("ScoringBox")],
            sound);

        Accepted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Сообщает об отказе от изменений.</summary>
    /// <param name="sender">Кнопка «Отмена».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>Подписывается на модель обновления, пока вид показан.</summary>
    /// <param name="e">Признак подключения к дереву видов.</param>
    /// <remarks>
    /// Подписка ставится и снимается вместе с показом: окно настроек создаётся заново на каждое
    /// открытие, а модель обновления живёт всё приложение — без снятия подписки она держала бы
    /// закрытые виды.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Снятие перед подпиской: повторное подключение того же вида не должно удваивать
        // обработчик — иначе на каждое изменение состояния полоса обновлялась бы дважды.
        _updates.PropertyChanged -= OnUpdatesChanged;
        _updates.PropertyChanged += OnUpdatesChanged;

        RefreshUpdates();
    }

    /// <summary>Снимает подписку на модель обновления.</summary>
    /// <param name="e">Признак отключения от дерева видов.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _updates.PropertyChanged -= OnUpdatesChanged;

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Показывает изменившееся состояние обновления.</summary>
    /// <param name="sender">Модель обновления.</param>
    /// <param name="e">Имя изменившегося свойства; не используется.</param>
    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => RefreshUpdates();

    /// <summary>Запускает проверку обновления.</summary>
    /// <param name="sender">Кнопка «Проверить обновления».</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Обработчик не <c>async void</c> (<c>AGENTS.md</c>, п. 11): задача запускается отдельно,
    /// а ход дела и исход показывает модель обновления — та же, что и у приглашения в оболочке.
    /// </remarks>
    private void OnCheckUpdatesClick(object? sender, RoutedEventArgs e) => _ = _updates.CheckAsync();

    /// <summary>Скачивает и устанавливает найденное обновление.</summary>
    /// <param name="sender">Кнопка «Скачать и установить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnInstallUpdateClick(object? sender, RoutedEventArgs e) => _ = _updates.DownloadAndInstallAsync();

    /// <summary>Останавливает скачивание обновления.</summary>
    /// <param name="sender">Кнопка «Отмена».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnCancelUpdateClick(object? sender, RoutedEventArgs e) => _updates.Cancel();

    /// <summary>Показывает состояние обновления: строку, полосу хода и кнопки.</summary>
    /// <remarks>
    /// Ничего не решает: состояние целиком берётся у модели обновления. Иначе проценты в настройках
    /// и в приглашении считались бы двумя разными способами и расходились.
    /// </remarks>
    private void RefreshUpdates()
    {
        if (this.FindControl<TextBlock>("UpdateStatusText") is { } status)
        {
            status.Text = _updates.StatusText;
        }

        if (this.FindControl<ProgressBar>("UpdateProgressBar") is { } bar)
        {
            // Установку по шагам не измерить: полоса идёт сама, а не показывает выдуманные проценты.
            bar.IsIndeterminate = _updates.IsProgressIndeterminate;
            bar.Value = _updates.Percent;
            bar.IsVisible = _updates.IsProgressVisible;
        }

        if (this.FindControl<Button>("InstallUpdateButton") is { } installButton)
        {
            installButton.IsVisible = _updates.IsUpdateNowVisible;
        }

        if (this.FindControl<Button>("CancelUpdateButton") is { } cancelButton)
        {
            cancelButton.IsVisible = _updates.IsCancelVisible;
        }

        var busy = _updates.Stage == UpdateStage.Checking
            || _updates.Stage == UpdateStage.Downloading
            || _updates.Stage == UpdateStage.Installing;

        foreach (var name in UpdateButtons)
        {
            if (this.FindControl<Button>(name) is { } button)
            {
                button.IsEnabled = !busy;
            }
        }
    }

    /// <summary>Возвращает выбранный индекс списка.</summary>
    /// <param name="name">Имя элемента управления.</param>
    /// <returns>Индекс выбранного элемента или 0.</returns>
    private int SelectedIndex(string name) => this.FindControl<ComboBox>(name)?.SelectedIndex ?? 0;
}
