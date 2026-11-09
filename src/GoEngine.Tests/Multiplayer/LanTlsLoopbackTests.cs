using System.Net;
using System.Net.Sockets;
using GoEngine.Multiplayer;

namespace GoEngine.Tests;

/// <summary>Loopback-тест TLS: обе стороны предъявляют эфемерные сертификаты и меняются сообщением.</summary>
public sealed class LanTlsLoopbackTests
{
    [TlsFact]
    public async Task Tls_Соединение_Доставляет_Сообщение()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var time = TimeProvider.System;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var connection = await LanTlsConnection.AcceptAsync(accepted, time, timeout.Token);
            var message = await connection.ReceiveAsync(timeout.Token);
            Assert.Equal(LanMessageTypes.Ping, message.Type);
            await connection.SendAsync(new LanMessage { Type = LanMessageTypes.Pong }, timeout.Token);
        }, timeout.Token);

        var client = Task.Run(async () =>
        {
            await using var connection = await LanTlsConnection.ConnectAsync("127.0.0.1", port, time, timeout.Token);
            await connection.SendAsync(new LanMessage { Type = LanMessageTypes.Ping }, timeout.Token);
            var answer = await connection.ReceiveAsync(timeout.Token);
            Assert.Equal(LanMessageTypes.Pong, answer.Type);
        }, timeout.Token);

        try
        {
            await Task.WhenAll(server, client);
        }
        catch (Exception exception)
        {
            Assert.Fail(exception.ToString());
        }
        finally
        {
            listener.Stop();
        }
    }
}
