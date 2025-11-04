using System;
using PrintingTools.Core;

namespace PrintingTools.Linux;

public sealed class LinuxPrintAdapterFactory
{
    public bool IsSupported => OperatingSystem.IsLinux() && CupsCommandClient.IsInstalled();

    public IPrintAdapter? CreateAdapter()
    {
        if (!IsSupported)
        {
            return null;
        }

        return new LinuxPrintAdapter();
    }
}
