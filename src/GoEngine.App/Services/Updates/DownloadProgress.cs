namespace GoEngine.App.Services.Updates;

/// <summary>Ход загрузки обновления: сколько байт уже получено и сколько ожидается.</summary>
/// <param name="Received">Сколько байт записано в файл.</param>
/// <param name="Total">Ожидаемый размер файла; <c>0</c> — размер не назван ни манифестом, ни сервером.</param>
/// <remarks>
/// Отчёт о ходе нужен интерфейсу для полосы и процентов. Проценты считаются здесь, а не в виде:
/// приглашение, экран настроек и проверочный режим показывают одно и то же число. Ход честный —
/// по записанным на диск байтам, а не по «этапам» загрузки.
/// </remarks>
public readonly record struct DownloadProgress(long Received, long Total)
{
    /// <summary>Известен ли общий размер файла.</summary>
    public bool HasTotal => Total > 0;

    /// <summary>Доля загрузки от 0 до 1; <c>0</c>, если размер неизвестен.</summary>
    public double Fraction => Total > 0 ? Math.Clamp((double)Received / Total, 0d, 1d) : 0d;

    /// <summary>Доля загрузки целыми процентами от 0 до 100.</summary>
    public int Percent => (int)Math.Round(Fraction * 100, MidpointRounding.AwayFromZero);
}
