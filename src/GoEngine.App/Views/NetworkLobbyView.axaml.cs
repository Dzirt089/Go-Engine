using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Экран сетевого лобби: аватар, комнаты и подтверждение гостя.</summary>
/// <remarks>
/// Вид не принимает решений: все действия — вызовы <see cref="NetworkLobbyViewModel"/>.
/// Список комнат и запрос гостя показываются по состоянию модели; текст в разметке не дублируется.
/// </remarks>
public sealed partial class NetworkLobbyView : UserControl
{
    private readonly NetworkLobbyViewModel _lobby;
    private readonly ComboBox? _avatarBox;
    private readonly ComboBox? _boardBox;
    private readonly Button? _createButton;
    private readonly Border? _hostingPanel;
    private readonly TextBlock? _hostingText;
    private readonly TextBlock? _statusText;
    private readonly TextBlock? _errorText;
    private readonly ItemsControl? _roomsList;
    private readonly TextBlock? _emptyRoomsText;
    private readonly Border? _joinRequestPanel;
    private readonly TextBlock? _guestRequestText;
    private readonly TextBlock? _manualHostText;
    private readonly Button? _manualToggleButton;
    private readonly StackPanel? _manualPanel;
    private readonly TextBox? _manualAddressBox;
    private readonly TextBox? _manualPortBox;
    private readonly TextBox? _manualCodeBox;

    /// <summary>Создаёт экран лобби с моделью по умолчанию для runtime-loader Avalonia.</summary>
    public NetworkLobbyView()
        : this(new NetworkLobbyViewModel(new MainViewModel(AppSettings.Default, new Random(0))))
    {
    }

    /// <summary>Создаёт экран лобби.</summary>
    /// <param name="lobby">Модель лобби.</param>
    public NetworkLobbyView(NetworkLobbyViewModel lobby)
    {
        ArgumentNullException.ThrowIfNull(lobby);
        _lobby = lobby;
        DataContext = lobby;

        AvaloniaXamlLoader.Load(this);

        _avatarBox = this.FindControl<ComboBox>("AvatarBox");
        _boardBox = this.FindControl<ComboBox>("BoardBox");
        _createButton = this.FindControl<Button>("CreateButton");
        _hostingPanel = this.FindControl<Border>("HostingPanel");
        _hostingText = this.FindControl<TextBlock>("HostingText");
        _statusText = this.FindControl<TextBlock>("StatusText");
        _errorText = this.FindControl<TextBlock>("ErrorText");
        _roomsList = this.FindControl<ItemsControl>("RoomsList");
        _emptyRoomsText = this.FindControl<TextBlock>("EmptyRoomsText");
        _joinRequestPanel = this.FindControl<Border>("JoinRequestPanel");
        _guestRequestText = this.FindControl<TextBlock>("GuestRequestText");
        _manualHostText = this.FindControl<TextBlock>("ManualHostText");
        _manualToggleButton = this.FindControl<Button>("ManualToggleButton");
        _manualPanel = this.FindControl<StackPanel>("ManualPanel");
        _manualAddressBox = this.FindControl<TextBox>("ManualAddressBox");
        _manualPortBox = this.FindControl<TextBox>("ManualPortBox");
        _manualCodeBox = this.FindControl<TextBox>("ManualCodeBox");

        if (_avatarBox is not null)
        {
            _avatarBox.ItemsSource = lobby.Avatars;
            _avatarBox.SelectedIndex = lobby.SelectedAvatarId;
            _avatarBox.SelectionChanged += OnAvatarChanged;
        }

        if (_boardBox is not null)
        {
            _boardBox.SelectedIndex = lobby.SelectedBoardSize switch
            {
                13 => 1,
                19 => 2,
                _ => 0
            };
            _boardBox.SelectionChanged += OnBoardChanged;
        }

        WireButton("CreateButton", OnCreateClick);
        WireButton("CancelHostButton", OnCancelHostClick);
        WireButton("AcceptGuestButton", OnAcceptGuestClick);
        WireButton("RejectGuestButton", OnRejectGuestClick);
        WireButton("ManualToggleButton", OnManualToggleClick);
        WireButton("ManualJoinButton", OnManualJoinClick);

        lobby.PropertyChanged += OnLobbyPropertyChanged;
        lobby.Closed += OnLobbyClosed;
        Refresh();
    }

    /// <summary>Лобби закрыто: игра началась или игрок вышел.</summary>
    public event EventHandler? Closed;

    /// <summary>Начинает поиск комнат.</summary>
    /// <returns>Задача запуска.</returns>
    public Task ActivateAsync()
    {
        Refresh();
        return _lobby.ActivateAsync();
    }

    /// <summary>Закрывает лобби и освобождает сетевые ресурсы.</summary>
    /// <returns>Задача закрытия.</returns>
    public Task CancelAsync() => _lobby.CancelAsync();

    private void OnAvatarChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_avatarBox is { SelectedIndex: >= 0 } box)
        {
            _lobby.SelectedAvatarId = box.SelectedIndex;
        }
    }

    private void OnBoardChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_boardBox is null)
        {
            return;
        }

        _lobby.SelectedBoardSize = _boardBox.SelectedIndex switch
        {
            1 => 13,
            2 => 19,
            _ => 9
        };
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e) => _ = _lobby.CreateRoomAsync();

    private void OnCancelHostClick(object? sender, RoutedEventArgs e) => _ = _lobby.CancelHostRoomAsync();

    private void OnAcceptGuestClick(object? sender, RoutedEventArgs e) => _ = _lobby.AcceptGuestAsync();

    private void OnRejectGuestClick(object? sender, RoutedEventArgs e) => _ = _lobby.RejectGuestAsync();

    private void OnManualToggleClick(object? sender, RoutedEventArgs e)
    {
        if (_manualPanel is not null)
        {
            _manualPanel.IsVisible = !_manualPanel.IsVisible;
        }
    }

    private void OnManualJoinClick(object? sender, RoutedEventArgs e)
    {
        var address = _manualAddressBox?.Text ?? string.Empty;
        var port = _manualPortBox?.Text ?? string.Empty;
        var code = _manualCodeBox?.Text ?? string.Empty;
        _ = _lobby.JoinManualAsync(address, port, code);
    }

    private void OnJoinRoomClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RoomCard room })
        {
            _ = _lobby.JoinAsync(room);
        }
    }

    private void OnLobbyPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnLobbyClosed(object? sender, EventArgs e) => Closed?.Invoke(this, EventArgs.Empty);

    private void Refresh()
    {
        if (_statusText is not null)
        {
            _statusText.Text = _lobby.StatusText;
        }

        if (_errorText is not null)
        {
            _errorText.Text = _lobby.ErrorText ?? string.Empty;
            _errorText.IsVisible = !string.IsNullOrWhiteSpace(_lobby.ErrorText);
        }

        if (_hostingPanel is not null)
        {
            // Пока висит запрос гостя, карточка комнаты уступает место большим кнопкам:
            // ребёнок не должен искать «Пустить» ниже сгиба экрана.
            _hostingPanel.IsVisible = _lobby.IsHosting && !_lobby.IsJoinRequestVisible;
        }

        if (_hostingText is not null)
        {
            _hostingText.Text = _lobby.HostRoomText;
        }

        if (_manualHostText is not null)
        {
            _manualHostText.IsVisible = _lobby.IsHosting && !string.IsNullOrWhiteSpace(_lobby.HostCode);
            _manualHostText.Text = _lobby.IsHosting
                ? $"Для взрослых: {_lobby.HostAddress}:{_lobby.HostPort}, код {_lobby.HostCode}"
                : string.Empty;
        }

        if (_joinRequestPanel is not null)
        {
            _joinRequestPanel.IsVisible = _lobby.IsJoinRequestVisible;
        }

        if (_guestRequestText is not null)
        {
            _guestRequestText.Text = _lobby.GuestRequestText;
        }

        if (_roomsList is not null)
        {
            _roomsList.ItemsSource = _lobby.Rooms;
        }

        if (_emptyRoomsText is not null)
        {
            _emptyRoomsText.IsVisible = !_lobby.HasRooms && !_lobby.IsHosting;
        }

        if (_createButton is not null)
        {
            _createButton.IsEnabled = !_lobby.IsBusy && !_lobby.IsHosting;
        }

        if (_avatarBox is not null)
        {
            _avatarBox.IsEnabled = !_lobby.IsBusy && !_lobby.IsHosting;
        }

        if (_boardBox is not null)
        {
            _boardBox.IsEnabled = !_lobby.IsBusy && !_lobby.IsHosting;
        }
    }

    private void WireButton(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += handler;
        }
    }
}
