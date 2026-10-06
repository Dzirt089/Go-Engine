using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.AI;
using GoEngine.App.Rendering;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Views;

/// <summary>Настройка партии двумя подразделами: чем играем и как считаются очки.</summary>
/// <remarks>
/// <para>
/// Вид не хранит настройки сам: он собирает их из полей (<see cref="Collect"/>) и сообщает
/// о согласии событием <see cref="Accepted"/>. Кто показывает этот вид — решает вызывающий:
/// настольное окно <see cref="SettingsWindow"/> или мобильный экран во всю высоту
/// (<see cref="BoardView"/>). Логика выбора доски и уровня одна на всех.
/// </para>
/// <para>
/// Подраздела «О программе» здесь нет: сведения о программе — не настройка партии, и они
/// переехали на отдельный экран <see cref="AboutView"/>, который на телефоне открывается
/// из общего меню, а в окне — пунктом «Справка» (замечание пользователя 2026-10-06, D-075).
/// Кнопки «Начать партию» здесь тоже нет (жалоба 2026-10-03): настройки применяются при закрытии
/// экрана, а начинает партию тот, кто экран открыл, — на телефоне разницу держит <see cref="BoardView"/>.
/// </para>
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

        // Низ экрана: «Сохранить» применяет выбор, «Отмена» закрывает настройки без изменений.
        // Решение принимает хозяин вида — он один знает, начинать ли партию (жалоба 2026-10-03).
        if (this.FindControl<Button>("SaveButton") is { } saveButton)
        {
            saveButton.Click += OnSaveClick;
        }

        if (this.FindControl<Button>("CancelButton") is { } cancelButton)
        {
            cancelButton.Click += OnCancelClick;
        }

        ShowScoringSystems();
        Initialize(current);
    }

    /// <summary>Настройки, выбранные в виде.</summary>
    public AppSettings Selected { get; private set; } = AppSettings.Default;

    /// <summary>Игрок закончил настройку: значения собраны, о согласии сообщено.</summary>
    /// <remarks>
    /// Событие поднимает <see cref="Accept"/> — его зовут хозяева вида: мобильный экран по кнопке
    /// «Готово» и настольное окно при закрытии. Своей кнопки у вида нет: одно место решения,
    /// а не две кнопки в разных углах экрана.
    /// </remarks>
    public event EventHandler? Accepted;

    /// <summary>Игрок отказался от изменений: экран закрывается, ничего не применяя.</summary>
    /// <remarks>
    /// Нужен ровно затем, чтобы у «Сохранить» была пара: без второго выхода игрок либо применял
    /// случайный выбор, либо не мог закрыть экран (жалоба пользователя 2026-10-03).
    /// </remarks>
    public event EventHandler? Cancelled;

    /// <summary>Игрок сменил систему подсчёта: счёт и победителя называет она.</summary>
    /// <remarks>
    /// <para>
    /// Отдельное событие, а не молчаливое изменение <see cref="Selected"/>: правило — не свойство
    /// партии, оно действует сразу, и хозяин вида обязан применить его к текущей партии
    /// (жалоба пользователя 2026-10-03). Звук вид применяет сам: он тоже не свойство партии.
    /// </para>
    /// <para>
    /// Выбранное правило идёт в самом событии, а не читается хозяином из вида повторно: так
    /// у правила одна точка правды — список, из которого его выбрал игрок (H1: настольное окно
    /// показывает свой экземпляр вида, и до этой правки выбор до партии не доходил).
    /// </para>
    /// </remarks>
    public event EventHandler<ScoringRule>? ScoringRuleChanged;

    /// <summary>Подписывает смену системы подсчёта на партию: одно место для обоих хозяев вида.</summary>
    /// <param name="settings">Вид настроек: источник выбора.</param>
    /// <param name="game">Партия: правило применяется к ней сразу.</param>
    /// <remarks>
    /// Хозяев двое — мобильный вид партии и настольное окно, — и оба обязаны применить правило
    /// к текущей партии одинаково (H1: до этой правки настольное окно показывало свой экземпляр
    /// вида, и выбор до партии не доходил). Логика применения живёт здесь одна, а не строкой
    /// в каждом хозяине; тест проверяет её на виде настроек, потому что окно без оконной платформы
    /// в тестах не построить, а живьём его смотрит независимая проверка.
    /// </remarks>
    public static void ApplyScoringRuleOnChange(SettingsView settings, MainViewModel game)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(game);

        settings.ScoringRuleChanged += (_, rule) => game.ScoringRule = rule;
    }

    /// <summary>Правило подсчёта, выбранное сейчас в списке.</summary>
    /// <remarks>
    /// Нужно тому, кто показывает вид: правило вступает в силу сразу, а не с новой партией,
    /// поэтому о смене сообщается событием <see cref="ScoringRuleChanged"/>.
    /// </remarks>
    public ScoringRule SelectedScoringRule => ScoringRules[SelectedIndex("ScoringBox")];

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

    /// <summary>Применяет выбор звука сразу: игрок щёлкает переключатель, чтобы стало тихо.</summary>
    /// <param name="sender">Переключатель «Звук ходов».</param>
    /// <param name="e">Событие смены состояния.</param>
    /// <remarks>
    /// Звук — не свойство партии, а выбор игрока, и действует он сразу, не дожидаясь «Сохранить».
    /// В файл выбор попадает при сохранении настроек, а «Отмена» возвращает прежний: у экрана
    /// теперь два явных выхода, и терять выбор при закрытии негде (жалоба пользователя 2026-10-03).
    /// </remarks>
    private void OnSoundChanged(object? sender, RoutedEventArgs e)
    {
        var enabled = this.FindControl<CheckBox>("SoundBox")?.IsChecked != false;

        Selected = Selected with { SoundEnabled = enabled };
        StoneSoundPlayer.SoundEnabled = enabled;
    }

    /// <summary>Показывает пояснения по обеим системам подсчёта и чем они отличаются.</summary>
    /// <remarks>
    /// Формулировки берутся у самих правил (<c>ScoringRule.Descriptions</c>) и из подсказки
    /// модели представления (<c>MainViewModel.ScoringHint</c>), которая опирается на
    /// <c>GO_RULES.md</c>, пп. 7–9: своих правил вид не придумывает — иначе объяснение разошлось бы
    /// с подсчётом.
    /// </remarks>
    private void ShowScoringSystems()
    {
        if (this.FindControl<TextBlock>("JapaneseText") is { } japanese)
        {
            japanese.Text = ScoringRule.Japanese.Descriptions ?? ScoringRule.Japanese.Name;
        }

        if (this.FindControl<TextBlock>("ChineseText") is { } chinese)
        {
            chinese.Text = ScoringRule.Chinese.Descriptions ?? ScoringRule.Chinese.Name;
        }

        if (this.FindControl<TextBlock>("DifferenceText") is { } difference)
        {
            difference.Text =
                "Территория в обеих системах считается по доске без мёртвых камней. "
                + "Китайская считает площадь: свои камни на доске плюс территория, пленные в очках "
                + "не участвуют. Японская считает территорию и пленные, а камень, снятый как мёртвый, "
                + "приносит очко как пленный. Коми в обеих системах добавляется белым.";
        }
    }

    /// <summary>Показывает пояснение к выбранной системе подсчёта.</summary>
    private void ShowScoringHint()
    {
        if (this.FindControl<TextBlock>("ScoringHintText") is { } hint)
        {
            hint.Text = ScoringRules[SelectedIndex("ScoringBox")].Descriptions ?? string.Empty;
        }
    }

    /// <summary>Обновляет пояснение и сразу применяет смену системы подсчёта.</summary>
    /// <param name="sender">Список систем.</param>
    /// <param name="e">Событие смены выбора.</param>
    /// <remarks>
    /// Правило подсчёта действует немедленно: игрок переключает систему и ждёт, что счёт
    /// и победитель назовутся по новой. Как и звук, выбор сохраняется сразу; партия при этом
    /// не пересоздаётся — правило лишь выбирает, по какой из двух посчитанных величин называется
    /// победитель. Заполнение списка при открытии экрана сменой не считается.
    /// </remarks>
    private void OnScoringChanged(object? sender, SelectionChangedEventArgs e)
    {
        ShowScoringHint();

        var rule = SelectedScoringRule;

        if (rule == Selected.ToScoringRule())
        {
            return;
        }

        Selected = Selected with { ScoringRule = rule.Name };

        // В файл выбор попадёт при закрытии экрана: там его сохраняют и остальные настройки,
        // а тест не должен переписывать настройки игрока на каждой смене списка.
        ScoringRuleChanged?.Invoke(this, rule);
    }

    /// <summary>Нажимает «Сохранить»: значения собраны, о согласии сообщено.</summary>
    /// <param name="sender">Кнопка «Сохранить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSaveClick(object? sender, RoutedEventArgs e) => Accept();

    /// <summary>Нажимает «Отмена»: экран закрывается, ничего не применяя.</summary>
    /// <param name="sender">Кнопка «Отмена».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancel();

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

    /// <summary>Собирает настройки из полей вида.</summary>
    /// <returns>Настройки, выбранные сейчас.</returns>
    /// <remarks>
    /// Отдельно от <see cref="Accept"/>: мобильный экран, открытый из раздела «Настройки»,
    /// сохраняет выбор, но не пересоздаёт партию, — ему нужны значения без согласия.
    /// </remarks>
    public AppSettings Collect()
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

        return Selected;
    }

    /// <summary>Собирает настройки и сообщает о согласии: так работает кнопка «Сохранить».</summary>
    public void Accept()
    {
        _ = Collect();
        Accepted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Сообщает об отказе: так работает кнопка «Отмена».</summary>
    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>Показывает подраздел «Партия».</summary>
    /// <remarks>С него начинается выбор перед новой партией: «Новая партия» в меню открывает
    /// настройки именно на нём.</remarks>
    public void SelectGameTab()
    {
        if (this.FindControl<TabControl>("SettingsTabs") is { } tabs)
        {
            tabs.SelectedIndex = 0;
        }
    }

    /// <summary>Возвращает выбранный индекс списка.</summary>
    /// <param name="name">Имя элемента управления.</param>
    /// <returns>Индекс выбранного элемента или 0.</returns>
    private int SelectedIndex(string name) => this.FindControl<ComboBox>(name)?.SelectedIndex ?? 0;
}
