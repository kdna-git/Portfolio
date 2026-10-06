using System.Globalization;

namespace Portfolio.Composition;

/// <summary>
/// Makes sure the app runs under a culture that can actually format money.
/// </summary>
/// <remarks>
/// Browsers report a language, not a region: <c>navigator.language</c> is often just <c>es</c> or
/// <c>en</c>. A neutral culture has no country, so it has no currency either, and every amount in the
/// app rendered with the generic currency sign instead of a real one. Desktop and Android report a
/// specific culture and never hit this, which is exactly the kind of difference that only shows up
/// once you run the thing somewhere else.
/// </remarks>
public static class AppCulture
{
    /// <summary>Used when the platform reports no usable culture at all.</summary>
    private const string Fallback = "en-US";

    public static void EnsureSpecific()
    {
        var current = CultureInfo.CurrentCulture;

        if (!current.IsNeutralCulture && !string.IsNullOrEmpty(current.Name))
            return;

        var wanted = string.IsNullOrEmpty(current.Name) ? Fallback : current.Name;

        CultureInfo specific;

        try
        {
            specific = CultureInfo.CreateSpecificCulture(wanted);
        }
        catch (CultureNotFoundException)
        {
            specific = CultureInfo.GetCultureInfo(Fallback);
        }

        CultureInfo.DefaultThreadCurrentCulture = specific;
        CultureInfo.DefaultThreadCurrentUICulture = specific;
        CultureInfo.CurrentCulture = specific;
        CultureInfo.CurrentUICulture = specific;
    }
}
