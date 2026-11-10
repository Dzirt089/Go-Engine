using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace GoEngine.App.Views;

/// <summary>Главный вид одновидового приложения и его отвязка от прежней активности.</summary>
/// <remarks>
/// <para>
/// Android может создать новую активность раньше, чем уничтожит прежнюю. Приложение Avalonia
/// и его виды при этом остаются живыми: `MainView` всё ещё лежит в `ContentPresenter` старой
/// активности, а `AvaloniaMainActivity.InitializeAvaloniaView` кладёт его в новый
/// `AvaloniaView` до того, как lifetime сообщит о смене активности. Второе добавление вида
/// в визуальное дерево падает с «already has a visual parent», и приложение закрывается.
/// </para>
/// <para>
/// Поэтому перед созданием вида в новой активности (до `base.OnCreate`) главный вид
/// отвязывается от прежнего родителя. Сам вид не уничтожается: партия, раздел и прогресс
/// остаются на месте. На настольной системе и в проверках без окна делать нечего.
/// </para>
/// </remarks>
public static class MainViewHost
{
    /// <summary>Отвязывает главный вид одновидового приложения от прежнего родителя.</summary>
    /// <returns><c>true</c>, если вид был отвязан; <c>false</c> — отвязывать было нечего.</returns>
    /// <remarks>
    /// Вызывается головой Android перед <c>base.OnCreate</c>. Без приложения, на настольной
    /// системе и при отсутствии главного вида молча возвращает <c>false</c>: отказ не должен
    /// мешать запуску.
    /// </remarks>
    public static bool Release()
    {
        if (Application.Current?.ApplicationLifetime is not ISingleViewApplicationLifetime mobile
            || mobile.MainView is not { } view)
        {
            return false;
        }

        return Detach(view);
    }

    /// <summary>Снимает вид с родителя, каким бы тот ни был.</summary>
    /// <param name="view">Вид, который сейчас в дереве.</param>
    /// <returns><c>true</c>, если вид был снят; <c>false</c> — родителя не было.</returns>
    /// <remarks>
    /// Родителем главного вида на Android оказывается <see cref="ContentPresenter"/> внутри
    /// <c>AvaloniaView</c>; в проверках и на настольной системе — панель, карточка или другой
    /// контейнер. Сначала берётся визуальный родитель: именно он запрещает второе добавление.
    /// Логический родитель — запасной путь, если вид ещё не попал в визуальное дерево.
    /// Отвязка идемпотентна: повторный вызов ничего не делает.
    /// </remarks>
    internal static bool Detach(Control view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var parent = view.GetVisualParent() as StyledElement ?? view.Parent;

        switch (parent)
        {
            case ContentPresenter presenter:
                presenter.Content = null;

                return true;
            case ContentControl content:
                content.Content = null;

                return true;
            case Panel panel:
                return panel.Children.Remove(view);
            case Decorator decorator:
                decorator.Child = null;

                return true;
            default:
                return false;
        }
    }
}
