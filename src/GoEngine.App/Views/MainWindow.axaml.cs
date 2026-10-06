using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using GoEngine.App.Rendering;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Views;

/// <summary>Главное окно партии: доска с панелью статуса и меню.</summary>
/// <remarks>
/// Доска и панель живут в <see cref="BoardView"/>: тот же вид использует мобильная версия
/// (`GoEngine.App.Android`). Здесь остаётся то, чего на Android нет, — меню, диалог настроек
/// и выбор файлов партии.
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>Создаёт окно партии.</summary>
    /// <remarks>
    /// Своей копии настроек у окна нет: их единственный источник — модель представления партии.
    /// Пока копия была, диалог настроек показывал «25 кю», когда панель играла «10 кю»
    /// (жалоба пользователя 2026-10-03 «не синхронизирован уровень AI»).
    /// </remarks>
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        DataContext = ViewModel;

        Wire("NewGameItem", OnNewGameClick);
        Wire("SettingsItem", OnSettingsClick);
        Wire("SaveGameItem", OnSaveGameClick);
        Wire("LoadGameItem", OnLoadGameClick);
        Wire("ExitItem", OnExitClick);
        Wire("AboutItem", OnAboutClick);

        // Кнопка «Настройки» в панели открывает окно: у мобильного вида подписчика нет,
        // и он показывает тот же SettingsView поверх доски.
        // Сохранение и загрузку панель делает сама (через потоки), поэтому пункты меню
        // вызывают те же методы вида — второго пути для этих действий нет.
        Board.SettingsRequested += (_, _) => _ = OpenSettingsAsync();

        // Меню действует на партию: в режиме задач его пункты недоступны, чтобы «Новая партия»
        // и «Сохранить» не срабатывали неожиданно.
        Shell.Shell.PropertyChanged += OnShellPropertyChanged;
        UpdateMenu();
    }

    /// <summary>Модель представления партии.</summary>
    public MainViewModel ViewModel => Board.ViewModel;

    /// <summary>Оболочка режимов: в ней живут виды партии и задач.</summary>
    private ShellView Shell => this.FindControl<ShellView>("ModeShell")
        ?? throw new DomainException("В окне нет оболочки режимов: разметка окна повреждена.");

    /// <summary>Доска с панелью статуса — внутри оболочки режимов.</summary>
    private BoardView Board => Shell.GameArea
        ?? throw new DomainException("В оболочке нет вида партии: разметка окна повреждена.");

    /// <summary>Включает и выключает меню при смене режима.</summary>
    /// <param name="sender">Модель оболочки.</param>
    /// <param name="e">Имя изменившегося свойства.</param>
    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsGameMode))
        {
            UpdateMenu();
        }
    }

    /// <summary>Приводит доступность меню партии в соответствие с текущим режимом.</summary>
    /// <remarks>
    /// Выключается только меню партии: «Справка» с «О программе» доступна в любом режиме —
    /// сведения о программе к партии не относятся (замечание пользователя 2026-10-06).
    /// </remarks>
    private void UpdateMenu()
    {
        if (this.FindControl<MenuItem>("GameMenuItem") is { } gameItem)
        {
            gameItem.IsEnabled = Shell.Shell.IsGameMode;
        }
    }

    /// <summary>Подписывает пункт меню на обработчик.</summary>
    /// <param name="name">Имя пункта меню.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void Wire(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<MenuItem>(name) is { } item)
        {
            item.Click += handler;
        }
    }

    /// <summary>Начинает новую партию по текущим настройкам.</summary>
    /// <param name="sender">Пункт меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNewGameClick(object? sender, RoutedEventArgs e) => ViewModel.StartNewGame();

    /// <summary>Открывает настройки и начинает партию с новыми значениями.</summary>
    /// <param name="sender">Пункт меню.</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Обработчик синхронный, а ожидание диалога вынесено в <see cref="OpenSettingsAsync"/>:
    /// <c>async void</c> в проекте запрещён (<c>AGENTS.md</c>, п. 11), а блокировать поток UI нельзя.
    /// </remarks>
    private void OnSettingsClick(object? sender, RoutedEventArgs e) => _ = OpenSettingsAsync();

    /// <summary>Показывает диалог настроек и сохраняет выбранные значения.</summary>
    /// <returns>Задача показа диалога.</returns>
    /// <remarks>
    /// Настройки не применяются к текущей партии: пересоздаёт её только сам игрок — пунктом
    /// «Новая партия». Правило подсчёта и звук действуют сразу и партию не трогают.
    /// </remarks>
    private async Task OpenSettingsAsync()
    {
        // Наличие модели считает модель представления: у неё те же оценщик и набор размеров,
        // что у приложения, но окно не читает статические поля само (D-059).
        // Настройки для показа берутся у модели представления: она один источник правды,
        // и диалог не может разойтись с панелью партии (жалоба 2026-10-03).
        var openedWith = ViewModel.Settings;
        var dialog = new SettingsWindow(openedWith, ViewModel.HasModelFor);

        // Система подсчёта действует сразу, как и на телефоне: окно показывает свой экземпляр
        // вида настроек, поэтому подписку ставит хозяин окна — тем же общим способом, что и вид
        // партии. Партия не пересоздаётся: система лишь выбирает, по какой величине называть
        // победителя (H1).
        SettingsView.ApplyScoringRuleOnChange(dialog.Settings, ViewModel);

        var accepted = await dialog.ShowDialog<bool>(this);

        if (!accepted)
        {
            // «Отмена» и крестик окна: правило подсчёта и звук действуют сразу, пока окно
            // открыто, поэтому возвращаем их по снимку на входе — ни партия, ни файл не тронуты
            // (жалоба пользователя 2026-10-03 «не хватает кнопки отмена»).
            ViewModel.ScoringRule = openedWith.ToScoringRule();
            StoneSoundPlayer.SoundEnabled = openedWith.SoundEnabled;

            return;
        }

        var chosen = dialog.Selected;

        // «Сохранить»: выбор попадает в файл и приводится в соответствие с партией — уровень
        // начинает играть со следующего хода соперника, а новый размер доски, цвет или коми
        // начинают новую партию. Иначе панель и настройки снова разошлись бы (жалоба 2026-10-03).
        _ = SettingsStore.Save(chosen);
        _ = ViewModel.ApplyChosenSettings(chosen);
    }

    /// <summary>Сохраняет партию в SGF.</summary>
    /// <param name="sender">Пункт меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSaveGameClick(object? sender, RoutedEventArgs e) => _ = Board.SaveGameAsync();

    /// <summary>Загружает партию из SGF.</summary>
    /// <param name="sender">Пункт меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLoadGameClick(object? sender, RoutedEventArgs e) => _ = Board.LoadGameAsync();

    /// <summary>Закрывает окно.</summary>
    /// <param name="sender">Пункт меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Показывает окно «О программе».</summary>
    /// <param name="sender">Пункт меню.</param>
    /// <param name="e">Событие нажатия.</param>
    /// <remarks>
    /// Обработчик синхронный, а ожидание диалога вынесено в <see cref="OpenAboutAsync"/>:
    /// <c>async void</c> в проекте запрещён (<c>AGENTS.md</c>, п. 11), а блокировать поток UI нельзя.
    /// </remarks>
    private void OnAboutClick(object? sender, RoutedEventArgs e) => _ = OpenAboutAsync();

    /// <summary>Открывает окно «О программе» как диалог главного окна.</summary>
    /// <returns>Задача показа диалога.</returns>
    private async Task OpenAboutAsync() => await new AboutWindow().ShowDialog(this);
}
