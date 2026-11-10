namespace GoEngine.Multiplayer;

/// <summary>Настройки локальной сетевой партии.</summary>
/// <remarks>
/// Значения по умолчанию рассчитаны на домашнюю Wi-Fi-сеть и детей 7–8 лет: ход 90 секунд,
/// переподключение до минуты, обнаружение комнат раз в 1,5 секунды. В тестах источник времени
/// подменяется <c>FakeTimeProvider</c>, поэтому все таймеры принимают <see cref="TimeProvider"/>.
/// </remarks>
public sealed record MultiplayerOptions
{
    /// <summary>Версия сетевого протокола; несовпадение отклоняется до обмена ходами.</summary>
    /// <remarks>Версия 2 добавила нигири-розыгрыш цвета (commit-reveal).</remarks>
    public int ProtocolVersion { get; init; } = 2;

    /// <summary>Сколько ждём ответа в нигири: угадывание и выбор цвета.</summary>
    /// <remarks>15 секунд — ребёнок успевает прочитать крупную кнопку и нажать.</remarks>
    public TimeSpan ColorDrawTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Сколько времени даётся на один ход до вынужденного паса.</summary>
    public TimeSpan MoveTimeout { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>Сколько времени ждём возвращения игрока после обрыва, прежде чем прервать партию.</summary>
    public TimeSpan ReconnectTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Пауза между попытками переподключиться.</summary>
    public TimeSpan ReconnectRetryInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Таймаут TLS-рукопожатия и первого сообщения при подключении.</summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Сколько ждём решения хозяина по заявке гостя, 2 минуты.</summary>
    /// <remarks>
    /// <para>
    /// Это не сетевой таймаут, а время человека: ребёнок-хозяин может отвлечься, прежде чем
    /// нажать большую кнопку. Короткий <see cref="HandshakeTimeout"/> здесь обрывал бы ожидание
    /// раньше, чем хозяин успевал прочитать запрос.
    /// </para>
    /// <para>
    /// Тот же срок действует и у хозяина: если он так и не решил, зависшая заявка снимается,
    /// и лобби снова ждёт гостя (R-007).
    /// </para>
    /// </remarks>
    public TimeSpan JoinDecisionTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Интервал heartbeat-пинга в установленном соединении.</summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>После какого молчания соединение считается потерянным.</summary>
    public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Как часто хозяин объявляет комнату в локальной сети.</summary>
    public TimeSpan DiscoveryInterval { get; init; } = TimeSpan.FromSeconds(1.5);

    /// <summary>Сколько живёт запись комнаты без нового объявления.</summary>
    public TimeSpan RoomLifetime { get; init; } = TimeSpan.FromSeconds(6);

    /// <summary>UDP-порт, на котором гости слушают объявления комнат.</summary>
    public int DiscoveryPort { get; init; } = 47812;

    /// <summary>Предпочтительный TCP-порт хозяина; если занят, берётся свободный порт ОС.</summary>
    public int PreferredTcpPort { get; init; } = 47810;

    /// <summary>Сколько вынужденных пасов подряд прерывают зависшую партию.</summary>
    /// <remarks>Авто-пас не завершает партию по правилам; это предохранитель сетевого слоя.</remarks>
    public int AutoPassStallLimit { get; init; } = 3;

    /// <summary>Источник времени для таймеров, heartbeat и срока жизни комнат.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
