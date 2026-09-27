namespace GoEngine.App.Services.Updates;

/// <summary>Результат проверки обновления: статус и сам манифест.</summary>
/// <param name="Status">Актуальная версия или доступное обновление.</param>
/// <param name="Manifest">Манифест, по которому принято решение.</param>
public readonly record struct UpdateCheck(UpdateStatus Status, UpdateManifest Manifest)
{
    /// <summary>Доступна ли новая версия.</summary>
    public bool IsAvailable => Status == UpdateStatus.Available;

    /// <summary>Версия доступного выпуска.</summary>
    public AppVersion Version => Manifest.Version;
}
