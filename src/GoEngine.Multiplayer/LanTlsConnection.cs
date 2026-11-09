using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace GoEngine.Multiplayer;

/// <summary>TLS-соединение с обменом кадрами сетевых сообщений.</summary>
/// <remarks>
/// Обе стороны предъявляют эфемерные self-signed сертификаты; цепочка и hostname не проверяются,
/// потому что PKI в локальной сети нет. Проверяется форма сертификата peer, а доверие к хозяину
/// подтверждает человек кнопкой «Пустить». После TLS сообщения идут кадрами с ограничением размера.
/// </remarks>
internal sealed class LanTlsConnection : IAsyncDisposable
{
    private const string TlsTargetHost = "goengine.lan";

    private readonly TcpClient _tcpClient;
    private readonly SslStream _sslStream;
    private readonly EphemeralTlsCertificate _localCertificate;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _receiveLock = new(1, 1);
    private readonly object _sync = new();
    private bool _closed;

    private LanTlsConnection(
        TcpClient tcpClient,
        SslStream sslStream,
        EphemeralTlsCertificate localCertificate)
    {
        _tcpClient = tcpClient;
        _sslStream = sslStream;
        _localCertificate = localCertificate;
    }

    internal bool IsClosed
    {
        get
        {
            lock (_sync)
            {
                return _closed;
            }
        }
    }

    internal string? RemoteAddress => _tcpClient.Client.RemoteEndPoint?.ToString();

    internal static async Task<LanTlsConnection> ConnectAsync(
        string host,
        int port,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var tcpClient = new TcpClient { NoDelay = true };
        EphemeralTlsCertificate? certificate = null;
        SslStream? sslStream = null;
        try
        {
            await tcpClient.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            certificate = EphemeralTlsCertificate.Create(timeProvider);
            sslStream = new SslStream(
                tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                (_, peerCertificate, _, _) => TlsCertificateValidator.IsAcceptable(
                    peerCertificate,
                    TlsCertificateValidator.ServerAuthenticationOid,
                    timeProvider));

            await sslStream.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = TlsTargetHost,
                    ClientCertificates = new X509CertificateCollection { certificate.Certificate },
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    AllowRenegotiation = false,
                    EncryptionPolicy = EncryptionPolicy.RequireEncryption
                },
                cancellationToken).ConfigureAwait(false);

            var connection = new LanTlsConnection(tcpClient, sslStream, certificate);
            tcpClient = null!;
            sslStream = null;
            certificate = null;
            return connection;
        }
        finally
        {
            sslStream?.Dispose();
            certificate?.Dispose();
            tcpClient?.Dispose();
        }
    }

    internal static async Task<LanTlsConnection> AcceptAsync(
        TcpClient tcpClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tcpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);

        EphemeralTlsCertificate? certificate = null;
        SslStream? sslStream = null;
        try
        {
            tcpClient.NoDelay = true;
            certificate = EphemeralTlsCertificate.Create(timeProvider);
            sslStream = new SslStream(
                tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                (_, peerCertificate, _, _) => TlsCertificateValidator.IsAcceptable(
                    peerCertificate,
                    TlsCertificateValidator.ClientAuthenticationOid,
                    timeProvider));

            await sslStream.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificateContext = certificate.CreateContext(),
                    ClientCertificateRequired = true,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    AllowRenegotiation = false,
                    EncryptionPolicy = EncryptionPolicy.RequireEncryption
                },
                cancellationToken).ConfigureAwait(false);

            var connection = new LanTlsConnection(tcpClient, sslStream, certificate);
            sslStream = null;
            certificate = null;
            return connection;
        }
        finally
        {
            sslStream?.Dispose();
            certificate?.Dispose();
        }
    }

    internal async Task SendAsync(LanMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payload = LanMessageCodec.Encode(message);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfClosed();
            await LanFrameCodec.WriteAsync(_sslStream, payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    internal async Task<LanMessage> ReceiveAsync(CancellationToken cancellationToken)
    {
        await _receiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfClosed();
            var payload = await LanFrameCodec.ReadAsync(_sslStream, cancellationToken).ConfigureAwait(false);
            return LanMessageCodec.TryDecode(payload, out var message) && message is not null
                ? message
                : throw new LanProtocolException("Сетевое сообщение не удалось разобрать.");
        }
        finally
        {
            _receiveLock.Release();
        }
    }

    private void ThrowIfClosed()
    {
        lock (_sync)
        {
            if (_closed)
            {
                throw new LanConnectionClosedException("Соединение закрыто.");
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_closed)
            {
                return ValueTask.CompletedTask;
            }

            _closed = true;
        }

        try
        {
            _sslStream.Flush();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // Peer мог закрыть соединение раньше: освобождение ресурсов важнее flush.
        }

        _sslStream.Dispose();
        _tcpClient.Dispose();
        _localCertificate.Dispose();
        return ValueTask.CompletedTask;
    }
}
