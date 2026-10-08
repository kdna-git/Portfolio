using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Portfolio.Composition;
using Portfolio.EntityFramework;
using Portfolio.Storage;

namespace Portfolio.Android;

[Application(Label = "Portfolio", Icon = "@mipmap/ic_launcher")]
public class Application : AvaloniaAndroidApplication<App>
{
    protected Application(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer) { }

    public override void OnCreate()
    {
        // FilesDir is private to the app and survives updates; it is also excluded from backups by
        // default, which is the right place for a local financial ledger.
        var directory = Context.FilesDir?.AbsolutePath ?? FilesDir!.AbsolutePath;

        App.Services = new AppServices(
            PortfolioDatabase.Open(directory),
            new FileDeviceStorage(directory, "Android")
        );

        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
