using Avalonia.Controls;
using Avalonia.Layout;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты приглашения обновления: карточка по центру экрана на затемнении.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-02: сообщение «вышло обновление» стояло полосой сверху и на телефоне
/// оказывалось строкой поверх доски — блок обязан быть по центру экрана. Слоёв два: затемнение
/// и карточка. Затемнение нажатия не ловит, иначе игрок остался бы заперт в приглашении
/// (во время установки кнопок на карточке нет вовсе), а «Позже» и «Отмена» обязаны работать,
/// как и раньше.
/// </remarks>
public sealed class UpdateBannerTests
{
    [Fact]
    public void Карточка_Приглашения_Стоит_По_Центру_Экрана()
    {
        var banner = new UpdateBanner();

        var card = Card(banner);

        Assert.Equal((HorizontalAlignment.Center, VerticalAlignment.Center), (card.HorizontalAlignment, card.VerticalAlignment));
    }

    [Fact]
    public void Карточка_Приглашения_Ограничена_По_Ширине()
    {
        var banner = new UpdateBanner();

        Assert.InRange(Card(banner).MaxWidth, 1, 900);
    }

    [Fact]
    public void Затемнение_Приглашения_Растянуто_На_Весь_Вид()
    {
        var banner = new UpdateBanner();

        var scrim = Scrim(banner);

        Assert.Equal((HorizontalAlignment.Stretch, VerticalAlignment.Stretch), (scrim.HorizontalAlignment, scrim.VerticalAlignment));
    }

    [Fact]
    public void Затемнение_Приглашения_Не_Ловит_Нажатия()
    {
        // Затемнение — отдельный слой: признак стоит на нём, а не на общем слое с карточкой,
        // иначе он унаследовался бы кнопками и «Позже» перестала бы работать.
        var banner = new UpdateBanner();

        Assert.False(Scrim(banner).IsHitTestVisible);
    }

    [Fact]
    public void Затемнение_Приглашения_Взято_Из_Темы()
    {
        var banner = new UpdateBanner();

        Assert.Contains("app-overlay", Scrim(banner).Classes);
    }

    [Fact]
    public void Кнопки_Приглашения_Называют_Свои_Действия()
    {
        var banner = new UpdateBanner();

        Assert.Equal(
            ("Отмена", "Позже", "Обновить сейчас"),
            (ActionButton(banner, "CancelButton").Content, ActionButton(banner, "LaterButton").Content, ActionButton(banner, "UpdateNowButton").Content));
    }

    /// <summary>Карточка приглашения.</summary>
    /// <param name="banner">Приглашение с разметкой.</param>
    /// <returns>Рамка карточки.</returns>
    private static Border Card(UpdateBanner banner) => banner.FindControl<Border>("BannerCard")!;

    /// <summary>Затемнение под карточкой.</summary>
    /// <param name="banner">Приглашение с разметкой.</param>
    /// <returns>Рамка затемнения.</returns>
    private static Border Scrim(UpdateBanner banner) => banner.FindControl<Border>("Scrim")!;

    /// <summary>Кнопка приглашения по имени в разметке.</summary>
    /// <param name="banner">Приглашение с разметкой.</param>
    /// <param name="name">Имя кнопки.</param>
    /// <returns>Кнопка разметки.</returns>
    private static Button ActionButton(UpdateBanner banner, string name) => banner.FindControl<Button>(name)!;
}
