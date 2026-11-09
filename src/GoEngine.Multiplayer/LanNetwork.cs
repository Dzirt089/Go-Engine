using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GoEngine.Multiplayer;

/// <summary>Локальные IPv4-адреса и адреса для широковещательной рассылки.</summary>
internal static class LanNetwork
{
    /// <summary>Возвращает IPv4-адрес, который показываем как адрес хозяина.</summary>
    /// <remarks>
    /// У телефона и ПК бывает несколько сетей: Wi-Fi, виртуальные адаптеры и VPN. Первый
    /// попавшийся адрес мог оказаться туннелем 26.26.26.1 или host-only сетью 192.168.56.1,
    /// и гость видел комнату, но не мог подключиться. Поэтому сначала берём интерфейс
    /// с шлюзом по умолчанию и частным адресом, затем Wi-Fi, затем Ethernet.
    /// </remarks>
    internal static string? GetLocalIPv4Address()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(static network => network.OperationalStatus == OperationalStatus.Up)
            .Where(static network => network.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .SelectMany(static network =>
            {
                var hasGateway = network.GetIPProperties().GatewayAddresses
                    .Any(static gateway => !IPAddress.Any.Equals(gateway.Address));
                return network.GetIPProperties().UnicastAddresses
                    .Where(static address => address.Address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(address.Address))
                    .Select(address => new
                    {
                        Address = address.Address,
                        HasGateway = hasGateway,
                        IsPrivate = IsPrivate(address.Address),
                        TypeRank = network.NetworkInterfaceType switch
                        {
                            NetworkInterfaceType.Wireless80211 => 0,
                            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet => 1,
                            _ => 2
                        }
                    });
            })
            .OrderByDescending(static candidate => candidate.HasGateway)
            .ThenByDescending(static candidate => candidate.IsPrivate)
            .ThenBy(static candidate => candidate.TypeRank)
            .Select(static candidate => candidate.Address.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return candidates.FirstOrDefault();
    }

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    /// <summary>Возвращает адреса для широковещательной рассылки объявлений комнаты.</summary>
    internal static IReadOnlyList<IPEndPoint> GetBroadcastEndpoints()
    {
        var endpoints = new List<IPEndPoint>();
        Add(endpoints, new IPEndPoint(IPAddress.Broadcast, 0));

        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up
                || network.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            foreach (var unicast in network.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork
                    || unicast.IPv4Mask is null
                    || IPAddress.IsLoopback(unicast.Address))
                {
                    continue;
                }

                var address = unicast.Address.GetAddressBytes();
                var mask = unicast.IPv4Mask.GetAddressBytes();
                var broadcast = new byte[4];
                for (var index = 0; index < 4; index++)
                {
                    broadcast[index] = (byte)(address[index] | (byte)~mask[index]);
                }

                Add(endpoints, new IPEndPoint(new IPAddress(broadcast), 0));
            }
        }

        return endpoints;
    }

    private static void Add(List<IPEndPoint> endpoints, IPEndPoint endpoint)
    {
        if (!endpoints.Any(existing => existing.Address.Equals(endpoint.Address)))
        {
            endpoints.Add(endpoint);
        }
    }
}