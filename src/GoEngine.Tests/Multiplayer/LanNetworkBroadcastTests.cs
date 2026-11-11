using System.Net;
using GoEngine.Multiplayer;

namespace GoEngine.Tests;

/// <summary>Направленные broadcast-адреса для объявлений комнат (Android-регресс «комнаты не видны»).</summary>
public sealed class LanNetworkBroadcastTests
{
    [Fact]
    public void Направленный_Broadcast_Строится_Для_24_И_16()
    {
        var broadcasts = LanNetwork.DirectedBroadcasts(IPAddress.Parse("192.168.3.64"));

        Assert.Equal(2, broadcasts.Count);
        Assert.Contains(broadcasts, address => address.Equals(IPAddress.Parse("192.168.3.255")));
        Assert.Contains(broadcasts, address => address.Equals(IPAddress.Parse("192.168.255.255")));
    }

    [Fact]
    public void Совпадающие_Broadcast_Не_Дублируются()
    {
        // 192.168.255.x: /24 и /16 broadcast совпадают — объявление не должно уходить дважды.
        var broadcasts = LanNetwork.DirectedBroadcasts(IPAddress.Parse("192.168.255.10"));

        Assert.Single(broadcasts);
        Assert.Equal(IPAddress.Parse("192.168.255.255"), broadcasts[0]);
    }

    [Fact]
    public void Broadcast_Для_10_Сети_Строится()
    {
        var broadcasts = LanNetwork.DirectedBroadcasts(IPAddress.Parse("10.20.30.40"));

        Assert.Contains(broadcasts, address => address.Equals(IPAddress.Parse("10.20.30.255")));
        Assert.Contains(broadcasts, address => address.Equals(IPAddress.Parse("10.20.255.255")));
    }
}
