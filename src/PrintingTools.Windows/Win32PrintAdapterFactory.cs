using System;
using PrintingTools.Core;

namespace PrintingTools.Windows;

public sealed class Win32PrintAdapterFactory
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public IPrintAdapter? CreateAdapter()
    {
        if (!IsSupported)
        {
            return null;
        }

        return new Win32PrintAdapter();
    }
}
