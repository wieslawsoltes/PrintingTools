using System;

namespace PrintingTools.UI.ViewModels;

public sealed class PreviewActionEventArgs : EventArgs
{
    public PreviewActionEventArgs(PreviewAction action)
    {
        Action = action;
    }

    public PreviewAction Action { get; }
}
