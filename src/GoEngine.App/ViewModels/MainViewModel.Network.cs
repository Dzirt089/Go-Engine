using System.Globalization;
using System.Net.Sockets;
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
    private bool _colorDrawActive;
    private bool _colorDrawShowGuess;
    private bool _colorDrawShowChoice;
    private bool _colorDrawGuestWins;
    private string? _colorDrawCommitment;
    private string _colorDrawStateText = string.Empty;
    private int _colorDrawCount;
    private string? _colorDrawNonce;
    private DateTimeOffset _colorDrawDeadline;
    private bool _colorDrawDeadlineSet;
    private TaskCompletionSource<string>? _colorDrawGuessSource;
    private TaskCompletionSource<StoneColor>? _colorDrawChoiceSource;
    private string? _colorDrawGuess;

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

            if (_colorDrawActive)
            {
                return "Розыгрыш цвета";
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
        host.ColorDrawGuessReceived += OnHostColorDrawGuess;
        host.ColorDrawChoiceReceived += OnHostColorDrawChoice;
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
        _networkHost.ColorDrawGuessReceived -= OnHostColorDrawGuess;
        _networkHost.ColorDrawChoiceReceived -= OnHostColorDrawChoice;
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
        client.ColorDrawOfferReceived += OnGuestColorDrawOffer;
        client.ColorDrawRevealReceived += OnGuestColorDrawReveal;
        client.ColorDrawDoneReceived += OnGuestColorDrawDone;
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
        _networkClient.ColorDrawOfferReceived -= OnGuestColorDrawOffer;
        _networkClient.ColorDrawRevealReceived -= OnGuestColorDrawReveal;
        _networkClient.ColorDrawDoneReceived -= OnGuestColorDrawDone;
    }

    private void OnHostMoveRequested(object? sender, GuestMoveRequestEventArgs e) =>
        PostToUi(() => _ = RunNetworkTaskAsync(() => HandleGuestMoveAsync(e)));

    private void OnHostStateSyncRequested(object? sender, GuestStateSyncRequestedEventArgs e) =>
        PostToUi(() => _ = RunNetworkTaskAsync(SendHostStateSyncAsync));

    private void OnHostDisconnected(object? sender, EventArgs e) =>
        PostToUi(() =>
        {
            if (!_networkMode)
            {
                return;
            }

            if (_colorDrawActive)
            {
                // Цвета ещё не разыграны: без розыгрыша партию не начать, а ждать возврата
                // с неопределёнными цветами — тупик. Закрываем терминалом (R-004).
                AbortColorDraw("Розыгрыш цвета прерван: связь потеряна.");
                return;
            }

            HandleHostDisconnectedCore();
        });

    private void HandleHostDisconnectedCore() =>
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

            if (_colorDrawActive)
            {
                // Цвета ещё не разыграны: ждать возврата без них нельзя, закрываем терминалом.
                AbortColorDraw("Розыгрыш цвета прерван: связь потеряна.");
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

        // Обрыв во время розыгрыша: ожидания цвета больше не нужны, оверлей закрывается,
        // а терминальное сообщение остаётся тем, что пришло первым (F3).
        CancelColorDraw();

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
        // Розыгрыш цвета в этот момент уже не нужен: отменяем ожидания (F3).
        CancelColorDraw();
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

    /// <summary>Идёт нигири-розыгрыш цвета в начале сетевой партии.</summary>
    public bool IsColorDrawActive => _colorDrawActive;

    /// <summary>Заголовок оверлея розыгрыша.</summary>
    public string ColorDrawTitle => "Розыгрыш цвета";

    /// <summary>Что происходит в розыгрыше: короткая фраза для ребёнка.</summary>
    public string ColorDrawStateText => _colorDrawStateText;

    /// <summary>Сколько секунд осталось на ответ в розыгрыше; 0 — ответ не ждём.</summary>
    public int ColorDrawSecondsLeft
    {
        get
        {
            if (!_colorDrawDeadlineSet)
            {
                return 0;
            }

            var left = _colorDrawDeadline - _timeProvider.GetUtcNow();

            return left > TimeSpan.Zero ? (int)Math.Ceiling(left.TotalSeconds) : 0;
        }
    }

    /// <summary>Гость должен выбрать «Чёт/Нечет»: показываем крупные кнопки.</summary>
    public bool ColorDrawShowGuess => _colorDrawActive && _colorDrawShowGuess;

    /// <summary>Победитель розыгрыша выбирает цвет: показываем «Чёрные/Белые».</summary>
    public bool ColorDrawShowChoice => _colorDrawActive && _colorDrawShowChoice;

    /// <summary>Начинает розыгрыш цвета на стороне хозяина (нигири, commit-reveal).</summary>
    /// <param name="cancellationToken">Отмена розыгрыша.</param>
    /// <returns>Задача розыгрыша; завершается, когда оба игрока получили цвета.</returns>
    /// <remarks>
    /// Хозяин — арбитр: он прячет камни, ждёт ответ гостя, раскрывает горсть и ждёт выбор
    /// победителя. Таймеры хода на время розыгрыша стоят: первый ход начинается только после
    /// @@color_draw_done@@. Таймауты закрываются случайным ответом, чтобы партия не зависла.
    /// </remarks>
    public async Task BeginColorDrawAsync(CancellationToken cancellationToken = default)
    {
        if (!_networkMode || _networkHost is not { } host || _colorDrawActive)
        {
            return;
        }

        try
        {
            _colorDrawActive = true;
            _networkTimerPaused = true;
            _networkTurnTimer?.Pause();
            _networkWarningLevel = 0;
            _colorDrawShowGuess = false;
            _colorDrawShowChoice = false;
            _colorDrawGuestWins = false;
            _colorDrawCount = ColorDraw.CreateCount(_random);
            _colorDrawNonce = ColorDraw.CreateNonce(_random);
            _colorDrawCommitment = ColorDraw.Commitment(_colorDrawCount, _colorDrawNonce);
            // Ребёнок-хозяин должен понимать, что он «держит в горсти»: число и чётность
            // видны только ему, гость увидит их лишь после раскрытия.
            var parity = ColorDraw.ParityOf(_colorDrawCount) == ColorDraw.GuessOdd ? "нечёт" : "чёт";
            _colorDrawStateText =
                $"В твоей горсти {_colorDrawCount} {ColorDraw.StonesWord(_colorDrawCount)} ({parity}). Друг не видит число — пусть угадывает.";
            StartColorDrawDeadline(_networkOptions.ColorDrawTimeout);
            _colorDrawGuessSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            NotifyColorDraw();

            var offer = await host.SendColorDrawOfferAsync(
                _colorDrawCommitment,
                (int)_networkOptions.ColorDrawTimeout.TotalMilliseconds,
                cancellationToken).ConfigureAwait(true);
            if (!offer.IsSuccess)
            {
                AbortColorDraw(offer.Error);
                return;
            }

            var guess = await WaitColorDrawGuessAsync(cancellationToken).ConfigureAwait(true);
            if (!_colorDrawActive)
            {
                return;
            }

            if (guess is null)
            {
                // Гость не ответил: хозяин угадывает сам, как в живом нигири при опоздании.
                guess = _random.Next(2) == 0 ? ColorDraw.GuessOdd : ColorDraw.GuessEven;
            }

            _colorDrawGuestWins = ColorDraw.IsGuessCorrect(_colorDrawCount, guess);
            _colorDrawStateText = ColorDrawResultText(_colorDrawCount, _colorDrawGuestWins);
            _colorDrawShowChoice = !_colorDrawGuestWins;
            StartColorDrawDeadline(_networkOptions.ColorDrawTimeout);
            NotifyColorDraw();

            var reveal = await host.SendColorDrawRevealAsync(
                _colorDrawCount,
                _colorDrawNonce!,
                _colorDrawGuestWins,
                cancellationToken).ConfigureAwait(true);
            if (!reveal.IsSuccess)
            {
                AbortColorDraw(reveal.Error);
                return;
            }

            StoneColor chosen;
            if (_colorDrawGuestWins)
            {
                _colorDrawStateText = "Друг выбирает цвет…";
                _colorDrawDeadlineSet = false;
                _colorDrawChoiceSource = new TaskCompletionSource<StoneColor>(TaskCreationOptions.RunContinuationsAsynchronously);
                NotifyColorDraw();
                var choice = await WaitColorDrawChoiceAsync(cancellationToken).ConfigureAwait(true);
                if (!_colorDrawActive)
                {
                    return;
                }

                chosen = choice ?? RandomColor();
            }
            else
            {
                _colorDrawStateText = "Ты угадал! Выбери цвет.";
                _colorDrawShowChoice = true;
                StartColorDrawDeadline(_networkOptions.ColorDrawTimeout);
                _colorDrawChoiceSource = new TaskCompletionSource<StoneColor>(TaskCreationOptions.RunContinuationsAsynchronously);
                NotifyColorDraw();
                var choice = await WaitColorDrawChoiceAsync(cancellationToken).ConfigureAwait(true);
                if (!_colorDrawActive)
                {
                    return;
                }

                chosen = choice ?? RandomColor();
            }

            // chosen — цвет того, кто выбирал: гостя в ветке «гость угадал», хозяина иначе.
            // Дальше раскладываем его по сторонам; перепутать местами — значит отдать гостю
            // цвет, противоположный его выбору (баг приёмки v0.2.130).
            var guestColor = _colorDrawGuestWins ? chosen : ColorDraw.Opposite(chosen);
            var hostColor = ColorDraw.Opposite(guestColor);
            var done = await host.SendColorDrawDoneAsync(hostColor, guestColor, cancellationToken).ConfigureAwait(true);
            if (!done.IsSuccess)
            {
                AbortColorDraw(done.Error);
                return;
            }

            CompleteColorDraw(hostColor);
        }
        catch (OperationCanceledException)
        {
            // Отмена (выход из партии): состояние сбросит ResetNetworkUiState.
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            AbortColorDraw($"Розыгрыш цвета прерван: {exception.Message}");
        }
        finally
        {
            _colorDrawGuessSource = null;
            _colorDrawChoiceSource = null;
        }
    }

    /// <summary>Гость отвечает на нигири: «Чёт» или «Нечет».</summary>
    /// <param name="odd"><c>true</c> — нечет, <c>false</c> — чёт.</param>
    /// <returns>Задача отправки.</returns>
    public async Task GuessColorDrawAsync(bool odd)
    {
        if (!_colorDrawActive || _networkClient is not { } client || !_colorDrawShowGuess)
        {
            return;
        }

        _colorDrawShowGuess = false;
        _colorDrawGuess = odd ? ColorDraw.GuessOdd : ColorDraw.GuessEven;
        _colorDrawStateText = "Ответ отправлен. Хозяин раскрывает камни…";

        // Ждём раскрытия не бесконечно: если хозяин пропал, гость сам закроет розыгрыш.
        StartColorDrawDeadline(_networkOptions.ColorDrawTimeout);
        NotifyColorDraw();

        var sent = await client.SendColorDrawGuessAsync(
            _colorDrawGuess,
            CancellationToken.None).ConfigureAwait(true);
        if (!sent.IsSuccess)
        {
            AbortColorDraw(sent.Error);
        }
    }

    /// <summary>Победитель розыгрыша выбирает свой цвет.</summary>
    /// <param name="color">Чёрные или белые.</param>
    /// <returns>Задача выбора.</returns>
    public async Task ChooseColorDrawColorAsync(StoneColor color)
    {
        if (!_colorDrawActive || color is null || color == StoneColor.Empty)
        {
            return;
        }

        if (_networkClient is { } client)
        {
            if (!_colorDrawGuestWins || !_colorDrawShowChoice)
            {
                return;
            }

            _colorDrawShowChoice = false;
            _colorDrawStateText = "Цвет выбран. Начинаем…";

            // Ждём итоговые цвета не бесконечно: пропавший хозяин не оставит оверлей навсегда.
            StartColorDrawDeadline(_networkOptions.ColorDrawTimeout);
            NotifyColorDraw();

            var sent = await client.SendColorDrawChoiceAsync(color, CancellationToken.None).ConfigureAwait(true);
            if (!sent.IsSuccess)
            {
                AbortColorDraw(sent.Error);
            }

            return;
        }

        // Хозяин-победитель: выбор идёт в его же модели, без сети.
        _colorDrawChoiceSource?.TrySetResult(color);
    }

    /// <summary>Ждёт ответ гостя на нигири; по таймауту возвращает <c>null</c>.</summary>
    private async Task<string?> WaitColorDrawGuessAsync(CancellationToken cancellationToken)
    {
        // Локальная ссылка: при выходе из партии ResetNetworkUiState обнуляет поле,
        // и сравнение «кто завершился» по полю дало бы NullReferenceException.
        var source = _colorDrawGuessSource ??= new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(_networkOptions.ColorDrawTimeout, _timeProvider, linked.Token);
        var completed = await Task.WhenAny(source.Task, delay).ConfigureAwait(true);
        await linked.CancelAsync().ConfigureAwait(true);

        return completed == source.Task ? await source.Task.ConfigureAwait(true) : null;
    }

    /// <summary>Ждёт выбора цвета победителем; по таймауту возвращает <c>null</c>.</summary>
    private async Task<StoneColor?> WaitColorDrawChoiceAsync(CancellationToken cancellationToken)
    {
        var source = _colorDrawChoiceSource ??= new TaskCompletionSource<StoneColor>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(_networkOptions.ColorDrawTimeout, _timeProvider, linked.Token);
        var completed = await Task.WhenAny(source.Task, delay).ConfigureAwait(true);
        await linked.CancelAsync().ConfigureAwait(true);

        if (completed != source.Task)
        {
            return null;
        }

        return await source.Task.ConfigureAwait(true);
    }

    /// <summary>Гость получил предложение нигири: показывает «Чёт/Нечет».</summary>
    private void OnGuestColorDrawOffer(object? sender, ColorDrawOfferEventArgs e) =>
        PostToUi(() =>
        {
            if (!_networkMode || _networkClient is null)
            {
                return;
            }

            _colorDrawActive = true;
            _networkTimerPaused = true;
            _colorDrawCommitment = e.Commitment;
            _colorDrawShowGuess = true;
            _colorDrawShowChoice = false;
            _colorDrawGuestWins = false;
            _colorDrawStateText = "Друг спрятал камни в горсть. Как думаешь, сколько их — чёт или нечёт?";
            StartColorDrawDeadline(TimeSpan.FromMilliseconds(Math.Max(1000, e.TimeoutMs)));
            NotifyColorDraw();
        });

    /// <summary>Гость увидел раскрытие: проверяет обязательство и показывает результат.</summary>
    private void OnGuestColorDrawReveal(object? sender, ColorDrawRevealEventArgs e) =>
        PostToUi(() =>
        {
            if (!_networkMode)
            {
                return;
            }

            if (!ColorDraw.Verify(e.Count, e.Nonce, _colorDrawCommitment ?? string.Empty))
            {
                AbortColorDraw("Хозяин прислал неверное раскрытие нигири.");
                return;
            }

            // Победителя считаем сами по своему ответу: хеш защищает число, но не флаг
            // winnerIsGuest. Расхождение — хозяин прислал противоречивый результат (F1).
            var expectedWinner = ColorDraw.IsGuessCorrect(e.Count, _colorDrawGuess ?? string.Empty);
            if (expectedWinner != e.WinnerIsGuest)
            {
                AbortColorDraw("Хозяин прислал противоречивый результат нигири.");
                return;
            }

            _colorDrawShowGuess = false;
            _colorDrawGuestWins = expectedWinner;
            _colorDrawShowChoice = expectedWinner;
            _colorDrawStateText = ColorDrawResultText(e.Count, expectedWinner)
                + (expectedWinner ? " Ты выбираешь цвет." : " Ждём выбора хозяина.");
            StartColorDrawDeadline(_networkOptions.ColorDrawTimeout);
            NotifyColorDraw();
        });

    /// <summary>Гость получил итоговые цвета: розыгрыш завершён.</summary>
    private void OnGuestColorDrawDone(object? sender, ColorDrawDoneEventArgs e) =>
        PostToUi(() =>
        {
            if (!_networkMode || !_colorDrawActive)
            {
                return;
            }

            // Цвета — только чёрные/белые и обязательно разные: иначе партия несогласована (F2).
            if ((e.HostColor != StoneColor.Black && e.HostColor != StoneColor.White)
                || (e.GuestColor != StoneColor.Black && e.GuestColor != StoneColor.White)
                || e.HostColor == e.GuestColor)
            {
                AbortColorDraw("Хозяин прислал некорректные цвета нигири.");
                return;
            }

            CompleteColorDraw(e.GuestColor);
        });

    /// <summary>Хозяин получил ответ гостя на нигири.</summary>
    private void OnHostColorDrawGuess(object? sender, ColorDrawGuessEventArgs e) =>
        PostToUi(() =>
        {
            // Хозяин — арбитр: принимаем только «чёт»/«нечет», остальное игнорируем (F2).
            if (e.Parity is ColorDraw.GuessOdd or ColorDraw.GuessEven)
            {
                _colorDrawGuessSource?.TrySetResult(e.Parity);
            }
        });

    /// <summary>Хозяин получил выбор цвета гостя-победителя.</summary>
    private void OnHostColorDrawChoice(object? sender, ColorDrawChoiceEventArgs e) =>
        PostToUi(() =>
        {
            // Только чёрные или белые: Empty/мусор в настройки не попадает (F2).
            if (e.Color == StoneColor.Black || e.Color == StoneColor.White)
            {
                _colorDrawChoiceSource?.TrySetResult(e.Color);
            }
        });

    /// <summary>Завершает розыгрыш: применяет цвет, запускает таймер хода и первый ход.</summary>
    private void CompleteColorDraw(StoneColor playerColor)
    {
        _colorDrawActive = false;
        _colorDrawShowGuess = false;
        _colorDrawShowChoice = false;
        _colorDrawDeadlineSet = false;
        _colorDrawStateText = string.Empty;
        _colorDrawCommitment = null;
        _colorDrawNonce = null;
        _networkPlayerColor = playerColor;
        _settings = _settings with { PlayerColor = playerColor.Name };
        _networkTimerPaused = false;

        if (_networkHost is not null)
        {
            StartNetworkTimer();
        }

        NotifyColorDraw();
        NotifyAll();
        NotifyNetworkTurnCue();

        if (_networkHost is not null)
        {
            _ = RunNetworkTaskAsync(SendHostTurnStateAsync);
        }
    }

    /// <summary>Сбрасывает состояние розыгрыша и отменяет ожидания, не ставя терминал.</summary>
    private void CancelColorDraw()
    {
        var wasActive = _colorDrawActive;
        _colorDrawActive = false;
        _colorDrawShowGuess = false;
        _colorDrawShowChoice = false;
        _colorDrawDeadlineSet = false;
        _colorDrawStateText = string.Empty;
        _colorDrawGuess = null;
        _colorDrawGuessSource?.TrySetCanceled();
        _colorDrawChoiceSource?.TrySetCanceled();
        _colorDrawGuessSource = null;
        _colorDrawChoiceSource = null;

        if (wasActive)
        {
            NotifyColorDraw();
        }
    }

    /// <summary>Прерывает розыгрыш до начала партии: причина уходит в терминальное состояние.</summary>
    private void AbortColorDraw(string? message)
    {
        CancelColorDraw();

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // Гость не должен остаться с оверлеем без объяснения: шлём ему ту же причину (F4).
        if (_networkHost is { } host)
        {
            _ = RunNetworkTaskAsync(async () =>
            {
                await host.SendErrorAsync(LanErrorCodes.ProtocolError, message, CancellationToken.None).ConfigureAwait(true);
            });
        }

        // Прерывание розыгрыша — терминальное состояние: партию без цветов не начать.
        AppLogMessages.NetworkError(_log, LanErrorCodes.ProtocolError, message);
        _networkError = message;
        _networkFinished = true;
        _networkAwaitingConfirmation = false;
        _networkTimerPaused = true;
        _networkTerminalMessage = message;
        StopReconnectCountdown();
        _clock.Stop();
        MarkHostFinished();
        NotifyAll();
    }

    /// <summary>Ставит дедлайн ответа и сообщает виду, что пошёл отсчёт.</summary>
    private void StartColorDrawDeadline(TimeSpan timeout)
    {
        _colorDrawDeadline = _timeProvider.GetUtcNow() + timeout;
        _colorDrawDeadlineSet = true;
    }

    /// <summary>Текст результата раскрытия: число, чётность и кто угадал.</summary>
    private static string ColorDrawResultText(int count, bool guestWon)
    {
        var word = count % 2 == 1 ? "нечет" : "чёт";

        return $"В горсти было {count} {ColorDraw.StonesWord(count)} — {word}. {(guestWon ? "Друг угадал!" : "Друг не угадал.")}";
    }

    /// <summary>Случайный цвет на случай, если победитель не успел выбрать.</summary>
    private StoneColor RandomColor() => _random.Next(2) == 0 ? StoneColor.Black : StoneColor.White;

    /// <summary>Сообщает виду о состоянии розыгрыша.</summary>
    private void NotifyColorDraw()
    {
        OnPropertyChanged(nameof(IsColorDrawActive));
        OnPropertyChanged(nameof(ColorDrawStateText));
        OnPropertyChanged(nameof(ColorDrawSecondsLeft));
        OnPropertyChanged(nameof(ColorDrawShowGuess));
        OnPropertyChanged(nameof(ColorDrawShowChoice));
        OnPropertyChanged(nameof(ColorDrawTitle));
    }

    /// <summary>Отправляет гостю состояние таймера после розыгрыша.</summary>
    private async Task SendHostTurnStateAsync()
    {
        var host = _networkHost;
        if (host is null)
        {
            return;
        }

        var sent = await host.SendTurnStateAsync(
            (int)GetNetworkRemaining().TotalMilliseconds,
            paused: false,
            CancellationToken.None).ConfigureAwait(true);
        if (!sent.IsSuccess)
        {
            _networkError = sent.Error;
            NotifyAll();
        }
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
        _colorDrawActive = false;
        _colorDrawShowGuess = false;
        _colorDrawShowChoice = false;
        _colorDrawGuestWins = false;
        _colorDrawDeadlineSet = false;
        _colorDrawStateText = string.Empty;
        _colorDrawCommitment = null;
        _colorDrawNonce = null;
        _colorDrawGuess = null;
        _colorDrawGuessSource?.TrySetCanceled();
        _colorDrawChoiceSource?.TrySetCanceled();
        _colorDrawGuessSource = null;
        _colorDrawChoiceSource = null;
        NotifyColorDraw();
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

        if (_colorDrawActive)
        {
            OnPropertyChanged(nameof(ColorDrawSecondsLeft));

            // Гость ждёт хозяина; если дедлайн истёк и ввода от гостя не ждём — розыгрыш
            // не завершился, закрываем терминалом, а не висим оверлеем (F4).
            if (_networkClient is not null
                && _colorDrawDeadlineSet
                && ColorDrawSecondsLeft == 0
                && !_colorDrawShowGuess
                && !_colorDrawShowChoice)
            {
                AbortColorDraw("Розыгрыш цвета не завершился: хозяин не прислал результат.");
            }
        }
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