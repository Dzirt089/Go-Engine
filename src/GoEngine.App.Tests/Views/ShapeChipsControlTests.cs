using System.ComponentModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using GoEngine.App.Controls;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Полоса форм: общий элемент видов задач, уроков и разбора партий.</summary>
/// <remarks>
/// D-116: выбранный чип обязан быть виден, а выбор — доезжать до доски и обратно. До починки
/// выбор не менял ни доску (не было перерисовки), ни полосу (выбор сбрасывался в «Партиях»).
/// </remarks>
public sealed class ShapeChipsControlTests
{
    [Fact]
    public void На_Каждую_Форму_Есть_Чип()
    {
        var control = new ShapeChipsControl { Shapes = Shapes(2) };

        Assert.Equal(2, Chips(control).Count);
    }

    [Fact]
    public void Выбран_Ровно_Один_Чип()
    {
        var control = new ShapeChipsControl { Shapes = Shapes(3) };

        control.SelectedIndex = 1;

        Assert.Single(Chips(control), chip => chip.IsSelected);
    }

    [Fact]
    public void Заголовок_Виден_Когда_Форм_Больше_Одной()
    {
        var control = new ShapeChipsControl { Shapes = Shapes(2) };

        Assert.True(control.FindControl<TextBlock>("HeaderText")!.IsVisible);
    }

    [Fact]
    public void Заголовок_Для_Одной_Формы_Скрыт()
    {
        var control = new ShapeChipsControl { Shapes = Shapes(1) };

        Assert.False(control.FindControl<TextBlock>("HeaderText")!.IsVisible);
    }

    [Fact]
    public void Заголовок_Показывает_Название_Полосы()
    {
        var control = new ShapeChipsControl { Shapes = Shapes(2), Header = "Формы решения" };

        Assert.Equal("Формы решения", control.FindControl<TextBlock>("HeaderText")!.Text);
    }

    [Fact]
    public void Объяснение_Показывает_Текст_Выбранной_Формы()
    {
        var control = new ShapeChipsControl { Shapes = Shapes(2, "разрезает противника") };

        Assert.Equal("разрезает противника", control.FindControl<TextBlock>("WhyText")!.Text);
    }

    [Fact]
    public void Объяснение_Скрыто_В_Разборе_Партий()
    {
        // В панели партий места мало, а объяснение формы живёт в подсказке чипа (D-116).
        var control = new ShapeChipsControl { Shapes = Shapes(2), ShowWhy = false };

        Assert.False(control.FindControl<TextBlock>("WhyText")!.IsVisible);
    }

    [Fact]
    public void Выбор_Формы_Не_Перезапускает_Анимацию_Хода()
    {
        // Раньше выбор формы поднимал NotifyAll, вид на LastMove перезапускал анимацию камня,
        // и доска перерисовывалась «боком». Теперь форма сообщает только о себе (D-116).
        var model = ShapeScenarios.ProblemWithSeveralShapes();
        List<string?> names = [];
        model.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        model.SelectShape(1);

        Assert.DoesNotContain(nameof(ProblemViewModel.LastMove), names);
    }

    [Fact]
    public void Выбор_Формы_Сообщает_О_Смене_Подсветки()
    {
        var model = ShapeScenarios.ProblemWithSeveralShapes();
        List<string?> names = [];
        model.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        model.SelectShape(1);

        Assert.Contains(nameof(ProblemViewModel.ShapePoints), names);
    }

    [Fact]
    public void Смена_Выбора_Не_Пересоздаёт_Чипы()
    {
        // Клик по чипу не должен выбивать нажатую кнопку из дерева: список чипов пересобирается
        // только при смене форм, признак выбора меняется на месте (находка ревью D-116).
        var control = new ShapeChipsControl { Shapes = Shapes(3) };
        var items = control.FindControl<ItemsControl>("Chips")!.ItemsSource;

        control.SelectedIndex = 2;

        Assert.Same(items, control.FindControl<ItemsControl>("Chips")!.ItemsSource);
    }

    [Fact]
    public void Форм_Стало_Меньше_Показывается_Доступная()
    {
        // Зажим только для отображения: свойство выбора остаётся за моделью, иначе запись
        // зажатого значения отменяла бы её сброс на первую форму (находка ревью D-116).
        var control = new ShapeChipsControl { Shapes = Shapes(3), SelectedIndex = 2 };

        control.Shapes = Shapes(1);

        Assert.Equal(1, Chips(control).Single(chip => chip.IsSelected).Number);
    }

    [Fact]
    public void Зажим_Выбора_Не_Пишется_В_Модель()
    {
        // Порядок уведомлений: модель сначала присылает новые формы, потом новый номер.
        // Если зажать старый номер и записать его обратно, сброс модели на первую форму
        // отменится — новая позиция откроется не с первой формы (находка ревью D-116).
        var stub = new ShapeStub();
        var control = new ShapeChipsControl();
        using var binding = control.Bind(
            ShapeChipsControl.SelectedIndexProperty,
            new Binding(nameof(ShapeStub.SelectedIndex)) { Source = stub, Mode = BindingMode.TwoWay });

        control.Shapes = Shapes(3);
        control.SelectedIndex = 2;
        stub.ResetSilently();
        control.Shapes = Shapes(2);

        Assert.Equal(0, stub.SelectedIndex);
    }

    [Fact]
    public void Клик_По_Чипу_Выбирает_Форму()
    {
        var control = new ShapeChipsControl { Shapes = Shapes(3) };
        var click = typeof(ShapeChipsControl).GetMethod("OnChipClick", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var button = new Button { Tag = 2 };

        click.Invoke(control, [button, new RoutedEventArgs()]);

        Assert.Equal(1, control.SelectedIndex);
    }

    [Fact]
    public void Выбор_На_Полосе_Меняет_Подсветку_Доски()
    {
        var model = ShapeScenarios.ProblemWithSeveralShapes();
        var view = new ProblemView(model);
        var board = view.FindControl<BoardControl>("Board")!;
        var chips = view.FindControl<ShapeChipsControl>("ShapeChips")!;

        chips.SelectedIndex = 1;

        Assert.Equal(model.Shapes[1].Points, board.ShapePoints);
    }

    [Fact]
    public void Выбор_В_Модели_Доходит_До_Полосы()
    {
        var model = ShapeScenarios.ProblemWithSeveralShapes();
        var view = new ProblemView(model);

        model.SelectShape(1);

        Assert.Equal(1, view.FindControl<ShapeChipsControl>("ShapeChips")!.SelectedIndex);
    }

    [Fact]
    public void Формы_Доходят_До_Полосы()
    {
        // Иначе сломанная привязка Shapes оставила бы полосу пустой при зелёных тестах выбора.
        var model = ShapeScenarios.ProblemWithSeveralShapes();
        var view = new ProblemView(model);

        var chips = view.FindControl<ShapeChipsControl>("ShapeChips")!;

        Assert.Equal(model.Shapes.Count, Chips(chips).Count);
    }

    /// <summary>Чипы, которые полоса собрала по формам.</summary>
    /// <param name="control">Полоса форм.</param>
    /// <returns>Чипы в порядке форм.</returns>
    private static IReadOnlyList<ShapeChip> Chips(ShapeChipsControl control) =>
        [.. (IEnumerable<ShapeChip>)control.FindControl<ItemsControl>("Chips")!.ItemsSource!];

    /// <summary>Формы-пустышки для проверки полосы без библиотеки контента.</summary>
    /// <param name="count">Сколько форм собрать.</param>
    /// <returns>Формы с номерами по порядку.</returns>
    private static IReadOnlyList<ShapeItem> Shapes(int count, string why = "") =>
        [.. Enumerable.Range(1, count).Select(number => new ShapeItem(number, $"форма {number}", true, [new Point((byte)number, 1)], why))];

    /// <summary>Модель формы для проверки двусторонней привязки без библиотеки контента.</summary>
    private sealed class ShapeStub : INotifyPropertyChanged
    {
        private int _selectedIndex;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Номер выбранной формы: установка уведомляет привязку.</summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value)
                {
                    return;
                }

                _selectedIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIndex)));
            }
        }

        /// <summary>Меняет поле без уведомления: модель уже сбросила выбор, но вид ещё не знает.</summary>
        public void ResetSilently() => _selectedIndex = 0;
    }
}
