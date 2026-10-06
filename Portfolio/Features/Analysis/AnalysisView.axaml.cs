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
    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ScopeCardViewModel card })
        {
            card.OpenCommand.Execute(null);
            e.Handled = true;
        }
    }
}
