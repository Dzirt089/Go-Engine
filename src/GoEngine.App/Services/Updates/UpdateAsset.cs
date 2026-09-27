namespace GoEngine.App.Services.Updates;

/// <summary>Файл обновления для одной платформы.</summary>
/// <param name="Name">Имя файла, под которым его сохраняет приложение.</param>
/// <param name="Url">Прямая ссылка на файл.</param>
/// <param name="Size">Размер в байтах; <c>0</c> — размер в манифесте не указан.</param>
/// <param name="Sha256">Ожидаемая контрольная сумма SHA-256 (шестнадцатеричная строка).</param>
public sealed record UpdateAsset(string Name, string Url, long Size, string Sha256);
