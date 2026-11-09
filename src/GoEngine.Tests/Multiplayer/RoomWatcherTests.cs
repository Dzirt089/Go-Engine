using System.Net;
using System.Net.Sockets;
using GoEngine.Multiplayer;

namespace GoEngine.Tests;

/// <summary>Тесты UDP-обнаружения комнат.</summary>
public sealed class RoomWatcherTests
{
    [Fact]
    public async Task Наблюдатель_Показывает_Объявленную_Комнату()
    {
        var options = new MultiplayerOptions { DiscoveryPort = 0 };
        var started = RoomWatcher.Start(options);
        Assert.True(started.IsSuccess, started.Error);
        await using var watcher = started.Value!;
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.RoomsChanged += (_, _) => changed.TrySetResult();

        var payload = RoomDiscoveryProtocol.Encode(new RoomAnnouncement(
            "0123456789abcdef0123456789abcdef", 3, 42, 9, 47810, 1));
        using var sender = new UdpClient(AddressFamily.InterNetwork);
        _ = await sender.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, watcher.BoundPort));

        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var room = Assert.Single(watcher.Rooms);
        Assert.Equal(42, room.RoomNumber);
        Assert.Equal("0123456789abcdef0123456789abcdef", room.GameId);
    }
}
