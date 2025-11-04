namespace PrintingTools.Core.Preview;

/// <summary>
/// Indicates why a preview refresh was requested.
/// </summary>
public enum PrintPreviewUpdateKind
{
    /// <summary>
    /// Preview generation triggered when the host is initialised.
    /// </summary>
    Initial,

    /// <summary>
    /// Refresh requested due to updated print options (paper size, margins, etc.).
    /// </summary>
    OptionsChanged,

    /// <summary>
    /// The user navigated to a different page in the preview UI.
    /// </summary>
    PageNavigation,

    /// <summary>
    /// A catch-all refresh request (e.g. diagnostics or manual reload).
    /// </summary>
    RefreshRequested
}
