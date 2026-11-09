using System.Net;
using System.Net.Sockets;
using GoEngine.Core;

namespace GoEngine.Multiplayer;

/// <summary>Слушает объявления комнат и поддерживает список доступных партий.</summary>
/// <remarks>
/// Гость занимает один фиксированный UDP-порт; хозяева шлют объявления с эфемерных портов,
/// поэтому несколько комнат в одной сети не конфликтуют. Записи без нового объявления
/// исчезают через <see cref="MultiplayerOptions.RoomLifetime"/>.
/// </remarks>
public sealed class RoomWatcher : IAsyncDisposable
{
    private readonly MultiplayerOptions _options;
    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<string, RoomInfo> _rooms = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private Task? _receiveLoop;
    private Task? _expiryLoop;
    private bool _disposed;

    private RoomWatcher(MultiplayerOptions options, UdpClient udp)
    {
        _options = options;
        _udp = udp;
    }

    /// <summary>Комнаты изменились: список можно перечитать и обновить экран.</summary>
    public event EventHandler? RoomsChanged;

    /// <summary>Фактический UDP-порт прослушивания (полезен, если запрошен порт 0).</summary>
    public int BoundPort => ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;

    /// <summary>Текущий список комнат, упорядоченный по номеру.</summary>
    /// <returns>Снимок списка; вызывающий не может изменить внутреннее состояние.</returns>
    public IReadOnlyList<RoomInfo> Rooms
    {
        get
        {
            lock (_sync)
            {
                return _rooms.Values
                    .OrderBy(static room => room.RoomNumber)
                    .ThenBy(static room => room.HostAvatarId)
                    .ToArray();
            }
        }
    }

    /// <summary>Запускает прослушивание объявлений комнат.</summary>
    /// <param name="options">Настройки сети; порт 0 означает выбрать свободный порт.</param>
    /// <returns>Активный наблюдатель или типизированная ошибка.</returns>
    public static Result<RoomWatcher> Start(MultiplayerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            // ReuseAddress позволяет двум экземплярам приложения на одной машине (и двум
            // гостевым окнам) слушать один порт объявлений; на разных устройствах конфликта нет.
            var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, options.DiscoveryPort));
            var watcher = new RoomWatcher(options, udp);
            watcher.StartLoops();
            return Result<RoomWatcher>.Ok(watcher);
        }
        catch (SocketException exception)
        {
            return Result<RoomWatcher>.Fail($"Не удалось слушать объявления комнат: {exception.Message}");
        }
    }

    private void StartLoops()
    {
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_lifetime.Token));
        _expiryLoop = Task.Run(() => ExpiryLoopAsync(_lifetime.Token));
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var received = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (!RoomDiscoveryProtocol.TryDecode(received.Buffer, out var announcement) || announcement is null)
                {
                    continue;
                }

                var room = new RoomInfo(
                    announcement.GameId,
                    announcement.HostAvatarId,
                    announcement.RoomNumber,
                    announcement.BoardSize,
                    announcement.TcpPort,
                    received.RemoteEndPoint.Address.ToString(),
                    announcement.ProtocolVersion,
                    _options.TimeProvider.GetUtcNow());
                var changed = false;
                lock (_sync)
                {
                    if (!_rooms.TryGetValue(room.GameId, out var previous)
                        || previous.RoomNumber != room.RoomNumber
                        || previous.TcpPort != room.TcpPort
                        || previous.HostAddress != room.HostAddress)
                    {
                        changed = true;
                    }

                    _rooms[room.GameId] = room;
                }

                if (changed)
                {
                    RaiseRoomsChanged();
                }
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
                // Сеть могла исчезнуть: продолжаем слушать, пока наблюдатель не остановят.
            }
        }
    }

    private async Task ExpiryLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _options.TimeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var now = _options.TimeProvider.GetUtcNow();
                var changed = false;
                lock (_sync)
                {
                    foreach (var gameId in _rooms
                                 .Where(pair => now - pair.Value.LastSeen > _options.RoomLifetime)
                                 .Select(static pair => pair.Key)
                                 .ToArray())
                    {
                        _ = _rooms.Remove(gameId);
                        changed = true;
                    }
                }

                if (changed)
                {
                    RaiseRoomsChanged();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Остановка наблюдателя.
        }
    }

    private void RaiseRoomsChanged() => RoomsChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _lifetime.Cancel();
        _udp.Dispose();
        _lifetime.Dispose();
        return ValueTask.CompletedTask;
    }
}
