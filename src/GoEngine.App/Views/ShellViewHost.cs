using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace GoEngine.App.Views;

/// <summary>Хранилище единственного экземпляра оболочки и её отвязка от прежнего родителя.</summary>
/// <remarks>
/// <para>
/// Android пересоздаёт активность в том же процессе: возврат из фона, смена конфигурации,
/// восстановление задачи. Приложение Avalonia и его виды при этом остаются теми же, а платформа
/// заново строит <c>AvaloniaView</c> и пытается положить в него <c>MainView</c>. Если вид уже
/// привязан к <c>ContentPresenter</c> прежней активности, добавление падает с
/// «ShellView already has a visual parent», и игрок теряет партию (N1 отчёта QA 2026-10-17).
/// </para>
/// <para>
/// Поэтому оболочка одна на процесс: <see cref="Acquire"/> возвращает тот же экземпляр и перед
/// показом отвязывает его от прежнего родителя. Второй экземпляр не создаётся никогда — иначе
/// партия, раздел и прогресс сбрасывались бы на каждом пересоздании активности.
/// </para>
/// <para>
/// Класс отдельный от <see cref="ShellView"/>, чтобы правило «одна оболочка» проверялось тестом
/// без окна и без Android: тест подставляет фабрику вида и считает, сколько раз её позвали.
/// </para>
/// </remarks>
public sealed class ShellViewHost
{
    /// <summary>Фабрика единственного вида: вызывается только при первом обращении.</summary>
    private readonly Func<ShellView> _create;

    /// <summary>Созданная оболочка; <c>null</c> — вид ещё не запрашивали.</summary>
    private ShellView? _view;

    /// <summary>Создаёт хранилище оболочки.</summary>
    /// <param name="create">Фабрика вида: в приложении создаёт <see cref="ShellView"/>, в тесте — подставной.</param>
    /// <exception cref="ArgumentNullException">Фабрика не задана.</exception>
    public ShellViewHost(Func<ShellView> create)
    {
        ArgumentNullException.ThrowIfNull(create);

        _create = create;
    }

    /// <summary>Единственный экземпляр оболочки на процесс.</summary>
    /// <remarks>Первое обращение создаёт вид; повторные возвращают тот же, не создавая второго.</remarks>
    public ShellView Current => _view ??= _create();

    /// <summary>Возвращает оболочку для показа, отвязав её от прежнего родителя.</summary>
    /// <returns>Единственный экземпляр вида, свободный от родителя.</returns>
    /// <remarks>
    /// Отвязка нужна перед каждым показом, а не только при закрытии: платформа вправе создать
    /// новую активность раньше, чем уничтожит прежнюю, и тогда вид ещё привязан к её дереву.
    /// </remarks>
    public ShellView Acquire()
    {
        var view = Current;

        Detach(view);

        return view;
    }

    /// <summary>Отвязывает оболочку от родителя при закрытии активности.</summary>
    /// <remarks>Сам вид при этом жив: его позиция, раздел и партия остаются на месте.</remarks>
    public void Release()
    {
        if (_view is { } view)
        {
            Detach(view);
        }
    }

    /// <summary>Отвязывает вид от родителя, чем бы этот родитель ни был.</summary>
    /// <param name="view">Вид, который сейчас в дереве.</param>
    /// <remarks>
    /// Родителем оболочки на Android оказывается <see cref="ContentPresenter"/> внутри
    /// <c>AvaloniaView</c>; на настольной системе и в тестах им может быть панель, карточка
    /// или другое содержимое. Все эти случаи снимаются здесь, чтобы вызывающему не приходилось
    /// знать, куда именно вид положили в прошлый раз.
    /// </remarks>
    internal static void Detach(Control view)
    {
        ArgumentNullException.ThrowIfNull(view);

        // Сначала визуальный родитель: именно он запрещает второе добавление. Логический
        // родитель — запасной путь, если вид ещё не успел попасть в визуальное дерево.
        var parent = (StyledElement?)view.GetVisualParent() ?? view.Parent;

        switch (parent)
        {
            case ContentPresenter presenter:
                presenter.Content = null;
                break;
            case ContentControl content:
                content.Content = null;
                break;
            case Panel panel:
                panel.Children.Remove(view);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
        }
    }
}
