using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Portfolio.Features.Shared;

/// <summary>
/// An inline "are you sure?" step, shared by every list screen.
/// </summary>
/// <remarks>
/// Deleting a platform takes its holdings and their entries with it, so the prompt names what will go
/// rather than asking a generic question. One instance per screen; the three list screens had three
/// near-identical copies of this before.
/// </remarks>
public sealed partial class DeleteConfirmation : ObservableObject
{
    private Func<Task>? _confirmed;

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; }

    [ObservableProperty]
    public partial string? Detail { get; set; }

    public DeleteConfirmation()
    {
        Message = string.Empty;
    }

    public void Ask(string message, string? detail, Func<Task> confirmed)
    {
        Message = message;
        Detail = detail;
        _confirmed = confirmed;
        IsVisible = true;
    }

    [RelayCommand]
    private async Task Confirm()
    {
        var action = _confirmed;
        Cancel();

        if (action is not null)
            await action().ConfigureAwait(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        _confirmed = null;
        IsVisible = false;
        Detail = null;
    }
}
