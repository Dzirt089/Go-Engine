using GoEngine.Core;

namespace GoEngine.App.Rendering;

/// <summary>Решает, когда звучать звуку хода, и проигрывает его в фоне.</summary>
/// <remarks>
/// Между видом и проигрывателем стоит обвязка: вид сообщает «ход сделан», а правила «звучать или
/// нет» собраны здесь и проверяются тестами без окна и без звуковой системы. Правил три.
/// Первое: звук играется ровно один раз на ход — повторное уведомление об одном и том же ходе
/// (модель представления сообщает о нескольких свойствах сразу) второго стука не даёт.
/// Второе: два звука не звучат чаще, чем раз в <see cref="MinInterval"/>; ход и снятие камней —
/// одно событие, и дробить его на два стука нельзя, а быстрый ответ соперника не должен
/// накладываться на звук игрока.
/// Третье: при выключенной анимации звука нет — звук идёт вместе с анимацией камня, а не вместо неё.
/// Проигрыватель всегда вызывается в фоне: воспроизведение не должно задерживать поток интерфейса.
/// </remarks>
public sealed class StoneSoundPlayer
{
    /// <summary>Минимальная пауза между звуками.</summary>
    /// <remarks>
    /// Шестьдесят миллисекунд — заметно меньше длительности самого звука, но достаточно, чтобы
    /// уведомления об одном ходе, пришедшие подряд, не превратились в несколько стуков.
    /// </remarks>
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(60);

    /// <summary>Общий проигрыватель приложения: один на процесс.</summary>
    /// <remarks>
    /// Общее состояние обязательно: «какой ход уже звучал» и «когда звучал предыдущий» — это
    /// свойства приложения, а не вида. Благодаря общему экземпляру вид не держит у себя ни одного
    /// поля ради звука, а проигрыватель берётся из <c>App.Sound</c> в момент звука — значит,
    /// порядок регистрации головы и создания вида роли не играет.
    /// </remarks>
    public static StoneSoundPlayer Shared { get; } = new();

    /// <summary>Проигрыватель, заданный головой; <c>null</c> — берётся <c>App.Sound</c>.</summary>
    private readonly ISoundPlayer? _player;

    /// <summary>Признак «анимация показывается»; <c>null</c> — показывается.</summary>
    private readonly Func<bool>? _animationsEnabled;

    /// <summary>Часы: пауза между звуками считается по ним, а не по системному времени напрямую.</summary>
    private readonly TimeProvider _time;

    /// <summary>Замок: общий экземпляр могут позвать из разных потоков.</summary>
    private readonly object _gate = new();

    /// <summary>Номер хода, который уже звучал: повтор этого номера второго звука не даёт.</summary>
    private string? _lastMoveNumber;

    /// <summary>Когда звучал предыдущий звук.</summary>
    private DateTimeOffset _lastPlayed;

    /// <summary>Создаёт обвязку.</summary>
    /// <param name="player">Проигрыватель; <c>null</c> — брать <c>App.Sound</c> в момент звука.</param>
    /// <param name="animationsEnabled">
    /// Признак «анимация хода показывается прямо сейчас»; <c>null</c> — показывается. Система может
    /// отключить анимации (настройки доступности, энергосбережение), и тогда звук идти не должен:
    /// он часть анимации, а не отдельное событие. Голова, знающая о такой настройке, задаёт
    /// признак или ставит <see cref="SilentSoundPlayer"/>.
    /// </param>
    /// <param name="timeProvider">Часы; <c>null</c> — системные.</param>
    /// <remarks>
    /// Параметры нужны тестам и голове: в приложении обвязка берётся готовой
    /// (<see cref="Shared"/>), а подставной проигрыватель и управляемые часы позволяют проверить
    /// правила звука без звуковой карты.
    /// </remarks>
    public StoneSoundPlayer(
        ISoundPlayer? player = null,
        Func<bool>? animationsEnabled = null,
        TimeProvider? timeProvider = null)
    {
        _player = player;
        _animationsEnabled = animationsEnabled;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Сообщает о ходе: звук играется один раз на ход, если анимация показывается.</summary>
    /// <param name="moveNumber">
    /// Номер хода так, как его показывает модель представления (<c>MainViewModel.MoveNumber</c>).
    /// Ключом служит именно номер, а не точка хода: два паса подряд дают одну и ту же пустую точку
    /// и пустой список снятых, и по ним второй ход не отличить от повтора первого.
    /// </param>
    /// <param name="captured">Камни, снятые ходом; пусто — ход обошёлся без снятия.</param>
    /// <returns>Задача фонового воспроизведения: вид её не ждёт, тесты ждут.</returns>
    /// <remarks>
    /// Один ход — один звук: если ход снял камни, играется тембр снятия, а не два стука подряд.
    /// </remarks>
    public Task OnMove(string moveNumber, IReadOnlyList<Point> captured)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moveNumber);
        ArgumentNullException.ThrowIfNull(captured);

        if (_animationsEnabled?.Invoke() == false)
        {
            return Task.CompletedTask;
        }

        ReadOnlyMemory<byte> wav;
        ISoundPlayer? player;

        lock (_gate)
        {
            if (moveNumber == _lastMoveNumber)
            {
                return Task.CompletedTask;
            }

            // Номер запоминается до проверки паузы: иначе уведомление о том же ходе, пришедшее
            // позже паузы, прозвучало бы ещё раз.
            _lastMoveNumber = moveNumber;

            var now = _time.GetUtcNow();

            if (now - _lastPlayed < MinInterval)
            {
                return Task.CompletedTask;
            }

            _lastPlayed = now;
            wav = StoneSound.Play(captured.Count > 0 ? StoneSoundKind.Capture : StoneSoundKind.Move);
            player = _player;
        }

        player ??= global::GoEngine.App.App.Sound;

        return Task.Run(() => PlaySafely(player, wav));
    }

    /// <summary>Проигрывает звук, не выпуская наружу ни одного сбоя.</summary>
    /// <param name="player">Проигрыватель.</param>
    /// <param name="wav">Байты WAV.</param>
    /// <remarks>
    /// Исключение гасится осознанно: проигрыватель обязан молчать о сбоях (<see cref="ISoundPlayer"/>),
    /// но обещание интерфейса — не гарантия, а подстраховка нужна: необработанный сбой в фоновой
    /// задаче всплыл бы в логе падений как ошибка игры, которой на самом деле нет.
    /// </remarks>
    private static void PlaySafely(ISoundPlayer player, ReadOnlyMemory<byte> wav)
    {
        try
        {
            player.Play(wav);
        }
        catch (Exception)
        {
            // Звук — украшение: нет устройства, занято, отказала система — игра идёт дальше.
        }
    }
}
