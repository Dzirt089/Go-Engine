using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GoEngine.Multiplayer;

/// <summary>Локальные IPv4-адреса и адреса для широковещательной рассылки.</summary>
/// <remarks>
/// На Android .NET 8 не поддерживает <see cref="NetworkInterface.GetAllNetworkInterfaces"/>
/// (PlatformNotSupportedException): официальный APK не мог даже создать комнату. Поэтому на
/// Android адрес определяется UDP-маршрутом, а broadcast берётся глобальный; на настольных
/// системах остаётся прежний выбор по шлюзу/частной сети.
/// </remarks>
internal static class LanNetwork
{
    /// <summary>Возвращает IPv4-адрес, который показываем как адрес хозяина.</summary>
    internal static string? GetLocalIPv4Address()
    {
        if (OperatingSystem.IsAndroid())
        {
            return GetRouteLocalAddress();
        }

        try
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

            return candidates.FirstOrDefault() ?? GetRouteLocalAddress();
        }
        catch (PlatformNotSupportedException)
        {
            return GetRouteLocalAddress();
        }
        catch (NetworkInformationException)
        {
            return GetRouteLocalAddress();
        }
    }

    /// <summary>Возвращает адреса для широковещательной рассылки объявлений комнаты.</summary>
    internal static IReadOnlyList<IPEndPoint> GetBroadcastEndpoints()
    {
        var endpoints = new List<IPEndPoint>();
        Add(endpoints, new IPEndPoint(IPAddress.Broadcast, 0));

        if (OperatingSystem.IsAndroid())
        {
            // Глобальный broadcast (255.255.255.255) на телефонах уходит через маршрут
            // по умолчанию и легко теряется: VPN, несколько интерфейсов, фильтрация Wi-Fi.
            // Дополнительно шлём направленный broadcast подсети, вычисленный по адресу
            // активного маршрута (/24 и /16 — типовые домашние сети). Перечисление
            // интерфейсов на Android по-прежнему недоступно (PlatformNotSupported).
            if (GetRouteLocalAddress() is { } local
                && IPAddress.TryParse(local, out var address)
                && IsPrivate(address))
            {
                foreach (var directed in DirectedBroadcasts(address))
                {
                    Add(endpoints, new IPEndPoint(directed, 0));
                }
            }

            return endpoints;
        }

        try
        {
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
        }
        catch (PlatformNotSupportedException)
        {
            // Android и урезанные платформы: глобального broadcast достаточно.
        }
        catch (NetworkInformationException)
        {
            // Сеть могла измениться между вызовами: глобальный broadcast остаётся.
        }

        return endpoints;
    }

    /// <summary>Определяет локальный адрес по маршруту UDP, не отправляя пакет.</summary>
    private static string? GetRouteLocalAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));
            if (socket.LocalEndPoint is IPEndPoint local
                && !IPAddress.IsLoopback(local.Address)
                && !IPAddress.Any.Equals(local.Address))
            {
                return local.Address.ToString();
            }
        }
        catch (SocketException)
        {
            // Нет маршрута по умолчанию.
        }
        catch (ObjectDisposedException)
        {
            // Сокет закрылся — адрес неизвестен.
        }

        return null;
    }

    /// <summary>Направленные broadcast-адреса подсети для частного IPv4-адреса.</summary>
    /// <param name="address">Локальный адрес активного маршрута.</param>
    /// <returns>Адреса /24 и /16 broadcast.</returns>
    /// <remarks>
    /// Маску на Android взять неоткуда, но типовые домашние и офисные сети — /24 (реже /16).
    /// Направленный broadcast уходит через конкретный Wi-Fi-интерфейс, а не через маршрут
    /// по умолчанию, поэтому объявления комнат не теряются при активном VPN или нескольких
    /// интерфейсах (жалоба «комнаты не видны» после 0.2.131).
    /// </remarks>
    internal static IReadOnlyList<IPAddress> DirectedBroadcasts(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var bytes = address.GetAddressBytes();

        IPAddress[] candidates =
        [
            new IPAddress([bytes[0], bytes[1], bytes[2], 255]),
            new IPAddress([bytes[0], bytes[1], 255, 255])
        ];

        return candidates.Distinct().ToArray();
    }

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static void Add(List<IPEndPoint> endpoints, IPEndPoint endpoint)
    {
        if (!endpoints.Any(existing => existing.Address.Equals(endpoint.Address)))
        {
            endpoints.Add(endpoint);
        }
    }
}
