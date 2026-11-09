using System.Security.Cryptography;
using GoEngine.Multiplayer;

namespace GoEngine.Tests;

/// <summary>Факт, который пропускается, если среда запрещает сохранять ключ TLS-сертификата.</summary>
/// <remarks>
/// Windows Schannel требует постоянный ключ, а песочница разработки может запрещать доступ
/// к хранилищу ключей пользователя. CI и обычная машина разработчика проходят проверку;
/// локальная песочница честно помечает тест пропущенным, а не притворяется зелёной.
/// </remarks>
public sealed class TlsFactAttribute : FactAttribute
{
    public TlsFactAttribute()
    {
        Skip = TlsEnvironment.IsAvailable ? null : "Среда запрещает хранилище ключей TLS-сертификата.";
    }

    private static class TlsEnvironment
    {
        internal static bool IsAvailable
        {
            get
            {
                try
                {
                    using var certificate = EphemeralTlsCertificate.Create(TimeProvider.System);
                    return certificate.Certificate.HasPrivateKey;
                }
                catch (CryptographicException)
                {
                    return false;
                }
                catch (NotSupportedException)
                {
                    return false;
                }
            }
        }
    }
}
