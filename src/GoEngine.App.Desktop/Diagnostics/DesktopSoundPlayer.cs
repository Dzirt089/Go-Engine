using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GoEngine.App.Rendering;

namespace GoEngine.App.Diagnostics;

/// <summary>Звук ходов на настольных системах: winmm на Windows, внешний проигрыватель на Linux.</summary>
/// <remarks>
/// Способ воспроизведения у каждой системы свой, а общее только одно — байты WAV. Поэтому звук
/// сначала кладётся во временный файл (один файл на тембр, при первом звуке), а дальше каждая
/// система распоряжается им по-своему: Windows отдаёт путь <c>PlaySound</c>, Linux — внешнему
/// проигрывателю. Файл нужен обеим: <c>PlaySound</c> с флагом <c>SND_FILENAME</c> читает файл сам,
/// а <c>paplay</c> и <c>aplay</c> играют только файл. Вариант «играть прямо из памяти»
/// (<c>SND_MEMORY</c>) потребовал бы закрепления буфера в неуправляемой памяти и освобождения его
/// после конца звука, а выигрыша не даёт: ход — событие частое, но чтение ста килобайт из кэша
/// системы не стоит ничего.
/// macOS остаётся без звука: <c>AudioServicesPlaySystemSound</c> играет системные звуки по номеру
/// и не умеет играть произвольный WAV; чтобы отдать ему наш файл, нужно сначала создать системный
/// звук из файла (<c>AudioServicesCreateSystemSoundID</c>), а для этого — построить <c>CFURL</c>
/// средствами CoreFoundation, то есть тянуть ещё одну привязку ради одного стука; проверить её
/// всё равно негде — сборки для macOS у проекта нет. Тишина честнее неработающего кода.
/// Отказ на любом шаге (нет библиотеки, нет проигрывателя, нет прав на временный каталог) означает
/// тишину: так записано в контракте <see cref="ISoundPlayer"/>, и ронять игру звуком нельзя.
/// </remarks>
public sealed class DesktopSoundPlayer : ISoundPlayer
{
    /// <summary>Играть файл, а не ресурс и не память.</summary>
    private const uint SoundFileName = 0x0002_0000;

    /// <summary>Не ждать конца звука: вызов обязан возвращаться сразу.</summary>
    private const uint SoundAsync = 0x0001;

    /// <summary>Не подменять звук системным, если играть нечем.</summary>
    private const uint SoundNoDefault = 0x0002;

    /// <summary>Системная настройка «показывать анимацию в элементах управления».</summary>
    private const uint ClientAreaAnimation = 0x1042;

    /// <summary>Внешние проигрыватели Linux в порядке предпочтения.</summary>
    /// <remarks><c>paplay</c> — PulseAudio, <c>aplay</c> — ALSA, <c>pw-play</c> — PipeWire.</remarks>
    private static readonly string[] LinuxPlayers = ["paplay", "aplay", "pw-play"];

    /// <summary>Записанные во временный каталог файлы: имя файла — свёртка содержимого.</summary>
    private static readonly Dictionary<string, string> _temporary = [];

    /// <summary>Найденный проигрыватель Linux: искать его на каждом ходу незачем.</summary>
    private static string? _linuxPlayer;

    /// <summary>Замок: звуки приходят из фоновых задач и могут совпасть по времени.</summary>
    private static readonly object _gate = new();

    /// <summary>Создаёт проигрыватель для этой системы.</summary>
    /// <returns>Проигрыватель звука; <see cref="SilentSoundPlayer"/>, если система просит без анимаций.</returns>
    /// <remarks>
    /// Решение «играть или молчать» принимает голова, а не обвязка: о системной настройке знает
    /// только она. Windows хранит её в <c>SPI_GETCLIENTAREAANIMATION</c> — это галочка
    /// «Показывать анимацию в элементах управления» («Специальные возможности» → «Визуальные
    /// эффекты»). Если игрок её снял, анимация хода не показывается — и звук, который её
    /// сопровождает, тоже: звук здесь часть анимации, а не отдельное событие.
    /// </remarks>
    public static ISoundPlayer Create() =>
        AnimationsEnabled() ? new DesktopSoundPlayer() : new SilentSoundPlayer();

    /// <inheritdoc />
    public void Play(ReadOnlyMemory<byte> wav)
    {
        if (wav.IsEmpty)
        {
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                PlayOnWindows(wav);
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                PlayOnLinux(wav);
            }
        }
        catch (Exception)
        {
            // Молча: обещание ISoundPlayer — звук не может уронить игру. Сюда попадают отказ
            // P/Invoke, недоступный временный каталог и любой другой отказ системы; сообщать
            // о них игроку нечем и незачем.
        }
    }

    /// <summary>Показывает ли система анимации элементов управления.</summary>
    /// <returns><c>true</c>, если анимации показываются; при неизвестности — тоже <c>true</c>.</returns>
    private static bool AnimationsEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            // На Linux и macOS такой настройки у нас нет: считаем, что анимации показываются.
            return true;
        }

        try
        {
            return ReadClientAreaAnimation();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Отказ вызова — не повод остаться без звука: молчание игрок заметит, а лишний
            // стук — нет. Поэтому неизвестность трактуется как «анимации показываются».
            return true;
        }
    }

    /// <summary>Читает системную настройку «показывать анимацию».</summary>
    /// <returns><c>true</c>, если анимации включены.</returns>
    [SupportedOSPlatform("windows")]
    private static bool ReadClientAreaAnimation() =>
        SystemParametersInfo(ClientAreaAnimation, 0, out var enabled, 0) && enabled != 0;

    /// <summary>Читает системную настройку Windows.</summary>
    /// <param name="action">Что читать; здесь — <c>SPI_GETCLIENTAREAANIMATION</c>.</param>
    /// <param name="parameter">Параметр действия; при чтении этой настройки не используется.</param>
    /// <param name="value">Куда положить результат: для этой настройки это <c>BOOL</c>.</param>
    /// <param name="flags">Флаги обновления профиля; при чтении не используются.</param>
    /// <returns><c>true</c>, если вызов удался.</returns>
    /// <remarks>
    /// Ищем строго в системном каталоге: <c>user32</c> — системная библиотека, и подсунутая рядом
    /// с приложением копия не должна попасть в процесс.
    /// </remarks>
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, out int value, uint flags);

    /// <summary>Играет звук через winmm: файл читает сама система, вызов не ждёт конца.</summary>
    /// <param name="wav">Байты WAV.</param>
    [SupportedOSPlatform("windows")]
    private static void PlayOnWindows(ReadOnlyMemory<byte> wav)
    {
        // SND_ASYNC возвращает управление сразу, а SND_NODEFAULT запрещает подменять наш звук
        // системным «дзынь», если файл почему-то не прочитался: тишина честнее чужого звука.
        _ = PlaySound(TemporaryFile(wav), IntPtr.Zero, SoundFileName | SoundAsync | SoundNoDefault);
    }

    /// <summary>Проигрывает WAV средствами winmm.</summary>
    /// <param name="sound">Путь к файлу WAV.</param>
    /// <param name="module">Модуль ресурса; не используется.</param>
    /// <param name="flags">Флаги воспроизведения.</param>
    /// <returns>Ненулевое значение при успехе; результат не проверяется — при отказе будет тишина.</returns>
    /// <remarks>
    /// Точка входа задана явно: в winmm экспортируются <c>PlaySoundW</c> и <c>PlaySoundA</c>,
    /// а <c>PlaySound</c> — макрос. Взят Unicode-вариант: путь к временному файлу может содержать
    /// буквы за пределами ANSI (имя пользователя на русском), и <c>PlaySoundA</c> его не нашёл бы.
    /// </remarks>
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string sound, IntPtr module, uint flags);

    /// <summary>Играет звук внешним проигрывателем Linux.</summary>
    /// <param name="wav">Байты WAV.</param>
    [SupportedOSPlatform("linux")]
    private static void PlayOnLinux(ReadOnlyMemory<byte> wav)
    {
        var path = TemporaryFile(wav);

        lock (_gate)
        {
            if (_linuxPlayer is { } known && StartPlayer(known, path))
            {
                return;
            }

            // Проигрывателя может не быть вовсе: тогда молча пробуем следующий, а не сообщаем
            // об ошибке — на системе без звука игра всё равно работает.
            foreach (var candidate in LinuxPlayers)
            {
                if (StartPlayer(candidate, path))
                {
                    _linuxPlayer = candidate;
                    return;
                }
            }
        }
    }

    /// <summary>Запускает внешний проигрыватель.</summary>
    /// <param name="player">Имя программы: ищется в PATH.</param>
    /// <param name="path">Путь к файлу WAV.</param>
    /// <returns><c>true</c>, если программа запустилась.</returns>
    private static bool StartPlayer(string player, string path)
    {
        try
        {
            // Процесс не ждём и его вывод не читаем: проигрыватель закрывается сам, когда
            // доиграет, а открытая труба с выводом осталась бы висеть до конца партии.
            using var process = Process.Start(new ProcessStartInfo(player)
            {
                ArgumentList = { path },
                UseShellExecute = false
            });

            return process is not null;
        }
        catch (Win32Exception)
        {
            // Программы нет в системе — обычное дело на голой системе, а не сбой.
            return false;
        }
    }

    /// <summary>Кладёт WAV во временный файл и возвращает путь к нему.</summary>
    /// <param name="wav">Байты WAV.</param>
    /// <returns>Путь к файлу.</returns>
    /// <remarks>
    /// Файл пишется один раз на тембр: имя выводится из содержимого, поэтому следующий такой же
    /// звук ничего не пишет. Каталог лежит во временном каталоге пользователя и чистится системой.
    /// </remarks>
    private static string TemporaryFile(ReadOnlyMemory<byte> wav)
    {
        var name = ContentName(wav);

        lock (_gate)
        {
            if (_temporary.TryGetValue(name, out var known))
            {
                return known;
            }

            var directory = Path.Combine(Path.GetTempPath(), "GoEngine", "sound");
            _ = Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, name + ".wav");
            File.WriteAllBytes(path, wav.ToArray());
            _temporary[name] = path;

            return path;
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
