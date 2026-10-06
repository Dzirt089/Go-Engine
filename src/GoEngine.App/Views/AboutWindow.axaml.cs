using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.Core;

namespace GoEngine.App.Views;

/// <summary>Окно «О программе»: общий вид сведений о программе в настольном окне.</summary>
/// <remarks>
/// Вся логика — в <see cref="AboutView"/>; здесь только оконная обвязка. На телефоне окон нет,
/// и тот же вид показывает оболочка вложенным экраном раздела «Меню» (<see cref="ShellView"/>).
/// </remarks>
public sealed partial class AboutWindow : Window
{
    /// <summary>Создаёт окно «О программе».</summary>
    public AboutWindow()
    {
        AvaloniaXamlLoader.Load(this);

        if (this.FindControl<Button>("CloseButton") is not { } closeButton)
        {
            throw new DomainException("В окне «О программе» нет кнопки закрытия: разметка повреждена.");
        }

        closeButton.Click += OnCloseClick;
    }

    /// <summary>Закрывает окно.</summary>
    /// <param name="sender">Кнопка «Закрыть».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
