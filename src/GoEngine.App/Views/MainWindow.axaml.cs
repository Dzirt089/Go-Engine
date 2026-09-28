using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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
    private AppSettings _settings;

    /// <summary>Создаёт окно партии.</summary>
    public MainWindow() : this(SettingsStore.Load())
    {
    }

    /// <summary>Создаёт окно партии с готовыми настройками.</summary>
    /// <param name="settings">Настройки партии.</param>
    public MainWindow(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;

        AvaloniaXamlLoader.Load(this);

        DataContext = ViewModel;

        Wire("NewGameItem", OnNewGameClick);
        Wire("SettingsItem", OnSettingsClick);
        Wire("SaveGameItem", OnSaveGameClick);
        Wire("LoadGameItem", OnLoadGameClick);
        Wire("ExitItem", OnExitClick);

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

    /// <summary>Приводит доступность меню в соответствие с текущим режимом.</summary>
    private void UpdateMenu()
    {
        if (this.FindControl<Menu>("MainMenu") is { } menu)
        {
            menu.IsEnabled = Shell.Shell.IsGameMode;
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

    /// <summary>Показывает диалог настроек и применяет выбранные значения.</summary>
    /// <returns>Задача показа диалога.</returns>
    private async Task OpenSettingsAsync()
    {
        // Наличие модели считает модель представления: у неё те же оценщик и набор размеров,
        // что у приложения, но окно не читает статические поля само (D-059).
        var dialog = new SettingsWindow(_settings, ViewModel.HasModelFor);
        var accepted = await dialog.ShowDialog<bool>(this);

        if (!accepted)
        {
            return;
        }

        _settings = dialog.Selected;
        ViewModel.ApplySettings(_settings);

        // Неудачная запись настроек не мешает играть: значения уже применены к партии.
        _ = SettingsStore.Save(_settings);
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
}
