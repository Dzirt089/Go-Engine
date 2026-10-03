using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Views;

/// <summary>Окно настроек партии: общий вид настроек в настольном окне.</summary>
/// <remarks>
/// Вся логика выбора — в <see cref="SettingsView"/>; здесь только оконная обвязка: показать
/// и закрыться с результатом. Так один и тот же код работает и в окне, и на телефоне.
/// </remarks>
public sealed partial class SettingsWindow : Window
{
    private readonly SettingsView _view;

    /// <summary>Согласие уже собрано: повторное закрытие проходит без отмены.</summary>
    private bool _accepted;

    /// <summary>Создаёт окно настроек со значениями по умолчанию.</summary>
    public SettingsWindow() : this(AppSettings.Default)
    {
    }

    /// <summary>Создаёт окно настроек с текущими значениями.</summary>
    /// <param name="current">Текущие настройки партии.</param>
    /// <param name="modelAvailable">
    /// Ответ на вопрос «есть ли модель для доски»; <c>null</c> — вид берёт статические данные.
    /// </param>
    public SettingsWindow(AppSettings current, Func<BoardSize, bool>? modelAvailable = null)
    {
        ArgumentNullException.ThrowIfNull(current);

        AvaloniaXamlLoader.Load(this);

        _view = this.FindControl<SettingsView>("SettingsArea")
            ?? throw new DomainException("В окне настроек нет вида настроек: разметка повреждена.");

        _view.Initialize(current, modelAvailable);
        _view.Accepted += (_, _) => Close(true);

        // Кнопок «Отмена» и «Начать партию» в настройках больше нет: окно закрывается крестиком,
        // и закрытие означает согласие — как раньше означала кнопка «Начать партию»
        // (жалоба пользователя 2026-10-03). Сначала собираются значения, потом окно закрывается.
        Closing += OnClosing;
    }

    /// <summary>Собирает выбранные настройки перед закрытием окна.</summary>
    /// <param name="sender">Окно.</param>
    /// <param name="e">Признак закрытия: первый раз отменяется, чтобы значения успели собраться.</param>
    /// <remarks>
    /// <c>Accept</c> поднимает <see cref="SettingsView.Accepted"/>, и обработчик закрывает окно
    /// повторно — уже с результатом. Без этой развилки закрытие крестиком вернуло бы отказ,
    /// и настройки игрока пропали бы.
    /// </remarks>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_accepted)
        {
            return;
        }

        _accepted = true;
        e.Cancel = true;
        _view.Accept();
    }

    /// <summary>Настройки, выбранные в окне.</summary>
    public AppSettings Selected => _view.Selected;
}
