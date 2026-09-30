using System.Globalization;
using Android.Content;
using Android.Media;
using GoEngine.App.Rendering;

namespace GoEngine.App.Android;

/// <summary>Звук ходов на Android: <c>SoundPool</c> играет загруженный WAV.</summary>
/// <remarks>
/// <c>SoundPool</c> — единственный звуковой API Android, пригодный для коротких частых звуков:
/// он держит готовый образец в памяти и играет его без задержки, тогда как <c>MediaPlayer</c>
/// создаётся под одну длинную запись и на каждый ход не годится.
/// Играть из памяти <c>SoundPool</c> не умеет: образец берётся из ресурса, файла или дескриптора,
/// поэтому байты WAV кладутся в кэш приложения — он всегда доступен на запись и чистится
/// системой. Файл пишется один раз на тембр: имя выводится из содержимого.
/// Сбои (нет места, отказ звуковой подсистемы) молча пропускаются — так записано в контракте
/// <see cref="ISoundPlayer"/>: звук не может уронить игру.
/// </remarks>
public sealed class AndroidSoundPlayer : ISoundPlayer
{
    /// <summary>Проигрыватель Android: создаётся один раз на всё приложение.</summary>
    private readonly SoundPool _pool;

    /// <summary>Каталог кэша приложения: <c>null</c> — система не дала каталог, звука не будет.</summary>
    private readonly string? _cacheDirectory;

    /// <summary>Загруженные образцы: имя файла — свёртка содержимого, значение — идентификатор звука.</summary>
    private readonly Dictionary<string, int> _sounds = [];

    /// <summary>Замок: звуки приходят из фоновых задач и могут совпасть по времени.</summary>
    private readonly object _gate = new();

    /// <summary>Создаёт проигрыватель для приложения.</summary>
    /// <param name="context">Контекст приложения: из него берётся каталог кэша.</param>
    public AndroidSoundPlayer(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _cacheDirectory = context.CacheDir?.AbsolutePath;

        // Атрибуты обязательны начиная с API 21: без них звук попадает в общий поток «системных»
        // и телефон вправе его приглушить или проиграть не той громкостью.
        var attributes = new AudioAttributes.Builder()!
            .SetUsage(AudioUsageKind.Game)!
            .SetContentType(AudioContentType.Sonification)!
            .Build()!;

        // Один поток: одновременно два стука не нужны, а новый звук обрывает предыдущий — ровно
        // то, что нужно при быстрых ходах.
        _pool = new SoundPool.Builder()!
            .SetMaxStreams(1)!
            .SetAudioAttributes(attributes)!
            .Build()!;
    }

    /// <inheritdoc />
    public void Play(ReadOnlyMemory<byte> wav)
    {
        if (wav.IsEmpty || string.IsNullOrWhiteSpace(_cacheDirectory))
        {
            return;
        }

        try
        {
            // Громкость 1 — «как записано»: сам звук синтезирован тихим, отдельной ручки
            // громкости в приложении нет.
            _ = _pool.Play(Load(wav), 1f, 1f, 0, 0, 1f);
        }
        catch (Exception)
        {
            // Молча и осознанно (AGENTS.md, п. 11 требует обоснования): обещание ISoundPlayer —
            // звук не может уронить игру. Сюда попадают отказ звуковой подсистемы и недоступный
            // кэш; сообщать о них игроку нечем, а писать в лог из звука нельзя: лог — это файл,
            // и его отказ снова приведёт сюда. Тихо пропущенный звук честнее упавшего хода.
        }
    }

    /// <summary>Загружает звук в <c>SoundPool</c> и возвращает его идентификатор.</summary>
    /// <param name="wav">Байты WAV.</param>
    /// <returns>Идентификатор звука в <c>SoundPool</c>.</returns>
    /// <remarks>
    /// Загрузка идёт фоном: первый такой же звук может не успеть сыграть, зато второй и все
    /// следующие звучат сразу. Ждать загрузку на каждом ходу нельзя — это задержка хода.
    /// </remarks>
    private int Load(ReadOnlyMemory<byte> wav)
    {
        var name = ContentName(wav);

        lock (_gate)
        {
            if (_sounds.TryGetValue(name, out var known))
            {
                return known;
            }

            var path = Path.Combine(_cacheDirectory!, name + ".wav");
            File.WriteAllBytes(path, wav.ToArray());

            var sound = _pool.Load(path, 1);
            _sounds[name] = sound;

            return sound;
        }
    }

    /// <summary>Свёртка содержимого: одинаковый WAV — одно и то же имя файла.</summary>
    /// <param name="wav">Байты WAV.</param>
    /// <returns>Шестнадцать шестнадцатеричных цифр (FNV-1a).</returns>
    private static string ContentName(ReadOnlyMemory<byte> wav)
    {
        var hash = 0xcbf2_9ce4_8422_2325UL;

        foreach (var value in wav.Span)
        {
            hash = (hash ^ value) * 0x0000_0100_0000_01b3UL;
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }
}
