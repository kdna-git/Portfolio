using Avalonia;
using Avalonia.Controls;

namespace MVVM.Core.Layout;

/// <summary>
/// Attached behaviours that turn a control's own width into something XAML and view models can
/// react to. Avalonia has no container queries, so this fills the gap:
/// <list type="bullet">
/// <item><c>layout:Adaptive.TrackWidth="True"</c> keeps the <c>compact</c>/<c>medium</c>/
/// <c>expanded</c> style classes in sync with the control's width, so you can write
/// <c>Style Selector="Grid.compact"</c>.</item>
/// <item><c>layout:Adaptive.Store="{Binding Layout}"</c> publishes the control's size into a
/// <see cref="LayoutStore"/> that view models bind to.</item>
/// </list>
/// Both work identically on desktop, Android, iOS and in the browser because they only look at
/// the measured size, never at the operating system.
/// </summary>
public static class Adaptive
{
    public static readonly AttachedProperty<bool> TrackWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("TrackWidth", typeof(Adaptive));

    public static void SetTrackWidth(Control control, bool value) =>
        control.SetValue(TrackWidthProperty, value);

    public static bool GetTrackWidth(Control control) => control.GetValue(TrackWidthProperty);

    public static readonly AttachedProperty<LayoutStore?> StoreProperty =
        AvaloniaProperty.RegisterAttached<Control, LayoutStore?>("Store", typeof(Adaptive));

    public static void SetStore(Control control, LayoutStore? value) =>
        control.SetValue(StoreProperty, value);

    public static LayoutStore? GetStore(Control control) => control.GetValue(StoreProperty);

    static Adaptive()
    {
        TrackWidthProperty.Changed.AddClassHandler<Control>(
            (control, args) =>
            {
                if (args.GetNewValue<bool>())
                {
                    control.SizeChanged += OnSizeChanged;
                    ApplyClasses(control, control.Bounds.Width);
                }
                else
                {
                    control.SizeChanged -= OnSizeChanged;
                }
            }
        );

        StoreProperty.Changed.AddClassHandler<Control>(
            (control, args) =>
            {
                control.SizeChanged -= OnSizeChangedForStore;

                if (args.GetNewValue<MVVM.Core.Layout.LayoutStore?>() is not null)
                {
                    control.SizeChanged += OnSizeChangedForStore;
                    Publish(control, control.Bounds.Size);
                }
            }
        );
    }

    private static void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Control control)
            ApplyClasses(control, e.NewSize.Width);
    }

    private static void OnSizeChangedForStore(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Control control)
            Publish(control, e.NewSize);
    }

    private static void Publish(Control control, Size size)
    {
        GetStore(control)?.Update(size);
    }

    private static void ApplyClasses(Control control, double width)
    {
        if (width <= 0)
            return;

        var wanted = Breakpoints.FromWidth(width).ClassName();

        foreach (var name in AllClassNames)
        {
            var shouldHave = name == wanted;
            var hasIt = control.Classes.Contains(name);

            if (shouldHave && !hasIt)
                control.Classes.Add(name);
            else if (!shouldHave && hasIt)
                control.Classes.Remove(name);
        }
    }

    private static readonly string[] AllClassNames = ["compact", "medium", "expanded"];
}
