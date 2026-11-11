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
}
