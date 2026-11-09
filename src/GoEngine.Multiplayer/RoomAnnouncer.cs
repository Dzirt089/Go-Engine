using System.Net;
using System.Net.Sockets;
using GoEngine.Core;

namespace GoEngine.Multiplayer;

/// <summary>Периодически объявляет комнату в локальной сети.</summary>
/// <remarks>
/// Хозяин шлёт UDP-датаграммы с эфемерного порта, поэтому входящий UDP ему не нужен:
/// меньше конфликтов портов и меньше запросов firewall. Объявление содержит только игровые
/// данные комнаты и никогда — имена, ключи или состояние партии.
/// </remarks>
internal sealed class RoomAnnouncer : IAsyncDisposable
{
    private readonly RoomAnnouncement _room;
    private readonly MultiplayerOptions _options;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly byte[] _payload;
    private UdpClient? _udp;
    private Task? _loop;

    internal RoomAnnouncer(RoomAnnouncement room, MultiplayerOptions options)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(options);

        _room = room;
        _options = options;
        _payload = RoomDiscoveryProtocol.Encode(room);
    }

    internal RoomAnnouncement Room => _room;

    internal Task<Result> StartAsync()
    {
        if (_udp is not null)
        {
            return Task.FromResult(Result.Fail("Объявление комнаты уже запущено."));
        }

        try
        {
            var udp = new UdpClient(AddressFamily.InterNetwork)
            {
                EnableBroadcast = true
            };
            _udp = udp;

            var loop = Task.Run(() => AnnounceLoopAsync(_lifetime.Token));
            _loop = loop;
            return Task.FromResult(Result.Ok());
        }
        catch (SocketException exception)
        {
            return Task.FromResult(Result.Fail($"Не удалось открыть UDP для объявления комнаты: {exception.Message}"));
        }
    }

    private async Task AnnounceLoopAsync(CancellationToken cancellationToken)
    {
        var endpoints = LanNetwork.GetBroadcastEndpoints()
            .Select(static endpoint => endpoint)
            .ToArray();
        if (endpoints.Length == 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.DiscoveryInterval, _options.TimeProvider);
        try
        {
            Send(endpoints);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Send(endpoints);
            }
        }
        catch (OperationCanceledException)
        {
            // Остановка комнаты — ожидаемый исход.
        }
        catch (ObjectDisposedException)
        {
            // Сокет закрыт вместе с остановкой.
        }
    }

    private void Send(IReadOnlyList<IPEndPoint> endpoints)
    {
        var udp = _udp;
        if (udp is null)
        {
            return;
        }

        foreach (var endpoint in endpoints)
        {
            try
            {
                var target = new IPEndPoint(endpoint.Address, _options.DiscoveryPort);
                _ = udp.Send(_payload, _payload.Length, target);
            }
            catch (SocketException)
            {
                // Сеть могла пропасть или broadcast запрещён: следующая попытка через интервал.
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        var udp = _udp;
        _udp = null;
        udp?.Dispose();

        var loop = _loop;
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Остановка — нормальный путь.
            }
        }

        _lifetime.Dispose();
    }
}
