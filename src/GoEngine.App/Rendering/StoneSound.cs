using System.Buffers.Binary;

namespace GoEngine.App.Rendering;

/// <summary>Какой из двух звуков хода играть.</summary>
/// <remarks>
/// Тембров два, а не один: снятие камней слышно иначе — оно ниже и мягче, и по звуку понятно,
/// что ход забрал камни, ещё не глядя на доску.
/// </remarks>
public enum StoneSoundKind
{
    /// <summary>Ход: камень поставлен на доску.</summary>
    Move,

    /// <summary>Снятие: ход снял камни соперника.</summary>
    Capture
}

/// <summary>Синтез звука хода: короткий стук камня о дерево.</summary>
/// <remarks>
/// Звук считается, а не берётся из файла. Причин три: проект offline-first и не тащит лишних
/// зависимостей; встроенный WAV пришлось бы держать в каждой голове (настольной и Android) и
/// отдельно следить за его лицензией; синтез не делает ввода-вывода и даёт одни и те же байты
/// на всех системах, поэтому звук проверяется тестами без звуковой карты — по заголовку,
/// длительности и форме сигнала.
/// Отсюда же требование детерминированности: в синтезе нет ни времени, ни <c>Random</c> без seed,
/// поэтому два вызова дают одинаковые байты, а подстановка звука в тестах не «плывёт».
/// Звук намеренно тихий и с плавной атакой: это сопровождение хода, а не удар в ухо.
/// </remarks>
public static class StoneSound
{
    /// <summary>Частота дискретизации всех звуков.</summary>
    public const int SampleRate = 44100;

    /// <summary>Число каналов: звук моно, делить стерео здесь нечего.</summary>
    public const int Channels = 1;

    /// <summary>Разрядность одного отсчёта в битах.</summary>
    public const int BitsPerSample = 16;

    /// <summary>Длина заголовка WAV: всё, что идёт до отсчётов.</summary>
    private const int HeaderLength = 44;

    /// <summary>Длительность стука хода в секундах: короткий, но не щелчок.</summary>
    private const double MoveSeconds = 0.12;

    /// <summary>Длительность снятия: чуть длиннее, потому что тембр ниже и гаснет медленнее.</summary>
    private const double CaptureSeconds = 0.13;

    /// <summary>Громкость хода: доля от полной шкалы.</summary>
    private const double MoveLevel = 0.34;

    /// <summary>Громкость снятия: тише хода, снятие — событие помягче.</summary>
    private const double CaptureLevel = 0.26;

    /// <summary>Сколько последних секунд буфера гасятся в ноль.</summary>
    private const double TailSeconds = 0.01;

    /// <summary>Что гаснет в <c>e</c> раз за долю длительности: общая огибающая звука.</summary>
    private const double OverallDecayShare = 0.45;

    /// <summary>Стук хода: считается один раз за запуск приложения.</summary>
    private static readonly ReadOnlyMemory<byte> _move = Synthesize(
        seconds: MoveSeconds,
        level: MoveLevel,
        seed: 0x51A7_0001u,
        partials:
        [
            new Partial(Frequency: 520, Gain: 1.0, DecaySeconds: 0.028),
            new Partial(Frequency: 1040, Gain: 0.35, DecaySeconds: 0.018),
            new Partial(Frequency: 1560, Gain: 0.15, DecaySeconds: 0.011)
        ],
        noiseGain: 0.35,
        noiseDecay: 0.006,
        attackSeconds: 0.0015);

    /// <summary>Снятие: ниже по тону, мягче по атаке и с более длинным хвостом.</summary>
    private static readonly ReadOnlyMemory<byte> _capture = Synthesize(
        seconds: CaptureSeconds,
        level: CaptureLevel,
        seed: 0x51A7_0002u,
        partials:
        [
            new Partial(Frequency: 330, Gain: 1.0, DecaySeconds: 0.042),
            new Partial(Frequency: 660, Gain: 0.30, DecaySeconds: 0.026),
            new Partial(Frequency: 990, Gain: 0.12, DecaySeconds: 0.015)
        ],
        noiseGain: 0.22,
        noiseDecay: 0.010,
        attackSeconds: 0.003);

    /// <summary>Отдаёт готовый WAV нужного тембра.</summary>
    /// <param name="kind">Что звучит: ход или снятие.</param>
    /// <returns>Байты файла WAV: PCM 16 бит, моно, 44100 Гц.</returns>
    /// <remarks>
    /// Байты кэшированы: синтез считается один раз за запуск, а проигрывателю отдаётся одна и та же
    /// память — копии на каждый ход не делаются. Проигрыватели распоряжаются ею по-своему
    /// (настольные пишут её во временный файл при первом звуке, Android отдаёт <c>SoundPool</c>),
    /// поэтому память обязана оставаться годной всё время работы приложения.
    /// </remarks>
    public static ReadOnlyMemory<byte> Play(StoneSoundKind kind) => kind switch
    {
        StoneSoundKind.Capture => _capture,
        _ => _move
    };

    /// <summary>Считает WAV заданного тембра.</summary>
    /// <param name="seconds">Длительность звука.</param>
    /// <param name="level">Громкость: доля от полной шкалы.</param>
    /// <param name="seed">Начальное состояние генератора шума: своё у каждого тембра.</param>
    /// <param name="partials">Составляющие тона.</param>
    /// <param name="noiseGain">Громкость шумовой части удара.</param>
    /// <param name="noiseDecay">Время затухания шумовой части.</param>
    /// <param name="attackSeconds">Длительность атаки: без неё начало звука щёлкает.</param>
    /// <returns>Байты WAV с заголовком и отсчётами.</returns>
    private static byte[] Synthesize(
        double seconds,
        double level,
        uint seed,
        Partial[] partials,
        double noiseGain,
        double noiseDecay,
        double attackSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds);
        ArgumentNullException.ThrowIfNull(partials);

        var count = (int)Math.Round(seconds * SampleRate);
        var samples = new double[count];
        var noise = seed;
        var smoothed = 0.0;
        var tail = Math.Max(1, (int)(TailSeconds * SampleRate));
        var decay = seconds * OverallDecayShare;

        for (var index = 0; index < count; index++)
        {
            var time = (double)index / SampleRate;

            // Шум сглаживается фильтром нижних частот: чистый белый шум слышен как шипение,
            // а удар камня — глухой; глухость и даёт этот фильтр.
            noise = NextNoise(noise);
            var white = ((noise / (double)uint.MaxValue) * 2) - 1;
            smoothed += 0.22 * (white - smoothed);

            var value = noiseGain * Math.Exp(-time / noiseDecay) * smoothed;

            foreach (var partial in partials)
            {
                value += partial.Gain
                    * Math.Exp(-time / partial.DecaySeconds)
                    * Math.Sin(2 * Math.PI * partial.Frequency * time);
            }

            value *= Attack(time, attackSeconds) * Math.Exp(-time / decay);

            // Хвост гасится в ноль: буфер, оборванный на середине волны, слышен щелчком.
            var remaining = count - index;

            if (remaining < tail)
            {
                value *= (double)remaining / tail;
            }

            samples[index] = value;
        }

        return ToWav(samples, level);
    }

    /// <summary>Огибающая атаки: плавный вход вместо мгновенного скачка.</summary>
    /// <param name="time">Время от начала звука.</param>
    /// <param name="attackSeconds">Длительность атаки.</param>
    /// <returns>Множитель от 0 до 1.</returns>
    private static double Attack(double time, double attackSeconds) =>
        time >= attackSeconds ? 1 : time / attackSeconds;

    /// <summary>Следующее значение шума.</summary>
    /// <param name="state">Состояние генератора.</param>
    /// <returns>Новое состояние.</returns>
    /// <remarks>
    /// xorshift32 вместо <c>Random</c>: он детерминирован по определению, не зависит от версии
    /// платформы и не тянет в синтез ничего лишнего. Начальное состояние задано константой тембра.
    /// </remarks>
    private static uint NextNoise(uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;

        return state;
    }

    /// <summary>Собирает WAV: заголовок и отсчёты PCM 16 бит.</summary>
    /// <param name="samples">Отсчёты в диапазоне примерно от −1 до 1.</param>
    /// <param name="level">Громкость: доля от полной шкалы.</param>
    /// <returns>Байты файла WAV.</returns>
    private static byte[] ToWav(double[] samples, double level)
    {
        var peak = 0.0;

        foreach (var sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        // Приводим к заданной громкости, а не к полной шкале: звук сопровождения должен быть тихим,
        // и клиппинга при этом не возникает по построению.
        var scale = peak > 0 ? level / peak : 0;
        var dataLength = samples.Length * (BitsPerSample / 8);
        var wav = new byte[HeaderLength + dataLength];
        var span = wav.AsSpan();

        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataLength);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], Channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], SampleRate * Channels * (BitsPerSample / 8));
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], Channels * (BitsPerSample / 8));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], BitsPerSample);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataLength);

        for (var index = 0; index < samples.Length; index++)
        {
            var value = (short)Math.Clamp(
                Math.Round(samples[index] * scale * short.MaxValue),
                short.MinValue,
                short.MaxValue);

            BinaryPrimitives.WriteInt16LittleEndian(span[(HeaderLength + (index * 2))..], value);
        }

        return wav;
    }

    /// <summary>Одна составляющая тембра: частота, громкость и время затухания.</summary>
    /// <param name="Frequency">Частота в герцах.</param>
    /// <param name="Gain">Громкость относительно основной составляющей.</param>
    /// <param name="DecaySeconds">Время, за которое составляющая гаснет в <c>e</c> раз.</param>
    private readonly record struct Partial(double Frequency, double Gain, double DecaySeconds);
}
