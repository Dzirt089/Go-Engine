using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GoEngine.Multiplayer;

/// <summary>Проверка формы self-signed сертификата peer до доверия к соединению.</summary>
internal static class TlsCertificateValidator
{
    internal const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    internal const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    private const int P256KeySizeBits = 256;

    internal static bool IsAcceptable(
        X509Certificate? certificate,
        string requiredEnhancedKeyUsageOid,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (certificate is not X509Certificate2 peerCertificate)
        {
            return false;
        }

        if (!string.Equals(peerCertificate.Subject, peerCertificate.Issuer, StringComparison.Ordinal))
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (peerCertificate.NotBefore.ToUniversalTime() > now || peerCertificate.NotAfter.ToUniversalTime() < now)
        {
            return false;
        }

        var basicConstraints = peerCertificate.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();
        if (basicConstraints is { CertificateAuthority: true })
        {
            return false;
        }

        var enhancedKeyUsage = peerCertificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        if (enhancedKeyUsage is null
            || !enhancedKeyUsage.EnhancedKeyUsages.Cast<Oid>().Any(oid =>
                string.Equals(oid.Value, requiredEnhancedKeyUsageOid, StringComparison.Ordinal)))
        {
            return false;
        }

        using var publicKey = peerCertificate.GetECDsaPublicKey();
        return publicKey is not null && publicKey.KeySize == P256KeySizeBits;
    }
}
