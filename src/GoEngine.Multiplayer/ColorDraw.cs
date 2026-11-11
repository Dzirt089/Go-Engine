using System.Security.Cryptography;
using System.Text;
using GoEngine.Core;

namespace GoEngine.Multiplayer;

/// <summary>Правила нигири: хозяин прячет камни, гость угадывает чёт/нечет, победитель выбирает цвет.</summary>
/// <remarks>
/// <para>
/// Commit-reveal: хозяин выбирает количество камней 1…9 и случайную соль, а гостю присылает
/// только хеш @@{count}|{nonce}@@. Гость отвечает «чёт/нечет» до раскрытия, поэтому не может
/// подстроить ответ под результат, а хозяин — подменить число после ответа (раскрытие
/// проверяется хешем). Победитель — угадавший; он и выбирает свой цвет.
/// </para>
/// <para>
/// Класс чистый и без сети: детерминируется переданным @@Random@@, поэтому проверяется
/// без TLS и без таймеров.
/// </para>
/// </remarks>
public static class ColorDraw
{
    /// <summary>Минимальное число камней в горсти.</summary>
    public const int MinCount = 1;

    /// <summary>Максимальное число камней в горсти.</summary>
    public const int MaxCount = 9;

    /// <summary>Ответ гостя «нечет».</summary>
    public const string GuessOdd = "odd";

    /// <summary>Ответ гостя «чёт».</summary>
    public const string GuessEven = "even";

    /// <summary>Длина соли в байтах.</summary>
    private const int NonceBytes = 16;

    /// <summary>Сколько камней хозяин прячет в горсти.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным зерном.</param>
    /// <returns>Число камней от 1 до 9.</returns>
    public static int CreateCount(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return random.Next(MinCount, MaxCount + 1);
    }

    /// <summary>Случайная соль раскрытия.</summary>
    /// <param name="random">Источник случайности.</param>
    /// <returns>32 шестнадцатеричных символа.</returns>
    public static string CreateNonce(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var bytes = new byte[NonceBytes];
        random.NextBytes(bytes);

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Обязательство хозяина: хеш числа камней и соли.</summary>
    /// <param name="count">Число камней.</param>
    /// <param name="nonce">Соль.</param>
    /// <returns>SHA-256 в нижнем регистре hex.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Число камней вне 1…9.</exception>
    public static string Commitment(int count, string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);

        if (count is < MinCount or > MaxCount)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Число камней должно быть от 1 до 9.");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{count}|{nonce}"));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Проверяет раскрытие: число и соль дают присланное обязательство.</summary>
    /// <param name="count">Раскрытое число камней.</param>
    /// <param name="nonce">Раскрытая соль.</param>
    /// <param name="commitment">Обязательство из начального сообщения.</param>
    /// <returns><c>true</c>, если раскрытие совпадает.</returns>
    public static bool Verify(int count, string nonce, string commitment)
    {
        if (count is < MinCount or > MaxCount || string.IsNullOrWhiteSpace(nonce))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(commitment))
        {
            return false;
        }

        var actual = Commitment(count, nonce);

        return string.Equals(actual, commitment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ответ гостя по числу камней: чёт или нечет.</summary>
    /// <param name="count">Число камней.</param>
    /// <returns><see cref="GuessOdd"/> или <see cref="GuessEven"/>.</returns>
    public static string ParityOf(int count) => count % 2 == 1 ? GuessOdd : GuessEven;

    /// <summary>Ответ гостя, был ли он прав.</summary>
    /// <param name="count">Раскрытое число камней.</param>
    /// <param name="guess">Ответ гостя: <see cref="GuessOdd"/> или <see cref="GuessEven"/>.</param>
    /// <returns><c>true</c>, если ответ совпал с чётностью горсти.</returns>
    public static bool IsGuessCorrect(int count, string guess)
    {
        var odd = string.Equals(guess, GuessOdd, StringComparison.Ordinal);

        return (count % 2 == 1) == odd;
    }

    /// <summary>Слово «камень» в нужной форме для числа камней.</summary>
    /// <param name="count">Число камней 1…9.</param>
    /// <returns>«камень», «камня» или «камней».</returns>
    public static string StonesWord(int count) => count switch
    {
        1 => "камень",
        >= 2 and <= 4 => "камня",
        _ => "камней"
    };

    /// <summary>Противоположный цвет.</summary>
    /// <param name="color">Цвет.</param>
    /// <returns>Другой цвет: у чёрных — белый, у белых — чёрный.</returns>
    public static StoneColor Opposite(StoneColor color) =>
        color == StoneColor.Black ? StoneColor.White : StoneColor.Black;
}
