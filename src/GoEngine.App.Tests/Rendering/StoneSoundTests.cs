using System.Buffers.Binary;
using System.Text;
using GoEngine.App.Rendering;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты звука хода: синтез WAV, заглушка и правила обвязки.</summary>
/// <remarks>
/// Звук проверяется без звуковой карты: синтез — чистая функция от констант, а обвязка получает
/// подставной проигрыватель и управляемые часы. Поэтому «играет» и «молчит» проверяются здесь
/// так же строго, как правила доски.
/// </remarks>
public sealed class StoneSoundTests
{
    /// <summary>Частота, с которой сравнивается «тихий» сигнал: заметно выше нуля, но не клиппинг.</summary>
    private const int QuietPeak = 1000;

    [Fact]
    public void Ход_Начинается_С_Заголовка_Riff_Wave()
    {
        var header = Header(StoneSound.Play(StoneSoundKind.Move).Span);

        Assert.Equal(("RIFF", "WAVE"), (header.Riff, header.Wave));
    }

    [Fact]
    public void Ход_Частота_Дискретизации_44100()
    {
        var header = Header(StoneSound.Play(StoneSoundKind.Move).Span);

        Assert.Equal(44100, header.SampleRate);
    }

    [Fact]
    public void Ход_Формат_Моно_Шестнадцать_Бит()
    {
        var header = Header(StoneSound.Play(StoneSoundKind.Move).Span);

        Assert.Equal(((short)1, (short)16, "fmt "), (header.Channels, header.BitsPerSample, header.Format));
    }

    [Fact]
    public void Ход_Блок_Данных_Называется_Data()
    {
        Assert.Equal("data", Header(StoneSound.Play(StoneSoundKind.Move).Span).Data);
    }

    [Fact]
    public void Ход_Длина_Данных_Совпадает_С_Размером_Файла()
    {
        var wav = StoneSound.Play(StoneSoundKind.Move).Span;

        Assert.Equal(wav.Length - 44, Header(wav).DataLength);
    }

    [Fact]
    public void Ход_Длительность_От_Девяноста_До_Ста_Сорока_Мс()
    {
        Assert.InRange(Milliseconds(StoneSound.Play(StoneSoundKind.Move).Span), 90d, 140d);
    }

    [Fact]
    public void Снятие_Длительность_От_Девяноста_До_Ста_Сорока_Мс()
    {
        Assert.InRange(Milliseconds(StoneSound.Play(StoneSoundKind.Capture).Span), 90d, 140d);
    }

    [Fact]
    public void Ход_Сигнал_Не_Пустой()
    {
        Assert.True(Peak(StoneSound.Play(StoneSoundKind.Move).Span) >= QuietPeak);
    }

    [Fact]
    public void Ход_Сигнал_Не_Клиппует()
    {
        Assert.True(Peak(StoneSound.Play(StoneSoundKind.Move).Span) < short.MaxValue);
    }

    [Fact]
    public void Снятие_Сигнал_Не_Клиппует()
    {
        Assert.True(Peak(StoneSound.Play(StoneSoundKind.Capture).Span) < short.MaxValue);
    }

    [Fact]
    public void Ход_Два_Вызова_Дают_Одинаковые_Байты()
    {
        var first = StoneSound.Play(StoneSoundKind.Move).ToArray();
        var second = StoneSound.Play(StoneSoundKind.Move).ToArray();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Снятие_Два_Вызова_Дают_Одинаковые_Байты()
    {
        var first = StoneSound.Play(StoneSoundKind.Capture).ToArray();
        var second = StoneSound.Play(StoneSoundKind.Capture).ToArray();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Снятие_Тембр_Отличается_От_Хода()
    {
        var move = StoneSound.Play(StoneSoundKind.Move).ToArray();
        var capture = StoneSound.Play(StoneSoundKind.Capture).ToArray();

        Assert.NotEqual(move, capture);
    }

    [Fact]
    public void Снятие_Тише_Хода()
    {
        Assert.True(
            Peak(StoneSound.Play(StoneSoundKind.Capture).Span) < Peak(StoneSound.Play(StoneSoundKind.Move).Span));
    }

    [Fact]
    public void Тишина_Готовый_Звук_Не_Бросает()
    {
        var player = new SilentSoundPlayer();

        var failure = Record.Exception(() => player.Play(StoneSound.Play(StoneSoundKind.Move)));

        Assert.Null(failure);
    }

    [Fact]
    public async Task Обвязка_Один_Ход_Звучит_Один_Раз()
    {
        var player = new RecordingSoundPlayer();
        var sound = new StoneSoundPlayer(player);

        await sound.OnMove("1", []);

        Assert.Equal(1, player.PlayCount);
    }

    [Fact]
    public async Task Обвязка_Повтор_Того_Же_Хода_Не_Звучит()
    {
        var player = new RecordingSoundPlayer();
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var sound = new StoneSoundPlayer(player, timeProvider: clock);

        await sound.OnMove("1", []);
        clock.Now = clock.Now.AddSeconds(1);
        await sound.OnMove("1", []);

        Assert.Equal(1, player.PlayCount);
    }

    [Fact]
    public async Task Обвязка_Выключенная_Анимация_Молчит()
    {
        var player = new RecordingSoundPlayer();
        var sound = new StoneSoundPlayer(player, animationsEnabled: () => false);

        await sound.OnMove("1", []);

        Assert.Equal(0, player.PlayCount);
    }

    [Fact]
    public async Task Обвязка_Два_Хода_В_Одну_Паузу_Звучат_Один_Раз()
    {
        var player = new RecordingSoundPlayer();
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var sound = new StoneSoundPlayer(player, timeProvider: clock);

        await sound.OnMove("1", []);
        clock.Now = clock.Now.AddMilliseconds(10);
        await sound.OnMove("2", []);

        Assert.Equal(1, player.PlayCount);
    }

    [Fact]
    public async Task Обвязка_Ход_После_Паузы_Звучит_Снова()
    {
        var player = new RecordingSoundPlayer();
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var sound = new StoneSoundPlayer(player, timeProvider: clock);

        await sound.OnMove("1", []);
        clock.Now = clock.Now.AddMilliseconds(100);
        await sound.OnMove("2", []);

        Assert.Equal(2, player.PlayCount);
    }

    /// <summary>Отмена хода возвращает прежний номер: звук при возврате — не повтор, а новое событие.</summary>
    [Fact]
    public async Task Обвязка_Возврат_К_Прежнему_Номеру_Звучит()
    {
        var player = new RecordingSoundPlayer();
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var sound = new StoneSoundPlayer(player, timeProvider: clock);

        await sound.OnMove("1", []);
        clock.Now = clock.Now.AddSeconds(1);
        await sound.OnMove("2", []);
        clock.Now = clock.Now.AddSeconds(1);
        await sound.OnMove("1", []);

        Assert.Equal(3, player.PlayCount);
    }

    [Fact]
    public async Task Обвязка_Снятие_Звучит_Тембром_Снятия()
    {
        var player = new RecordingSoundPlayer();
        var sound = new StoneSoundPlayer(player);

        await sound.OnMove("1", [new Point(4, 4)]);

        Assert.Equal(StoneSound.Play(StoneSoundKind.Capture).ToArray(), player.Last);
    }

    [Fact]
    public async Task Обвязка_Ход_Без_Снятия_Звучит_Тембром_Хода()
    {
        var player = new RecordingSoundPlayer();
        var sound = new StoneSoundPlayer(player);

        await sound.OnMove("1", []);

        Assert.Equal(StoneSound.Play(StoneSoundKind.Move).ToArray(), player.Last);
    }

    /// <summary>Разбирает заголовок WAV: только те поля, которые проверяются тестами.</summary>
    /// <param name="wav">Байты файла.</param>
    /// <returns>Поля заголовка.</returns>
    private static WavHeader Header(ReadOnlySpan<byte> wav) => new(
        Riff: Encoding.ASCII.GetString(wav[..4]),
        Wave: Encoding.ASCII.GetString(wav[8..12]),
        Format: Encoding.ASCII.GetString(wav[12..16]),
        Data: Encoding.ASCII.GetString(wav[36..40]),
        Channels: BinaryPrimitives.ReadInt16LittleEndian(wav[22..]),
        SampleRate: BinaryPrimitives.ReadInt32LittleEndian(wav[24..]),
        BitsPerSample: BinaryPrimitives.ReadInt16LittleEndian(wav[34..]),
        DataLength: BinaryPrimitives.ReadInt32LittleEndian(wav[40..]));

    /// <summary>Считает длительность звука по длине данных.</summary>
    /// <param name="wav">Байты файла.</param>
    /// <returns>Длительность в миллисекундах.</returns>
    /// <remarks>Длина данных — в байтах, а отсчёт занимает два байта (16 бит на канал).</remarks>
    private static double Milliseconds(ReadOnlySpan<byte> wav) =>
        Header(wav).DataLength / (double)(StoneSound.SampleRate * (StoneSound.BitsPerSample / 8)) * 1000;

    /// <summary>Находит наибольшую амплитуду сигнала: клиппинг и тишина видны по ней.</summary>
    /// <param name="wav">Байты файла.</param>
    /// <returns>Наибольший модуль отсчёта.</returns>
    private static int Peak(ReadOnlySpan<byte> wav)
    {
        var peak = 0;

        for (var offset = 44; offset < wav.Length - 1; offset += 2)
        {
            peak = Math.Max(peak, Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(wav[offset..])));
        }

        return peak;
    }

    /// <summary>Разобранный заголовок WAV.</summary>
    /// <param name="Riff">Сигнатура контейнера.</param>
    /// <param name="Wave">Признак формата WAVE.</param>
    /// <param name="Format">Имя блока формата.</param>
    /// <param name="Data">Имя блока данных.</param>
    /// <param name="Channels">Число каналов.</param>
    /// <param name="SampleRate">Частота дискретизации.</param>
    /// <param name="BitsPerSample">Разрядность отсчёта.</param>
    /// <param name="DataLength">Длина блока данных в байтах.</param>
    private sealed record WavHeader(
        string Riff,
        string Wave,
        string Format,
        string Data,
        short Channels,
        int SampleRate,
        short BitsPerSample,
        int DataLength);

    /// <summary>Подставной проигрыватель: запоминает, что и сколько раз просили сыграть.</summary>
    /// <remarks>Замок нужен потому, что обвязка играет в фоне, а проверки идут в тестовом потоке.</remarks>
    private sealed class RecordingSoundPlayer : ISoundPlayer
    {
        private readonly List<byte[]> _played = [];
        private readonly object _gate = new();

        /// <summary>Сколько раз просили сыграть.</summary>
        public int PlayCount
        {
            get
            {
                lock (_gate)
                {
                    return _played.Count;
                }
            }
        }

        /// <summary>Последний сыгранный звук или <c>null</c>, если звука не было.</summary>
        public byte[]? Last
        {
            get
            {
                lock (_gate)
                {
                    return _played.Count == 0 ? null : _played[^1];
                }
            }
        }

        /// <inheritdoc />
        public void Play(ReadOnlyMemory<byte> wav)
        {
            lock (_gate)
            {
                _played.Add(wav.ToArray());
            }
        }
    }
}
