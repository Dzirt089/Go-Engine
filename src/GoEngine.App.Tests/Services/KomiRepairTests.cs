using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты исправления пары «доска + коми», оставшейся от прежних версий (D-051, D-054).</summary>
/// <remarks>
/// До D-051 экран настроек не пересчитывал коми при смене размера доски, поэтому в файл настроек
/// могла попасть пара «9×9 + 7.5». Для 9×9 и 13×13 коми 7.5 правилами не предусмотрено, такая пара
/// распознаётся и заменяется правилом. Осознанно выставленные значения не трогаются.
/// </remarks>
public sealed class KomiRepairTests
{
    [Theory]
    [InlineData(9, 7.5, 5.5)]
    [InlineData(13, 7.5, 5.5)]
    [InlineData(9, 5.5, 5.5)]
    [InlineData(13, 5.5, 5.5)]
    [InlineData(9, 6.5, 6.5)]
    [InlineData(13, 0.5, 0.5)]
    [InlineData(19, 7.5, 7.5)]
    [InlineData(19, 5.5, 5.5)]
    public void Загрузка_Исправляет_Коми_От_Чужой_Доски(int size, double stored, double expected)
    {
        var path = Path.GetTempFileName();

        try
        {
            File.WriteAllText(
                path,
                $"{{ \"boardSize\": {size}, \"aiRankKyu\": 1, \"playerColor\": \"Black\", \"komi\": {stored.ToString(System.Globalization.CultureInfo.InvariantCulture)} }}");

            var settings = SettingsStore.Load(path);

            Assert.Equal(expected, settings.ToKomi().Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Исправление_Не_Трогает_Файл()
    {
        var path = Path.GetTempFileName();
        var json = "{ \"boardSize\": 9, \"aiRankKyu\": 1, \"playerColor\": \"Black\", \"komi\": 7.5 }";

        try
        {
            File.WriteAllText(path, json);

            _ = SettingsStore.Load(path);

            // Файл — данные игрока: приложение правит только то, что показывает, а не чужой файл.
            Assert.Equal(json, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
