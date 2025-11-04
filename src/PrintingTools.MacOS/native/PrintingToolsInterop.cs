using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Win32.SafeHandles;

namespace PrintingTools.MacOS.Native;

internal static partial class PrintingToolsInterop
{
    private const string LibraryName = "PrintingToolsMacBridge";

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_CreatePrintOperation")]
    public static partial IntPtr CreatePrintOperation(IntPtr context);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_DisposePrintOperation")]
    private static partial void DisposePrintOperation(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_BeginPreview")]
    public static partial void BeginPreview(IntPtr operation);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_CommitPrint")]
    public static partial int CommitPrint(IntPtr operation);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_RunModalPrintOperation")]
    public static partial int RunModalPrintOperation(IntPtr operation);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_RunPdfPrintOperation")]
    public static partial int RunPdfPrintOperation(byte[] pdfData, int length, int showPrintPanel);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_RunVectorPreview")]
    public static partial int RunVectorPreview(ref VectorDocument document);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_ConfigurePrintOperation")]
    public static partial void ConfigurePrintOperation(IntPtr operation, ref PrintSettings settings);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_GetPrintInfo")]
    public static partial int GetPrintInfo(IntPtr operation, ref PrintInfo info);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_ShowPageLayout")]
    public static partial int ShowPageLayout(IntPtr operation, ref PrintPanelOptions options);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_ShowPrintPanel")]
    public static partial int ShowPrintPanel(IntPtr operation, ref PrintPanelOptions options);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_ShowPrintPanelSheet")]
    public static partial int ShowPrintPanelSheet(IntPtr operation, IntPtr window, ref PrintPanelOptions options);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_GetPreviewView")]
    public static partial IntPtr GetPreviewView(IntPtr operation);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_CreateHostWindow")]
    public static partial IntPtr CreateHostWindow(double width, double height);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_ShowWindow")]
    public static partial void ShowWindow(IntPtr window);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_DestroyHostWindow")]
    public static partial void DestroyHostWindow(IntPtr window);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_CreateManagedPreviewHost")]
    public static partial IntPtr CreateManagedPreviewHost();

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_DestroyManagedPreviewHost")]
    public static partial void DestroyManagedPreviewHost(IntPtr host);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_GetManagedPreviewView")]
    public static partial IntPtr GetManagedPreviewView(IntPtr host);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_UpdateManagedPreviewWithPdf")]
    public static partial void UpdateManagedPreviewWithPdf(IntPtr host, byte[] pdfData, int length, double dpi);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_SetWindowContent")]
    public static partial void SetWindowContent(IntPtr window, IntPtr view);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_DrawBitmap")]
    public static partial void DrawBitmap(
        IntPtr cgContext,
        IntPtr pixels,
        int width,
        int height,
        int stride,
        double destX,
        double destY,
        double destWidth,
        double destHeight,
        int pixelFormat,
        int colorSpace);

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_GetPrinterNames")]
    public static partial StringArray GetPrinterNames();

    [LibraryImport(LibraryName, EntryPoint = "PrintingTools_FreePrinterNames")]
    public static partial void FreePrinterNames(StringArray array);

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
        public int Copies;
        public int ColorMode;
        public int Duplex;
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
        public double DpiX;
        public double DpiY;
        public int PreferredColorSpace;
        public int LayoutMode;
        public int LayoutNUpRows;
        public int LayoutNUpColumns;
        public int LayoutNUpOrder;
        public int LayoutBookletBinding;
        public int LayoutPosterRows;
        public int LayoutPosterColumns;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PrintInfo
    {
        public double PaperWidth;
        public double PaperHeight;
        public double MarginLeft;
        public double MarginTop;
        public double MarginRight;
        public double MarginBottom;
        public int Orientation;
        public int Copies;
        public int ColorMode;
        public int Duplex;
        public int FromPage;
        public int ToPage;
        public int PageRangeEnabled;
        public int SelectionOnly;
        public IntPtr PaperName;
        public int PaperNameLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PrintPanelOptions
    {
        public IntPtr Operation;
        public int AllowsSelection;
        public int RequestedRangeStart;
        public int RequestedRangeEnd;
        public int RequestedCopies;
        public IntPtr Title;
        public int TitleLength;
    }

    public const int PrintPanelResultCancel = 0;
    public const int PrintPanelResultOK = 1;

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
        public IntPtr LogDiagnostic;
        public IntPtr JobEvent;

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
