using System.Globalization;
using GoEngine.App.Services.Logging;

namespace GoEngine.App.Services;

/// <summary>Счётчик времени партии: идёт, пока игрок играет, и замирает на паузе.</summary>
/// <remarks>
/// <para>
/// Время партии считается в одном месте — здесь. Панель партии только показывает строку
/// (<see cref="Display"/>, <see cref="ShortDisplay"/>) и признаки «идут» и «на паузе»,
/// а сама времени не хранит: иначе вид и модель разошлись бы в том, сколько партия идёт.
/// </para>
/// <para>
/// Служба не знает про Avalonia: тиканье заказывает вид своим таймером (<see cref="Tick"/>),
/// а <see cref="Elapsed"/> считается по запросу. Поэтому пауза не зависит от того, успел ли
/// вид обновить строку, и время не «догоняет» себя после пропущенного тика.
/// </para>
/// <para>
/// Время берётся у <see cref="TimeProvider"/>: прямых обращений к системным часам в проекте нет
/// (<c>AGENTS.md</c>, п. 6), а в тестах источник времени подменяется.
/// </para>
/// </remarks>
public sealed class GameClock
{
    private readonly TimeProvider _time;

    /// <summary>Время, накопленное законченными отрезками.</summary>
    private TimeSpan _elapsed;

    /// <summary>Метка начала текущего отрезка; <c>null</c> — часы стоят.</summary>
    private long? _segmentStart;

    /// <summary>Создаёт часы партии.</summary>
    /// <param name="time">Источник времени; <c>null</c> — часы приложения (<see cref="AppLog.Time"/>).</param>
    public GameClock(TimeProvider? time = null) => _time = time ?? AppLog.Time;

    /// <summary>Часы изменились: время, состояние или и то и другое.</summary>
    public event EventHandler? Changed;

    /// <summary>Часы идут: время партии увеличивается прямо сейчас.</summary>
    public bool IsRunning => _segmentStart.HasValue;

    /// <summary>Часы стоят на паузе: партию остановил игрок.</summary>
    /// <remarks>Завершение партии паузой не считается — там часы останавливает <see cref="Stop"/>.</remarks>
    public bool IsPaused { get; private set; }

    /// <summary>Сколько идёт партия.</summary>
    public TimeSpan Elapsed => _segmentStart is { } start ? _elapsed + _time.GetElapsedTime(start) : _elapsed;

    /// <summary>Время партии строкой: «ММ:СС», а после часа — «Ч:ММ:СС».</summary>
    public string Display => Format(Elapsed, withHours: true);

    /// <summary>Время партии коротко: всегда «ММ:СС» — для узкой строки на телефоне.</summary>
    public string ShortDisplay => Format(Elapsed, withHours: false);

    /// <summary>Начинает счёт с нуля: так начинается партия.</summary>
    /// <remarks>Прежнее время забывается: партия начата заново, и её время считается заново.</remarks>
    public void Start()
    {
        _elapsed = TimeSpan.Zero;
        _segmentStart = _time.GetTimestamp();
        IsPaused = false;
        Notify();
    }

    /// <summary>Останавливает часы на паузе, сохраняя накопленное время.</summary>
    /// <remarks>Повторная пауза ничего не делает: останавливать нечего.</remarks>
    public void Pause()
    {
        if (_segmentStart is not { } start)
        {
            return;
        }

        _elapsed += _time.GetElapsedTime(start);
        _segmentStart = null;
        IsPaused = true;
        Notify();
    }

    /// <summary>Продолжает счёт с накопленного времени.</summary>
    /// <remarks>Если часы уже идут, продолжение ничего не меняет: новый отрезок не начинается.</remarks>
    public void Resume()
    {
        if (_segmentStart.HasValue)
        {
            return;
        }

        _segmentStart = _time.GetTimestamp();
        IsPaused = false;
        Notify();
    }

    /// <summary>Останавливает часы, не считая это паузой: например, партия завершена.</summary>
    /// <remarks>
    /// Отдельно от <see cref="Pause"/>: у завершённой партии время замирает на итоговом значении,
    /// но игрок её не останавливал — «Пауза» в панели была бы неправдой, а «Продолжить» предлагало
    /// бы продолжить законченную игру. Накопленное время сохраняется.
    /// </remarks>
    public void Stop()
    {
        if (_segmentStart is { } start)
        {
            _elapsed += _time.GetElapsedTime(start);
            _segmentStart = null;
        }

        IsPaused = false;
        Notify();
    }

    /// <summary>Обнуляет и останавливает часы: партия отменена или ещё не начата.</summary>
    public void Reset()
    {
        _elapsed = TimeSpan.Zero;
        _segmentStart = null;
        IsPaused = false;
        Notify();
    }

    /// <summary>Пульс часов: вид зовёт его по таймеру раз в секунду.</summary>
    /// <remarks>
    /// Пересчитывать нечего: <see cref="Elapsed"/> берётся у источника времени по запросу,
    /// и это единственный способ не отставать от него. Пульс нужен виду: он сообщает, что строку
    /// времени пора показать заново. У стоящих часов пульса нет — будить вид незачем.
    /// </remarks>
    public void Tick()
    {
        if (IsRunning)
        {
            Notify();
        }
    }

    /// <summary>Собирает строку времени: часы показываются только тогда, когда они есть.</summary>
    /// <param name="elapsed">Прошедшее время.</param>
    /// <param name="withHours">Показывать часы, если партия идёт больше часа.</param>
    /// <returns>Строку времени.</returns>
    /// <remarks>
    /// Часы идут без ведущего нуля («1:05:03»), минуты и секунды — всегда двузначные. Так строка
    /// не дёргается по ширине каждую секунду и не притворяется счётчиком, у которого нечего считать.
    /// </remarks>
    private static string Format(TimeSpan elapsed, bool withHours)
    {
        var totalMinutes = (long)elapsed.TotalMinutes;

        return withHours && totalMinutes >= 60
            ? string.Create(CultureInfo.InvariantCulture, $"{totalMinutes / 60}:{totalMinutes % 60:D2}:{elapsed.Seconds:D2}")
            : string.Create(CultureInfo.InvariantCulture, $"{totalMinutes:D2}:{elapsed.Seconds:D2}");
    }

    /// <summary>Сообщает об изменении часов.</summary>
    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
