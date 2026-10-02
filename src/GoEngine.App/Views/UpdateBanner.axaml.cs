using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Приглашение обновления: доступная версия, ход скачивания и установки.</summary>
/// <remarks>
/// <para>
/// Вид не знает ни про сеть, ни про платформу: он получает модель представления и показывает
/// готовые строки, проценты и кнопки, а те зовут её методы. Поэтому одна и та же карточка
/// работает и в настольном окне, и на телефоне, а установку выполняет платформенный установщик.
/// </para>
/// <para>
/// Разметка создаёт карточку конструктором без параметров — передать ей модель нечем, поэтому
/// модель подключает владелец вида методом <see cref="Attach"/>. Без подключения карточка молчит,
/// а её кнопки мертвы.
/// </para>
/// </remarks>
public sealed partial class UpdateBanner : UserControl
{
    /// <summary>Класс крупной кнопки для телефона: в настольной раскладке его нет.</summary>
    private const string TouchClass = "touch";

    /// <summary>Кнопки карточки: на телефоне всем им нужен размер пальца.</summary>
    private static readonly string[] ActionButtons = ["CancelButton", "LaterButton", "UpdateNowButton"];

    /// <summary>Подключённая модель; <c>null</c>, пока владелец вида не подключил карточку.</summary>
    private UpdateViewModel? _viewModel;

    /// <summary>Создаёт карточку без модели: так её создаёт разметка.</summary>
    public UpdateBanner() => AvaloniaXamlLoader.Load(this);

    /// <summary>Создаёт карточку, сразу подключённую к модели обновления.</summary>
    /// <param name="viewModel">Модель обновления.</param>
    public UpdateBanner(UpdateViewModel viewModel)
        : this() => Attach(viewModel);

    /// <summary>Раскладка мобильная: кнопки карточки становятся крупнее.</summary>
    /// <remarks>
    /// Класс ставит оболочка при смене раскладки: карточка одна на обе раскладки, а размер цели
    /// нажатия у них разный (48 точек на телефоне против 38 на настольной версии).
    /// </remarks>
    public bool IsTouch
    {
        get => this.FindControl<Button>("UpdateNowButton")?.Classes.Contains(TouchClass) == true;
        set
        {
            foreach (var name in ActionButtons)
            {
                if (this.FindControl<Button>(name) is { } button)
                {
                    button.Classes.Set(TouchClass, value);
                }
            }
        }
    }

    /// <summary>Подключает карточку к модели обновления.</summary>
    /// <param name="viewModel">Модель обновления; не <c>null</c>.</param>
    /// <remarks>
    /// Повторный вызов безопасен: прежняя подписка снимается, новая ставится ровно одна — иначе
    /// одно нажатие запускало бы две загрузки.
    /// </remarks>
    public void Attach(UpdateViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        Detach();

        _viewModel = viewModel;

        Wire("UpdateNowButton", OnUpdateNowClick);
        Wire("LaterButton", OnLaterClick);
        Wire("CancelButton", OnCancelClick);

        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Refresh();
    }

    /// <summary>Показывает карточку по текущему состоянию модели.</summary>
    /// <remarks>
    /// Карточка читает готовые свойства и ничего не решает сама: правила «что видно на этом этапе»
    /// живут в модели, поэтому их проверяют тестами.
    /// </remarks>
    public void Refresh()
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        IsVisible = viewModel.IsBannerVisible;

        if (this.FindControl<TextBlock>("HeadlineText") is { } headline)
        {
            headline.Text = viewModel.Headline;
        }

        if (this.FindControl<TextBlock>("StatusText") is { } status)
        {
            status.Text = viewModel.StatusLine;
            status.IsVisible = viewModel.HasStatus;
        }

        if (this.FindControl<ProgressBar>("ProgressBar") is { } bar)
        {
            // Установку по шагам не измерить: полоса идёт сама, а не показывает выдуманные проценты.
            bar.IsIndeterminate = viewModel.IsProgressIndeterminate;
            bar.Value = viewModel.Percent;
            bar.IsVisible = viewModel.IsProgressVisible;
        }

        Show("UpdateNowButton", viewModel.IsUpdateNowVisible);
        Show("CancelButton", viewModel.IsCancelVisible);
        Show("LaterButton", viewModel.IsDismissVisible);

        if (this.FindControl<Button>("LaterButton") is { } later)
        {
            later.Content = viewModel.DismissLabel;
        }
    }

    /// <summary>Снимает подписки прежнего подключения.</summary>
    private void Detach()
    {
        if (_viewModel is { } previous)
        {
            previous.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Обработчики снимаются теми же методами, которыми ставились: лямбда не дала бы снять
        // подписку, и после повторного Attach одно нажатие запускало бы две загрузки.
        Unwire("UpdateNowButton", OnUpdateNowClick);
        Unwire("LaterButton", OnLaterClick);
        Unwire("CancelButton", OnCancelClick);

        _viewModel = null;
    }

    /// <summary>Обновляет карточку по любому изменению модели: что важно — решает она сама.</summary>
    /// <param name="sender">Модель представления.</param>
    /// <param name="e">Имя изменившегося свойства; не используется.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    /// <summary>Скачивает и устанавливает обновление.</summary>
    /// <param name="sender">Кнопка «Обновить сейчас».</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Обработчик не <c>async void</c> (<c>AGENTS.md</c>, п. 11): задача запускается отдельно,
    /// а ход дела виден на самой карточке.
    /// </remarks>
    private void OnUpdateNowClick(object? sender, RoutedEventArgs e) => _ = _viewModel?.DownloadAndInstallAsync();

    /// <summary>Убирает приглашение до следующего запуска.</summary>
    /// <param name="sender">Кнопка «Позже» или «Скрыть».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLaterClick(object? sender, RoutedEventArgs e) => _viewModel?.Later();

    /// <summary>Останавливает скачивание.</summary>
    /// <param name="sender">Кнопка «Отмена».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => _viewModel?.Cancel();

    /// <summary>Показывает или прячет кнопку.</summary>
    /// <param name="name">Имя кнопки в разметке.</param>
    /// <param name="visible">Показывать ли её.</param>
    private void Show(string name, bool visible)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.IsVisible = visible;
        }
    }

    /// <summary>Подписывает кнопку на обработчик нажатия.</summary>
    /// <param name="name">Имя кнопки в разметке.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void Wire(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += handler;
        }
    }

    /// <summary>Снимает обработчик нажатия.</summary>
    /// <param name="name">Имя кнопки в разметке.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void Unwire(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click -= handler;
        }
    }
}
