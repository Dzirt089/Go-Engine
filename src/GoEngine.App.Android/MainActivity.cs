using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;

namespace GoEngine.App.Android;

/// <summary>Точка входа приложения на Android: доска и панель статуса без меню.</summary>
/// <remarks>
/// Вид берётся общий с настольной версией (<c>GoEngine.App.Views.BoardView</c>), поэтому
/// правила и модель представления на телефоне те же. Модели нейросети копируются из пакета
/// в каталог приложения до создания вида: список уровней строится в конструкторе вида,
/// и уровни с сетью должны быть уже доступны.
/// </remarks>
[Activity(
    Label = "Go Engine",
    Icon = "@mipmap/ic_launcher",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity<global::GoEngine.App.App>
{
    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AndroidModelStartup.Prepare(this);

        base.OnCreate(savedInstanceState);
    }
}
