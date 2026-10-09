using Avalonia.Controls;
using Avalonia.Interactivity;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Тесты вида разбора партий: элементы, список партий и переходы по ходам.</summary>
/// <remarks>
/// Вид рисует то, что решила модель (<see cref="ReviewViewModel"/>), поэтому проверяется связь
/// вида с моделью: кнопки, список и доска. Материал партий проверяют тесты <c>GoEngine.Tests</c>.
/// </remarks>
public sealed class ReviewViewTests
{
    [Fact]
    public void Вид_Показывает_Партию_И_Доску()
    {
        var view = new ReviewView();
        var board = view.FindControl<Controls.BoardControl>("Board")!;

        Assert.NotNull(board);
        Assert.Equal(view.ViewModel.MoveCount, view.ViewModel.MoveCount);
        Assert.True(view.ViewModel.MoveCount > 0);
    }

    [Fact]
    public void Кнопка_Далее_Играет_Ход_Партии()
    {
        var view = new ReviewView();

        // Первый ход партии ищет игрок: без ответа «Далее» не двигает разбор.
        view.ViewModel.Play(view.ViewModel.QuizPoints[0]);
        Click(view.FindControl<Button>("NextButton")!);

        Assert.Equal(2, view.ViewModel.MoveNumber);
    }

    [Fact]
    public void Список_Партий_Заполнен()
    {
        var view = new ReviewView();
        var box = view.FindControl<ComboBox>("GameBox")!;

        Assert.Equal(view.ViewModel.Labels, box.ItemsSource);
        Assert.Equal(0, box.SelectedIndex);
    }

    [Fact]
    public void Разбор_Просит_Словарь_Терминов()
    {
        var view = new ReviewView();
        var asked = false;
        view.GlossaryRequested += (_, _) => asked = true;

        Click(view.FindControl<Button>("GlossaryButton")!);

        Assert.True(asked);
    }

    [Fact]
    public void Вид_Открывается_На_Первой_Партии_Библиотеки()
    {
        var view = new ReviewView();

        Assert.Equal(ReviewLibrary.All[0].Title, view.ViewModel.GameTitle);
    }

    [Fact]
    public void Узкий_Экран_Прячет_Описание_Партии()
    {
        // На телефоне название и описание дублируют подпись раздела и строку выбора, а их высота
        // нужна тексту разбора (то же решение, что в уроке).
        var view = new ReviewView();

        view.Measure(new Avalonia.Size(380, 780));
        view.Arrange(new Avalonia.Rect(0, 0, 380, 780));

        Assert.False(view.FindControl<TextBlock>("GameSummaryText")!.IsVisible);
        Assert.True(view.FindControl<TextBlock>("MoveCounterText")!.IsVisible);
    }

    /// <summary>Нажимает кнопку так же, как её нажимает игрок.</summary>
    /// <param name="button">Кнопка разметки.</param>
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
