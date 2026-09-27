using GoEngine.App.Services.Updates;

namespace GoEngine.App.Tests;

/// <summary>Тесты разбора манифеста обновления: формат — публичный контракт.</summary>
public sealed class UpdateManifestTests
{
    private const string Valid = """
        {
          "version": "0.2.15",
          "tag": "v0.2.15",
          "publishedAt": "2026-09-28T12:00:00Z",
          "assets": {
            "win-x64": { "name": "GoEngine-Setup.exe", "url": "https://example.org/setup.exe", "size": 1234, "sha256": "AA11" },
            "android": { "name": "app.apk", "url": "https://example.org/app.apk", "size": 4321, "sha256": "BB22" }
          }
        }
        """;

    [Fact]
    public void Разбор_Читает_Версию_Тег_И_Файлы()
    {
        var parsed = UpdateManifest.Parse(Valid);

        Assert.True(parsed.IsSuccess, parsed.Error);

        var manifest = parsed.Value!;
        Assert.Equal(new AppVersion(0, 2, 15), manifest.Version);
        Assert.Equal("v0.2.15", manifest.Tag);
        Assert.Equal(2, manifest.Assets.Count);

        var windows = manifest.AssetFor("win-x64");
        Assert.True(windows.IsSuccess);
        Assert.Equal("GoEngine-Setup.exe", windows.Value!.Name);
        Assert.Equal(1234, windows.Value.Size);
        Assert.Equal("AA11", windows.Value.Sha256);
    }

    [Fact]
    public void Разбор_Отказывает_Для_Чужой_Платформы()
    {
        var manifest = UpdateManifest.Parse(Valid).Value!;
        var asset = manifest.AssetFor("plan9");

        Assert.False(asset.IsSuccess);
        Assert.Contains("plan9", asset.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("не json")]
    [InlineData("[1,2,3]")]
    [InlineData("""{ "version": "0.2.15" }""")]
    [InlineData("""{ "tag": "v1", "assets": {} }""")]
    [InlineData("""{ "version": "abc", "assets": { "win-x64": { "name": "a", "url": "u", "sha256": "s" } } }""")]
    [InlineData("""{ "version": "1.0.0", "assets": { "win-x64": { "name": "a", "url": "u" } } }""")]
    public void Разбор_Отказывает_На_Битом_Манифесте(string? json)
    {
        var parsed = UpdateManifest.Parse(json);

        Assert.False(parsed.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(parsed.Error));
    }

    [Fact]
    public void Разбор_Прощает_Отсутствие_Даты_И_Размера()
    {
        var json = """{ "version": "1.0.0", "assets": { "android": { "name": "a.apk", "url": "u", "sha256": "s" } } }""";

        var parsed = UpdateManifest.Parse(json);

        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(DateTimeOffset.UnixEpoch, parsed.Value!.PublishedAt);
        Assert.Equal(0, parsed.Value.AssetFor("android").Value!.Size);
    }
}
