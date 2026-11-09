using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GoEngine.Multiplayer;

/// <summary>Эфемерный self-signed TLS-сертификат на одно соединение (ECDSA P-256).</summary>
/// <remarks>
/// <para>
/// Долгосрочных ключей в сетевом режиме нет: сертификат живёт ограниченное время и только шифрует
/// канал. Хозяина подтверждает человек кнопкой «Пустить», а не PKI.
/// </para>
/// <para>
/// Windows Schannel не принимает сертификаты с ephemeral CNG-ключами, поэтому на Windows PFX
/// переимпортируется. На некоторых профилях Windows импорт PFX в хранилище ключей запрещён
/// политикой (NTE_PERM); тогда сертификат один раз создаётся в пользовательском хранилище
/// штатным <c>New-SelfSignedCertificate</c> и дальше переиспользуется. Это тот же TLS, меняется
/// только способ получить ключ; сетевой протокол и проверки не меняются.
/// </para>
/// </remarks>
internal sealed class EphemeralTlsCertificate : IDisposable
{
    private const string StoreSubject = "CN=goengine-lan";
    private const string EnhancedKeyUsageExtension =
        "2.5.29.37={text}1.3.6.1.5.5.7.3.1,1.3.6.1.5.5.7.3.2";

    private readonly X509Certificate2 _certificate;
    private bool _disposed;

    private EphemeralTlsCertificate(X509Certificate2 certificate)
    {
        _certificate = certificate;
    }

    internal X509Certificate2 Certificate => _certificate;

    internal static EphemeralTlsCertificate Create(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        try
        {
            return CreateFromPfx(timeProvider);
        }
        catch (CryptographicException) when (OperatingSystem.IsWindows())
        {
            return CreateFromWindowsUserStore(timeProvider);
        }
    }

    internal SslStreamCertificateContext CreateContext() =>
        SslStreamCertificateContext.Create(_certificate, additionalCertificates: null, offline: true);

    private static EphemeralTlsCertificate CreateFromPfx(TimeProvider timeProvider)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(StoreSubject, ecdsa, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection
            {
                new(TlsCertificateValidator.ServerAuthenticationOid),
                new(TlsCertificateValidator.ClientAuthenticationOid)
            },
            critical: false));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: false));

        var now = timeProvider.GetUtcNow();
        var certificate = request.CreateSelfSigned(now.AddHours(-1), now.AddHours(24));
        try
        {
            var keyStorageFlags = OperatingSystem.IsWindows()
                ? X509KeyStorageFlags.DefaultKeySet
                : X509KeyStorageFlags.EphemeralKeySet;
            var pfx = certificate.Export(X509ContentType.Pfx);
            try
            {
                var imported = new X509Certificate2(pfx, (string?)null, keyStorageFlags);
                return new EphemeralTlsCertificate(imported);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(pfx);
            }
        }
        finally
        {
            certificate.Dispose();
        }
    }

    private static EphemeralTlsCertificate CreateFromWindowsUserStore(TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        var existing = FindStoreCertificate(StoreSubject, now);
        if (existing is not null)
        {
            return new EphemeralTlsCertificate(existing);
        }

        var thumbprint = CreateStoreCertificateViaPowerShell();
        var created = thumbprint is null ? null : FindStoreCertificate(thumbprint, now);
        return created is null
            ? throw new CryptographicException("Не удалось подготовить TLS-сертификат в хранилище пользователя Windows.")
            : new EphemeralTlsCertificate(created);
    }

    private static X509Certificate2? FindStoreCertificate(string value, DateTimeOffset now)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        var byThumbprint = value.Length == 40;
        var certificates = byThumbprint
            ? store.Certificates.Find(X509FindType.FindByThumbprint, value, validOnly: false)
            : store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, value, validOnly: false);

        foreach (var certificate in certificates.Cast<X509Certificate2>())
        {
            if (!certificate.HasPrivateKey
                || certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime.AddHours(1)
                || !TlsCertificateValidator.IsAcceptable(
                    certificate,
                    TlsCertificateValidator.ServerAuthenticationOid,
                    new FixedTimeProvider(now)))
            {
                certificate.Dispose();
                continue;
            }

            return certificate;
        }

        return null;
    }

    private static string? CreateStoreCertificateViaPowerShell()
    {
        // Сначала PowerShell 7, затем встроенный Windows PowerShell: в разных профилях
        // Windows один из них может быть недоступен или без провайдера Cert:.
        return TryCreateStoreCertificate("pwsh.exe") ?? TryCreateStoreCertificate("powershell.exe");
    }

    private static string? TryCreateStoreCertificate(string shell)
    {
        const string script =
            "$c=New-SelfSignedCertificate -Subject 'CN=goengine-lan' -CertStoreLocation 'Cert:\\CurrentUser\\My' " +
            "-KeyAlgorithm ECDSA_P256 -KeyExportPolicy Exportable -NotAfter (Get-Date).AddDays(30) " +
            "-TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.1,1.3.6.1.5.5.7.3.2'); $c.Thumbprint";
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = shell,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(script);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            if (!process.WaitForExit(15000))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            var thumbprint = process.StandardOutput.ReadToEnd().Trim();
            return thumbprint.Length == 40 ? thumbprint : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>Источник времени с фиксированным текущим моментом для проверки сертификата из хранилища.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        internal FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _certificate.Dispose();
    }
}