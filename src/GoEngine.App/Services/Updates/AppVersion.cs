using System.Globalization;
using System.Reflection;
using GoEngine.Core;

namespace GoEngine.App.Services.Updates;

/// <summary>Версия приложения или выпуска: <c>major.minor.patch</c>.</summary>
/// <remarks>
/// Версия выпуска приходит из манифеста обновления, версия приложения — из сборки головы
/// (<c>AssemblyInformationalVersion</c>). Локальная сборка без номера считается версией
/// <see cref="Unknown"/>: она старее любого выпуска, поэтому обновление ей предложат.
/// </remarks>
public readonly record struct AppVersion(int Major, int Minor, int Patch) : IComparable<AppVersion>
{
    /// <summary>Версия неизвестна: сборка без номера.</summary>
    public static AppVersion Unknown => new(0, 0, 0);

    /// <summary>Ключ метки сборки выпуска в атрибутах сборки.</summary>
    private const string OfficialBuildKey = "OfficialBuild";

    /// <summary>Строка версии из сборки головы — как есть, вместе с хвостом коммита.</summary>
    /// <remarks>Нужна для диагностики: игроку показывается <see cref="Display"/>.</remarks>
    public static string InformationalVersion { get; } = ReadInformationalVersion();

    /// <summary>Версия запущенного приложения.</summary>
    public static AppVersion Current { get; } = ParseOrUnknown(InformationalVersion);

    /// <summary>Сборка выпуска: её собрал CI и пометил меткой <c>OfficialBuild</c>.</summary>
    /// <remarks>
    /// Признак нужен потому, что SDK добавляет к версии хвост коммита и локальной сборке
    /// (<c>1.0.0+707b3cc…</c>), поэтому «заводскую» версию по строке от версии выпуска не отличить.
    /// Метку ставит только сборка CI (<c>-p:OfficialBuild=true</c>).
    /// </remarks>
    public static bool IsOfficialBuild { get; } = HasOfficialMarker();

    /// <summary>Локальная сборка: её собрали не для выпуска.</summary>
    public static bool IsLocalBuild => !IsOfficialBuild;

    /// <summary>Что показывать игроку: номер выпуска или номер с пометкой локальной сборки.</summary>
    public static string Display { get; } = DisplayFor(InformationalVersion, IsOfficialBuild);

    /// <summary>Разбирает версию из строки.</summary>
    /// <param name="text">Строка вида «0.2.15», «v0.2.15» или «0.2.15+коммит».</param>
    /// <returns>Версия или причина отказа.</returns>
    /// <remarks>
    /// Части после третьей отбрасываются: номер сборки к сравнению версий не относится.
    /// Пропущенные части считаются нулями, поэтому «1.2» — это «1.2.0».
    /// </remarks>
    public static Result<AppVersion> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<AppVersion>.Fail("Версия не указана.");
        }

        var value = text.Trim();
        var plus = value.IndexOf('+', StringComparison.Ordinal);
        var minus = value.IndexOf('-', StringComparison.Ordinal);

        if (plus >= 0)
        {
            value = value[..plus];
        }

        if (minus >= 0)
        {
            value = value[..minus];
        }

        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var parts = value.Split('.', StringSplitOptions.TrimEntries);
        var numbers = new int[3];

        for (var index = 0; index < parts.Length && index < numbers.Length; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return Result<AppVersion>.Fail($"Версия «{text}» не разбирается.");
            }

            numbers[index] = number;
        }

        return Result<AppVersion>.Ok(new AppVersion(numbers[0], numbers[1], numbers[2]));
    }

    /// <summary>Разбирает версию или возвращает <see cref="Unknown"/>.</summary>
    /// <param name="text">Строка версии.</param>
    /// <returns>Версия или <see cref="Unknown"/>, если строка не разбирается.</returns>
    public static AppVersion ParseOrUnknown(string? text)
    {
        var parsed = Parse(text);

        return parsed.IsSuccess ? parsed.Value : Unknown;
    }

    /// <inheritdoc />
    public int CompareTo(AppVersion other)
    {
        var major = Major.CompareTo(other.Major);

        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);

        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    /// <summary>Сравнивает версии операторами отношения.</summary>
    /// <param name="left">Левая версия.</param>
    /// <param name="right">Правая версия.</param>
    /// <returns><c>true</c>, если левая версия новее.</returns>
    public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;

    /// <summary>Сравнивает версии операторами отношения.</summary>
    /// <param name="left">Левая версия.</param>
    /// <param name="right">Правая версия.</param>
    /// <returns><c>true</c>, если левая версия старее.</returns>
    public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;

    /// <summary>Сравнивает версии операторами отношения.</summary>
    /// <param name="left">Левая версия.</param>
    /// <param name="right">Правая версия.</param>
    /// <returns><c>true</c>, если версии равны.</returns>
    public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

    /// <summary>Сравнивает версии операторами отношения.</summary>
    /// <param name="left">Левая версия.</param>
    /// <param name="right">Правая версия.</param>
    /// <returns><c>true</c>, если левая версия не новее.</returns>
    public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    /// <summary>Собирает подпись версии для экрана настроек.</summary>
    /// <param name="informational">Строка версии из сборки.</param>
    /// <param name="official">Сборка выпуска: её собрал CI.</param>
    /// <returns>Например, «0.2.30» или «1.0.0 (локальная сборка)».</returns>
    /// <remarks>Хвост коммита в подпись не попадает: он нужен для диагностики, а не игроку.</remarks>
    public static string DisplayFor(string? informational, bool official)
    {
        var version = ParseOrUnknown(informational).ToString();

        return official
            ? version
            : string.Create(CultureInfo.InvariantCulture, $"{version} (локальная сборка)");
    }

    /// <summary>Читает строку версии из сборки головы.</summary>
    /// <returns>Строка версии или пустая строка, если атрибута нет.</returns>
    private static string ReadInformationalVersion() => EntryAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

    /// <summary>Проверяет метку сборки выпуска.</summary>
    /// <returns><c>true</c>, если сборку пометил CI.</returns>
    private static bool HasOfficialMarker() => EntryAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Any(static attribute => attribute.Key == OfficialBuildKey && attribute.Value == "true");

    /// <summary>Сборка, из которой запущено приложение.</summary>
    /// <returns>Голова приложения или текущая сборка, если головы нет (тесты).</returns>
    private static Assembly EntryAssembly() => Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
}
