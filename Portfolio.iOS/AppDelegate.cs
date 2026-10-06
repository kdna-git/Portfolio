using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Foundation;
using Portfolio.Composition;
using Portfolio.EntityFramework;
using Portfolio.Storage;

namespace Portfolio.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Library is the right home for a database on iOS: backed up, but not shown to the user in
        // the Files app the way Documents is.
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Portfolio"
        );

        App.Services = new AppServices(
            PortfolioDatabase.Open(directory),
            new FileDeviceStorage(directory, "iOS")
        );

        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
