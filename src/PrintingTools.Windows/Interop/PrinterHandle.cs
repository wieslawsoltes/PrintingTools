using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PrintingTools.Windows.Interop;

internal sealed class PrinterHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public PrinterHandle()
        : base(true)
    {
    }

    public PrinterHandle(IntPtr handle)
        : base(true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        return Win32NativeMethods.ClosePrinter(handle) != 0;
    }
}
