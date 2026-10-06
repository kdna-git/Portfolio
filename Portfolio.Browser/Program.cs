using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using Portfolio;
using Portfolio.Browser;
using Portfolio.Composition;
using Portfolio.Storage;

[SupportedOSPlatform("browser")]
internal sealed partial class Program
{
    private const string StorageKey = "portfolio.snapshot.v1";

    private static async Task Main(string[] args)
    {
        var repository = new JsonPortfolioRepository(new LocalStorageBlobStore(StorageKey))
        {
            // A browser visitor has nothing stored, and an empty dashboard teaches nothing. Seeding a
            // plausible three-year portfolio on first run makes the charts and statistics explorable
            // straight away; anything the visitor adds is kept alongside it.
            SeedFactory = () => DemoData.CreateSnapshot(),
            DamagedCopyStore = new LocalStorageBlobStore(StorageKey + ".damaged"),
        };

        App.Services = new AppServices(
            repository,
            new LocalStorageDeviceStorage("portfolio.", "Browser")
        );

        await BuildAvaloniaApp()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();
}
