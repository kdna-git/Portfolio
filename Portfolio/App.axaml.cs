using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MVVM.Core.ViewModels;
using Portfolio.Composition;
using Portfolio.Features.Shell;

namespace Portfolio;

public partial class App : Application
{
    /// <summary>
    /// Set by the platform head before Avalonia starts. The head owns storage, so it owns this.
    /// </summary>
    public static AppServices? Services { get; set; }

    private MainViewModel? _shellViewModel;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        if (Avalonia.Controls.Design.IsDesignMode)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        // Again here, not only in AppServices: the platform can replace the thread culture while the
        // framework starts up, and this runs after that.
        AppCulture.EnsureSpecific();

        var services =
            Services
            ?? throw new InvalidOperationException(
                $"{nameof(App)}.{nameof(Services)} must be set by the platform head before the app starts."
            );

        // Charts pick their colours from the current theme variant, so a theme switch has to rebuild
        // the current screen. The navigation store keeps the factory, which makes that one call.
        ActualThemeVariantChanged += (_, _) => services.Navigation.Refresh();

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                _shellViewModel = BuildShellViewModel(services, withHeader: true);
                desktop.MainWindow = new DesktopShellWindow { DataContext = _shellViewModel };
                break;

            case ISingleViewApplicationLifetime singleView:
                _shellViewModel = BuildShellViewModel(services, withHeader: false);
                singleView.MainView = new SingleViewShell { DataContext = _shellViewModel };
                break;
        }

        base.OnFrameworkInitializationCompleted();

        try
        {
            await services.StartAsync();
        }
        catch (Exception exception)
        {
            // Nothing else has run yet, so there is no screen to report this on; the shell shows its
            // empty state and the message goes to the platform log.
            System.Diagnostics.Trace.TraceError($"Failed to load the portfolio: {exception}");
        }
    }

    /// <summary>
    /// Handles a platform back request (Android's gesture, a browser back button). Returns false when
    /// the app has nowhere left to go, which is the head's signal to let the system handle it.
    /// </summary>
    public static bool TryGoBack() => Services?.Navigator.TryGoBack() ?? false;

    private static MainViewModel BuildShellViewModel(AppServices services, bool withHeader) =>
        new(
            services.Navigation,
            services.Modal,
            services.Layout,
            withHeader ? new HeaderViewModel("Portfolio") : null,
            new NavigationViewModel(services.Navigator, services.Layout)
        );
}
