using Avalonia;
using Portfolio.Composition;
using Portfolio.EntityFramework;
using Portfolio.Storage;

namespace Portfolio.Desktop;

internal sealed class Program
{
    /// <summary>
    /// Windows, macOS and Linux. SQLite in the per-user application data folder.
    /// </summary>
    /// <remarks>
    /// The whole head is now the two lines that pick storage and hand it to the app. Everything the
    /// previous version wired by hand — twelve commands and queries, the store, the navigation
    /// services — lives in <see cref="AppServices"/>, so a head cannot start half-configured.
    /// </remarks>
    [STAThread]
    public static async Task Main(string[] args)
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Portfolio"
        );

        App.Services = new AppServices(
            await PortfolioDatabase.OpenAsync(dataDirectory),
            new FileDeviceStorage(dataDirectory, "Desktop")
        );

#if DEBUG
        // Avalonia logs binding failures through Trace, which by default goes nowhere in a console
        // app — so a mistyped binding path fails silently. Route it to stdout while developing.
        System.Diagnostics.Trace.Listeners.Add(
            new System.Diagnostics.TextWriterTraceListener(Console.Out)
        );
        System.Diagnostics.Trace.AutoFlush = true;
#endif

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
