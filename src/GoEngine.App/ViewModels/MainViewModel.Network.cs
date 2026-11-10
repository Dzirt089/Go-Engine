using System.Globalization;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.Core;
using GoEngine.Multiplayer;

namespace GoEngine.App.ViewModels;

/// <summary>Сетевая часть модели партии: ходы, таймер 90 секунд, авто-пас и переподключение.</summary>
/// <remarks>
/// <para>
/// Хозяин — единственный арбитр: он применяет ходы, ведёт таймер и делает вынужденный пас.
/// Гость отправляет просьбу о ходе и применяет только подтверждённый ход. Поэтому два авто-паса
/// не могут появиться на одном ходе, а таймер не зависит от часов чужого устройства.
/// </para>
/// <para>
/// Вынужденный пас не завершает партию: <see cref="GameState.PlayAutoPass"/> сбрасывает цепочку
/// ручных пасов. Чтобы зависшая игра не длилась вечно, после
/// <see cref="MultiplayerOptions.AutoPassStallLimit"/> авто-пасов подряд партия прерывается.
/// </para>
/// </remarks>
public sealed partial class MainViewModel : IDisposable
{
    /// <summary>Короткое сообщение о передаче хода.</summary>
    private const string NoticeMyTurn = "Твой ход!";

    /// <summary>Короткое сообщение о вынужденном пасе по таймеру.</summary>
    private const string NoticeTimeIsUp = "Время вышло — пас";

    /// <summary>Короткое сообщение о возвращении соперника.</summary>
    private const string NoticeOpponentBack = "Соперник вернулся";

    /// <summary>Подсказка перед вынужденным пасом.</summary>
    private const string TimerWarning = "Походи, а то будет пас";

    /// <summary>Сколько живёт короткое сетевое сообщение.</summary>
    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(3);

    private readonly TimeProvider _timeProvider;
    private readonly MultiplayerOptions _networkOptions;
    private readonly List<AppliedMove> _networkHistory = [];
    private INetworkGameHost? _networkHost;
    private INetworkGameClient? _networkClient;
    private TurnTimer? _networkTurnTimer;
    private CancellationTokenSource? _networkReconnectTimeout;
    private Action<Action>? _networkDispatch;
    private bool _networkMode;
    private bool _networkTimerPaused;
    private bool _networkAwaitingConfirmation;
    private bool _networkFinished;
    private bool _networkHostSubscribed;
    private bool _networkClientSubscribed;
    private StoneColor _networkPlayerColor = StoneColor.Black;
    private int _networkOpponentAvatarId = -1;
    private int _networkRemainingMs;
    private DateTimeOffset _networkRemainingAt;
    private int _networkConsecutiveAutoPasses;
    private string? _networkError;
    private string? _networkNotice;
    private DateTimeOffset _networkNoticeUntil;
    private string? _networkTerminalMessage;
    private DateTimeOffset _networkReconnectDeadline;
    private bool _networkReconnectDeadlineSet;
    private bool _networkBannerDismissed;
    private int _networkWarningLevel;
    private int _networkTurnCueMoveNumber = -1;

    /// <summary>Идёт сетевая партия.</summary>
    public bool IsNetworkGame => _networkMode;

    /// <summary>Текущий игрок — хозяин комнаты.</summary>
    public bool IsNetworkHost => _networkHost is not null;

    /// <summary>Сейчас ход этого игрока и можно отправить ход.</summary>
    public bool IsNetworkMyTurn =>
        !_networkMode
        || (_gameStarted
            && _game.Status == GameStatus.InProgress
            && _game.ToMove == _networkPlayerColor
            && !_networkAwaitingConfirmation
            && !_networkTimerPaused
            && !_networkFinished
            && IsNetworkConnected);

    /// <summary>Есть активное соединение с соперником.</summary>
    public bool IsNetworkConnected => _networkHost?.IsConnected == true || _networkClient?.IsConnected == true;

    /// <summary>Можно выйти из сетевой игры.</summary>
    public bool CanLeaveNetwork => _networkMode;

    /// <summary>Можно начать новую обычную партию: в сетевом режиме сначала нужно выйти.</summary>
    public bool CanStartNewGame => !_networkMode;

    /// <summary>Можно менять настройки партии: в сетевом режиме они зафиксированы хозяином.</summary>
    public bool CanChangeGameSettings => !_networkMode;

    /// <summary>Сдаться в сетевой партии.</summary>
    /// <returns><c>true</c>, если сдача принята.</returns>
    public Task<bool> ResignNetworkAsync() =>
        _networkMode && _game.Status == GameStatus.InProgress
            ? PlayNetworkMoveAsync(Move.Resign(_game.ToMove))
            : Task.FromResult(false);

    /// <summary>Номер зверя-аватара соперника для подписи в панели.</summary>
    public int NetworkOpponentAvatarId => _networkOpponentAvatarId;

    /// <summary>Цвет игрока в сетевой партии словами.</summary>
    public string NetworkPlayerColorText => _networkMode
        ? (_networkPlayerColor == StoneColor.Black ? "чёрные" : "белые")
        : string.Empty;

    /// <summary>Цвет соперника в сетевой партии словами.</summary>
    public string NetworkOpponentColorText => _networkMode
        ? (_networkPlayerColor == StoneColor.Black ? "белые" : "чёрные")
        : string.Empty;

    /// <summary>Показывать ли короткое сетевое сообщение: передача хода, авто-пас, возврат.</summary>
    /// <remarks>
    /// Сообщение живёт несколько секунд и гаснет по таймеру модели (вид зовёт
    /// <see cref="TickClock"/> раз в секунду). Оно не заменяет строку статуса: та называет
    /// очередь хода постоянно, а сообщение — заметный момент смены.
    /// </remarks>
    public bool IsNetworkNoticeVisible => _networkMode && _networkNotice is not null;

    /// <summary>Текст короткого сетевого сообщения; пустая строка — сообщения нет.</summary>
    public string NetworkNoticeText => _networkNotice ?? string.Empty;

    /// <summary>Показывать ли подсказку перед вынужденным пасом.</summary>
    /// <remarks>Показывается только тому, чей ход: пас делает он, а не зритель.</remarks>
    public bool IsNetworkTimerWarningVisible => GetNetworkWarningLevel() > 0;

    /// <summary>Текст подсказки перед вынужденным пасом.</summary>
    public string NetworkTimerWarningText => IsNetworkTimerWarningVisible ? TimerWarning : string.Empty;

    /// <summary>Последние 10 секунд на ход: мягкое выделение, без красного.</summary>
    public bool IsNetworkTimerUrgent => GetNetworkWarningLevel() == 10;

    /// <summary>Соперник потерял связь: идёт ожидание переподключения с обратным отсчётом.</summary>
    /// <remarks>
    /// Признак — именно запущенный отсчёт, а не «таймер на паузе»: пауза бывает и от
    /// протокольной ошибки, которая ждать нечего (M1 ревью P4).
    /// </remarks>
    public bool IsNetworkDisconnected =>
        _networkMode && _networkReconnectDeadlineSet && !_networkFinished && !IsNetworkTerminal;

    /// <summary>Сколько секунд осталось сопернику на возвращение; 0 — ожидание не идёт.</summary>
    public int NetworkReconnectSecondsLeft
    {
        get
        {
            if (!_networkReconnectDeadlineSet)
            {
                return 0;
            }

            var left = _networkReconnectDeadline - _timeProvider.GetUtcNow();

            return left > TimeSpan.Zero ? (int)Math.Ceiling(left.TotalSeconds) : 0;
        }
    }

    /// <summary>Партия прервана сетевой ошибкой: показывается итоговая карточка, а не строка.</summary>
    public bool IsNetworkTerminal =>
        _networkMode && _networkFinished && _networkTerminalMessage is not null;

    /// <summary>Заголовок баннера сетевого состояния.</summary>
    public string NetworkBannerTitle => IsNetworkTerminal ? "Партия прервана" : "Соперник потерял связь";

    /// <summary>Текст баннера: причина обрыва и обратный отсчёт переподключения.</summary>
    public string NetworkBannerText => IsNetworkTerminal
        ? _networkTerminalMessage ?? string.Empty
        : $"Ждём возвращения · {NetworkReconnectSecondsLeft} с";

    /// <summary>Подпись действия, закрывающего баннер: до терминала — выход, после — «В меню».</summary>
    public string NetworkBannerLeaveText => IsNetworkTerminal ? "В меню" : "Выйти";

    /// <summary>Показывать ли баннер сетевого состояния.</summary>
    public bool IsNetworkBannerVisible =>
        _networkMode && (IsNetworkTerminal || (IsNetworkDisconnected && !_networkBannerDismissed));

    /// <summary>Можно ли отложить баннер обрыва и продолжить ждать («Остаться»).</summary>
    public bool IsNetworkBannerCanWait => IsNetworkDisconnected && !IsNetworkTerminal;

    /// <summary>Откладывает баннер обрыва: игрок выбрал ждать соперника.</summary>
    public void DismissNetworkBanner()
    {
        _networkBannerDismissed = true;
        OnPropertyChanged(nameof(IsNetworkBannerVisible));
    }

    /// <summary>Строка состояния сетевой партии.</summary>
    public string NetworkStatusText
    {
        get
        {
            if (!_networkMode)
            {
                return string.Empty;
            }

            if (IsNetworkTerminal)
            {
                // Причина важнее ярлыка: строка статуса называет её так же, как карточка итога.
                return _networkError ?? "Партия прервана";
            }

            if (!string.IsNullOrWhiteSpace(_networkError))
            {
                return _networkError;
            }

            if (_networkFinished || _game.Status != GameStatus.InProgress)
            {
                return "Партия окончена";
            }

            if (_networkTimerPaused)
            {
                return "Связь потеряна. Ждём возвращения…";
            }

            if (_networkAwaitingConfirmation)
            {
                return "Ждём подтверждения хода…";
            }

            return _game.ToMove == _networkPlayerColor
                ? "Твой ход"
                : $"{NetworkAvatars.NameOf(_networkOpponentAvatarId)} думает";
        }
    }

    /// <summary>Остаток времени на ход строкой «М:СС».</summary>
    public string NetworkTurnText
    {
        get
        {
            if (!_networkMode || _networkFinished || _game.Status != GameStatus.InProgress)
            {
                return string.Empty;
            }

            var remaining = GetNetworkRemaining();
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}");
        }
    }

    /// <summary>Запускает сетевую партию на стороне хозяина.</summary>
    /// <param name="host">Уже принявшая гостя комната.</param>
    /// <param name="boardSize">Сторона доски.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="hostColor">Цвет хозяина.</param>
    /// <param name="opponentAvatarId">Аватар гостя для подписи.</param>
    /// <param name="cancellationToken">Отмена запуска.</param>
    /// <returns>Успех или причина отказа.</returns>
    public async Task<Result> StartNetworkHostAsync(
        INetworkGameHost host,
        int boardSize,
        double komi,
        StoneColor hostColor,
        int opponentAvatarId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (_networkMode)
        {
            return Result.Fail("Сетевая партия уже идёт.");
        }

        if (boardSize is not (9 or 13 or 19))
        {
            return Result.Fail("Размер доски должен быть 9, 13 или 19.");
        }

        if (hostColor is null || hostColor == StoneColor.Empty)
        {
            return Result.Fail("Хозяин должен играть чёрными или белыми.");
        }

        _networkHost = host;
        _networkClient = null;
        _networkPlayerColor = hostColor;
        _networkOpponentAvatarId = opponentAvatarId;
        _networkMode = true;
        _networkError = null;
        _networkFinished = false;
        _networkAwaitingConfirmation = false;
        _networkTimerPaused = false;
        _networkConsecutiveAutoPasses = 0;
        _networkHistory.Clear();
        ResetNetworkUiState();
        _networkDispatch = SynchronizationContext.Current is { } context
            ? action => context.Post(static state => ((Action)state!)(), action)
            : null;
        SubscribeHost(host);

        _settings = AppSettings.From(
            ToBoardSize(boardSize),
            _settings.ToDifficultyLevel(),
            hostColor,
            new Komi(komi),
            _settings.ToScoringRule(),
            _settings.SoundEnabled);
        BeginGame(_settings);
        MarkNetworkStarted();
        _networkRemainingMs = (int)_networkOptions.MoveTimeout.TotalMilliseconds;
        _networkRemainingAt = _timeProvider.GetUtcNow();
        _networkTurnTimer?.Dispose();
        _networkTurnTimer = new TurnTimer(_timeProvider);
        _networkTurnTimer.Expired += OnNetworkTurnExpired;
        StartNetworkTimer();
        NotifyAll();
        NotifyNetworkTurnCue();

        var sent = await host.SendTurnStateAsync(_networkRemainingMs, paused: false, cancellationToken).ConfigureAwait(true);
        if (!sent.IsSuccess)
        {
            _networkError = sent.Error;
            NotifyAll();
        }

        return Result.Ok();
    }

    /// <summary>Запускает сетевую партию на стороне гостя.</summary>
    /// <param name="client">Клиент, уже вошедший в комнату.</param>
    /// <param name="started">Данные партии от хозяина.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Result StartNetworkGuest(INetworkGameClient client, GuestSessionStarted started)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(started);
        if (_networkMode)
        {
            return Result.Fail("Сетевая партия уже идёт.");
        }

        _networkHost = null;
        _networkClient = client;
        _networkPlayerColor = started.GuestColor;
        _networkOpponentAvatarId = started.HostAvatarId;
        _networkMode = true;
        _networkError = null;
        _networkFinished = false;
        _networkAwaitingConfirmation = false;
        _networkTimerPaused = false;
        _networkConsecutiveAutoPasses = 0;
        _networkHistory.Clear();
        ResetNetworkUiState();
        _networkDispatch = SynchronizationContext.Current is { } context
            ? action => context.Post(static state => ((Action)state!)(), action)
            : null;
        SubscribeClient(client);

        _settings = AppSettings.From(
            ToBoardSize(started.BoardSize),
            _settings.ToDifficultyLevel(),
            started.GuestColor,
            new Komi(started.Komi),
            _settings.ToScoringRule(),
            _settings.SoundEnabled);
        BeginGame(_settings);
        MarkNetworkStarted();
        _networkRemainingMs = started.RemainingMs;
        _networkRemainingAt = _timeProvider.GetUtcNow();
        NotifyAll();
        NotifyNetworkTurnCue();

        return Result.Ok();
    }

    /// <summary>Выйти из сетевой партии, закрыть соединение и вернуться в обычный режим.</summary>
    /// <returns>Задача закрытия.</returns>
    public async Task LeaveNetworkGameAsync()
    {
        if (!_networkMode)
        {
            return;
        }

        await StopNetworkSessionsAsync().ConfigureAwait(true);
        _networkMode = false;
        _networkFinished = false;
        _networkAwaitingConfirmation = false;
        _networkTimerPaused = false;
        _networkError = null;
        _networkOpponentAvatarId = -1;
        ResetNetworkUiState();

        // После выхода из сети доска не должна оставаться доской сетевой партии с идущими
        // часами: это выглядело как «ИИ · Ход» посреди чужого поля. Начинаем чистую партию
        // с ИИ, но не запускаем часы — первый ход игрока сделает это сам.
        BeginGame(_settings);
        _gameStarted = false;
        _clock.Reset();
        NotifyAll();
    }

    private async Task StopNetworkSessionsAsync()
    {
        UnsubscribeHost();
        UnsubscribeClient();
        var host = _networkHost;
        var client = _networkClient;
        _networkHost = null;
        _networkClient = null;

        _networkReconnectTimeout?.Cancel();
        _networkReconnectTimeout?.Dispose();
        _networkReconnectTimeout = null;
        _networkTurnTimer?.Stop();
        _networkTurnTimer?.Dispose();
        _networkTurnTimer = null;
        _networkTimerPaused = false;

        if (host is not null)
        {
            await host.StopAsync().ConfigureAwait(true);
            await host.DisposeAsync().ConfigureAwait(true);
        }

        if (client is not null)
        {
            await client.LeaveAsync().ConfigureAwait(true);
            await client.DisposeAsync().ConfigureAwait(true);
        }
    }

    private async Task<bool> PlayNetworkMoveAsync(Move move)
    {
        if (!_networkMode || !CanPlayMove || move.IsNone || move.Color != _game.ToMove)
        {
            return false;
        }

        if (_networkHost is not null)
        {
            if (!TryPlay(move))
            {
                return false;
            }

            _networkHistory.Add(new AppliedMove(_game.MoveNumber, move, autoPass: false));
            _networkConsecutiveAutoPasses = 0;
            await PublishHostMoveAsync(move, autoPass: false).ConfigureAwait(true);
            return true;
        }

        if (_networkClient is null || !IsNetworkMyTurn)
        {
            return false;
        }

        _networkAwaitingConfirmation = true;
        NotifyAll();
        var sent = await _networkClient.RequestMoveAsync(move, _game.MoveNumber + 1, CancellationToken.None).ConfigureAwait(true);
        if (!sent.IsSuccess)
        {
            _networkAwaitingConfirmation = false;
            _networkError = sent.Error;
            NotifyAll();
            return false;
        }

        return true;
    }

    private async Task PublishHostMoveAsync(Move move, bool autoPass)
    {
        var host = _networkHost;
        if (host is null)
        {
            return;
        }

        int remaining;
        if (_game.Status == GameStatus.InProgress)
        {
            StartNetworkTimer();
            remaining = (int)GetNetworkRemaining().TotalMilliseconds;
        }
        else
        {
            _networkTurnTimer?.Stop();
            _networkFinished = true;
            _networkTimerPaused = false;
            _networkTerminalMessage = null;
            StopReconnectCountdown();
            MarkHostFinished();
            remaining = 0;
        }

        var sent = await host.SendMoveAppliedAsync(move, autoPass, _game.MoveNumber, remaining, CancellationToken.None).ConfigureAwait(true);
        if (!sent.IsSuccess)
        {
            _networkError = sent.Error;
        }

        NotifyAll();
        NotifyNetworkTurnCue(autoPass);
    }

    private void StartNetworkTimer()
    {
        _networkTurnTimer ??= new TurnTimer(_timeProvider);
        _networkTurnTimer.Start(_networkOptions.MoveTimeout);
        _networkRemainingMs = (int)_networkOptions.MoveTimeout.TotalMilliseconds;
        _networkRemainingAt = _timeProvider.GetUtcNow();
        _networkTimerPaused = false;
        _networkWarningLevel = 0;
    }

    private void OnNetworkTurnExpired(object? sender, EventArgs e) =>
        PostToUi(() => _ = RunNetworkTaskAsync(HandleNetworkTimeoutAsync));

    private async Task HandleNetworkTimeoutAsync()
    {
        if (!_networkMode || _networkHost is null || _game.Status != GameStatus.InProgress || _networkTimerPaused)
        {
            return;
        }

        var pass = Move.Pass(_game.ToMove);
        if (!TryPlay(pass, autoPass: true))
        {
            return;
        }

        _networkHistory.Add(new AppliedMove(_game.MoveNumber, pass, autoPass: true));
        _networkConsecutiveAutoPasses++;
        await PublishHostMoveAsync(pass, autoPass: true).ConfigureAwait(true);

        if (_networkMode
            && _game.Status == GameStatus.InProgress
            && _networkConsecutiveAutoPasses >= _networkOptions.AutoPassStallLimit)
        {
            await EndNetworkGameAsync("Игра прервана: оба игрока не делают ходы.").ConfigureAwait(true);
        }
    }

    private async Task HandleGuestMoveAsync(GuestMoveRequestEventArgs request)
    {
        if (!_networkMode || _networkHost is null || _game.Status != GameStatus.InProgress || _networkTimerPaused)
        {
            return;
        }

        if (request.MoveNumber <= _game.MoveNumber)
        {
            // Ход опоздал: пока он шёл, хозяин уже сделал вынужденный пас. Гость получит его
            // подтверждение и сам обновит доску; второй раз применять ход нельзя.
            return;
        }

        if (request.MoveNumber != _game.MoveNumber + 1 || request.Move.Color != _game.ToMove)
        {
            _networkError = "Соперник прислал ход вне очереди.";
            _ = _networkHost?.SendMoveRejectedAsync(LanErrorCodes.ProtocolError, _networkError, CancellationToken.None);
            NotifyAll();
            return;
        }

        if (!TryPlay(request.Move))
        {
            _networkError = "Ход соперника не принят правилами.";
            _ = _networkHost?.SendMoveRejectedAsync(LanErrorCodes.MoveRejected, _networkError, CancellationToken.None);
            NotifyAll();
            return;
        }

        _networkHistory.Add(new AppliedMove(_game.MoveNumber, request.Move, autoPass: false));
        _networkError = null;
        _networkConsecutiveAutoPasses = 0;
        await PublishHostMoveAsync(request.Move, autoPass: false).ConfigureAwait(true);
    }

    private void SubscribeHost(INetworkGameHost host)
    {
        if (_networkHostSubscribed)
        {
            return;
        }

        _networkHostSubscribed = true;
        host.GuestMoveRequested += OnHostMoveRequested;
        host.GuestStateSyncRequested += OnHostStateSyncRequested;
        host.GuestDisconnected += OnHostDisconnected;
        host.GuestReconnected += OnHostReconnected;
        host.GuestLeft += OnHostGuestLeft;
        host.Failed += OnHostFailed;
    }

    private void UnsubscribeHost()
    {
        if (!_networkHostSubscribed || _networkHost is null)
        {
            return;
        }

        _networkHostSubscribed = false;
        _networkHost.GuestMoveRequested -= OnHostMoveRequested;
        _networkHost.GuestStateSyncRequested -= OnHostStateSyncRequested;
        _networkHost.GuestDisconnected -= OnHostDisconnected;
        _networkHost.GuestReconnected -= OnHostReconnected;
        _networkHost.GuestLeft -= OnHostGuestLeft;
        _networkHost.Failed -= OnHostFailed;
    }

    private void SubscribeClient(INetworkGameClient client)
    {
        if (_networkClientSubscribed)
        {
            return;
        }

        _networkClientSubscribed = true;
        client.MoveApplied += OnGuestMoveApplied;
        client.TurnStateReceived += OnGuestTurnStateReceived;
        client.StateSyncReceived += OnGuestStateSyncReceived;
        client.Disconnected += OnGuestDisconnected;
        client.Reconnected += OnGuestReconnected;
        client.Failed += OnGuestFailed;
    }

    private void UnsubscribeClient()
    {
        if (!_networkClientSubscribed || _networkClient is null)
        {
            return;
        }

        _networkClientSubscribed = false;
        _networkClient.MoveApplied -= OnGuestMoveApplied;
        _networkClient.TurnStateReceived -= OnGuestTurnStateReceived;
        _networkClient.StateSyncReceived -= OnGuestStateSyncReceived;
        _networkClient.Disconnected -= OnGuestDisconnected;
        _networkClient.Reconnected -= OnGuestReconnected;
        _networkClient.Failed -= OnGuestFailed;
    }

    private void OnHostMoveRequested(object? sender, GuestMoveRequestEventArgs e) =>
        PostToUi(() => _ = RunNetworkTaskAsync(() => HandleGuestMoveAsync(e)));

    private void OnHostStateSyncRequested(object? sender, GuestStateSyncRequestedEventArgs e) =>
        PostToUi(() => _ = RunNetworkTaskAsync(SendHostStateSyncAsync));

    private void OnHostDisconnected(object? sender, EventArgs e) =>
        PostToUi(HandleHostDisconnected);

    private void OnHostReconnected(object? sender, EventArgs e) =>
        PostToUi(() => _ = RunNetworkTaskAsync(HandleHostReconnectedAsync));

    private void OnHostGuestLeft(object? sender, EventArgs e) =>
        PostToUi(() => _ = RunNetworkTaskAsync(() => EndNetworkGameAsync("Соперник вышел из игры.")));

    private void OnHostFailed(object? sender, LanErrorEventArgs e) =>
        PostToUi(() => HandleNetworkError(e));

    private void HandleHostDisconnected()
    {
        if (!_networkMode || _networkFinished)
        {
            return;
        }

        _networkRemainingMs = (int)(_networkTurnTimer?.Remaining ?? TimeSpan.FromMilliseconds(_networkRemainingMs)).TotalMilliseconds;
        _networkTurnTimer?.Pause();
        _networkTimerPaused = true;
        _networkReconnectTimeout?.Cancel();
        _networkReconnectTimeout?.Dispose();
        _networkReconnectTimeout = new CancellationTokenSource();
        StartReconnectCountdown();
        NotifyAll();
        _ = RunNetworkTaskAsync(() => WaitReconnectTimeoutAsync(_networkReconnectTimeout.Token));
    }

    private async Task WaitReconnectTimeoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_networkOptions.ReconnectTimeout, _networkOptions.TimeProvider, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_networkMode && _networkTimerPaused)
        {
            await EndNetworkGameAsync("Соперник не вернулся в игру.").ConfigureAwait(true);
        }
    }

    private async Task HandleHostReconnectedAsync()
    {
        if (!_networkMode || _networkFinished)
        {
            return;
        }

        _networkReconnectTimeout?.Cancel();
        _networkTimerPaused = false;
        StopReconnectCountdown();
        ShowNetworkNotice(NoticeOpponentBack);
        _networkTurnTimer?.Resume();
        await SendHostStateSyncAsync().ConfigureAwait(true);
        if (_networkHost is not null)
        {
            _ = await _networkHost.SendTurnStateAsync((int)GetNetworkRemaining().TotalMilliseconds, paused: false, CancellationToken.None).ConfigureAwait(true);
        }

        NotifyAll();
    }

    private async Task SendHostStateSyncAsync()
    {
        if (_networkHost is null)
        {
            return;
        }

        var sent = await _networkHost.SendStateSyncAsync(
            _networkHistory.ToArray(),
            (int)GetNetworkRemaining().TotalMilliseconds,
            _networkTimerPaused,
            CancellationToken.None).ConfigureAwait(true);
        if (!sent.IsSuccess)
        {
            _networkError = sent.Error;
            NotifyAll();
        }
    }

    private void OnGuestMoveApplied(object? sender, MoveAppliedEventArgs e) =>
        PostToUi(() => _ = RunNetworkTaskAsync(() => HandleGuestMoveAppliedAsync(e)));

    private async Task HandleGuestMoveAppliedAsync(MoveAppliedEventArgs applied)
    {
        if (!_networkMode || _networkClient is null || _game.Status != GameStatus.InProgress)
        {
            return;
        }

        if (applied.MoveNumber != _game.MoveNumber + 1 || applied.Move.Color != _game.ToMove)
        {
            _networkError = "Хозяин прислал ход вне очереди.";
            NotifyAll();
            return;
        }

        if (!TryPlay(applied.Move, autoPass: applied.AutoPass))
        {
            _networkError = "Подтверждённый ход не принят правилами.";
            NotifyAll();
            return;
        }

        _networkHistory.Add(new AppliedMove(applied.MoveNumber, applied.Move, applied.AutoPass));
        _networkError = null;
        _networkAwaitingConfirmation = false;
        _networkRemainingMs = Math.Max(0, applied.RemainingMs);
        _networkRemainingAt = _timeProvider.GetUtcNow();
        _networkTimerPaused = false;
        _networkWarningLevel = 0;
        if (_game.Status != GameStatus.InProgress)
        {
            _networkFinished = true;
            _networkTerminalMessage = null;
            StopReconnectCountdown();
        }

        NotifyAll();
        NotifyNetworkTurnCue(applied.AutoPass);
        await Task.CompletedTask.ConfigureAwait(true);
    }

    private void OnGuestTurnStateReceived(object? sender, TurnStateEventArgs e) =>
        PostToUi(() =>
        {
            if (!_networkMode)
            {
                return;
            }

            _networkRemainingMs = Math.Max(0, e.RemainingMs);
            _networkRemainingAt = _timeProvider.GetUtcNow();
            _networkTimerPaused = e.Paused;
            _networkError = null;
            NotifyAll();
        });

    private void OnGuestStateSyncReceived(object? sender, StateSyncEventArgs e) =>
        PostToUi(() => RestoreNetworkState(e));

    private void RestoreNetworkState(StateSyncEventArgs sync)
    {
        if (!_networkMode)
        {
            return;
        }

        _game = CreateGame(_settings);
        foreach (var applied in sync.Moves)
        {
            var result = applied.AutoPass
                ? _game.PlayAutoPass(applied.Move)
                : _game.Play(applied.Move);
            if (!result.IsSuccess)
            {
                _networkError = "Состояние партии не удалось восстановить.";
                NotifyAll();
                return;
            }
        }

        _networkHistory.Clear();
        _networkHistory.AddRange(sync.Moves);
        _scoring.Sync(_game.Board, _game.Status);
        _networkRemainingMs = Math.Max(0, sync.RemainingMs);
        _networkRemainingAt = _timeProvider.GetUtcNow();
        _networkTimerPaused = sync.Paused;
        _networkError = null;
        _networkAwaitingConfirmation = false;
        _networkFinished = _game.Status != GameStatus.InProgress;
        _networkWarningLevel = 0;
        _networkTurnCueMoveNumber = -1;
        _networkTerminalMessage = null;
        StopReconnectCountdown();
        StopClockIfFinished();
        NotifyAll();
        NotifyNetworkTurnCue();
    }

    private void OnGuestDisconnected(object? sender, EventArgs e) =>
        PostToUi(() =>
        {
            if (!_networkMode)
            {
                return;
            }

            _networkRemainingMs = (int)GetNetworkRemaining().TotalMilliseconds;
            _networkTimerPaused = true;
            StartReconnectCountdown();
            NotifyAll();
        });

    private void OnGuestReconnected(object? sender, EventArgs e) =>
        PostToUi(() =>
        {
            if (!_networkMode)
            {
                return;
            }

            _networkTimerPaused = false;
            StopReconnectCountdown();
            ShowNetworkNotice(NoticeOpponentBack);
            NotifyAll();
        });

    private void OnGuestFailed(object? sender, LanErrorEventArgs e) =>
        PostToUi(() => HandleNetworkError(e));

    private void HandleNetworkError(LanErrorEventArgs error)
    {
        if (!_networkMode)
        {
            return;
        }

        if (error.Code == LanErrorCodes.MoveRejected)
        {
            // Отклонённый ход — не потеря связи: гость остаётся в партии и может сходить снова.
            AppLogMessages.NetworkError(_log, error.Code, error.Message);
            _networkAwaitingConfirmation = false;
            NotifyAll();
            return;
        }

        if (_networkFinished && _game.Status != GameStatus.InProgress)
        {
            // Поздняя транспортная ошибка по уже доигранной партии: настоящий итог важнее (B1).
            _ = RunNetworkTaskAsync(async () =>
            {
                await StopNetworkSessionsAsync().ConfigureAwait(true);
                NotifyAll();
            });
            return;
        }

        // Любая другая сетевая ошибка не имеет осмысленного «продолжить ждать»: таймер
        // остался бы замершим навсегда, а баннер показывал «0 с» (M1 ревью P4).
        AppLogMessages.NetworkError(_log, error.Code, error.Message);
        _networkError = error.Message;
        _networkFinished = true;
        _networkAwaitingConfirmation = false;
        _networkTimerPaused = true;
        _networkTerminalMessage = error.Message;
        StopReconnectCountdown();
        _clock.Stop();
        MarkHostFinished();
        NotifyAll();
    }

    private async Task EndNetworkGameAsync(string message)
    {
        // Партия уже окончена по правилам — поздний обрыв (гость вышел после итога, истёк
        // 60-секундный срок) не должен превращать настоящий итог в «Партия прервана» (B1).
        var alreadyFinished = _game.Status != GameStatus.InProgress || HasOutcome;
        var host = _networkHost;
        if (host is not null && !alreadyFinished)
        {
            _ = await host.SendErrorAsync(LanErrorCodes.Stalled, message, CancellationToken.None).ConfigureAwait(true);
        }

        await StopNetworkSessionsAsync().ConfigureAwait(true);
        _networkFinished = true;
        _networkAwaitingConfirmation = false;
        _networkTimerPaused = true;
        StopReconnectCountdown();

        if (alreadyFinished || _networkTerminalMessage is not null)
        {
            // Итог уже показан (или партия доиграна): тихо закрываем сессию, экран не трогаем.
            NotifyAll();
            return;
        }

        _networkError = message;
        _networkTerminalMessage = message;
        _clock.Stop();
        AppLogMessages.NetworkError(_log, LanErrorCodes.Stalled, message);
        NotifyAll();
    }

    /// <summary>Сбрасывает сетевое состояние интерфейса при старте, выходе и восстановлении.</summary>
    private void ResetNetworkUiState()
    {
        _networkNotice = null;
        _networkNoticeUntil = default;
        _networkTerminalMessage = null;
        _networkReconnectDeadlineSet = false;
        _networkReconnectDeadline = default;
        _networkBannerDismissed = false;
        _networkWarningLevel = 0;
        _networkTurnCueMoveNumber = -1;
    }

    /// <summary>Сообщает хозяину, что партия окончена: переподключение её не оживит.</summary>
    /// <remarks>
    /// Хозяин в модели представления объявлен интерфейсом, а флаг «окончена» нужен только
    /// реальной комнате: подставные хозяева в проверках состояния партии не ведут.
    /// </remarks>
    private void MarkHostFinished()
    {
        if (_networkHost is LanGameHost host)
        {
            host.MarkFinished();
        }
    }

    /// <summary>Показывает короткое сетевое сообщение на несколько секунд.</summary>
    /// <param name="text">Текст сообщения.</param>
    private void ShowNetworkNotice(string text)
    {
        _networkNotice = text;
        _networkNoticeUntil = _timeProvider.GetUtcNow() + NoticeLifetime;
        OnPropertyChanged(nameof(NetworkNoticeText));
        OnPropertyChanged(nameof(IsNetworkNoticeVisible));
    }

    /// <summary>Убирает истёкшее сообщение.</summary>
    private void ClearExpiredNotice()
    {
        if (_networkNotice is null || _timeProvider.GetUtcNow() < _networkNoticeUntil)
        {
            return;
        }

        _networkNotice = null;
        OnPropertyChanged(nameof(NetworkNoticeText));
        OnPropertyChanged(nameof(IsNetworkNoticeVisible));
    }

    /// <summary>Сообщает, что ход перешёл к игроку (или что его время вышло).</summary>
    /// <param name="autoPass">Ход перешёл вынужденным пасом: сообщение об этом важнее cue.</param>
    private void NotifyNetworkTurnCue(bool autoPass = false)
    {
        if (!_networkMode || _networkFinished || _networkTimerPaused)
        {
            return;
        }

        var mine = _game.Status == GameStatus.InProgress && _game.ToMove == _networkPlayerColor;

        if (autoPass)
        {
            // Один текст на оба события: ребёнок видит и причину смены, и что ходить ему.
            ShowNetworkNotice(mine ? $"{NoticeTimeIsUp}. {NoticeMyTurn}" : NoticeTimeIsUp);
            _networkTurnCueMoveNumber = _game.MoveNumber;

            return;
        }

        if (mine && _networkTurnCueMoveNumber != _game.MoveNumber)
        {
            _networkTurnCueMoveNumber = _game.MoveNumber;
            ShowNetworkNotice(NoticeMyTurn);
        }
    }

    /// <summary>Уровень подсказки о таймере: 0 — рано, 30 — тридцать секунд, 10 — десять.</summary>
    /// <returns>0, 30 или 10.</returns>
    private int GetNetworkWarningLevel()
    {
        if (!_networkMode
            || _networkFinished
            || _networkTimerPaused
            || !_gameStarted
            || _game.Status != GameStatus.InProgress
            || _game.ToMove != _networkPlayerColor)
        {
            return 0;
        }

        var seconds = GetNetworkRemaining().TotalSeconds;

        if (seconds <= 10)
        {
            return 10;
        }

        return seconds <= 30 ? 30 : 0;
    }

    /// <summary>Обновляет подсказки таймера и мягкое выделение последних секунд.</summary>
    private void UpdateNetworkTimerWarning()
    {
        var level = GetNetworkWarningLevel();

        if (level > _networkWarningLevel && level > 0)
        {
            // Порог пересечён впервые за этот ход: короткое сообщение поверх плашки.
            ShowNetworkNotice(TimerWarning);
        }

        if (level == _networkWarningLevel)
        {
            return;
        }

        _networkWarningLevel = level;

        OnPropertyChanged(nameof(IsNetworkTimerWarningVisible));
        OnPropertyChanged(nameof(NetworkTimerWarningText));
        OnPropertyChanged(nameof(IsNetworkTimerUrgent));
    }

    /// <summary>Запоминает дедлайн возвращения соперника и показывает баннер обрыва.</summary>
    private void StartReconnectCountdown()
    {
        _networkReconnectDeadline = _timeProvider.GetUtcNow() + _networkOptions.ReconnectTimeout;
        _networkReconnectDeadlineSet = true;
        _networkBannerDismissed = false;

        OnPropertyChanged(nameof(NetworkReconnectSecondsLeft));
        OnPropertyChanged(nameof(IsNetworkDisconnected));
        OnPropertyChanged(nameof(IsNetworkBannerVisible));
        OnPropertyChanged(nameof(NetworkBannerTitle));
        OnPropertyChanged(nameof(NetworkBannerText));
        OnPropertyChanged(nameof(NetworkBannerLeaveText));
        OnPropertyChanged(nameof(IsNetworkBannerCanWait));
    }

    /// <summary>Снимает ожидание возвращения: соперник вернулся или партия окончена.</summary>
    private void StopReconnectCountdown()
    {
        _networkReconnectDeadlineSet = false;
        _networkBannerDismissed = false;

        OnPropertyChanged(nameof(NetworkReconnectSecondsLeft));
        OnPropertyChanged(nameof(IsNetworkDisconnected));
        OnPropertyChanged(nameof(IsNetworkBannerVisible));
        OnPropertyChanged(nameof(IsNetworkBannerCanWait));
        OnPropertyChanged(nameof(NetworkBannerText));
    }

    /// <summary>Отмечает сетевую партию начатой без часов ИИ.</summary>
    /// <remarks>
    /// В сетевой партии время партии не считается: единственный таймер — ход в 90 секунд,
    /// и второй (AI) таймер на экране только путал игрока (находка tester, R-010).
    /// </remarks>
    private void MarkNetworkStarted()
    {
        _gameStarted = true;
        _clock.Reset();
        StopClockIfFinished();
    }

    private TimeSpan GetNetworkRemaining()
    {
        if (_networkTimerPaused)
        {
            return TimeSpan.FromMilliseconds(Math.Max(0, _networkRemainingMs));
        }

        if (_networkHost is not null && _networkTurnTimer is not null)
        {
            return _networkTurnTimer.Remaining;
        }

        var elapsed = _timeProvider.GetUtcNow() - _networkRemainingAt;
        var left = TimeSpan.FromMilliseconds(_networkRemainingMs) - elapsed;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private void TickNetworkTimer()
    {
        if (!_networkMode)
        {
            return;
        }

        ClearExpiredNotice();
        UpdateNetworkTimerWarning();
        NotifyReconnectCountdown();

        OnPropertyChanged(nameof(NetworkTurnText));
        OnPropertyChanged(nameof(NetworkStatusText));
    }

    /// <summary>Обновляет обратный отсчёт возвращения соперника раз в секунду.</summary>
    private void NotifyReconnectCountdown()
    {
        if (!_networkReconnectDeadlineSet)
        {
            return;
        }

        OnPropertyChanged(nameof(NetworkReconnectSecondsLeft));
        OnPropertyChanged(nameof(NetworkBannerText));
        OnPropertyChanged(nameof(IsNetworkBannerVisible));
        OnPropertyChanged(nameof(IsNetworkDisconnected));
    }

    private void PostToUi(Action action)
    {
        var dispatch = _networkDispatch;
        if (dispatch is null)
        {
            action();
            return;
        }

        dispatch(action);
    }

    private async Task RunNetworkTaskAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            // Любая ошибка сетевого обработчика не должна валить интерфейс партии:
            // игрок увидит причину, а приложение останется живым.
            _networkError = $"Сетевая ошибка: {exception.Message}";
            NotifyAll();
        }
    }

    /// <summary>Освобождает таймеры и ожидание переподключения.</summary>
    public void Dispose()
    {
        _networkReconnectTimeout?.Cancel();
        _networkReconnectTimeout?.Dispose();
        _networkReconnectTimeout = null;
        _networkTurnTimer?.Dispose();
        _networkTurnTimer = null;
    }

    private static BoardSize ToBoardSize(int value) => value switch
    {
        13 => BoardSize.Size13,
        19 => BoardSize.Size19,
        _ => BoardSize.Size9
    };
}