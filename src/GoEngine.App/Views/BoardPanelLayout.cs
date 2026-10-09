using Avalonia;
using Avalonia.Controls;
using GoEngine.App.Controls;
using GoEngine.App.Services;

namespace GoEngine.App.Views;

/// <summary>Раскладка экрана «доска и панель»: доска сверху и панель снизу на узком экране.</summary>
/// <remarks>
/// <para>
/// Правила одни для партии, задач, уроков и разбора партий: на телефоне доска сверху, панель
/// снизу, на широком экране — доска слева, панель справа. Повторять эту логику в каждом виде
/// значило бы плодить расхождения, поэтому она живёт здесь, а вид только передаёт свои элементы.
/// </para>
/// <para>
/// Высота доски пересчитывается при каждом изменении размера, а не только при смене раскладки:
/// окно на телефоне меняется и в повороте, и когда появляется экранная клавиатура.
/// </para>
/// </remarks>
internal sealed class BoardPanelLayout
{
    /// <summary>Наименьшая высота панели в вертикальной раскладке.</summary>
    /// <remarks>
    /// Текст шага и три ряда кнопок занимают около 230 точек; остальное — название материала
    /// и список выбора, и они прокручиваются. Меньше этой границы панель отдавать нельзя: кнопки
    /// уехали бы за край экрана, и материал нельзя было бы пройти. Число то же, что у панели
    /// задач: раскладки одинаковые, и нижняя навигация на телефоне обязана помещаться целиком.
    /// </remarks>
    public const double MinimumPanelHeight = 280;

    /// <summary>Высота доски на совсем низком экране, где половина высоты меньше разумного минимума.</summary>
    private const double MinimumBoardHeightForTinyScreen = 120;

    private readonly Grid _layout;
    private readonly BoardControl _board;
    private readonly Border _panel;
    private readonly IReadOnlyList<Control> _wideOnly;

    /// <summary>Текущая раскладка: <c>null</c> — ещё не выбрана.</summary>
    private bool? _narrow;

    /// <summary>Собирает раскладку экрана материала.</summary>
    /// <param name="layout">Сетка с доской и панелью.</param>
    /// <param name="board">Доска.</param>
    /// <param name="panel">Панель с текстом и кнопками.</param>
    /// <param name="wideOnly">Элементы, которые прячутся на узком экране (дублируют подписи).</param>
    public BoardPanelLayout(Grid layout, BoardControl board, Border panel, IReadOnlyList<Control> wideOnly)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(wideOnly);

        _layout = layout;
        _board = board;
        _panel = panel;
        _wideOnly = wideOnly;
    }

    /// <summary>Раскладывает доску и панель по размеру вида.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    public void Apply(double width, double height)
    {
        var narrow = BoardLayoutRules.IsNarrow(width, height);

        if (_narrow != narrow)
        {
            _narrow = narrow;

            _layout.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,320");
            _layout.RowDefinitions = new RowDefinitions(narrow ? "Auto,*" : "*");

            Grid.SetColumn(_board, 0);
            Grid.SetRow(_board, 0);
            Grid.SetColumn(_panel, narrow ? 0 : 1);
            Grid.SetRow(_panel, narrow ? 1 : 0);

            _panel.BorderThickness = narrow ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);
        }

        // На телефоне заголовок и описание дублируют подпись раздела в нижней навигации
        // и строку выбора, а их высота нужна тексту материала: без этого текст уходил
        // под прокрутку целиком (проверено живым прогоном).
        foreach (var control in _wideOnly)
        {
            control.IsVisible = !narrow;
        }

        _board.Height = narrow ? NarrowBoardHeight(width, height) : double.NaN;
    }

    /// <summary>Считает высоту доски в вертикальной раскладке.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    /// <returns>Высота доски в точках.</returns>
    private static double NarrowBoardHeight(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return double.NaN;
        }

        var available = height - MinimumPanelHeight;

        if (available < BoardLayoutRules.MinimumBoardHeight)
        {
            // Экран ниже, чем доска вместе с панелью (телефон в повороте): делим высоту пополам,
            // но панель не отдаём целиком — иначе кнопки снова уедут за край.
            return Math.Max(height / 2, MinimumBoardHeightForTinyScreen);
        }

        return Math.Min(BoardLayoutRules.BoardHeight(width, height), available);
    }
}
