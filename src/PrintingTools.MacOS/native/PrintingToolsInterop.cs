using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PrintingTools.MacOS.Native;

internal static class PrintingToolsInterop
{
    private const string LibraryName = "PrintingToolsMacBridge";

    [DllImport(LibraryName, EntryPoint = "PrintingTools_CreatePrintOperation")]
    public static extern IntPtr CreatePrintOperation(IntPtr context);

    [DllImport(LibraryName, EntryPoint = "PrintingTools_DisposePrintOperation")]
    private static extern void DisposePrintOperation(IntPtr handle);

    [DllImport(LibraryName, EntryPoint = "PrintingTools_BeginPreview")]
    public static extern void BeginPreview(IntPtr operation);

    [DllImport(LibraryName, EntryPoint = "PrintingTools_CommitPrint")]
    public static extern int CommitPrint(IntPtr operation);

    [DllImport(LibraryName, EntryPoint = "PrintingTools_RunModalPrintOperation")]
    public static extern int RunModalPrintOperation(IntPtr operation);

    [DllImport(LibraryName, EntryPoint = "PrintingTools_RunPdfPrintOperation")]
    public static extern int RunPdfPrintOperation(byte[] pdfData, int length, int showPrintPanel);

    [DllImport(LibraryName, EntryPoint = "PrintingTools_RunVectorPreview")]
    public static extern int RunVectorPreview(ref VectorDocument document);

    [DllImport(LibraryName, EntryPoint = "PrintingTools_ConfigurePrintOperation")]
    public static extern void ConfigurePrintOperation(IntPtr operation, ref PrintSettings settings);

    [DllImport(LibraryName, EntryPoint = "PrintingTools_DrawBitmap")]
    public static extern void DrawBitmap(
        IntPtr cgContext,
        IntPtr pixels,
        int width,
        int height,
        int stride,
        double destX,
        double destY,
        double destWidth,
        double destHeight,
        int pixelFormat);

    [DllImport(LibraryName, EntryPoint = "PrintingTools_GetPrinterNames")]
    public static extern StringArray GetPrinterNames();

    [DllImport(LibraryName, EntryPoint = "PrintingTools_FreePrinterNames")]
    public static extern void FreePrinterNames(StringArray array);

    [StructLayout(LayoutKind.Sequential)]
    public struct PrintSettings
    {
        public double PaperWidth;
        public double PaperHeight;
        public double MarginLeft;
        public double MarginTop;
        public double MarginRight;
        public double MarginBottom;
        public int HasPageRange;
        public int FromPage;
        public int ToPage;
        public int Orientation;
        public int ShowPrintPanel;
        public int ShowProgressPanel;
        public IntPtr JobName;
        public int JobNameLength;
        public IntPtr PrinterName;
        public int PrinterNameLength;
        public int EnablePdfExport;
        public IntPtr PdfPath;
        public int PdfPathLength;
        public int PageCount;
    }

    public sealed class PrintOperationHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public PrintOperationHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            DisposePrintOperation(handle);
            return true;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ManagedCallbacks
    {
        public IntPtr Context;
        public IntPtr RenderPage;
        public IntPtr GetPageCount;

        public IntPtr ToNative()
        {
            var size = Marshal.SizeOf<ManagedCallbacks>();
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(this, ptr, false);
            return ptr;
        }

        public static void FreeNative(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StringArray
    {
        public IntPtr Items;
        public IntPtr Lengths;
        public int Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VectorDocument
    {
        public IntPtr PdfBytes;
        public int Length;
        public int ShowPrintPanel;
    }
}
