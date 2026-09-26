using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App;

/// <summary>Главное окно партии: доска, панель статуса и меню.</summary>
/// <remarks>
/// Окно связывает доску с моделью представления: щелчок по доске превращается в ход игрока,
/// после которого отвечает AI. Настройки читаются и пишутся через <see cref="SettingsStore"/>.
/// </remarks>
public sealed partial class MainWindow : Window
{
    private readonly BoardControl? _boardControl;
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

        ViewModel = new MainViewModel(settings, Random.Shared);
        DataContext = ViewModel;

        _boardControl = this.FindControl<BoardControl>("Board");

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
        }

        Wire("NewGameItem", OnNewGameClick);
        Wire("SettingsItem", OnSettingsClick);
        Wire("ExitItem", OnExitClick);
    }

    /// <summary>Модель представления окна.</summary>
    public MainViewModel ViewModel { get; }

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

    /// <summary>Обрабатывает щелчок по доске.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка хода.</param>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e) => ViewModel.PlayMove(e.Point);

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
        var dialog = new SettingsWindow(_settings);
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

    /// <summary>Закрывает окно.</summary>
    /// <param name="sender">Пункт меню.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
