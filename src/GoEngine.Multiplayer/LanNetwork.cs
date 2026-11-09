using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GoEngine.Multiplayer;

/// <summary>Локальные IPv4-адреса и адреса для широковещательной рассылки.</summary>
internal static class LanNetwork
{
    /// <summary>Возвращает IPv4-адрес, который показываем как адрес хозяина.</summary>
    internal static string? GetLocalIPv4Address()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(static network => network.OperationalStatus == OperationalStatus.Up)
            .Where(static network => network.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .SelectMany(static network => network.GetIPProperties().UnicastAddresses)
            .Where(static address => address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
            .Select(static address => address.Address.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return candidates.FirstOrDefault();
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
