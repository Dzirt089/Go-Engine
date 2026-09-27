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
        _view.Cancelled += (_, _) => Close(false);
    }

    /// <summary>Настройки, выбранные в окне.</summary>
    public AppSettings Selected => _view.Selected;
}
