using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Баннер итога партии: карточка по центру экрана называет исход и предлагает новую партию.</summary>
/// <remarks>
/// <para>
/// Итог виден и в строке состояния, но её легко не заметить: на настольной версии она стоит
/// среди прочих строк панели, на телефоне — в свёрнутой шторке. Баннер выносит исход отдельно
/// и красит его по признаку «выиграл / проиграл / ничья»: цвет победителя игроку ничего не
/// говорит — ему важно, выиграл он сам или нет (жалоба 2026-09-30).
/// </para>
/// <para>
/// Карточка стоит оверлеем поверх вида, по центру экрана, и одна на обе раскладки: полосой сверху
/// она на телефоне оказывалась отдельной строкой сетки над доской и уезжала от взгляда
/// (жалоба пользователя 2026-10-02). Раскладка меняет только размер кнопок
/// (<see cref="IsTouch"/>). «Закрыть» прячет карточку, чтобы игрок посмотрел итоговую доску;
/// следующий завершённый итог показывает её снова.
/// </para>
/// <para>
/// Вид не знает про партию: он получает модель представления и показывает готовые строки.
/// Разметка создаёт баннер конструктором без параметров — передать ей модель нечем, поэтому
/// модель подключает владелец вида методом <see cref="Attach"/>. Без подключения баннер молчит,
/// а его кнопки мертвы: итог не доходил до экрана ни на настольной версии, ни на телефоне
/// (регрессия D-065 §6).
/// </para>
/// </remarks>
public sealed partial class GameResultBanner : UserControl
{
    /// <summary>Классы цвета баннера: ставятся по признаку <see cref="GameTone"/>.</summary>
    private static readonly (GameTone Tone, string Class)[] ToneClasses =
    [
        (GameTone.Win, "result-win"),
        (GameTone.Loss, "result-loss"),
        (GameTone.Draw, "result-draw")
    ];

    /// <summary>Класс крупной кнопки для телефона: в настольной раскладке его нет.</summary>
    private const string TouchClass = "touch";

    /// <summary>Кнопки карточки: на телефоне всем им нужен размер пальца.</summary>
    private static readonly string[] ActionButtons = ["NewGameButton", "CloseButton"];

    /// <summary>Подключённая модель представления; <c>null</c>, пока владелец вида не подключил баннер.</summary>
    private MainViewModel? _viewModel;

    /// <summary>Итог, при котором игрок закрыл карточку; <c>null</c> — карточка не закрыта.</summary>
    /// <remarks>
    /// <para>
    /// Итог партии — не сообщение, которое можно потерять: игрок обязан посмотреть заключительную
    /// позицию и разметку, а карточка закрывает их собой. Поэтому «Закрыть» прячет карточку,
    /// но не отменяет итог.
    /// </para>
    /// <para>
    /// Запоминается сам итог, а не признак «показана/скрыта»: карточка молчит, пока итог тот же,
    /// и возвращается, когда он сменился (игрок начал и завершил новую партию, загрузил партию
    /// с другим результатом). Сравнение по тексту устойчиво к перерисовке и смене раскладки —
    /// они итог не меняют и карточку не возвращают.
    /// </para>
    /// </remarks>
    private string? _dismissedOutcome;

    /// <summary>Создаёт баннер без модели: так его создаёт XAML.</summary>
    /// <remarks>
    /// Модель приходит позже — через <see cref="Attach"/> или конструктор с параметром:
    /// у разметки нет способа передать аргумент.
    /// </remarks>
    public GameResultBanner() => AvaloniaXamlLoader.Load(this);

    /// <summary>Создаёт баннер, сразу подключённый к партии.</summary>
    /// <param name="viewModel">Модель представления партии: из неё берутся строки итога.</param>
    /// <remarks>
    /// Модель подписывается на изменения: баннер появляется и исчезает вместе с итогом партии,
    /// а не по отдельной команде вида. Иначе каждый путь (ход, отмена, новая партия, загрузка
    /// файла) должен был бы помнить о баннере.
    /// </remarks>
    public GameResultBanner(MainViewModel viewModel)
        : this()
    {
        Attach(viewModel);
    }

    /// <summary>Подключает баннер к модели представления партии.</summary>
    /// <param name="viewModel">Модель представления партии; не <c>null</c>.</param>
    /// <remarks>
    /// Экземпляры баннера создаёт разметка — конструктором без параметров, поэтому подписку
    /// делает владелец вида сразу после <c>FindControl</c>. Повторный вызов безопасен: прежняя
    /// подписка снимается, новая ставится ровно одна — раскладка и пересоздание вида могут
    /// позвать метод ещё раз, а лишний обработчик удваивал бы и перерисовку, и запуск новой
    /// партии по нажатию.
    /// </remarks>
    public void Attach(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        Detach();

        _viewModel = viewModel;

        Wire("NewGameButton", OnNewGameClick);
        Wire("CloseButton", OnCloseClick);

        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Refresh();
    }

    /// <summary>Раскладка мобильная: кнопка баннера становится крупнее.</summary>
    /// <remarks>
    /// Класс ставит вид партии при смене раскладки: баннер один на обе раскладки, а размер
    /// цели нажатия у них разный (48 точек на телефоне против 38 на настольной версии).
    /// </remarks>
    public bool IsTouch
    {
        get => this.FindControl<Button>("NewGameButton")?.Classes.Contains(TouchClass) == true;
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

    /// <summary>Показывает текущий итог партии или прячет баннер, если партия идёт.</summary>
    /// <remarks>
    /// Карточка показывается по состоянию партии, а закрытая игроком молчит, пока итог тот же:
    /// решение игрока снимается сменой самого итога, а не перерисовкой карточки.
    /// </remarks>
    public void Refresh()
    {
        if (_viewModel is null)
        {
            return;
        }

        // Итог называется сразу, как только партия закончилась: шага подтверждения подсчёта нет.
        var show = GameStatusLines.ShowOutcome(_viewModel.HasOutcome);
        var headline = _viewModel.ResultHeadline;
        var detail = _viewModel.ResultDetail;

        // Итог сменился (новая партия, другой результат) — закрытие снимается. Пока итога нет,
        // текст пуст, и это тоже «другой итог»: начатая партия отменяет прошлое решение игрока.
        if (!string.Equals(OutcomeKey(headline, detail), _dismissedOutcome, StringComparison.Ordinal))
        {
            _dismissedOutcome = null;
        }

        IsVisible = show && _dismissedOutcome is null;

        if (this.FindControl<TextBlock>("HeadlineText") is { } headlineText)
        {
            headlineText.Text = headline;
        }

        if (this.FindControl<TextBlock>("DetailText") is { } detailText)
        {
            detailText.Text = detail;
        }

        if (this.FindControl<Border>("BannerCard") is { } card)
        {
            foreach (var (tone, name) in ToneClasses)
            {
                card.Classes.Set(name, tone == _viewModel.ResultTone);
            }
        }
    }

    /// <summary>Снимает подписки прежнего подключения: иначе повторный вызов удваивал бы обработчики.</summary>
    private void Detach()
    {
        if (_viewModel is { } previous)
        {
            previous.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Обработчики кнопок снимаются теми же методами, которыми ставились: лямбда не дала бы
        // снять подписку, и после повторного Attach одно нажатие начинало бы две партии.
        Unwire("NewGameButton", OnNewGameClick);
        Unwire("CloseButton", OnCloseClick);

        _viewModel = null;
    }

    /// <summary>Обновляет баннер по любому изменению модели: какие именно свойства влияют на итог, решает она сама.</summary>
    /// <param name="sender">Модель представления.</param>
    /// <param name="e">Имя изменившегося свойства; не используется — баннер читает готовые строки.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    /// <summary>Закрывает карточку: игрок смотрит итоговую доску.</summary>
    /// <param name="sender">Кнопка «Закрыть».</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Карточка стоит на затемнении поверх доски, и разметку итога (мёртвые камни, территория)
    /// за ней не видно. Закрытие — не отказ от итога: запоминается, какой именно итог закрыт,
    /// и <see cref="Refresh"/> вернёт карточку, когда итог станет другим.
    /// </remarks>
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _dismissedOutcome = _viewModel is { } viewModel
            ? OutcomeKey(viewModel.ResultHeadline, viewModel.ResultDetail)
            : null;

        IsVisible = false;
    }

    /// <summary>Ключ итога: заголовок и строка исхода — то, что игрок видит на карточке.</summary>
    /// <param name="headline">Крупная строка итога.</param>
    /// <param name="detail">Строка исхода словами.</param>
    /// <returns>Текст итога, по которому видно, тот же он или сменился.</returns>
    private static string OutcomeKey(string headline, string detail) => $"{headline}\n{detail}";

    /// <summary>Подписывает кнопку карточки на обработчик нажатия.</summary>
    /// <param name="name">Имя кнопки в разметке.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void Wire(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += handler;
        }
    }

    /// <summary>Снимает обработчик нажатия с кнопки карточки.</summary>
    /// <param name="name">Имя кнопки в разметке.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void Unwire(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click -= handler;
        }
    }

    /// <summary>Начинает новую партию по текущим настройкам.</summary>
    /// <param name="sender">Кнопка «Новая партия».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNewGameClick(object? sender, RoutedEventArgs e)
    {
        // Итог партии уже мог быть подтверждён, поэтому новая партия начинается тем же путём,
        // что и кнопка панели: без разницы, откуда игрок её нажал. Задача не ожидается
        // осознанно: обработчик нажатия синхронный (async void запрещён, AGENTS.md п. 11),
        // а интерфейс ждать поиск хода соперника не должен.
        _ = _viewModel?.StartNewGameAsync();
    }
}
