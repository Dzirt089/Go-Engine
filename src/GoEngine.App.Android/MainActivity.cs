using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace GoEngine.App.Android;

/// <summary>Точка входа приложения на Android: доска и панель статуса без меню.</summary>
/// <remarks>
/// Вид берётся общий с настольной версией (<c>GoEngine.App.Views.BoardView</c>), поэтому
/// правила и модель представления на телефоне те же. Меню, диалог настроек и файловые диалоги
/// остались настольными: на Android их заменят системные экраны, когда дойдёт очередь.
/// </remarks>
[Activity(
    Label = "Go Engine",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity<App>
{
}
