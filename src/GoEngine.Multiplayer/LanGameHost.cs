using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using GoEngine.Core;

namespace GoEngine.Multiplayer;

/// <summary>Хозяин комнаты: слушает гостя, объявляет комнату и передаёт сообщения партии.</summary>
/// <remarks>
/// <para>
/// Хозяин — единственный арбитр партии: он применяет ходы, ведёт таймер 90 секунд и делает
/// вынужденный пас. Гость присылает только просьбы о ходе, поэтому двойного паса и рассинхрона
/// быть не может. Сетевой слой не знает правил игры и не хранит <see cref="GameState"/>:
/// применение ходов и подсчёт — дело модели представления.
/// </para>
/// <para>
/// Комната объявляется в локальной сети, пока идёт ожидание. После подключения гостя объявления
/// прекращаются; при обрыве хозяин продолжает слушать только переподключение с тем же токеном.
/// </para>
/// </remarks>
public sealed class LanGameHost : INetworkGameHost
{
    private readonly MultiplayerOptions _options;
    private readonly object _sync = new();
    private TcpListener? _listener;
    private RoomAnnouncer? _announcer;
    private CancellationTokenSource? _lifetime;
    private Task? _acceptLoop;
    private LanTlsConnection? _connection;
    private Task? _receiveLoop;
    private Task? _monitorLoop;
    private RoomInfo? _room;
    private string? _gameId;
    private string? _sessionToken;
    private int _boardSize;
    private int _hostAvatarId;
    private int _roomNumber;
    private StoneColor _guestColor = StoneColor.White;
    private double _komi;
    private bool _inGame;
    private bool _disposed;
    private DateTimeOffset _lastReceivedAt;
    private PendingJoin? _pendingJoin;
    private TaskCompletionSource<JoinDecision>? _pendingDecision;
    private bool _finished;

    /// <summary>Создаёт хозяина комнаты.</summary>
    /// <param name="options">Настройки сети; <c>null</c> — значения по умолчанию.</param>
    public LanGameHost(MultiplayerOptions? options = null)
    {
        _options = options ?? new MultiplayerOptions();
    }

    /// <summary>Гость подключился и ждёт решения хозяина.</summary>
    public event EventHandler<GuestJoinRequestEventArgs>? GuestJoinRequested;

    /// <summary>Гость просит сделать ход.</summary>
    public event EventHandler<GuestMoveRequestEventArgs>? GuestMoveRequested;

    /// <summary>Гость просит полное состояние партии после переподключения.</summary>
    public event EventHandler<GuestStateSyncRequestedEventArgs>? GuestStateSyncRequested;

    /// <summary>Гость подключён, партия началась.</summary>
    public event EventHandler? GuestConnected;

    /// <summary>Связь с гостем потеряна; хозяин ждёт переподключения.</summary>
    public event EventHandler? GuestDisconnected;

    /// <summary>Гость вернулся после обрыва.</summary>
    public event EventHandler? GuestReconnected;

    /// <summary>Гость корректно вышел из партии.</summary>
    public event EventHandler? GuestLeft;

    /// <summary>Заявка гостя снята до решения хозяина: гость закрыл соединение или истёк таймаут.</summary>
    /// <remarks>
    /// Отдельное событие, а не <see cref="Failed"/>: это не ошибка сети, а конец ожидания —
    /// лобби должно убрать карточку «хочет играть» и ждать следующего гостя.
    /// </remarks>
    public event EventHandler? GuestJoinCancelled;

    /// <summary>Сетевая ошибка, которую нужно показать игроку.</summary>
    public event EventHandler<LanErrorEventArgs>? Failed;

    /// <summary>Гость ответил на нигири: чёт или нечет.</summary>
    public event EventHandler<ColorDrawGuessEventArgs>? ColorDrawGuessReceived;

    /// <summary>Гость выбрал цвет после победы в нигири.</summary>
    public event EventHandler<ColorDrawChoiceEventArgs>? ColorDrawChoiceReceived;

    /// <summary>Комната, пока хозяин её ждёт; <c>null</c> до запуска.</summary>
    public RoomInfo? Room
    {
        get
        {
            lock (_sync)
            {
                return _room;
            }
        }
    }

    /// <summary>Гость подключён и партия идёт.</summary>
    public bool IsInGame
    {
        get
        {
            lock (_sync)
            {
                return _inGame;
            }
        }
    }

    /// <summary>Есть активное соединение с гостем.</summary>
    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _connection is not null;
            }
        }
    }

    /// <summary>Отмечает партию оконченной: переподключение больше не оживляет её.</summary>
    /// <remarks>
    /// Вызывается моделью представления, когда партия завершилась по правилам или сдачей.
    /// Соединение при этом не рвётся сразу: гость получает итоговый ход и итог, а попытка
    /// resume после конца получает отказ (R-007).
    /// </remarks>
    public void MarkFinished()
    {
        lock (_sync)
        {
            _finished = true;
        }
    }

    /// <summary>Запускает комнату: слушает порт и объявляет её в локальной сети.</summary>
    /// <param name="hostAvatarId">Номер зверя-аватара хозяина, 0..7.</param>
    /// <param name="roomNumber">Номер комнаты для выбора, 1..999.</param>
    /// <param name="boardSize">Сторона доски: 9, 13 или 19.</param>
    /// <param name="cancellationToken">Отмена запуска.</param>
    /// <returns>Описание комнаты или типизированная ошибка.</returns>
    public async Task<Result<RoomInfo>> StartHostingAsync(
        int hostAvatarId,
        int roomNumber,
        int boardSize,
        CancellationToken cancellationToken)
    {
        if (hostAvatarId is < 0 or > 7)
        {
            return Result<RoomInfo>.Fail("Номер аватара хозяина должен быть от 0 до 7.");
        }

        if (roomNumber is < 1 or > 999)
        {
            return Result<RoomInfo>.Fail("Номер комнаты должен быть от 1 до 999.");
        }

        if (boardSize is not (9 or 13 or 19))
        {
            return Result<RoomInfo>.Fail("Размер доски должен быть 9, 13 или 19.");
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return Result<RoomInfo>.Fail("Хозяин комнаты уже остановлен.");
            }

            if (_listener is not null)
            {
                return Result<RoomInfo>.Fail("Комната уже создана.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var listenerResult = StartListener();
        if (!listenerResult.IsSuccess)
        {
            return Result<RoomInfo>.Fail(listenerResult.Error!);
        }

        var listener = listenerResult.Value!;
        var localAddress = LanNetwork.GetLocalIPv4Address();
        if (string.IsNullOrWhiteSpace(localAddress))
        {
            listener.Stop();
            return Result<RoomInfo>.Fail("Не удалось определить адрес в локальной сети. Проверьте Wi-Fi.");
        }

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Короткий цифровой код вместо 32-символьного GUID: его диктуют взрослому
        // при ручном подключении. Уникальности шести цифр в одной локальной сети достаточно;
        // безопасность даёт отдельный sessionToken, а не этот код.
        var gameId = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var sessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var announcement = new RoomAnnouncement(gameId, hostAvatarId, roomNumber, boardSize, port, _options.ProtocolVersion);
        var announcer = new RoomAnnouncer(announcement, _options);
        var announced = await announcer.StartAsync().ConfigureAwait(false);
        if (!announced.IsSuccess)
        {
            listener.Stop();
            return Result<RoomInfo>.Fail(announced.Error!);
        }

        var room = new RoomInfo(
            gameId,
            hostAvatarId,
            roomNumber,
            boardSize,
            port,
            localAddress,
            _options.ProtocolVersion,
            _options.TimeProvider.GetUtcNow());
        var lifetime = new CancellationTokenSource();
        lock (_sync)
        {
            _listener = listener;
            _announcer = announcer;
            _lifetime = lifetime;
            _gameId = gameId;
            _sessionToken = sessionToken;
            _boardSize = boardSize;
            _hostAvatarId = hostAvatarId;
            _roomNumber = roomNumber;
            _room = room;
            _finished = false;
            _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, lifetime.Token), CancellationToken.None);
        }

        return Result<RoomInfo>.Ok(room);
    }

    /// <summary>Пустить гостя в комнату и начать партию.</summary>
    /// <param name="guestColor">Цвет гостя; хозяин играет противоположным.</param>
    /// <param name="komi">Коми партии, неотрицательное.</param>
    /// <param name="cancellationToken">Отмена ответа.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> AcceptGuestAsync(StoneColor guestColor, double komi, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (guestColor is null || guestColor == StoneColor.Empty)
        {
            return Task.FromResult(Result.Fail("Гость должен играть чёрными или белыми."));
        }

        if (double.IsNaN(komi) || komi < 0)
        {
            return Task.FromResult(Result.Fail("Коми должно быть неотрицательным числом."));
        }

        TaskCompletionSource<JoinDecision>? decision;
        lock (_sync)
        {
            decision = _pendingDecision;
        }

        if (decision is null)
        {
            return Task.FromResult(Result.Fail("Нет гостя, ожидающего решения."));
        }

        return decision.TrySetResult(new JoinDecision(true, guestColor, komi, null))
            ? Task.FromResult(Result.Ok())
            : Task.FromResult(Result.Fail("Решение по гостю уже принято."));
    }

    /// <summary>Отклонить подключение гостя.</summary>
    /// <param name="reason">Причина для гостя на русском языке.</param>
    /// <param name="cancellationToken">Отмена ответа.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> RejectGuestAsync(string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        TaskCompletionSource<JoinDecision>? decision;
        lock (_sync)
        {
            decision = _pendingDecision;
        }

        if (decision is null)
        {
            return Task.FromResult(Result.Fail("Нет гостя, ожидающего решения."));
        }

        return decision.TrySetResult(new JoinDecision(false, StoneColor.White, 0, reason))
            ? Task.FromResult(Result.Ok())
            : Task.FromResult(Result.Fail("Решение по гостю уже принято."));
    }

    /// <summary>Сообщить гостю о применённом ходе.</summary>
    /// <param name="move">Применённый ход.</param>
    /// <param name="autoPass">Ход был вынужденным пасом.</param>
    /// <param name="moveNumber">Номер хода.</param>
    /// <param name="remainingMs">Сколько осталось на следующий ход.</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> SendMoveAppliedAsync(
        Move move,
        bool autoPass,
        int moveNumber,
        int remainingMs,
        CancellationToken cancellationToken)
    {
        if (move.IsNone)
        {
            return Task.FromResult(Result.Fail("Нельзя отправить пустой ход."));
        }

        if (moveNumber <= 0 || remainingMs < 0)
        {
            return Task.FromResult(Result.Fail("Некорректный номер хода или остаток времени."));
        }

        var point = move.Type == MoveType.Play ? move.Point : default;
        return SendAsync(new LanMessage
        {
            Type = LanMessageTypes.MoveApplied,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            MoveNumber = moveNumber,
            MoveType = move.Type.Name,
            Color = move.Color.Name,
            X = move.Type == MoveType.Play ? point.X : null,
            Y = move.Type == MoveType.Play ? point.Y : null,
            AutoPass = autoPass,
            RemainingMs = remainingMs
        }, cancellationToken);
    }

    /// <summary>Сообщить гостю состояние таймера (пауза или продолжение).</summary>
    /// <param name="remainingMs">Сколько осталось на ход.</param>
    /// <param name="paused">Таймер стоит на паузе.</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> SendTurnStateAsync(int remainingMs, bool paused, CancellationToken cancellationToken)
    {
        if (remainingMs < 0)
        {
            return Task.FromResult(Result.Fail("Остаток времени не может быть отрицательным."));
        }

        return SendAsync(new LanMessage
        {
            Type = LanMessageTypes.TurnState,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            RemainingMs = remainingMs,
            Paused = paused
        }, cancellationToken);
    }

    /// <summary>Отправить гостю полное состояние партии после переподключения.</summary>
    /// <param name="moves">Все ходы партии по порядку.</param>
    /// <param name="remainingMs">Сколько осталось на текущий ход.</param>
    /// <param name="paused">Таймер стоит на паузе.</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> SendStateSyncAsync(
        IReadOnlyList<AppliedMove> moves,
        int remainingMs,
        bool paused,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moves);
        if (remainingMs < 0)
        {
            return Task.FromResult(Result.Fail("Остаток времени не может быть отрицательным."));
        }

        var wireMoves = moves
            .Select(move => MoveWire.FromMove(move.MoveNumber, move.Move, move.AutoPass))
            .ToArray();
        return SendAsync(new LanMessage
        {
            Type = LanMessageTypes.StateSync,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            RemainingMs = remainingMs,
            Paused = paused,
            Moves = wireMoves
        }, cancellationToken);
    }

    /// <summary>Сообщить гостю, что его ход отклонён, не закрывая соединение.</summary>
    /// <param name="code">Код ошибки из <see cref="LanErrorCodes"/>.</param>
    /// <param name="message">Понятное сообщение на русском.</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> SendMoveRejectedAsync(string code, string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return SendAsync(new LanMessage
        {
            Type = LanMessageTypes.MoveRejected,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            Code = code,
            Message = message
        }, cancellationToken);
    }

    /// <summary>Сообщить гостю об ошибке и завершить партию.</summary>
    /// <param name="code">Код ошибки из <see cref="LanErrorCodes"/>.</param>
    /// <param name="message">Понятное сообщение на русском.</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> SendErrorAsync(string code, string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return SendAsync(new LanMessage
        {
            Type = LanMessageTypes.Error,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            Code = code,
            Message = message
        }, cancellationToken);
    }

    /// <summary>Предложить гостю нигири: обязательство и срок ответа.</summary>
    /// <param name="commitment">Хеш числа камней и соли.</param>
    /// <param name="timeoutMs">Сколько миллисекунд даётся гостю на ответ.</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> SendColorDrawOfferAsync(string commitment, int timeoutMs, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitment);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);

        return SendAsync(new LanMessage
        {
            Type = LanMessageTypes.ColorDrawOffer,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            Commitment = commitment,
            TimeoutMs = timeoutMs
        }, cancellationToken);
    }

    /// <summary>Раскрыть горсть: число камней, соль и победитель.</summary>
    /// <param name="count">Число камней 1…9.</param>
    /// <param name="nonce">Соль раскрытия.</param>
    /// <param name="winnerIsGuest">Победил гость (угадал).</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> SendColorDrawRevealAsync(int count, string nonce, bool winnerIsGuest, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);

        return SendAsync(new LanMessage
        {
            Type = LanMessageTypes.ColorDrawReveal,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            Count = count,
            Nonce = nonce,
            WinnerIsGuest = winnerIsGuest
        }, cancellationToken);
    }

    /// <summary>Сообщить итоговые цвета: розыгрыш завершён.</summary>
    /// <param name="hostColor">Цвет хозяина.</param>
    /// <param name="guestColor">Цвет гостя.</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> SendColorDrawDoneAsync(StoneColor hostColor, StoneColor guestColor, CancellationToken cancellationToken) =>
        SendAsync(new LanMessage
        {
            Type = LanMessageTypes.ColorDrawDone,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            HostColor = hostColor.Name,
            GuestColor = guestColor.Name
        }, cancellationToken);

    /// <summary>Останавливает комнату, соединение и объявления.</summary>
    /// <returns>Задача остановки.</returns>
    public async Task StopAsync()
    {
        CancellationTokenSource? lifetime;
        TcpListener? listener;
        RoomAnnouncer? announcer;
        LanTlsConnection? connection;
        TaskCompletionSource<JoinDecision>? decision;
        Task? acceptLoop;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lifetime = _lifetime;
            listener = _listener;
            announcer = _announcer;
            connection = _connection;
            decision = _pendingDecision;
            acceptLoop = _acceptLoop;
            _lifetime = null;
            _listener = null;
            _announcer = null;
            _connection = null;
            _pendingDecision = null;
            _pendingJoin = null;
            _acceptLoop = null;
            _receiveLoop = null;
            _monitorLoop = null;
        }

        lifetime?.Cancel();
        listener?.Stop();
        decision?.TrySetCanceled();

        if (announcer is not null)
        {
            await announcer.DisposeAsync().ConfigureAwait(false);
        }

        if (connection is not null)
        {
            try
            {
                await connection.SendAsync(new LanMessage
                {
                    Type = LanMessageTypes.Bye,
                    ProtocolVersion = _options.ProtocolVersion,
                    GameId = _gameId
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or SocketException or LanConnectionClosedException or ObjectDisposedException)
            {
                // Peer уже мог закрыть соединение: остановка важнее прощального сообщения.
            }

            await connection.DisposeAsync().ConfigureAwait(false);
        }

        if (acceptLoop is not null)
        {
            try
            {
                await acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Остановка — ожидаемый исход.
            }
        }

        lifetime?.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private Result<TcpListener> StartListener()
    {
        try
        {
            var preferred = new TcpListener(IPAddress.Any, _options.PreferredTcpPort);
            preferred.Start();
            return Result<TcpListener>.Ok(preferred);
        }
        catch (SocketException)
        {
            // Порт занят: берём свободный, в интерфейсе показываем фактический.
        }

        try
        {
            var fallback = new TcpListener(IPAddress.Any, 0);
            fallback.Start();
            return Result<TcpListener>.Ok(fallback);
        }
        catch (SocketException exception)
        {
            return Result<TcpListener>.Fail($"Не удалось открыть порт для комнаты: {exception.Message}");
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                continue;
            }

            await HandleClientAsync(client, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        LanTlsConnection? connection = null;
        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshake.CancelAfter(_options.HandshakeTimeout);
        try
        {
            connection = await LanTlsConnection.AcceptAsync(client, _options.TimeProvider, handshake.Token)
                .ConfigureAwait(false);
            client = null!;

            var first = await connection.ReceiveAsync(handshake.Token).ConfigureAwait(false);
            var transferred = first.Type switch
            {
                LanMessageTypes.Join => await HandleJoinAsync(connection, first, cancellationToken).ConfigureAwait(false),
                LanMessageTypes.Resume => await HandleResumeAsync(connection, first, cancellationToken).ConfigureAwait(false),
                _ => false
            };

            if (!transferred)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                connection = null;
            }
            else
            {
                connection = null;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Хозяина остановили.
        }
        catch (OperationCanceledException)
        {
            // Истёк таймаут рукопожатия: комната продолжает ждать гостя.
            RaiseFailed(LanErrorCodes.ConnectionLost, "Гость не успел подключиться.");
        }
        catch (Exception exception) when (exception is IOException or SocketException or AuthenticationException or LanProtocolException or LanConnectionClosedException)
        {
            RaiseFailed(LanErrorCodes.ConnectionLost, $"Не удалось подключить гостя: {exception.Message}");
        }
        catch (Exception exception)
        {
            // Любая ошибка одного подключения не должна останавливать комнату.
            RaiseFailed(LanErrorCodes.ProtocolError, $"Ошибка подключения гостя: {exception.Message}");
        }
        finally
        {
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            client?.Dispose();
        }
    }

    private async Task<bool> HandleJoinAsync(LanTlsConnection connection, LanMessage message, CancellationToken cancellationToken)
    {
        if (message.ProtocolVersion != _options.ProtocolVersion)
        {
            await TrySendJoinResultAsync(connection, accepted: false, LanErrorCodes.ProtocolMismatch, "Версия игры не совпадает: обновите приложение на обоих устройствах.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (message.GameId != _gameId)
        {
            await TrySendJoinResultAsync(connection, accepted: false, LanErrorCodes.GameNotFound, "Комната не найдена или код не совпадает.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (message.AvatarId is null or < 0 or > 7)
        {
            await TrySendJoinResultAsync(connection, accepted: false, LanErrorCodes.ProtocolError, "Некорректный аватар гостя.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        lock (_sync)
        {
            // Заявка на рассмотрении — комната тоже занята: второй гость не должен
            // перезаписывать ожидающее решение хозяина (R-007).
            if (_inGame || _connection is not null || _pendingDecision is not null)
            {
                _ = TrySendJoinResultAsync(connection, accepted: false, LanErrorCodes.RoomBusy, "Комната уже занята.", cancellationToken);
                return false;
            }
        }

        var decision = new TaskCompletionSource<JoinDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync)
        {
            _pendingJoin = new PendingJoin(connection, message.AvatarId.Value);
            _pendingDecision = decision;
        }

        RaiseJoinRequested(message.AvatarId.Value, connection.RemoteAddress ?? string.Empty);

        // Хозяин не должен ждать вечно: истёк срок решения или гость закрыл соединение —
        // заявка снимается, лобби убирает карточку и ждёт следующего гостя (R-007).
        using var decisionLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        decisionLifetime.CancelAfter(_options.JoinDecisionTimeout);
        var watch = WatchPendingJoinAsync(connection, decisionLifetime.Token);

        JoinDecision answer;
        try
        {
            answer = await decision.Task.WaitAsync(decisionLifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            decisionLifetime.Cancel();
            await WaitQuietlyAsync(watch).ConfigureAwait(false);
            ClearPending();

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            RaiseJoinCancelled();
            await TrySendJoinResultAsync(connection, accepted: false, LanErrorCodes.JoinTimeout, "Хозяин не ответил вовремя.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        decisionLifetime.Cancel();
        await WaitQuietlyAsync(watch).ConfigureAwait(false);
        ClearPending();

        if (!answer.Accepted)
        {
            await TrySendJoinResultAsync(connection, accepted: false, LanErrorCodes.JoinRejected, answer.Reason ?? "Хозяин не пустил в комнату.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        // Сначала сообщаем гостю решение: пока JoinResult не ушёл, партия не началась.
        // Раньше _inGame ставился до отправки, и неудачная отправка оставляла комнату
        // «занятой» без гостя (R-007).
        var guestColor = answer.GuestColor;
        var komi = answer.Komi;
        try
        {
            await connection.SendAsync(new LanMessage
            {
                Type = LanMessageTypes.JoinResult,
                ProtocolVersion = _options.ProtocolVersion,
                GameId = _gameId,
                Accepted = true,
                BoardSize = _boardSize,
                Komi = komi,
                GuestColor = guestColor.Name,
                AvatarId = _hostAvatarId,
                Token = _sessionToken,
                RemainingMs = (int)_options.MoveTimeout.TotalMilliseconds
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or SocketException or AuthenticationException or LanProtocolException or LanConnectionClosedException or ObjectDisposedException)
        {
            RaiseFailed(LanErrorCodes.ConnectionLost, $"Гость не получил ответ: {exception.Message}");
            return false;
        }

        lock (_sync)
        {
            _connection = connection;
            _inGame = true;
            _guestColor = guestColor;
            _komi = komi;
            _lastReceivedAt = _options.TimeProvider.GetUtcNow();
        }

        RoomAnnouncer? announcer;
        lock (_sync)
        {
            announcer = _announcer;
            _announcer = null;
        }

        if (announcer is not null)
        {
            await announcer.DisposeAsync().ConfigureAwait(false);
        }

        StartConnectionLoops(connection);
        GuestConnected?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Следит за соединением, пока хозяин не решил по заявке.</summary>
    /// <param name="connection">Соединение с гостем.</param>
    /// <param name="cancellationToken">Отмена: решение принято, срок истёк или хозяин остановлен.</param>
    /// <remarks>
    /// До ответа гость по протоколу ничего не присылает, поэтому чтение здесь — только ожидание:
    /// закрытие соединения даёт исключение, и заявка снимается, не дожидаясь двух минут.
    /// </remarks>
    private async Task WatchPendingJoinAsync(LanTlsConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var message = await connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);

                if (message.Type != LanMessageTypes.Bye)
                {
                    // До ответа хозяина гость не должен присылать ничего, кроме «до свидания»:
                    // прочие сообщения игнорируем, заявка остаётся.
                    continue;
                }

                CancelPendingJoin("Гость вышел до ответа хозяина.");

                return;
            }
        }
        catch (OperationCanceledException)
        {
            // Срок истёк или хозяин уже решил: заявку снимет основной путь.
        }
        catch (Exception exception) when (exception is IOException or SocketException or AuthenticationException or LanProtocolException or LanConnectionClosedException or ObjectDisposedException)
        {
            CancelPendingJoin("Гость закрыл соединение.");
        }
    }

    /// <summary>Дожидается фонового наблюдателя, не превращая его отмену в ошибку.</summary>
    /// <param name="task">Задача наблюдателя.</param>
    private static async Task WaitQuietlyAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Отмена наблюдателя — ожидаемый исход.
        }
    }

    private async Task<bool> HandleResumeAsync(LanTlsConnection connection, LanMessage message, CancellationToken cancellationToken)
    {
        var valid = message.ProtocolVersion == _options.ProtocolVersion
                    && message.GameId == _gameId
                    && string.Equals(message.Token, _sessionToken, StringComparison.Ordinal);
        if (!valid)
        {
            await TrySendJoinResultAsync(connection, accepted: false, LanErrorCodes.GameNotFound, "Партия не найдена или ключ сессии не совпал.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        LanTlsConnection? previous;
        lock (_sync)
        {
            if (_finished)
            {
                // Оконченную партию не оживляем: иначе гость вернулся бы в завершённую игру (R-007).
                _ = TrySendJoinResultAsync(connection, accepted: false, LanErrorCodes.GameNotFound, "Партия уже окончена.", cancellationToken);
                return false;
            }

            if (!_inGame)
            {
                _ = TrySendJoinResultAsync(connection, accepted: false, LanErrorCodes.GameNotFound, "Партия ещё не началась.", cancellationToken);
                return false;
            }

            previous = _connection;
            _connection = connection;
            _lastReceivedAt = _options.TimeProvider.GetUtcNow();
        }

        if (previous is not null)
        {
            await previous.DisposeAsync().ConfigureAwait(false);
        }

        await connection.SendAsync(new LanMessage
        {
            Type = LanMessageTypes.JoinResult,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            Accepted = true,
            BoardSize = _boardSize,
            Komi = _komi,
            GuestColor = _guestColor.Name,
            AvatarId = _hostAvatarId,
            Token = _sessionToken
        }, cancellationToken).ConfigureAwait(false);

        StartConnectionLoops(connection);
        GuestReconnected?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void StartConnectionLoops(LanTlsConnection connection)
    {
        var lifetime = _lifetime;
        if (lifetime is null)
        {
            return;
        }

        var receive = Task.Run(() => ReceiveLoopAsync(connection, lifetime.Token), CancellationToken.None);
        var monitor = Task.Run(() => MonitorConnectionAsync(connection, lifetime.Token), CancellationToken.None);
        lock (_sync)
        {
            _receiveLoop = receive;
            _monitorLoop = monitor;
        }
    }

    private async Task ReceiveLoopAsync(LanTlsConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                lock (_sync)
                {
                    if (ReferenceEquals(_connection, connection))
                    {
                        _lastReceivedAt = _options.TimeProvider.GetUtcNow();
                    }
                }

                switch (message.Type)
                {
                    case LanMessageTypes.Ping:
                        await SendToAsync(connection, new LanMessage
                        {
                            Type = LanMessageTypes.Pong,
                            ProtocolVersion = _options.ProtocolVersion
                        }, cancellationToken).ConfigureAwait(false);
                        break;

                    case LanMessageTypes.MoveRequest:
                        HandleMoveRequest(connection, message);
                        break;

                    case LanMessageTypes.StateSyncRequest:
                        GuestStateSyncRequested?.Invoke(this, new GuestStateSyncRequestedEventArgs());
                        break;

                    case LanMessageTypes.ColorDrawGuess:
                        // Хозяин — арбитр: принимаем только «чёт»/«нечет» (F2).
                        if (message.Parity is not (ColorDraw.GuessOdd or ColorDraw.GuessEven))
                        {
                            RaiseFailed(LanErrorCodes.ProtocolError, "Хозяин получил некорректный ответ нигири.");
                            break;
                        }

                        ColorDrawGuessReceived?.Invoke(this, new ColorDrawGuessEventArgs(message.Parity));
                        break;

                    case LanMessageTypes.ColorDrawChoice:
                        // Только чёрные или белые: Empty/мусор не должны попасть в настройки партии (F2).
                        var choice = Enumeration.TryFromName<StoneColor>(message.Color);
                        if (choice is null || (choice != StoneColor.Black && choice != StoneColor.White))
                        {
                            RaiseFailed(LanErrorCodes.ProtocolError, "Хозяин получил некорректный выбор цвета нигири.");
                            break;
                        }

                        ColorDrawChoiceReceived?.Invoke(this, new ColorDrawChoiceEventArgs(choice));
                        break;

                    case LanMessageTypes.Bye:
                        GuestLeft?.Invoke(this, EventArgs.Empty);
                        OnConnectionLost(connection);
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Остановка хозяина.
        }
        catch (Exception exception) when (exception is IOException or SocketException or LanProtocolException or LanConnectionClosedException or ObjectDisposedException or AuthenticationException)
        {
            OnConnectionLost(connection);
        }
    }

    private void HandleMoveRequest(LanTlsConnection connection, LanMessage message)
    {
        var dto = new LanMoveDto(
            message.MoveNumber ?? 0,
            message.MoveType ?? string.Empty,
            message.Color ?? string.Empty,
            message.X,
            message.Y,
            message.AutoPass ?? false);
        if (!MoveWire.TryToMove(dto, out var move, out var error))
        {
            _ = SendToAsync(connection, new LanMessage
            {
                Type = LanMessageTypes.MoveRejected,
                ProtocolVersion = _options.ProtocolVersion,
                GameId = _gameId,
                Code = LanErrorCodes.ProtocolError,
                Message = error
            }, CancellationToken.None);
            return;
        }

        GuestMoveRequested?.Invoke(this, new GuestMoveRequestEventArgs(dto.MoveNumber, move));
    }

    private async Task MonitorConnectionAsync(LanTlsConnection connection, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _options.TimeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                bool stale;
                lock (_sync)
                {
                    if (!ReferenceEquals(_connection, connection))
                    {
                        return;
                    }

                    stale = _options.TimeProvider.GetUtcNow() - _lastReceivedAt > _options.HeartbeatTimeout;
                }

                if (stale)
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Остановка хозяина.
        }
    }

    private void OnConnectionLost(LanTlsConnection connection)
    {
        var lost = false;
        lock (_sync)
        {
            if (ReferenceEquals(_connection, connection))
            {
                _connection = null;
                lost = true;
            }
        }

        if (lost)
        {
            GuestDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<Result> SendAsync(LanMessage message, CancellationToken cancellationToken)
    {
        LanTlsConnection? connection;
        lock (_sync)
        {
            connection = _connection;
        }

        if (connection is null)
        {
            return Result.Fail("Гость не подключён.");
        }

        try
        {
            await connection.SendAsync(message, cancellationToken).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (Exception exception) when (exception is IOException or SocketException or LanConnectionClosedException or ObjectDisposedException)
        {
            OnConnectionLost(connection);
            return Result.Fail($"Не удалось отправить сообщение: {exception.Message}");
        }
    }

    private async Task TrySendJoinResultAsync(
        LanTlsConnection connection,
        bool accepted,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(new LanMessage
            {
                Type = LanMessageTypes.JoinResult,
                ProtocolVersion = _options.ProtocolVersion,
                GameId = _gameId,
                Accepted = accepted,
                Code = code,
                Message = message
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or SocketException or LanConnectionClosedException or ObjectDisposedException)
        {
            // Отказ уже принят: peer мог закрыть соединение раньше.
        }
    }

    private async Task SendToAsync(LanTlsConnection connection, LanMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or SocketException or LanConnectionClosedException or ObjectDisposedException)
        {
            OnConnectionLost(connection);
        }
    }

    private void ClearPending()
    {
        lock (_sync)
        {
            _pendingJoin = null;
            _pendingDecision = null;
        }
    }

    private void RaiseJoinRequested(int avatarId, string address) =>
        GuestJoinRequested?.Invoke(this, new GuestJoinRequestEventArgs(avatarId, address));

    private void RaiseJoinCancelled() => GuestJoinCancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>Снимает ожидающую заявку гостя с причиной и сообщает лобби.</summary>
    /// <param name="reason">Почему заявка снята; уходит гостю в отказе.</param>
    private void CancelPendingJoin(string reason)
    {
        TaskCompletionSource<JoinDecision>? decision;
        lock (_sync)
        {
            decision = _pendingDecision;
        }

        if (decision is not null
            && decision.TrySetResult(new JoinDecision(false, StoneColor.White, 0, reason)))
        {
            RaiseJoinCancelled();
        }
    }

    private void RaiseFailed(string code, string message) =>
        Failed?.Invoke(this, new LanErrorEventArgs(code, message));

    private sealed record PendingJoin(LanTlsConnection Connection, int AvatarId);

    private readonly record struct JoinDecision(bool Accepted, StoneColor GuestColor, double Komi, string? Reason);
}
