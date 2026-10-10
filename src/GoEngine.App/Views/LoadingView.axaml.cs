using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GoEngine.App.Views;

/// <summary>Экран загрузки: показывает, что приложение готовится, а не зависло.</summary>
/// <remarks>
/// <para>
/// Жалоба пользователя 2026-10-10: при первом запуске «несколько (3–7) секунд белый/чёрный
/// экран… люди думают, что игра зависла». Экран показывается окном (настольные системы)
/// или главным видом (телефон) до того, как создана оболочка: он лёгкий и появляется сразу,
/// а нейросеть грузится в фоне.
/// </para>
/// <para>
/// Вид ничего не решает: он показывает строку, которую ему дали (<see cref="Show"/>), и крутит
/// полосу. Кто и когда грузит модели, знает голова — библиотека интерфейса про ONNX не знает
/// (D-034).
/// </para>
/// </remarks>
public sealed partial class LoadingView : UserControl
{
    private readonly TextBlock? _status;

    /// <summary>Создаёт экран загрузки.</summary>
    public LoadingView()
    {
        AvaloniaXamlLoader.Load(this);

        _status = this.FindControl<TextBlock>("StatusText");
    }

    /// <summary>Показывает, что происходит прямо сейчас.</summary>
    /// <param name="message">Строка состояния: её видит игрок.</param>
    public void Show(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (_status is not null)
        {
            _status.Text = message;
        }
    }
}
