using System.Globalization;
using System.Text.Json;
using GoEngine.Core;

namespace GoEngine.App.Services.Updates;

/// <summary>Манифест обновления: что за версия и какие файлы к ней приложены.</summary>
/// <param name="Version">Версия выпуска.</param>
/// <param name="Tag">Тег выпуска на GitHub, например <c>v0.2.15</c>.</param>
/// <param name="PublishedAt">Когда выпуск опубликован.</param>
/// <param name="Assets">Файлы по ключам платформ: <c>win-x64</c>, <c>android</c>, <c>linux-x64</c>, <c>osx-x64</c>, <c>osx-arm64</c>.</param>
/// <remarks>
/// Формат — публичный контракт между сборкой и приложением, описан в <c>PROJECT.md</c>.
/// Разбор возвращает <see cref="Result{T}"/>: битый или чужой манифест — ожидаемый исход,
/// а не исключение.
/// </remarks>
public sealed record UpdateManifest(
    AppVersion Version,
    string Tag,
    DateTimeOffset PublishedAt,
    IReadOnlyDictionary<string, UpdateAsset> Assets)
{
    /// <summary>Разбирает манифест из JSON.</summary>
    /// <param name="json">Содержимое <c>update.json</c>.</param>
    /// <returns>Манифест или причина отказа.</returns>
    public static Result<UpdateManifest> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<UpdateManifest>.Fail("Манифест обновления пуст.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Result<UpdateManifest>.Fail("Манифест обновления не является объектом JSON.");
            }

            var root = document.RootElement;
            var version = AppVersion.Parse(Text(root, "version"));

            if (!version.IsSuccess)
            {
                return Result<UpdateManifest>.Fail(version.Error!);
            }

            if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Object)
            {
                return Result<UpdateManifest>.Fail("В манифесте обновления нет списка файлов.");
            }

            Dictionary<string, UpdateAsset> assets = [];

            foreach (var property in assetsElement.EnumerateObject())
            {
                var asset = ReadAsset(property.Value);

                if (asset is null)
                {
                    return Result<UpdateManifest>.Fail($"Файл обновления для «{property.Name}» описан не полностью.");
                }

                assets[property.Name] = asset;
            }

            if (assets.Count == 0)
            {
                return Result<UpdateManifest>.Fail("В манифесте обновления нет ни одного файла.");
            }

            return Result<UpdateManifest>.Ok(new UpdateManifest(
                version.Value,
                Text(root, "tag") ?? string.Empty,
                ReadDate(root),
                assets));
        }
        catch (JsonException exception)
        {
            return Result<UpdateManifest>.Fail($"Манифест обновления не разбирается: {exception.Message}");
        }
    }

    /// <summary>Возвращает файл для платформы.</summary>
    /// <param name="platformKey">Ключ платформы.</param>
    /// <returns>Файл или причина отказа.</returns>
    public Result<UpdateAsset> AssetFor(string platformKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformKey);

        return Assets.TryGetValue(platformKey, out var asset)
            ? Result<UpdateAsset>.Ok(asset)
            : Result<UpdateAsset>.Fail($"В обновлении нет файла для платформы «{platformKey}».");
    }

    /// <summary>Читает строковое поле.</summary>
    /// <param name="root">Корень манифеста.</param>
    /// <param name="name">Имя поля.</param>
    /// <returns>Значение или <c>null</c>.</returns>
    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    /// <summary>Читает дату выпуска.</summary>
    /// <param name="root">Корень манифеста.</param>
    /// <returns>Дата или текущее время, если поле не разбирается.</returns>
    private static DateTimeOffset ReadDate(JsonElement root) =>
        DateTimeOffset.TryParse(Text(root, "publishedAt"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var published)
            ? published
            : DateTimeOffset.UnixEpoch;

    /// <summary>Читает описание файла.</summary>
    /// <param name="element">Элемент файла.</param>
    /// <returns>Файл или <c>null</c>, если полей не хватает.</returns>
    private static UpdateAsset? ReadAsset(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = Text(element, "name");
        var url = Text(element, "url");
        var sha = Text(element, "sha256");

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(sha))
        {
            return null;
        }

        var size = element.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var value) ? value : 0;

        return new UpdateAsset(name, url, size, sha);
    }
}
