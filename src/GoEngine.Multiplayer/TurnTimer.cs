namespace GoEngine.Multiplayer;

/// <summary>Таймер одного хода с паузой и продолжением с остатка.</summary>
/// <remarks>
/// В сетевой партии таймер ведёт только хозяин: он один решает, когда истекли 90 секунд,
/// и только он делает вынужденный пас. Гость отображает остаток, полученный в сообщениях.
/// Источник времени инжектируется, поэтому тесты двигают время через <c>FakeTimeProvider</c>.
/// </remarks>
public sealed class TurnTimer : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly object _sync = new();
    private ITimer? _timer;
    private DateTimeOffset _deadline;
    private TimeSpan _remaining;
    private bool _running;
    private bool _paused;
    private bool _disposed;

    /// <summary>Создаёт таймер хода.</summary>
    /// <param name="timeProvider">Источник времени; в тестах — поддельный.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> равен <see langword="null"/>.</exception>
    public TurnTimer(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Время хода истекло: хозяин должен сделать вынужденный пас.</summary>
    public event EventHandler? Expired;

    /// <summary>Таймер идёт прямо сейчас.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _running;
            }
        }
    }

    /// <summary>Таймер стоит на паузе (например, на время переподключения).</summary>
    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _paused;
            }
        }
    }

    /// <summary>Сколько времени осталось на ход.</summary>
    public TimeSpan Remaining
    {
        get
        {
            lock (_sync)
            {
                return RemainingNoLock();
            }
        }
    }

    /// <summary>Запускает таймер на новый ход.</summary>
    /// <param name="duration">Длительность хода; больше нуля.</param>
    /// <exception cref="ArgumentOutOfRangeException">Длительность не положительна.</exception>
    /// <exception cref="ObjectDisposedException">Таймер уже освобождён.</exception>
    public void Start(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Длительность хода должна быть больше нуля.");
        }

        lock (_sync)
        {
            ThrowIfDisposed();
            CancelTimerNoLock();
            _remaining = duration;
            _deadline = _timeProvider.GetUtcNow() + duration;
            _running = true;
            _paused = false;
            _timer = _timeProvider.CreateTimer(OnTimer, null, duration, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Ставит таймер на паузу; остаток сохраняется.</summary>
    public void Pause()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_running || _paused)
            {
                return;
            }

            _remaining = RemainingNoLock();
            CancelTimerNoLock();
            _running = false;
            _paused = true;
        }
    }

    /// <summary>Продолжает таймер с сохранённого остатка.</summary>
    public void Resume()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_paused)
            {
                return;
            }

            _paused = false;
            _running = true;
            _deadline = _timeProvider.GetUtcNow() + _remaining;
            _timer = _timeProvider.CreateTimer(OnTimer, null, _remaining, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Останавливает таймер и обнуляет остаток.</summary>
    public void Stop()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            CancelTimerNoLock();
            _running = false;
            _paused = false;
            _remaining = TimeSpan.Zero;
        }
    }

    private void OnTimer(object? state)
    {
        lock (_sync)
        {
            if (_disposed || !_running || _paused)
            {
                return;
            }

            _running = false;
            _remaining = TimeSpan.Zero;
            CancelTimerNoLock();
        }

        Expired?.Invoke(this, EventArgs.Empty);
    }

    private TimeSpan RemainingNoLock()
    {
        if (!_running)
        {
            return _remaining;
        }

        var left = _deadline - _timeProvider.GetUtcNow();
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private void CancelTimerNoLock()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelTimerNoLock();
            _running = false;
            _paused = false;
        }
    }
}
