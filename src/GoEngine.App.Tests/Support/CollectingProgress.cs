using GoEngine.App.Services.Updates;

namespace GoEngine.App.Tests;

/// <summary>Собирает отчёты о ходе загрузки: загрузчик зовёт Report прямо из своего потока.</summary>
/// <param name="reports">Список, куда складываются отчёты.</param>
/// <remarks>
/// Был описан дважды — в тестах загрузчика и в тестах службы обновления. Вынесен в опору:
/// подставной сборщик отчётов должен меняться в одном месте.
/// </remarks>
internal sealed class CollectingProgress(List<DownloadProgress> reports) : IProgress<DownloadProgress>
{
    /// <inheritdoc />
    public void Report(DownloadProgress value) => reports.Add(value);
}
