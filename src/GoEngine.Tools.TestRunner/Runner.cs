using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Runtime.Loader;
using System.Threading;
using Xunit;
using Xunit.Abstractions;
using Xunit.Runners;

internal static class Runner
{
    private static int Main(string[] args)
    {
        var path = Path.GetFullPath(args[0]);
        var needle = args.Length > 1 ? args[1] : null;
        var directory = Path.GetDirectoryName(path)!;

        // Культура — как у testhost: иначе десятичная запятая в строках счёта расходится
        // с ожиданиями тестов («5,5» вместо «5.5»), и падения выглядят как дефекты кода.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        // Тестовая сборка лежит вне каталога утилиты: без этого обработчика xunit не найдёт
        // ни её саму, ни её зависимости (Avalonia, Skia, GoEngine.*).
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var candidate = Path.Combine(directory, name.Name + ".dll");

            return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
        };

        // Чужие копии нативных библиотек в System32 (например, onnxruntime.dll от Windows ML)
        // перебивают наши: загрузчик Windows ищет в системном каталоге раньше, чем в каталоге
        // приложения. Поэтому нужные копии загружаются явно и первыми — тогда P/Invoke находит
        // уже загруженный модуль по имени и не идёт в System32.
        PreloadNative(directory, "onnxruntime");
        PreloadNative(directory, "onnxruntime_providers_shared");

        // Нативные библиотеки SkiaSharp и HarfBuzz лежат рядом с тестовой сборкой, а не с утилитой:
        // без этого обработчика Skia падает на инициализации (TypeInitializationException SKImageInfo).
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, library) =>
        {
            // Нативные библиотеки SkiaSharp лежат в runtimes/<rid>/native рядом с тестовой сборкой:
            // сам рантайм подкладывает их только когда запущен настоящим хостом тестов.
            var roots = new[]
            {
                directory,
                Path.Combine(directory, "runtimes", "win-x64", "native"),
                Path.Combine(directory, "runtimes", "win-x86", "native"),
                Path.Combine(directory, "runtimes", "win-arm64", "native")
            };

            foreach (var root in roots)
            {
                foreach (var candidate in new[] { library, library + ".dll", "lib" + library + ".dll" })
                {
                    var path2 = Path.Combine(root, candidate);

                    if (File.Exists(path2))
                    {
                        return System.Runtime.InteropServices.NativeLibrary.Load(path2);
                    }
                }
            }

            // В PATH этой машины лежит чужая onnxruntime.dll (компонент Windows ML): если отдать
            // поиск системе, загрузится она, и модель не откроется («opset 5 не поддерживается»).
            // Поэтому каталог нативных библиотек .NET ищется и по имени пакета рантайма.
            var packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

            foreach (var package in new[] { "microsoft.ml.onnxruntime", "skiasharp", "harfbuzzsharp" })
            {
                var versionRoot = Path.Combine(packages, package);

                if (!Directory.Exists(versionRoot))
                {
                    continue;
                }

                foreach (var version in Directory.GetDirectories(versionRoot))
                {
                    var path2 = Path.Combine(version, "runtimes", "win-x64", "native", library + ".dll");

                    if (File.Exists(path2))
                    {
                        return System.Runtime.InteropServices.NativeLibrary.Load(path2);
                    }
                }
            }

            return IntPtr.Zero;
        };

        using var controller = new XunitFrontController(AppDomainSupport.Denied, path, null, false);
        using var discovery = new TestDiscoveryVisitor();
        controller.Find(false, discovery, TestFrameworkOptions.ForDiscovery());
        discovery.Finished.WaitOne();

        var cases = needle is null
            ? discovery.TestCases
            : discovery.TestCases.Where(c => c.DisplayName.Contains(needle, StringComparison.Ordinal)).ToList();

        Console.WriteLine("Найдено тестов: " + cases.Count);

        using var execution = new TestExecutionVisitor();
        controller.RunTests(cases, execution, TestFrameworkOptions.ForExecution());
        execution.Finished.WaitOne();

        foreach (var failure in execution.Failures)
        {
            var type = failure.ExceptionTypes.Length > 0 ? failure.ExceptionTypes[0] : "исключение";
            var message = failure.Messages.Length > 0 ? failure.Messages[0] : string.Empty;
            Console.WriteLine("ПРОВАЛ: " + failure.TestCase.DisplayName + " :: " + type + " :: " + message);
        }

        Console.WriteLine("Пройдено: " + execution.Passed + ", провалено: " + execution.Failed + ", пропущено: " + execution.Skipped);

        return execution.Failed > 0 ? 1 : 0;
    }

    /// <summary>Загружает нативную библиотеку из каталога тестовой сборки до первого P/Invoke.</summary>
    /// <param name="directory">Каталог тестовой сборки.</param>
    /// <param name="name">Имя библиотеки без расширения.</param>
    private static void PreloadNative(string directory, string name)
    {
        foreach (var root in new[]
        {
            directory,
            Path.Combine(directory, "runtimes", "win-x64", "native")
        })
        {
            var path = Path.Combine(root, name + ".dll");

            if (File.Exists(path))
            {
                _ = System.Runtime.InteropServices.NativeLibrary.Load(path);

                return;
            }
        }
    }
}

internal sealed class TestDiscoveryVisitor : TestMessageVisitor<IDiscoveryCompleteMessage>
{
    public List<ITestCase> TestCases { get; } = new List<ITestCase>();

    protected override bool Visit(ITestCaseDiscoveryMessage testCaseDiscovered)
    {
        TestCases.Add(testCaseDiscovered.TestCase);
        return true;
    }
}

internal sealed class TestExecutionVisitor : TestMessageVisitor<ITestAssemblyFinished>
{
    public List<ITestFailed> Failures { get; } = new List<ITestFailed>();
    public int Passed { get; private set; }
    public int Failed { get; private set; }
    public int Skipped { get; private set; }

    protected override bool Visit(ITestPassed testPassed)
    {
        Passed++;
        return true;
    }

    protected override bool Visit(ITestFailed testFailed)
    {
        Failed++;
        Failures.Add(testFailed);
        return true;
    }

    protected override bool Visit(ITestSkipped testSkipped)
    {
        Skipped++;
        return true;
    }
}
