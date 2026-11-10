using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GoEngine.App.Services.Logging;
using GoEngine.Core;
using GoEngine.Multiplayer;

namespace GoEngine.App.ViewModels;

/// <summary>Один зверь-аватар для выбора ребёнком.</summary>
public sealed class AvatarChoice
{
    /// <summary>Создаёт вариант аватара.</summary>
    /// <param name="id">Номер аватара.</param>
    /// <param name="name">Короткое имя.</param>
    /// <param name="glyph">Крупный значок для карточки.</param>
    public AvatarChoice(int id, string name, string glyph)
    {
        Id = id;
        Name = name;
        Glyph = glyph;
    }

    /// <summary>Номер аватара.</summary>
    public int Id { get; }

    /// <summary>Короткое имя, например «Лиса».</summary>
    public string Name { get; }

    /// <summary>Крупный значок для карточки.</summary>
    public string Glyph { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Glyph} {Name}";
}

/// <summary>Карточка найденной комнаты.</summary>
public sealed class RoomCard
{
    internal RoomCard(RoomInfo room, AvatarChoice host)
    {
        Room = room;
        Host = host;
    }

    /// <summary>Данные комнаты для подключения.</summary>
    public RoomInfo Room { get; }

    /// <summary>Аватар хозяина.</summary>
    public AvatarChoice Host { get; }

    /// <summary>Подпись комнаты: аватар, номер и доска.</summary>
    public string Title => $"{Host.Glyph} {Host.Name} · №{Room.RoomNumber} · {Room.BoardSize}×{Room.BoardSize}";

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>Лобби сетевой игры: список комнат, создание комнаты и подключение.</summary>
/// <remarks>
/// Модель не рисует и не знает Avalonia: она ведёт обнаружение комнат и сетевые сессии,
/// а после успешного подключения передаёт их <see cref="MainViewModel"/>. События сети
/// приходят с фоновых потоков; для интерфейса они перекладываются в захваченный контекст.
/// </remarks>
public sealed class NetworkLobbyViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private static readonly Microsoft.Extensions.Logging.ILogger Log = AppLog.For<NetworkLobbyViewModel>();
    private readonly MainViewModel _game;
    private readonly MultiplayerOptions _options;
    private readonly Action<Action>? _dispatch;
    private List<RoomCard> _rooms = [];
    private IReadOnlyList<RoomCard> _roomsView = [];
    private RoomWatcher? _watcher;
    private LanGameHost? _host;
    private LanGameClient? _client;
    private CancellationTokenSource? _lifetime;
    private bool _hosting;
    private bool _busy;
    private bool _joinRequestVisible;
    private bool _closed;
    private int _selectedAvatarId;
    private int _selectedBoardSize = 9;
    private int _guestAvatarId;
    private string _status = "Ищем комнаты рядом…";
    private string? _error;
    private string _hostRoomText = string.Empty;
    private string _guestRequestText = string.Empty;
    private StoneColor _hostColor = StoneColor.Black;
    private StoneColor _guestColor = StoneColor.White;
    private string? _colorDrawText;

    /// <summary>Создаёт лобби для модели партии.</summary>
    /// <param name="game">Модель партии, которой после подключения передаются сессии.</param>
    /// <param name="options">Настройки сети; <c>null</c> — значения по умолчанию.</param>
    public NetworkLobbyViewModel(MainViewModel game, MultiplayerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        _game = game;
        _options = options ?? new MultiplayerOptions();
        _dispatch = SynchronizationContext.Current is { } context
            ? action => context.Post(static state => ((Action)state!)(), action)
            : null;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Лобби закрыто: игра началась или игрок вышел.</summary>
    public event EventHandler? Closed;

    /// <summary>Доступные аватары.</summary>
    public IReadOnlyList<AvatarChoice> Avatars => NetworkAvatars.All;

    /// <summary>Найденные комнаты.</summary>
    public IReadOnlyList<RoomCard> Rooms => _roomsView;

    /// <summary>Номер выбранного аватара.</summary>
    public int SelectedAvatarId
    {
        get => _selectedAvatarId;
        set
        {
            if (value is < 0 or > 7 || value == _selectedAvatarId)
            {
                return;
            }

            _selectedAvatarId = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Размер доски для новой комнаты: 9, 13 или 19.</summary>
    public int SelectedBoardSize
    {
        get => _selectedBoardSize;
        set
        {
            if (value is not (9 or 13 or 19) || value == _selectedBoardSize)
            {
                return;
            }

            _selectedBoardSize = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Хозяин создал комнату и ждёт гостя.</summary>
    public bool IsHosting => _hosting;

    /// <summary>Идёт подключение или ожидание ответа.</summary>
    public bool IsBusy => _busy;

    /// <summary>Показывать запрос гостя «Пустить / Не сейчас».</summary>
    public bool IsJoinRequestVisible => _joinRequestVisible;

    /// <summary>Текст состояния.</summary>
    public string StatusText => _status;

    /// <summary>Текст ошибки или <c>null</c>.</summary>
    public string? ErrorText => _error;

    /// <summary>Подпись созданной комнаты.</summary>
    public string HostRoomText => _hostRoomText;

    /// <summary>Кто просится в комнату.</summary>
    public string GuestRequestText => _guestRequestText;

    /// <summary>Адрес хозяина для ручного подключения; <c>null</c>, пока комната не создана.</summary>
    public string? HostAddress => _host?.Room?.HostAddress;

    /// <summary>TCP-порт хозяина для ручного подключения; 0, пока комната не создана.</summary>
    public int HostPort => _host?.Room?.TcpPort ?? 0;

    /// <summary>Код комнаты (идентификатор партии) для ручного подключения.</summary>
    public string? HostCode => _host?.Room?.GameId;

    /// <summary>Розыгрыш цвета перед «Пустить»: кто играет чёрными, кто белыми.</summary>
    public string? ColorDrawText => _colorDrawText;

    /// <summary>Есть ли комнаты для подключения.</summary>
    public bool HasRooms => _roomsView.Count > 0;

    /// <summary>Начинает поиск комнат.</summary>
    /// <returns>Задача запуска.</returns>
    public Task ActivateAsync()
    {
        if (_watcher is not null || _hosting || _closed)
        {
            return Task.CompletedTask;
        }

        if (_game.IsNetworkGame)
        {
            SetError("Сначала выйди из текущей сетевой игры.");
            return Task.CompletedTask;
        }

        StartWatcher();
        return Task.CompletedTask;
    }

    /// <summary>Создаёт комнату и начинает ждать гостя.</summary>
    /// <returns>Задача создания.</returns>
    public async Task CreateRoomAsync()
    {
        if (_busy || _hosting || _closed)
        {
            return;
        }

        if (_game.IsNetworkGame)
        {
            SetError("Сначала выйди из текущей сетевой игры.");
            return;
        }

        _busy = true;
        SetError(null);
        SetStatus("Создаём комнату…");
        var lifetime = new CancellationTokenSource();
        _lifetime?.Dispose();
        _lifetime = lifetime;
        var host = new LanGameHost(_options);
        try
        {
            host.GuestJoinRequested += OnGuestJoinRequested;
            host.GuestJoinCancelled += OnGuestJoinCancelled;
            host.GuestConnected += OnGuestConnected;
            host.Failed += OnHostFailed;
            var result = await host.StartHostingAsync(
                SelectedAvatarId,
                Random.Shared.Next(1, 1000),
                SelectedBoardSize,
                lifetime.Token).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                host.GuestJoinRequested -= OnGuestJoinRequested;
                host.GuestJoinCancelled -= OnGuestJoinCancelled;
                host.GuestConnected -= OnGuestConnected;
                host.Failed -= OnHostFailed;
                await host.DisposeAsync().ConfigureAwait(true);
                AppLogMessages.NetworkRoomCreateFailed(Log, result.Error!);
                SetError(result.Error);
                StartWatcher();
                return;
            }

            _host = host;
            _hosting = true;
            _hostRoomText = $"Комната №{result.Value!.RoomNumber}";
            SetStatus("Комната создана. Ждём друга…");
            OnPropertyChanged(nameof(IsHosting));
            OnPropertyChanged(nameof(HostRoomText));
            NotifyHostManualInfo();
        }
        catch (OperationCanceledException)
        {
            // Отмена — ожидаемый исход.
        }
        catch (Exception exception)
        {
            // Любая ошибка создания комнаты остаётся в лобби: игрок видит причину и может повторить.
            SetError($"Не удалось создать комнату: {exception.Message}");
        }
        finally
        {
            _busy = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    /// <summary>Пустить гостя и начать партию.</summary>
    /// <returns>Задача ответа.</returns>
    public async Task AcceptGuestAsync()
    {
        var host = _host;
        if (host is null || _busy || !_joinRequestVisible)
        {
            return;
        }

        _busy = true;
        _joinRequestVisible = false;
        OnPropertyChanged(nameof(IsJoinRequestVisible));
        SetStatus("Пустили гостя. Начинаем…");
        var komi = Komi.For(ToBoardSize(SelectedBoardSize)).Value;
        var result = await host.AcceptGuestAsync(_guestColor, komi, CancellationToken.None).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            SetError(result.Error);
        }

        _busy = false;
        OnPropertyChanged(nameof(IsBusy));
    }

    /// <summary>Отклонить подключение гостя.</summary>
    /// <returns>Задача ответа.</returns>
    public async Task RejectGuestAsync()
    {
        var host = _host;
        if (host is null)
        {
            return;
        }

        _joinRequestVisible = false;
        OnPropertyChanged(nameof(IsJoinRequestVisible));
        SetStatus("Ждём другого гостя…");
        var result = await host.RejectGuestAsync("Хозяин не пустил в комнату.", CancellationToken.None).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            SetError(result.Error);
        }
    }

    /// <summary>Отменить свою комнату, оставшись в лобби: можно выбрать комнату друга.</summary>
    /// <returns>Задача остановки комнаты.</returns>
    public async Task CancelHostRoomAsync()
    {
        var host = _host;
        if (host is null)
        {
            return;
        }

        _host = null;
        _hosting = false;
        _joinRequestVisible = false;
        OnPropertyChanged(nameof(IsHosting));
        OnPropertyChanged(nameof(IsJoinRequestVisible));
        NotifyHostManualInfo();
        SetStatus(_roomsView.Count > 0 ? "Комната отменена. Выбери комнату друга." : "Комната отменена. Можно создать новую.");

        host.GuestJoinRequested -= OnGuestJoinRequested;
        host.GuestJoinCancelled -= OnGuestJoinCancelled;
        host.GuestConnected -= OnGuestConnected;
        host.Failed -= OnHostFailed;
        await host.StopAsync().ConfigureAwait(true);
        await host.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Подключиться к комнате.</summary>
    /// <param name="room">Карточка комнаты.</param>
    /// <returns>Задача подключения.</returns>
    public async Task JoinAsync(RoomCard room)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (_busy || _closed)
        {
            return;
        }

        if (_game.IsNetworkGame)
        {
            SetError("Сначала выйди из текущей сетевой игры.");
            return;
        }

        if (_hosting)
        {
            await CancelHostRoomAsync().ConfigureAwait(true);
        }

        _busy = true;
        SetError(null);
        SetStatus($"Подключаемся к {room.Host.Name}…");
        StopWatcher();
        var lifetime = new CancellationTokenSource();
        _lifetime?.Dispose();
        _lifetime = lifetime;
        var client = new LanGameClient(_options);
        try
        {
            client.Failed += OnClientFailed;
            var result = await client.JoinAsync(room.Room, SelectedAvatarId, lifetime.Token).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                client.Failed -= OnClientFailed;
                await client.DisposeAsync().ConfigureAwait(true);
                SetError(result.Error);
                StartWatcher();
                return;
            }

            var started = result.Value!;
            var gameResult = _game.StartNetworkGuest(client, started);
            if (!gameResult.IsSuccess)
            {
                client.Failed -= OnClientFailed;
                await client.DisposeAsync().ConfigureAwait(true);
                SetError(gameResult.Error);
                StartWatcher();
                return;
            }

            client.Failed -= OnClientFailed;
            _client = null;
            Close();
        }
        catch (OperationCanceledException)
        {
            // Отмена — ожидаемый исход.
        }
        catch (Exception exception)
        {
            // Любая ошибка подключения остаётся в лобби: игрок видит причину и может выбрать другую комнату.
            SetError($"Не удалось подключиться: {exception.Message}");
            StartWatcher();
        }
        finally
        {
            _busy = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    /// <summary>Подключиться вручную по адресу и коду комнаты — аварийный путь для взрослых.</summary>
    /// <param name="hostAddress">IPv4-адрес хозяина.</param>
    /// <param name="portText">TCP-порт хозяина строкой.</param>
    /// <param name="gameId">Код комнаты (идентификатор партии).</param>
    /// <returns>Задача подключения.</returns>
    public async Task JoinManualAsync(string hostAddress, string portText, string gameId)
    {
        if (!int.TryParse(portText, out var port) || port is < 1 or > 65535)
        {
            SetError("Порт — число от 1 до 65535.");
            return;
        }

        SetError(null);
        if (string.IsNullOrWhiteSpace(hostAddress) || string.IsNullOrWhiteSpace(gameId))
        {
            SetError("Нужны адрес и код комнаты.");
            return;
        }

        var room = new RoomInfo(
            gameId.Trim(),
            SelectedAvatarId,
            roomNumber: 1,
            SelectedBoardSize,
            port,
            hostAddress.Trim(),
            _options.ProtocolVersion,
            DateTimeOffset.UtcNow);
        var card = new RoomCard(room, NetworkAvatars.All[SelectedAvatarId]);
        await JoinAsync(card).ConfigureAwait(true);
    }

    /// <summary>Закрыть лобби и освободить сетевые ресурсы.</summary>
    /// <returns>Задача закрытия.</returns>
    public async Task CancelAsync()
    {
        if (_closed)
        {
            return;
        }

        await DisposeSessionsAsync().ConfigureAwait(true);
        Close();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeSessionsAsync().ConfigureAwait(true);
        _closed = true;
    }

    private async Task DisposeSessionsAsync()
    {
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
        StopWatcher();

        var host = _host;
        _host = null;
        _hosting = false;
        if (host is not null)
        {
            host.GuestJoinRequested -= OnGuestJoinRequested;
            host.GuestJoinCancelled -= OnGuestJoinCancelled;
            host.GuestConnected -= OnGuestConnected;
            host.Failed -= OnHostFailed;
            await host.StopAsync().ConfigureAwait(true);
            await host.DisposeAsync().ConfigureAwait(true);
        }

        var client = _client;
        _client = null;
        if (client is not null)
        {
            client.Failed -= OnClientFailed;
            await client.LeaveAsync().ConfigureAwait(true);
            await client.DisposeAsync().ConfigureAwait(true);
        }
    }

    private void StartWatcher()
    {
        if (_watcher is not null || _closed)
        {
            return;
        }

        var started = RoomWatcher.Start(_options);
        if (!started.IsSuccess)
        {
            SetError(started.Error);
            return;
        }

        _watcher = started.Value!;
        _watcher.RoomsChanged += OnRoomsChanged;
        RefreshRooms();
        SetStatus(_roomsView.Count > 0 ? "Выбери комнату друга." : "Пока никого нет.");
    }

    private void StopWatcher()
    {
        var watcher = _watcher;
        _watcher = null;
        if (watcher is null)
        {
            return;
        }

        watcher.RoomsChanged -= OnRoomsChanged;
        _ = watcher.DisposeAsync().AsTask();
    }

    private void OnRoomsChanged(object? sender, EventArgs e) =>
        Dispatch(() =>
        {
            RefreshRooms();
            if (!_hosting)
            {
                // Пустой список объясняет строка под ним; повторять её же в статусе — дубль.
                SetStatus(_roomsView.Count > 0 ? "Выбери комнату друга." : "Пока никого нет.");
            }
        });

    private void RefreshRooms()
    {
        var watcher = _watcher;
        if (watcher is null)
        {
            return;
        }

        var rooms = new List<RoomCard>();
        var ownGameId = _host?.Room?.GameId;
        foreach (var room in watcher.Rooms)
        {
            // Свою комнату в списке не показываем: иначе ребёнок видит две одинаковые карточки
            // и может попытаться подключиться к самому себе.
            if (string.Equals(room.GameId, ownGameId, StringComparison.Ordinal))
            {
                continue;
            }

            var avatar = NetworkAvatars.All[room.HostAvatarId is >= 0 and < 8 ? room.HostAvatarId : 0];
            rooms.Add(new RoomCard(room, avatar));
        }

        _rooms = rooms;
        _roomsView = rooms.AsReadOnly();
        OnPropertyChanged(nameof(Rooms));
        OnPropertyChanged(nameof(HasRooms));
    }

    private void OnGuestJoinRequested(object? sender, GuestJoinRequestEventArgs e) =>
        Dispatch(() =>
        {
            _guestAvatarId = e.GuestAvatarId;

            // Цвета не назначаются при заявке: их разыграют нигири в начале партии (R-004).
            // Здесь временные значения — их заменит color_draw_done.
            _hostColor = StoneColor.Black;
            _guestColor = StoneColor.White;
            _colorDrawText = "Цвета разыграете в начале партии: нигири.";
            _guestRequestText = GuestRequestLabel(AvatarOf(e.GuestAvatarId));
            _joinRequestVisible = true;
            SetStatus($"{_guestRequestText} Пустить?");
            OnPropertyChanged(nameof(IsJoinRequestVisible));
            OnPropertyChanged(nameof(GuestRequestText));
            OnPropertyChanged(nameof(ColorDrawText));
        });

    /// <summary>Заявка гостя снята до решения хозяина: карточка «хочет играть» убирается.</summary>
    /// <param name="sender">Хозяин комнаты.</param>
    /// <param name="e">Признак события; не используется.</param>
    /// <remarks>Internal — чтобы поведение карточки проверялось без живого гостя.</remarks>
    internal void OnGuestJoinCancelled(object? sender, EventArgs e) =>
        Dispatch(() =>
        {
            if (!_hosting)
            {
                return;
            }

            // Гость закрыл соединение или не дождался решения: карточка «хочет играть» снимается,
            // иначе хозяин видел бы заявку, на которую уже некому ответить (R-007).
            _joinRequestVisible = false;
            _colorDrawText = null;
            OnPropertyChanged(nameof(IsJoinRequestVisible));
            OnPropertyChanged(nameof(ColorDrawText));
            SetStatus("Гость закрыл соединение. Ждём другого.");
        });

    private void OnGuestConnected(object? sender, EventArgs e) =>
        Dispatch(() => _ = RunAsync(StartHostGameAsync));

    private void OnHostFailed(object? sender, LanErrorEventArgs e) =>
        Dispatch(() =>
        {
            AppLogMessages.NetworkError(Log, e.Code, e.Message);
            SetError(e.Message);
        });

    private void OnClientFailed(object? sender, LanErrorEventArgs e) =>
        Dispatch(() =>
        {
            AppLogMessages.NetworkError(Log, e.Code, e.Message);
            SetError(e.Message);
        });

    private async Task StartHostGameAsync()
    {
        var host = _host;
        if (host is null || !_hosting || _closed)
        {
            return;
        }

        _busy = true;
        SetStatus("Гость подключён. Начинаем…");
        var komi = Komi.For(ToBoardSize(SelectedBoardSize)).Value;
        var result = await _game.StartNetworkHostAsync(
            host,
            SelectedBoardSize,
            komi,
            _hostColor,
            _guestAvatarId,
            _lifetime?.Token ?? CancellationToken.None).ConfigureAwait(true);
        _busy = false;
        OnPropertyChanged(nameof(IsBusy));
        if (!result.IsSuccess)
        {
            SetError(result.Error);
            return;
        }

        host.GuestJoinRequested -= OnGuestJoinRequested;
        host.GuestJoinCancelled -= OnGuestJoinCancelled;
        host.GuestConnected -= OnGuestConnected;
        host.Failed -= OnHostFailed;
        _host = null;
        _hosting = false;
        OnPropertyChanged(nameof(IsHosting));
        StopWatcher();
        Close();

        // Розыгрыш цвета идёт уже на экране партии: лобби закрыто, оверлей розыгрыша виден.
        _ = BeginColorDrawSafelyAsync();
    }

    /// <summary>Запускает нигири и не даёт исключению уйти в никуда.</summary>
    /// <returns>Задача розыгрыша.</returns>
    private async Task BeginColorDrawSafelyAsync()
    {
        try
        {
            await _game.BeginColorDrawAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            AppLogMessages.NetworkError(Log, "network.color_draw", $"Розыгрыш цвета: {exception.Message}");
        }
    }

    private void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void Dispatch(Action action)
    {
        var dispatch = _dispatch;
        if (dispatch is null)
        {
            action();
            return;
        }

        dispatch(action);
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            // Ошибка сети не должна ронять экран: покажем причину и останемся в лобби.
            SetError($"Сетевая ошибка: {exception.Message}");
        }
    }

    private void NotifyHostManualInfo()
    {
        OnPropertyChanged(nameof(HostAddress));
        OnPropertyChanged(nameof(HostPort));
        OnPropertyChanged(nameof(HostCode));
    }

    private void SetStatus(string text)
    {
        _status = text;
        OnPropertyChanged(nameof(StatusText));
    }

    private void SetError(string? error)
    {
        _error = error;
        OnPropertyChanged(nameof(ErrorText));
    }

    /// <summary>Текст розыгрыша цвета с явными ролями.</summary>
    /// <param name="hostColor">Цвет хозяина.</param>
    /// <param name="guestColor">Цвет гостя.</param>
    /// <returns>«Ты — чёрные · соперник — белые».</returns>
    /// <remarks>
    /// Имена-звери у обоих игроков могут совпасть: роли названы явно, чтобы «два Лиса»
    /// не запутали ребёнка (R-010).
    /// </remarks>
    internal static string ColorDrawLabel(StoneColor hostColor, StoneColor guestColor) =>
        $"Ты — {ColorName(hostColor)} · соперник — {ColorName(guestColor)}";

    /// <summary>Подпись заявки гостя с явной ролью.</summary>
    /// <param name="guest">Выбранный гостем зверь.</param>
    /// <returns>«Соперник 🦊 Лиса хочет играть!».</returns>
    internal static string GuestRequestLabel(AvatarChoice guest)
    {
        ArgumentNullException.ThrowIfNull(guest);

        return $"Соперник {guest} хочет играть!";
    }

    private static string ColorName(StoneColor color) =>
        color == StoneColor.Black ? "чёрные" : "белые";

    private static AvatarChoice AvatarOf(int id) => NetworkAvatars.All[id is >= 0 and < 8 ? id : 0];

    private static BoardSize ToBoardSize(int value) => value switch
    {
        13 => BoardSize.Size13,
        19 => BoardSize.Size19,
        _ => BoardSize.Size9
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}