using Avalonia.Controls;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты экрана «О программе»: версия, модели, логи и обновление.</summary>
/// <remarks>
/// Раньше это был третий подраздел настроек; теперь сведения о программе живут отдельным экраном:
/// на телефоне его показывает оболочка вложенным экраном раздела «Меню», в настольном окне —
/// диалог <see cref="AboutWindow"/> (замечание пользователя 2026-10-06, <c>DECISIONS.md</c>, D-075).
/// Проверки идут по разметке: окно в этой среде не открывается (<c>AGENTS.md</c>, п. 14).
/// </remarks>
public sealed class AboutViewTests
{
    /// <summary>Поля экрана: версия, модели, логи и обновление.</summary>
    private static readonly string[] Controls =
        ["VersionText", "ModelsText", "LogsText", "CrashText", "OpenLogsButton", "CheckUpdatesButton"];

    [Fact]
    public void Экран_Содержит_Версию_Модели_Логи_И_Обновление()
    {
        var view = new AboutView();

        Assert.All(
            Controls,
            name => Assert.True(view.FindControl<Control>(name) is not null, name));
    }

    [Fact]
    public void Версия_Показана_Игроку()
    {
        var view = new AboutView();

        Assert.StartsWith("Версия: ", view.FindControl<TextBlock>("VersionText")!.Text ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Состояние_Моделей_Заполнено()
    {
        // Пустой строки быть не должно: без неё отказ сети виден только по отсутствию уровней
        // с нейросетью, и причину на телефоне узнать нечем.
        var view = new AboutView();

        Assert.False(string.IsNullOrWhiteSpace(view.FindControl<TextBlock>("ModelsText")!.Text));
    }

    [Fact]
    public void Строка_Логов_Заполнена()
    {
        var view = new AboutView();

        Assert.StartsWith("Логи: ", view.FindControl<TextBlock>("LogsText")!.Text ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Обновление_Показано_Той_Же_Моделью_Что_И_Приглашение()
    {
        // Состояние берётся у общей модели приложения: иначе проценты на этом экране и в приглашении
        // считались бы по-разному.
        var view = new AboutView();

        Assert.Equal(
            global::GoEngine.App.App.Updates.StatusText,
            view.FindControl<TextBlock>("UpdateStatusText")!.Text);
    }

    [Fact]
    public void Кнопки_Обновления_Есть()
    {
        var view = new AboutView();

        Assert.Equal("Проверить обновления", view.FindControl<Button>("CheckUpdatesButton")!.Content);
        Assert.Equal("Скачать и установить", view.FindControl<Button>("InstallUpdateButton")!.Content);
        Assert.Equal("Отмена", view.FindControl<Button>("CancelUpdateButton")!.Content);
    }

    [Fact]
    public void Кнопок_Начать_Партию_И_Сохранить_Здесь_Нет()
    {
        // Экран ничего не настраивает и не начинает: это сведения, а не настройка партии.
        var view = new AboutView();

        Assert.Null(view.FindControl<Button>("SaveButton"));
        Assert.Null(view.FindControl<Button>("CancelButton"));
    }
}
