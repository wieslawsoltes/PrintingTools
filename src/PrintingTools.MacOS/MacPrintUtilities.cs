using System;
using System.Runtime.InteropServices;
using PrintingTools.MacOS.Native;

namespace PrintingTools.MacOS;

public static class MacPrintUtilities
{
    public static bool ShowVectorPreview(byte[] pdfBytes, bool showPrintPanel = true)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
        {
            return false;
        }

        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        try
        {
            var document = new PrintingToolsInterop.VectorDocument
            {
                PdfBytes = handle.AddrOfPinnedObject(),
                Length = pdfBytes.Length,
                ShowPrintPanel = showPrintPanel ? 1 : 0
            };

            return PrintingToolsInterop.RunVectorPreview(ref document) != 0;
        }
        finally
        {
            handle.Free();
        }
    }
}
