namespace GoEngine.Tests;

/// <summary>TLS-loopback-тесты идут отдельной последовательной коллекцией.</summary>
/// <remarks>
/// Три теста поднимают настоящий TLS и сертификат; при параллельном запуске с остальными
/// они изредка не успевали за таймауты рукопожатия на загруженном CI. Последовательная
/// коллекция убирает эту гонку, не замедляя остальные тесты.
/// </remarks>
[CollectionDefinition("TLS-loopback", DisableParallelization = true)]
public sealed class TlsLoopbackGroup
{
}
