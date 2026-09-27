using Android.Content;
using GoEngine.App.Services.Updates;
using GoEngine.Core;

namespace GoEngine.App.Android.Updates;

/// <summary>Установка обновления на Android: системный установщик пакетов.</summary>
/// <remarks>
/// Android не разрешает тихо заменить установленное приложение: скачанный APK отдаётся системе
/// через <c>FileProvider</c> (обычная ссылка <c>file://</c> запрещена с Android 7 и приводит
/// к <c>FileUriExposedException</c>), а игрок подтверждает установку в системном окне. Для этого
/// в манифесте объявлены разрешения <c>INTERNET</c> и <c>REQUEST_INSTALL_PACKAGES</c> и описан
/// <c>FileProvider</c> с путями к каталогу загрузок.
/// </remarks>
public sealed class AndroidUpdateInstaller : IUpdateInstaller
{
    /// <summary>Тип файла пакета Android: по нему система открывает установщик.</summary>
    private const string PackageMimeType = "application/vnd.android.package-archive";

    /// <inheritdoc />
    public string PlatformKey => "android";

    /// <inheritdoc />
    public Result<string> Install(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return Result<string>.Fail("Скачанный файл обновления не найден.");
        }

        try
        {
            var context = global::Android.App.Application.Context;
            var file = new Java.IO.File(filePath);
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, Authority(context), file);
            var intent = new Intent(Intent.ActionView);

            intent.SetDataAndType(uri, PackageMimeType);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);

            return Result<string>.Ok("Открыт системный установщик Android: подтвердите обновление. Приложение может закрыться.");
        }
        catch (ActivityNotFoundException)
        {
            return Result<string>.Fail($"Системный установщик пакетов не найден. Файл обновления: {filePath}");
        }
        catch (Java.Lang.Exception exception)
        {
            return Result<string>.Fail($"Не удалось открыть установщик: {exception.Message}. Файл обновления: {filePath}");
        }
    }

    /// <inheritdoc />
    public Result<string> OpenDownloadPage(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        try
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url.ToString()));

            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);

            return Result<string>.Ok("Страница загрузки открыта в браузере.");
        }
        catch (Java.Lang.Exception exception)
        {
            return Result<string>.Fail($"Не удалось открыть браузер: {exception.Message}. Скачайте обновление вручную: {url}");
        }
    }

    /// <summary>Собирает имя поставщика файлов.</summary>
    /// <param name="context">Контекст приложения.</param>
    /// <returns>Имя вида <c>com.goengine.app.fileprovider</c>.</returns>
    /// <remarks>Имя обязано совпадать с <c>android:authorities</c> в манифесте.</remarks>
    private static string Authority(Context context) => context.PackageName + ".fileprovider";
}
