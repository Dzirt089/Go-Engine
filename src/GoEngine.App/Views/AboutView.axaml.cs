using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.App.Services.Updates;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Экран «О программе»: версия, модели, логи, сбой прошлого запуска и обновление.</summary>
/// <remarks>
/// <para>
/// Вид один на оба хозяина: на телефоне его показывает оболочка вложенным экраном раздела «Меню»
/// (<see cref="ShellView"/>), в настольном окне — диалог <see cref="AboutWindow"/>. Сведений о
/// программе партия не касается, поэтому в настройках этого подраздела больше нет
/// (замечание пользователя 2026-10-06).
/// </para>
/// <para>
/// Состояние обновления берётся у общей модели (<c>App.Updates</c>) — той же, что показывает
/// приглашение в оболочке: иначе проценты в двух местах считались бы по-разному.
/// </para>
/// </remarks>
public sealed partial class AboutView : UserControl
{
    /// <summary>Кнопки, которые выключаются на время проверки, скачивания и установки.</summary>
    private static readonly string[] UpdateButtons = ["CheckUpdatesButton", "InstallUpdateButton"];

    /// <summary>Модель обновления: та же, что у приглашения в оболочке.</summary>
    private readonly UpdateViewModel _updates = global::GoEngine.App.App.Updates;

    /// <summary>Создаёт экран «О программе» и показывает текущее состояние.</summary>
    public AboutView()
    {
        AvaloniaXamlLoader.Load(this);

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

        Refresh();
    }

    /// <summary>Обновляет сведения о сборке, моделях и логах.</summary>
    /// <remarks>
    /// Нужно и конструктору, и тому, кто показывает уже созданный вид: оболочка создаёт экран один
    /// раз при запуске, а модели к этому моменту могут быть ещё не загружены. Поэтому сведения
    /// перечитываются при каждом показе — как это делал экран настроек.
    /// </remarks>
    public void Refresh()
    {
        ShowVersion();
        ShowModels();
        ShowLogs();
        RefreshUpdates();
    }

    /// <summary>Показывает номер сборки и её полное имя в подсказке.</summary>
    /// <remarks>
    /// Показываем версию без хвоста коммита, а полную строку — в подсказке: она нужна
    /// для диагностики, но не для игрока. Локальная сборка помечается словом, иначе
    /// заводское «1.0.0» выглядит как номер выпуска.
    /// </remarks>
    private void ShowVersion()
    {
        if (this.FindControl<TextBlock>("VersionText") is { } versionText)
        {
            versionText.Text = $"Версия: {AppVersion.Display}";
            ToolTip.SetTip(versionText, $"Сборка: {AppVersion.InformationalVersion}");
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

    /// <summary>Показывает каталог логов, сообщение о сбое прошлого запуска и кнопку открытия папки.</summary>
    /// <remarks>
    /// Кнопка открытия видна только там, где голова задала обработчик: на Android открывать
    /// нечего, и там игрок видит путь к файлам, который можно переписать или переслать.
    /// Сообщение о сбое живёт до конца сеанса: файл-признак уже прочитан и удалён, поэтому
    /// вечно оно не показывается, а копия лога остаётся в каталоге.
    /// </remarks>
    /// <summary>Пояснение к пути логов: на телефоне папка лежит на внешнем носителе.</summary>
    /// <remarks>
    /// Без пояснения путь <c>Android/data/…</c> выглядит как служебный, и игрок не понимает,
    /// куда идти за файлом (жалоба 2026-10-10: «записанный путь логов не существует на телефоне»).
    /// </remarks>
    private static string LogHint => OperatingSystem.IsAndroid()
        ? " (папка приложения на телефоне: Android/data/com.goengine.app/files/logs)"
        : string.Empty;

    private void ShowLogs()
    {
        if (this.FindControl<TextBlock>("LogsText") is { } logsText)
        {
            logsText.Text = AppLog.IsFileLogging
                ? $"Логи: {AppLog.Directory}{LogHint}"
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

    /// <summary>Подписывается на модель обновления, пока вид показан.</summary>
    /// <param name="e">Признак подключения к дереву видов.</param>
    /// <remarks>
    /// Подписка ставится и снимается вместе с показом: диалог в настольном окне создаётся заново
    /// на каждое открытие, а модель обновления живёт всё приложение — без снятия подписки она
    /// держала бы закрытые виды.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Снятие перед подпиской: повторное подключение того же вида не должно удваивать
        // обработчик — иначе на каждое изменение состояния полоса обновлялась бы дважды.
        _updates.PropertyChanged -= OnUpdatesChanged;
        _updates.PropertyChanged += OnUpdatesChanged;

        Refresh();
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
    /// Ничего не решает: состояние целиком берётся у модели обновления. Иначе проценты на этом
    /// экране и в приглашении считались бы двумя разными способами и расходились.
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
}
