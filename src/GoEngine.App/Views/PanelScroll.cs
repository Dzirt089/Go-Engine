using Avalonia.Controls;
using Avalonia.Threading;

namespace GoEngine.App.Views;

/// <summary>Прокрутка панели разбора: новый шаг или ход показывается с начала.</summary>
/// <remarks>
/// <para>
/// Жалоба пользователя 2026-10-15: «На 8-м ходу и далее место урезанное, ничего не видно» —
/// с первого по седьмой ход панель читалась, дальше текст уезжал. Причина: прокрутка панели
/// сохраняла смещение между ходами, а заметки разбора разной длины. Игрок видел середину текста:
/// заголовок заметки обрезан сверху, а под текстом оставалась пустота.
/// </para>
/// <para>
/// Решение: при смене хода или шага прокрутка возвращается в начало. Возврат идёт через очередь
/// диспетчера: к моменту вызова раскладка ещё не пересчитана, и прокрутка «в начало» применилась бы
/// к прежнему размеру содержимого.
/// </para>
/// </remarks>
internal static class PanelScroll
{
    /// <summary>Возвращает панель к началу текста.</summary>
    /// <param name="scroll">Прокрутка панели или <c>null</c>, если её нет в разметке.</param>
    public static void Home(ScrollViewer? scroll)
    {
        if (scroll is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => scroll.Offset = new Avalonia.Vector(0, 0), DispatcherPriority.Loaded);
    }
}
