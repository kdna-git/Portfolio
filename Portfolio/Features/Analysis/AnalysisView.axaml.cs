using Avalonia.Controls;
using Avalonia.Input;
using Portfolio.Features.Shared;

namespace Portfolio.Features.Analysis;

public partial class AnalysisView : UserControl
{
    public AnalysisView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// A whole card is the tap target rather than a button inside it: on a touch screen the card is
    /// what people aim at, and a 300-pixel target beats a 60-pixel one.
    /// </summary>
    /// <remarks>
    /// Tapped rather than PointerPressed: a press is also how a swipe or a held click begins, so opening
    /// on it hijacked every scroll that started on a card. Tapped waits for the release, and only counts
    /// it if the pointer stayed within the platform's tap slop and the page did not take the gesture.
    /// </remarks>
    private void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ScopeCardViewModel card })
        {
            card.OpenCommand.Execute(null);
            e.Handled = true;
        }
    }
}
