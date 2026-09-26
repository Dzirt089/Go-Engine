using GoEngine.Core;

namespace GoEngine.App.Rendering;

/// <summary>Состояние анимации камней: появление нового и исчезновение снятых.</summary>
/// <param name="Appearing">Точка только что поставленного камня или <c>null</c>.</param>
/// <param name="Disappearing">Точки снятых камней; пустой список, если ничего не снято.</param>
/// <param name="DisappearingColor">Цвет снятых камней.</param>
/// <param name="Progress">Прогресс анимации от 0 до 1.</param>
/// <remarks>
/// Состояние не зависит от Avalonia и Skia: это чистая арифметика кадра, поэтому её можно
/// проверить без окна. Кадры рисует <see cref="BoardRenderer"/>, а время двигает
/// <see cref="Controls.BoardControl"/> через <c>DispatcherTimer</c>.
/// </remarks>
public readonly record struct StoneAnimation(
    Point? Appearing,
    IReadOnlyList<Point> Disappearing,
    StoneColor DisappearingColor,
    double Progress)
{
    /// <summary>Длительность анимации в секундах.</summary>
    public const double DurationSeconds = 0.18;

    /// <summary>Насколько камень меньше своего размера в начале появления.</summary>
    private const double StartScale = 0.6;

    /// <summary>Анимации нет: всё нарисовано в конечном виде.</summary>
    public static StoneAnimation None => new(null, [], StoneColor.Empty, 1);

    /// <summary>Идёт ли анимация прямо сейчас.</summary>
    public bool IsActive => Progress < 1 && (Appearing is not null || Disappearing.Count > 0);

    /// <summary>Размер появляющегося камня: от <see cref="StartScale"/> до полного.</summary>
    public double AppearanceScale => StartScale + ((1 - StartScale) * Clamped);

    /// <summary>Прозрачность появляющегося камня: от 0 до 255.</summary>
    public byte AppearanceAlpha => (byte)(255 * Clamped);

    /// <summary>Прозрачность исчезающих камней: от 255 до 0.</summary>
    public byte DisappearanceAlpha => (byte)(255 * (1 - Clamped));

    /// <summary>Прогресс, приведённый к отрезку от 0 до 1.</summary>
    private double Clamped => Math.Clamp(Progress, 0, 1);

    /// <summary>Продвигает анимацию на прошедшее время.</summary>
    /// <param name="seconds">Сколько секунд прошло с прошлого кадра.</param>
    /// <returns>Состояние следующего кадра.</returns>
    public StoneAnimation Advance(double seconds) =>
        this with { Progress = Math.Min(1, Progress + (seconds / DurationSeconds)) };
}
