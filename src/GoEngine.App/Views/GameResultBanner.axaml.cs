using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Баннер итога партии: крупно называет исход и предлагает начать новую партию.</summary>
/// <remarks>
/// <para>
/// Итог виден и в строке состояния, но её легко не заметить: на настольной версии она стоит
/// среди прочих строк панели, на телефоне — в свёрнутой шторке. Баннер выносит исход отдельно
/// и красит его по признаку «выиграл / проиграл / ничья»: цвет победителя игроку ничего не
/// говорит — ему важно, выиграл он сам или нет (жалоба 2026-09-30).
/// </para>
/// <para>
/// Вид не знает про партию: он получает модель представления и показывает готовые строки.
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

    private readonly MainViewModel? _viewModel;

    /// <summary>Создаёт баннер без модели: так его создаёт XAML.</summary>
    public GameResultBanner() => AvaloniaXamlLoader.Load(this);

    /// <summary>Создаёт баннер для партии.</summary>
    /// <param name="viewModel">Модель представления партии: из неё берутся строки итога.</param>
    /// <remarks>
    /// Модель подписывается на изменения: баннер появляется и исчезает вместе с итогом партии,
    /// а не по отдельной команде вида. Иначе каждый путь (ход, отмена, новая партия, загрузка
    /// файла) должен был бы помнить о баннере.
    /// </remarks>
    public GameResultBanner(MainViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;

        if (this.FindControl<Button>("NewGameButton") is { } newGame)
        {
            newGame.Click += OnNewGameClick;
        }

        viewModel.PropertyChanged += (_, _) => Refresh();
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
            if (this.FindControl<Button>("NewGameButton") is { } button)
            {
                button.Classes.Set(TouchClass, value);
            }
        }
    }

    /// <summary>Показывает текущий итог партии или прячет баннер, если партия идёт.</summary>
    public void Refresh()
    {
        if (_viewModel is null)
        {
            return;
        }

        // Во время согласования мёртвых групп итог ещё не подтверждён: баннер молчит, а игрок
        // видит приглашение проверить группы. Победителя до подтверждения не называем.
        IsVisible = GameStatusLines.ShowOutcome(_viewModel.IsCounting, _viewModel.HasOutcome);

        if (this.FindControl<TextBlock>("HeadlineText") is { } headline)
        {
            headline.Text = _viewModel.ResultHeadline;
        }

        if (this.FindControl<TextBlock>("DetailText") is { } detail)
        {
            detail.Text = _viewModel.ResultDetail;
        }

        if (this.FindControl<Border>("BannerCard") is { } card)
        {
            foreach (var (tone, name) in ToneClasses)
            {
                card.Classes.Set(name, tone == _viewModel.ResultTone);
            }
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
