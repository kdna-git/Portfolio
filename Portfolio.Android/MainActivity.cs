using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Portfolio.Android;

[Activity(
    Label = "Portfolio",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    // Without these the activity is destroyed and recreated on rotation and on a light/dark switch,
    // which would throw away the whole navigation stack.
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.Density
)]
public class MainActivity : AvaloniaMainActivity
{
    /// <summary>
    /// The system back gesture unwinds the app's own navigation first — closing a modal, leaving a
    /// drill-down — and only leaves the app when there is nothing left to go back to.
    /// </summary>
    // Deprecated from API 33 in favour of OnBackInvokedCallback, but Android keeps routing back
    // presses here unless an app opts into the new dispatcher in its manifest — which this one does
    // not. Overriding it stays the correct hook, and the one that works on every supported version.
#pragma warning disable CA1422
    public override void OnBackPressed()
    {
        if (App.TryGoBack())
            return;

        base.OnBackPressed();
    }
#pragma warning restore CA1422
}
