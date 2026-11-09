using System.Net.Sockets;
using GoEngine.Core;

namespace GoEngine.Multiplayer;

/// <summary>Гость комнаты: подключается к хозяину, присылает просьбы о ходе и принимает ходы.</summary>
/// <remarks>
/// Гость не применяет свои ходы локально: он отправляет просьбу, а хозяин проверяет очередь,
/// правила и дедлайн, после чего присылает подтверждённый ход. Это даёт единый источник истины
/// и исключает двойной авто-пас. При обрыве гость сам пытается вернуться в ту же партию,
/// пока не истечёт <see cref="MultiplayerOptions.ReconnectTimeout"/>.
/// </remarks>
public sealed class LanGameClient : INetworkGameClient
{
    private readonly MultiplayerOptions _options;
    private readonly object _sync = new();
    private LanTlsConnection? _connection;
    private CancellationTokenSource? _lifetime;
    private Task? _receiveLoop;
    private Task? _heartbeatLoop;
    private Task? _reconnectLoop;
    private RoomInfo? _room;
    private int _avatarId;
    private string? _gameId;
    private string? _token;
    private int _boardSize;
    private double _komi;
    private StoneColor _guestColor = StoneColor.White;
    private int _hostAvatarId;
    private bool _started;
    private bool _disposed;
    private bool _reconnecting;
    private DateTimeOffset _lastServerActivity;
    private int _remainingMs;

    /// <summary>Создаёт гостевой клиент.</summary>
    /// <param name="options">Настройки сети; <c>null</c> — значения по умолчанию.</param>
    public LanGameClient(MultiplayerOptions? options = null)
    {
        _options = options ?? new MultiplayerOptions();
    }

    /// <summary>Хозяин прислал применённый ход.</summary>
    public event EventHandler<MoveAppliedEventArgs>? MoveApplied;

    /// <summary>Хозяин сообщил состояние таймера.</summary>
    public event EventHandler<TurnStateEventArgs>? TurnStateReceived;

    /// <summary>Хозяин прислал полное состояние партии после переподключения.</summary>
    public event EventHandler<StateSyncEventArgs>? StateSyncReceived;

    /// <summary>Связь потеряна; клиент пытается переподключиться.</summary>
    public event EventHandler? Disconnected;

    /// <summary>Удалось вернуться в ту же партию.</summary>
    public event EventHandler? Reconnected;

    /// <summary>Ошибка, после которой партия не может продолжаться.</summary>
    public event EventHandler<LanErrorEventArgs>? Failed;

    /// <summary>Есть активное соединение с хозяином.</summary>
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

    /// <summary>Подключается к комнате и ждёт, пока хозяин пустит.</summary>
    /// <param name="room">Найденная комната.</param>
    /// <param name="avatarId">Номер зверя-аватара гостя, 0..7.</param>
    /// <param name="cancellationToken">Отмена подключения.</param>
    /// <returns>Данные начавшейся партии или типизированная ошибка.</returns>
    public async Task<Result<GuestSessionStarted>> JoinAsync(
        RoomInfo room,
        int avatarId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (avatarId is < 0 or > 7)
        {
            return Result<GuestSessionStarted>.Fail("Номер аватара гостя должен быть от 0 до 7.");
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return Result<GuestSessionStarted>.Fail("Клиент уже остановлен.");
            }

            if (_started)
            {
                return Result<GuestSessionStarted>.Fail("Подключение уже выполнено.");
            }
        }

        var lifetime = new CancellationTokenSource();
        LanTlsConnection? connection = null;
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            handshake.CancelAfter(_options.HandshakeTimeout);
            connection = await LanTlsConnection.ConnectAsync(
                room.HostAddress,
                room.TcpPort,
                _options.TimeProvider,
                handshake.Token).ConfigureAwait(false);

            await connection.SendAsync(new LanMessage
            {
                Type = LanMessageTypes.Join,
                ProtocolVersion = _options.ProtocolVersion,
                GameId = room.GameId,
                AvatarId = avatarId
            }, handshake.Token).ConfigureAwait(false);

            // Соединение и отправка join уложились в HandshakeTimeout. Дальше ждём решения
            // человека: у хозяина есть JoinDecisionTimeout, а не 15 секунд сетевого таймаута.
            using var decision = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            decision.CancelAfter(_options.JoinDecisionTimeout);
            var response = await connection.ReceiveAsync(decision.Token).ConfigureAwait(false);
            if (response.Type != LanMessageTypes.JoinResult || response.Accepted != true)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                return Result<GuestSessionStarted>.Fail(response.Message ?? "Хозяин не пустил в комнату.");
            }

            var guestColor = Enumeration.TryFromName<StoneColor>(response.GuestColor);
            if (guestColor is null
                || guestColor == StoneColor.Empty
                || response.BoardSize is not (9 or 13 or 19)
                || string.IsNullOrWhiteSpace(response.Token)
                || string.IsNullOrWhiteSpace(response.GameId)
                || response.AvatarId is null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                return Result<GuestSessionStarted>.Fail("Хозяин прислал некорректные данные партии.");
            }

            var started = new GuestSessionStarted(
                response.GameId,
                response.BoardSize.Value,
                response.Komi ?? 0,
                guestColor,
                response.AvatarId.Value,
                response.RemainingMs is > 0 ? (int)response.RemainingMs.Value : (int)_options.MoveTimeout.TotalMilliseconds);

            lock (_sync)
            {
                _room = room;
                _avatarId = avatarId;
                _gameId = response.GameId;
                _token = response.Token;
                _boardSize = response.BoardSize.Value;
                _komi = response.Komi ?? 0;
                _guestColor = guestColor;
                _hostAvatarId = response.AvatarId.Value;
                _remainingMs = started.RemainingMs;
                _connection = connection;
                _lifetime = lifetime;
                _lastServerActivity = _options.TimeProvider.GetUtcNow();
                _started = true;

                // Лямбды захватывают значения локальных переменных: ниже connection и lifetime
                // обнуляются, чтобы finally не закрыл переданное модели соединение.
                var receiveConnection = connection;
                var receiveLifetime = lifetime;
                _receiveLoop = Task.Run(() => ReceiveLoopAsync(receiveConnection, receiveLifetime.Token), CancellationToken.None);
                _heartbeatLoop = Task.Run(() => HeartbeatLoopAsync(receiveConnection, receiveLifetime.Token), CancellationToken.None);
            }

            connection = null;
            lifetime = null;
            return Result<GuestSessionStarted>.Ok(started);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<GuestSessionStarted>.Fail("Хозяин не ответил вовремя.");
        }
        catch (Exception exception) when (exception is IOException or SocketException or System.Security.Authentication.AuthenticationException or LanProtocolException or LanConnectionClosedException)
        {
            return Result<GuestSessionStarted>.Fail($"Не удалось подключиться к комнате: {exception.Message}");
        }
        finally
        {
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            lifetime?.Dispose();
        }
    }

    /// <summary>Попросить хозяина применить ход.</summary>
    /// <param name="move">Предложенный ход.</param>
    /// <param name="moveNumber">Ожидаемый номер хода.</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> RequestMoveAsync(Move move, int moveNumber, CancellationToken cancellationToken)
    {
        if (move.IsNone || moveNumber <= 0)
        {
            return Task.FromResult(Result.Fail("Некорректный ход или номер хода."));
        }

        var point = move.Type == MoveType.Play ? move.Point : default;
        return SendAsync(new LanMessage
        {
            Type = LanMessageTypes.MoveRequest,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId,
            MoveNumber = moveNumber,
            MoveType = move.Type.Name,
            Color = move.Color.Name,
            X = move.Type == MoveType.Play ? point.X : null,
            Y = move.Type == MoveType.Play ? point.Y : null
        }, cancellationToken);
    }

    /// <summary>Попросить полное состояние партии после переподключения.</summary>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    public Task<Result> RequestStateSyncAsync(CancellationToken cancellationToken) =>
        SendAsync(new LanMessage
        {
            Type = LanMessageTypes.StateSyncRequest,
            ProtocolVersion = _options.ProtocolVersion,
            GameId = _gameId
        }, cancellationToken);

    /// <summary>Корректно выйти из партии и остановить переподключение.</summary>
    /// <returns>Задача остановки.</returns>
    public async Task LeaveAsync()
    {
        CancellationTokenSource? lifetime;
        LanTlsConnection? connection;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lifetime = _lifetime;
            connection = _connection;
            _lifetime = null;
            _connection = null;
        }

        lifetime?.Cancel();
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
                // Peer мог закрыть соединение раньше.
            }

            await connection.DisposeAsync().ConfigureAwait(false);
        }

        lifetime?.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await LeaveAsync().ConfigureAwait(false);

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
                        _lastServerActivity = _options.TimeProvider.GetUtcNow();
                    }
                }

                switch (message.Type)
                {
                    case LanMessageTypes.MoveApplied:
                        HandleMoveApplied(message);
                        break;

                    case LanMessageTypes.TurnState:
                        if (message.RemainingMs is >= 0)
                        {
                            lock (_sync)
                            {
                                _remainingMs = (int)message.RemainingMs.Value;
                            }

                            TurnStateReceived?.Invoke(this, new TurnStateEventArgs((int)message.RemainingMs.Value, message.Paused ?? false));
                        }

                        break;

                    case LanMessageTypes.StateSync:
                        HandleStateSync(message);
                        break;

                    case LanMessageTypes.Ping:
                        await SendToAsync(connection, new LanMessage
                        {
                            Type = LanMessageTypes.Pong,
                            ProtocolVersion = _options.ProtocolVersion
                        }, cancellationToken).ConfigureAwait(false);
                        break;

                    case LanMessageTypes.MoveRejected:
                        RaiseFailed(message.Code ?? LanErrorCodes.MoveRejected, message.Message ?? "Хозяин отклонил ход.");
                        break;

                    case LanMessageTypes.Error:
                        RaiseFailed(message.Code ?? LanErrorCodes.ProtocolError, message.Message ?? "Сетевая ошибка.");
                        OnConnectionLost(connection);
                        return;

                    case LanMessageTypes.Bye:
                        RaiseFailed(LanErrorCodes.HostLeft, "Хозяин вышел из игры.");
                        OnConnectionLost(connection, allowReconnect: false);
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Остановка клиента.
        }
        catch (Exception exception) when (exception is IOException or SocketException or LanProtocolException or LanConnectionClosedException or ObjectDisposedException)
        {
            OnConnectionLost(connection);
        }
    }

    private void HandleMoveApplied(LanMessage message)
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
            RaiseFailed(LanErrorCodes.ProtocolError, error ?? "Не удалось разобрать ход хозяина.");
            return;
        }

        var remaining = message.RemainingMs is >= 0 ? (int)message.RemainingMs.Value : _remainingMs;
        MoveApplied?.Invoke(this, new MoveAppliedEventArgs(dto.MoveNumber, move, dto.AutoPass, remaining));
    }

    private void HandleStateSync(LanMessage message)
    {
        var moves = new List<AppliedMove>();
        if (message.Moves is not null)
        {
            foreach (var dto in message.Moves)
            {
                if (!MoveWire.TryToMove(dto, out var move, out var error))
                {
                    RaiseFailed(LanErrorCodes.ProtocolError, error ?? "В состоянии партии некорректный ход.");
                    return;
                }

                moves.Add(new AppliedMove(dto.MoveNumber, move, dto.AutoPass));
            }
        }

        StateSyncReceived?.Invoke(this, new StateSyncEventArgs(
            moves.AsReadOnly(),
            message.RemainingMs is >= 0 ? (int)message.RemainingMs.Value : 0,
            message.Paused ?? false));
    }

    private async Task HeartbeatLoopAsync(LanTlsConnection connection, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.HeartbeatInterval, _options.TimeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                bool current;
                lock (_sync)
                {
                    current = ReferenceEquals(_connection, connection);
                }

                if (!current)
                {
                    return;
                }

                try
                {
                    await connection.SendAsync(new LanMessage
                    {
                        Type = LanMessageTypes.Ping,
                        ProtocolVersion = _options.ProtocolVersion
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or SocketException or LanConnectionClosedException or ObjectDisposedException)
                {
                    OnConnectionLost(connection);
                    return;
                }

                bool stale;
                lock (_sync)
                {
                    stale = _options.TimeProvider.GetUtcNow() - _lastServerActivity > _options.HeartbeatTimeout;
                }

                if (stale)
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                    OnConnectionLost(connection);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Остановка клиента.
        }
    }

    private void OnConnectionLost(LanTlsConnection connection, bool allowReconnect = true)
    {
        bool lost;
        lock (_sync)
        {
            lost = ReferenceEquals(_connection, connection);
            if (lost)
            {
                _connection = null;
            }
        }

        if (!lost)
        {
            return;
        }

        Disconnected?.Invoke(this, EventArgs.Empty);
        if (allowReconnect)
        {
            StartReconnect();
        }
    }

    private void StartReconnect()
    {
        lock (_sync)
        {
            if (_disposed || _reconnecting || _lifetime is null)
            {
                return;
            }

            _reconnecting = true;
            var lifetime = _lifetime;
            _reconnectLoop = Task.Run(() => ReconnectLoopAsync(lifetime.Token), CancellationToken.None);
        }
    }

    private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deadline = _options.TimeProvider.GetUtcNow() + _options.ReconnectTimeout;
            while (!cancellationToken.IsCancellationRequested
                   && _options.TimeProvider.GetUtcNow() < deadline)
            {
                await Task.Delay(_options.ReconnectRetryInterval, _options.TimeProvider, cancellationToken).ConfigureAwait(false);
                try
                {
                    using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    handshake.CancelAfter(_options.HandshakeTimeout);
                    var room = _room;
                    if (room is null)
                    {
                        break;
                    }

                    var connection = await LanTlsConnection.ConnectAsync(
                        room.HostAddress,
                        room.TcpPort,
                        _options.TimeProvider,
                        handshake.Token).ConfigureAwait(false);
                    try
                    {
                        await connection.SendAsync(new LanMessage
                        {
                            Type = LanMessageTypes.Resume,
                            ProtocolVersion = _options.ProtocolVersion,
                            GameId = _gameId,
                            Token = _token,
                            AvatarId = _avatarId
                        }, handshake.Token).ConfigureAwait(false);

                        var response = await connection.ReceiveAsync(handshake.Token).ConfigureAwait(false);
                        if (response.Type == LanMessageTypes.JoinResult && response.Accepted == true)
                        {
                            lock (_sync)
                            {
                                _connection = connection;
                                _lastServerActivity = _options.TimeProvider.GetUtcNow();
                                _receiveLoop = Task.Run(() => ReceiveLoopAsync(connection, cancellationToken), CancellationToken.None);
                                _heartbeatLoop = Task.Run(() => HeartbeatLoopAsync(connection, cancellationToken), CancellationToken.None);
                            }

                            await connection.SendAsync(new LanMessage
                            {
                                Type = LanMessageTypes.StateSyncRequest,
                                ProtocolVersion = _options.ProtocolVersion,
                                GameId = _gameId
                            }, cancellationToken).ConfigureAwait(false);
                            Reconnected?.Invoke(this, EventArgs.Empty);
                            return;
                        }

                        await connection.DisposeAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        await connection.DisposeAsync().ConfigureAwait(false);
                        throw;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception) when (exception is IOException or SocketException or System.Security.Authentication.AuthenticationException or LanProtocolException or LanConnectionClosedException or ObjectDisposedException)
                {
                    // Следующая попытка через интервал.
                }
            }

            RaiseFailed(LanErrorCodes.ReconnectTimeout, "Не удалось вернуться в партию за отведённое время.");
        }
        finally
        {
            lock (_sync)
            {
                _reconnecting = false;
            }
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
            return Result.Fail("Нет соединения с хозяином.");
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

    private void RaiseFailed(string code, string message) =>
        Failed?.Invoke(this, new LanErrorEventArgs(code, message));
}
