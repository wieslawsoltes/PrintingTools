using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using PrintingTools.Core;
using PrintingTools.Core.Rendering;
using PrintingTools.Windows.Interop;
using PrintingTools.Windows.Rendering;

namespace PrintingTools.Windows;

/// <summary>
/// Windows implementation of <see cref="IPrintAdapter"/> using Win32/XPS interop rather than System.Printing.
/// </summary>
internal sealed class Win32PrintAdapter : IPrintAdapter
{
    private static readonly Vector TargetPrintDpi = new(300, 300);
    private static readonly Vector TargetPreviewDpi = new(144, 144);
    private const string DiagnosticsCategory = "Win32PrintAdapter";

    private readonly SkiaVectorPageRenderer _vectorRenderer = new();
    private readonly SkiaXpsExporter _xpsExporter = new();

    public Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported();

        IReadOnlyList<PrinterInfo> results;
        try
        {
            var printers = Win32NativeMethods.EnumeratePrinters();
            var list = new List<PrinterInfo>(printers.Count);
            foreach (var printer in printers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Driver"] = printer.DriverName,
                    ["Port"] = printer.Port
                };

                if (!string.IsNullOrWhiteSpace(printer.Location))
                {
                    attributes["Location"] = printer.Location;
                }

                if (!string.IsNullOrWhiteSpace(printer.Comment))
                {
                    attributes["Comment"] = printer.Comment;
                }

                list.Add(new PrinterInfo(
                    new PrinterId(printer.Name),
                    printer.Name,
                    isDefault: printer.IsDefault,
                    isOnline: true,
                    isLocal: !printer.IsNetwork,
                    attributes));
            }

            results = list;
        }
        catch (Win32Exception ex)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Enumerating printers failed.", ex);
            results = Array.Empty<PrinterInfo>();
        }

        return Task.FromResult(results);
    }

    public Task<PrintCapabilities> GetCapabilitiesAsync(PrinterId printerId, PrintTicketModel? baseTicket = null, CancellationToken cancellationToken = default)
    {
        EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        PrintCapabilities capabilities;
        if (!TryBuildCapabilities(printerId, out capabilities))
        {
            capabilities = PrintCapabilities.CreateDefault();
        }

        return Task.FromResult(capabilities);
    }

    public async Task<PrintSession> CreateSessionAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        var options = request.Options?.Clone() ?? new PrintOptions();
        var ticket = (request.Ticket ?? PrintTicketModel.CreateDefault()).Clone();

        var session = new PrintSession(request.Document, options, request.Description, ticket: ticket);

        try
        {
            var printers = await GetPrintersAsync(cancellationToken).ConfigureAwait(false);
            PrinterInfo? selected = null;

            if (request.PreferredPrinterId is { } preferredId)
            {
                foreach (var printer in printers)
                {
                    if (printer.Id == preferredId)
                    {
                        selected = printer;
                        break;
                    }
                }
            }

            selected ??= printers.Count > 0
                ? FindDefaultPrinter(printers)
                : null;

            if (selected is not null)
            {
                session.AssignPrinter(selected);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Failed to select default printer during session creation.", ex);
        }

        return session;
    }

    public Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureSupported();

        var includeVector = session.Options.UseVectorRenderer;
        var preview = PrintPreviewModel.Create(
            session,
            TargetPreviewDpi,
            includeBitmaps: true,
            includeVectorDocument: includeVector,
            vectorRenderer: includeVector ? _vectorRenderer : null,
            cancellationToken);

        return Task.FromResult(preview);
    }

    public async Task PrintAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureSupported();

        if (session.Options.ShowPrintDialog)
        {
            var dialogResult = await ShowPrintDialogAsync(session, cancellationToken).ConfigureAwait(false);
            if (!dialogResult)
            {
                PrintDiagnostics.Report(DiagnosticsCategory, "Print dialog cancelled or failed.", context: new { session.Description });
                return;
            }
            session.Options.ShowPrintDialog = false;
        }

        var pages = PrintRenderPipeline.CollectPages(session, TargetPrintDpi, cancellationToken);
        if (pages.Count == 0)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Print session contained no pages.", context: new { session.Description });
            return;
        }

        var options = session.Options;
        var jobName = session.Description ?? options.JobName ?? "Avalonia Print Job";

        IReadOnlyList<RenderTargetBitmap>? rasterBitmaps = null;
        if (!options.UseVectorRenderer)
        {
            rasterBitmaps = PrintRenderPipeline.RenderBitmaps(pages, TargetPrintDpi, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(options.PdfOutputPath))
        {
            var path = Path.GetFullPath(options.PdfOutputPath);
            if (Path.GetExtension(path).Equals(".xps", StringComparison.OrdinalIgnoreCase))
            {
                _xpsExporter.ExportXps(path, pages, rasterBitmaps);
            }
            else
            {
                _vectorRenderer.ExportPdf(path, pages);
            }

            if (!options.ShowPrintDialog)
            {
                return;
            }
        }

        var printerName = options.PrinterName ?? session.Printer?.Name;
        if (string.IsNullOrWhiteSpace(printerName))
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Printer name is not set; skipping native submission.", context: new { session.Description });
            return;
        }

        var xpsBytes = _xpsExporter.CreateXpsBytes(pages, rasterBitmaps);
        if (xpsBytes.Length == 0)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Generated XPS payload is empty; aborting print.", context: new { session.Description });
            DisposeBitmaps(rasterBitmaps);
            return;
        }

        var layoutMetadata = LayoutMetadata.FromTicket(session.Ticket);

        if (!TrySubmitRawJob(session, printerName, jobName, xpsBytes, layoutMetadata, out var jobId))
        {
            PrintDiagnostics.Report(
                DiagnosticsCategory,
                "Submitting XPS spool job failed.",
                context: new { Printer = printerName, session.Description });
            session.NotifyJobEvent(PrintJobEventKind.Failed, $"Failed to submit print job '{jobName}' to '{printerName}'.");
            DisposeBitmaps(rasterBitmaps);
            return;
        }

        if (jobId > 0)
        {
            session.NotifyJobEvent(PrintJobEventKind.Started, $"Spooling job '{jobName}' to '{printerName}' (Job {jobId}).");
            _ = MonitorJobAsync(session, printerName, jobId, cancellationToken);
        }

        DisposeBitmaps(rasterBitmaps);
    }

    private static PrinterInfo? FindDefaultPrinter(IReadOnlyList<PrinterInfo> printers)
    {
        foreach (var printer in printers)
        {
            if (printer.IsDefault)
            {
                return printer;
            }
        }

        return printers.Count > 0 ? printers[0] : null;
    }

    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Windows print adapter can only be used on Windows.");
        }
    }

    private async Task<bool> ShowPrintDialogAsync(PrintSession session, CancellationToken cancellationToken)
    {
        try
        {
            var printers = await GetPrintersAsync(cancellationToken).ConfigureAwait(false);
            var currentPrinterName = session.Options.PrinterName ?? session.Printer?.Name;

            PrinterInfo? printerInfo = null;
            if (!string.IsNullOrWhiteSpace(currentPrinterName))
            {
                foreach (var printer in printers)
                {
                    if (string.Equals(printer.Name, currentPrinterName, StringComparison.OrdinalIgnoreCase))
                    {
                        printerInfo = printer;
                        break;
                    }
                }
            }

            printerInfo ??= FindDefaultPrinter(printers) ?? printers.FirstOrDefault();

            if (printerInfo is null)
            {
                PrintDiagnostics.Report(DiagnosticsCategory, "ShowPrintDialog: no printers available.");
                return false;
            }

            if (!Win32NativeMethods.OpenPrinter(printerInfo.Name, out var printerHandle, IntPtr.Zero))
            {
                PrintDiagnostics.Report(
                    DiagnosticsCategory,
                    "ShowPrintDialog: OpenPrinter failed.",
                    context: new { Printer = printerInfo.Name, Error = Win32NativeMethods.GetLastErrorMessage() });
                return false;
            }

            using var printerHandleWrapper = new PrinterHandle(printerHandle);

            if (!TryGetDevMode(printerHandleWrapper.DangerousGetHandle(), printerInfo.Name, out var devMode))
            {
                PrintDiagnostics.Report(
                    DiagnosticsCategory,
                    "ShowPrintDialog: Unable to retrieve DEVMODE.",
                    context: new { Printer = printerInfo.Name });
                return false;
            }

            return TryShowPrintDialogCore(session, printers, printerInfo, devMode, printerHandleWrapper.DangerousGetHandle());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "ShowPrintDialog threw an exception.", ex);
            return false;
        }
    }

    private bool TryShowPrintDialogCore(
        PrintSession session,
        IReadOnlyList<PrinterInfo> printers,
        PrinterInfo selectedPrinter,
        Win32NativeMethods.DEVMODE devMode,
        IntPtr printerHandle)
    {
        IntPtr hDevMode = IntPtr.Zero;
        IntPtr hDevNames = IntPtr.Zero;
        IntPtr pageRangesPtr = IntPtr.Zero;

        try
        {
            var devModeSize = Marshal.SizeOf<Win32NativeMethods.DEVMODE>();
            hDevMode = Win32NativeMethods.GlobalAlloc(Win32NativeMethods.GMEM_MOVEABLE | Win32NativeMethods.GMEM_ZEROINIT, (nuint)devModeSize);
            if (hDevMode == IntPtr.Zero)
            {
                PrintDiagnostics.Report(DiagnosticsCategory, "ShowPrintDialog: GlobalAlloc for DEVMODE failed.");
                return false;
            }

            var devModePtr = Win32NativeMethods.GlobalLock(hDevMode);
            Marshal.StructureToPtr(devMode, devModePtr, false);
            Win32NativeMethods.GlobalUnlock(hDevMode);

            hDevNames = CreateDevNamesHandle(selectedPrinter);
            if (hDevNames == IntPtr.Zero)
            {
                PrintDiagnostics.Report(DiagnosticsCategory, "ShowPrintDialog: failed to allocate DEVNAMES.");
                return false;
            }

            uint flags = Win32NativeMethods.PD_USEDEVMODECOPIESANDCOLLATE | Win32NativeMethods.PD_RETURNDC | Win32NativeMethods.PD_ALLPAGES;
            var pageRangeCount = 0u;
            if (session.Options.PageRange is { } range)
            {
                pageRangesPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Win32NativeMethods.PRINTPAGERANGE>());
                var pr = new Win32NativeMethods.PRINTPAGERANGE
                {
                    nFromPage = (uint)Math.Max(range.StartPage, 1),
                    nToPage = (uint)Math.Max(range.EndPage, range.StartPage)
                };
                Marshal.StructureToPtr(pr, pageRangesPtr, false);
                pageRangeCount = 1;
                flags |= Win32NativeMethods.PD_PAGENUMS;
            }

            var dialog = new Win32NativeMethods.PRINTDLGEX
            {
                lStructSize = (uint)Marshal.SizeOf<Win32NativeMethods.PRINTDLGEX>(),
                hwndOwner = IntPtr.Zero,
                hDevMode = hDevMode,
                hDevNames = hDevNames,
                Flags = flags,
                nMinPage = 1,
                nMaxPage = 9999,
                nCopies = (uint)Math.Clamp(devMode.dmCopies > 0 ? devMode.dmCopies : 1, 1, 999),
                nPageRanges = pageRangeCount,
                nMaxPageRanges = pageRangeCount == 0 ? 1u : pageRangeCount,
                lpPageRanges = pageRangesPtr,
                nStartPage = Win32NativeMethods.START_PAGE_GENERAL,
                dwResultAction = Win32NativeMethods.PD_RESULT_CANCEL
            };

            var hr = Win32NativeMethods.PrintDlgEx(ref dialog);
            if (hr != 0)
            {
                PrintDiagnostics.Report(
                    DiagnosticsCategory,
                    "ShowPrintDialog: PrintDlgEx failed.",
                    context: new { HRESULT = hr, Printer = selectedPrinter.Name });
                return false;
            }

            if (dialog.dwResultAction != Win32NativeMethods.PD_RESULT_PRINT)
            {
                return false; // user cancelled
            }

            var updatedDevModePtr = Win32NativeMethods.GlobalLock(dialog.hDevMode);
            var updatedDevMode = Marshal.PtrToStructure<Win32NativeMethods.DEVMODE>(updatedDevModePtr);
            Win32NativeMethods.GlobalUnlock(dialog.hDevMode);

            ApplyDevModeToSession(session, updatedDevMode);

            var printerName = ReadDeviceName(dialog.hDevNames) ?? selectedPrinter.Name;
            session.Options.PrinterName = printerName;
            UpdateSessionPrinter(session, printers, printerName);

            if (dialog.nPageRanges > 0 && dialog.lpPageRanges != IntPtr.Zero)
            {
                var pageRange = Marshal.PtrToStructure<Win32NativeMethods.PRINTPAGERANGE>(dialog.lpPageRanges);
                var start = (int)Math.Max(pageRange.nFromPage, 1);
                var end = (int)Math.Max(pageRange.nToPage, start);
                session.Options.PageRange = new PrintPageRange(start, end);
            }
            else if ((dialog.Flags & Win32NativeMethods.PD_PAGENUMS) == 0)
            {
                session.Options.PageRange = null;
            }

            return true;
        }
        finally
        {
            if (pageRangesPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pageRangesPtr);
            }

            if (hDevMode != IntPtr.Zero)
            {
                Win32NativeMethods.GlobalFree(hDevMode);
            }

            if (hDevNames != IntPtr.Zero)
            {
                Win32NativeMethods.GlobalFree(hDevNames);
            }
        }
    }

    private void ApplyDevModeToSession(PrintSession session, Win32NativeMethods.DEVMODE devMode)
    {
        var ticket = session.Ticket.Clone();

        if ((devMode.dmFields & Win32NativeMethods.DM_ORIENTATION) != 0)
        {
            ticket.Orientation = devMode.dmOrientation == Win32NativeMethods.DMORIENT_LANDSCAPE
                ? PageOrientation.Landscape
                : PageOrientation.Portrait;
        }

        if ((devMode.dmFields & Win32NativeMethods.DM_DUPLEX) != 0)
        {
            ticket.Duplex = devMode.dmDuplex switch
            {
                Win32NativeMethods.DMDUP_HORIZONTAL => DuplexingMode.TwoSidedLongEdge,
                Win32NativeMethods.DMDUP_VERTICAL => DuplexingMode.TwoSidedShortEdge,
                _ => DuplexingMode.OneSided
            };
        }
        else
        {
            ticket.Duplex = DuplexingMode.OneSided;
        }

        if ((devMode.dmFields & Win32NativeMethods.DM_COLOR) != 0)
        {
            ticket.ColorMode = devMode.dmColor == Win32NativeMethods.DMCOLOR_COLOR
                ? ColorMode.Color
                : ColorMode.Monochrome;
        }
        else
        {
            ticket.ColorMode = ColorMode.Auto;
        }

        if ((devMode.dmFields & Win32NativeMethods.DM_COPIES) != 0 && devMode.dmCopies > 0)
        {
            ticket.Copies = Math.Clamp((int)devMode.dmCopies, 1, 999);
        }

        session.UpdateTicket(ticket, adoptWarnings: false);
    }

    private void UpdateSessionPrinter(PrintSession session, IReadOnlyList<PrinterInfo> printers, string printerName)
    {
        PrinterInfo? match = null;
        foreach (var printer in printers)
        {
            if (string.Equals(printer.Name, printerName, StringComparison.OrdinalIgnoreCase))
            {
                match = printer;
                break;
            }
        }

        session.AssignPrinter(match ?? new PrinterInfo(new PrinterId(printerName), printerName));
    }

    private static IntPtr CreateDevNamesHandle(PrinterInfo printer)
    {
        var driverName = printer.Attributes.TryGetValue("Driver", out var driver) ? driver : "winspool";
        var deviceName = printer.Name;
        var outputName = printer.Attributes.TryGetValue("Port", out var port) ? port : "PORT:";

        var structSize = Marshal.SizeOf<Win32NativeMethods.DEVNAMES>();
        var totalChars = driverName.Length + deviceName.Length + outputName.Length + 3;
        var handle = Win32NativeMethods.GlobalAlloc(Win32NativeMethods.GMEM_MOVEABLE | Win32NativeMethods.GMEM_ZEROINIT, (nuint)(structSize + totalChars * sizeof(char)));
        if (handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var ptr = Win32NativeMethods.GlobalLock(handle);
        var devNames = new Win32NativeMethods.DEVNAMES
        {
            wDriverOffset = (ushort)(structSize / sizeof(char)),
            wDeviceOffset = (ushort)(structSize / sizeof(char) + driverName.Length + 1),
            wOutputOffset = (ushort)(structSize / sizeof(char) + driverName.Length + deviceName.Length + 2),
            wDefault = 0
        };

        Marshal.StructureToPtr(devNames, ptr, false);
        WriteUnicodeString(IntPtr.Add(ptr, devNames.wDriverOffset * sizeof(char)), driverName);
        WriteUnicodeString(IntPtr.Add(ptr, devNames.wDeviceOffset * sizeof(char)), deviceName);
        WriteUnicodeString(IntPtr.Add(ptr, devNames.wOutputOffset * sizeof(char)), outputName);
        Win32NativeMethods.GlobalUnlock(handle);
        return handle;
    }

    private static string? ReadDeviceName(IntPtr hDevNames)
    {
        if (hDevNames == IntPtr.Zero)
        {
            return null;
        }

        var locked = Win32NativeMethods.GlobalLock(hDevNames);
        if (locked == IntPtr.Zero)
        {
            locked = hDevNames;
        }

        try
        {
            var devNames = Marshal.PtrToStructure<Win32NativeMethods.DEVNAMES>(locked);
            return Marshal.PtrToStringUni(IntPtr.Add(locked, devNames.wDeviceOffset * sizeof(char)));
        }
        finally
        {
            if (locked != hDevNames)
            {
                Win32NativeMethods.GlobalUnlock(hDevNames);
            }
        }
    }

    private static void WriteUnicodeString(IntPtr destination, string value)
    {
        var bytes = Encoding.Unicode.GetBytes(value + "\0");
        Marshal.Copy(bytes, 0, destination, bytes.Length);
    }

    private static bool TrySubmitRawJob(PrintSession session, string printerName, string documentName, byte[] payload, LayoutMetadata layout, out uint jobId)
    {
        jobId = 0;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (!Win32NativeMethods.OpenPrinter(printerName, out var handle, IntPtr.Zero))
        {
            PrintDiagnostics.Report(
                DiagnosticsCategory,
                "OpenPrinter failed.",
                context: new { Printer = printerName, Error = Win32NativeMethods.GetLastErrorMessage() });
            return false;
        }

        using var printer = new PrinterHandle(handle);

        IntPtr originalDevMode = IntPtr.Zero;
        IntPtr updatedDevMode = IntPtr.Zero;
        IntPtr printerInfoPtr = IntPtr.Zero;
        var devModeApplied = false;

        var docInfo = new Win32NativeMethods.DOC_INFO_1
        {
            pDocName = documentName,
            pDatatype = "RAW",
            pOutputFile = null
        };

        IntPtr docInfoPtr = IntPtr.Zero;
        var docStarted = false;
        int startDocResult = 0;

        try
        {
            if (TryCreateLayoutDevMode(printer.DangerousGetHandle(), printerName, layout, out originalDevMode, out updatedDevMode))
            {
                printerInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Win32NativeMethods.PRINTER_INFO_9>());
                Marshal.StructureToPtr(new Win32NativeMethods.PRINTER_INFO_9 { pDevMode = updatedDevMode }, printerInfoPtr, fDeleteOld: false);

                if (Win32NativeMethods.SetPrinter(printer.DangerousGetHandle(), 9, printerInfoPtr, 0))
                {
                    devModeApplied = true;
                    PrintDiagnostics.Report(
                        DiagnosticsCategory,
                        "Applied layout overrides to DEVMODE.",
                        context: new
                        {
                            Printer = printerName,
                            layout.Kind,
                            layout.NUpRows,
                            layout.NUpColumns,
                            layout.PosterRows,
                            layout.PosterColumns,
                            layout.BookletBindLongEdge
                        });
                }
                else
                {
                    PrintDiagnostics.Report(
                        DiagnosticsCategory,
                        "SetPrinter (layout) failed; continuing with default DEVMODE.",
                        context: new { Printer = printerName, Error = Win32NativeMethods.GetLastErrorMessage() });
                }
            }

            docInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Win32NativeMethods.DOC_INFO_1>());
            Marshal.StructureToPtr(docInfo, docInfoPtr, fDeleteOld: false);

            startDocResult = Win32NativeMethods.StartDocPrinter(printer.DangerousGetHandle(), 1, docInfoPtr);
            if (startDocResult == 0)
            {
                PrintDiagnostics.Report(
                    DiagnosticsCategory,
                    "StartDocPrinter failed.",
                    context: new { Printer = printerName, Error = Win32NativeMethods.GetLastErrorMessage() });
                session.NotifyJobEvent(PrintJobEventKind.Failed, $"StartDocPrinter failed for '{printerName}'.");
                return false;
            }

            docStarted = true;
            jobId = (uint)startDocResult;

            if (Win32NativeMethods.StartPagePrinter(printer.DangerousGetHandle()) == 0)
            {
                PrintDiagnostics.Report(
                    DiagnosticsCategory,
                    "StartPagePrinter failed.",
                    context: new { Printer = printerName, Error = Win32NativeMethods.GetLastErrorMessage() });
                session.NotifyJobEvent(PrintJobEventKind.Failed, $"StartPagePrinter failed for '{printerName}'.");
                return false;
            }

            try
            {
                var buffer = Marshal.AllocHGlobal(payload.Length);
                try
                {
                    Marshal.Copy(payload, 0, buffer, payload.Length);
                    if (!Win32NativeMethods.WritePrinter(printer.DangerousGetHandle(), buffer, (uint)payload.Length, out var written) ||
                        written != payload.Length)
                    {
                        PrintDiagnostics.Report(
                            DiagnosticsCategory,
                            "WritePrinter failed.",
                            context: new
                            {
                                Printer = printerName,
                                Expected = payload.Length,
                                Written = written,
                                Error = Win32NativeMethods.GetLastErrorMessage()
                            });
                        session.NotifyJobEvent(PrintJobEventKind.Failed, $"WritePrinter failed for '{printerName}' after spooling {written} of {payload.Length} bytes.");
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                Win32NativeMethods.EndPagePrinter(printer.DangerousGetHandle());
            }
        }
        finally
        {
            if (docStarted)
            {
                Win32NativeMethods.EndDocPrinter(printer.DangerousGetHandle());
            }

            if (docInfoPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(docInfoPtr);
            }

            if (devModeApplied && printerInfoPtr != IntPtr.Zero && originalDevMode != IntPtr.Zero)
            {
                Marshal.StructureToPtr(new Win32NativeMethods.PRINTER_INFO_9 { pDevMode = originalDevMode }, printerInfoPtr, fDeleteOld: false);
                Win32NativeMethods.SetPrinter(printer.DangerousGetHandle(), 9, printerInfoPtr, 0);
            }

            if (printerInfoPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(printerInfoPtr);
            }

            if (updatedDevMode != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(updatedDevMode);
            }

            if (originalDevMode != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(originalDevMode);
            }
        }

        PrintDiagnostics.Report(
            DiagnosticsCategory,
            "Submitted XPS payload via Win32 spooler.",
            context: new { Printer = printerName, Bytes = payload.Length, JobId = jobId, layout.Kind });

        return true;
    }

    private static bool TryCreateLayoutDevMode(IntPtr printerHandle, string printerName, LayoutMetadata layout, out IntPtr originalDevMode, out IntPtr updatedDevMode)
    {
        originalDevMode = IntPtr.Zero;
        updatedDevMode = IntPtr.Zero;

        var size = Win32NativeMethods.DocumentProperties(IntPtr.Zero, printerHandle, printerName, IntPtr.Zero, IntPtr.Zero, 0);
        if (size <= 0)
        {
            return false;
        }

        originalDevMode = Marshal.AllocHGlobal(size);
        updatedDevMode = Marshal.AllocHGlobal(size);

        if (Win32NativeMethods.DocumentProperties(IntPtr.Zero, printerHandle, printerName, originalDevMode, IntPtr.Zero, Win32NativeMethods.DM_OUT_BUFFER | Win32NativeMethods.DM_OUT_DEFAULT) < 0)
        {
            Marshal.FreeHGlobal(originalDevMode);
            Marshal.FreeHGlobal(updatedDevMode);
            originalDevMode = IntPtr.Zero;
            updatedDevMode = IntPtr.Zero;
            return false;
        }

        unsafe
        {
            Buffer.MemoryCopy((void*)originalDevMode, (void*)updatedDevMode, size, size);
        }

        var devMode = Marshal.PtrToStructure<Win32NativeMethods.DEVMODE>(updatedDevMode);
        if (!ApplyLayoutToDevMode(layout, ref devMode))
        {
            Marshal.FreeHGlobal(originalDevMode);
            Marshal.FreeHGlobal(updatedDevMode);
            originalDevMode = IntPtr.Zero;
            updatedDevMode = IntPtr.Zero;
            return false;
        }

        Marshal.StructureToPtr(devMode, updatedDevMode, fDeleteOld: false);

        if (Win32NativeMethods.DocumentProperties(IntPtr.Zero, printerHandle, printerName, updatedDevMode, updatedDevMode, Win32NativeMethods.DM_IN_BUFFER | Win32NativeMethods.DM_OUT_BUFFER) < 0)
        {
            Marshal.FreeHGlobal(originalDevMode);
            Marshal.FreeHGlobal(updatedDevMode);
            originalDevMode = IntPtr.Zero;
            updatedDevMode = IntPtr.Zero;
            return false;
        }

        return true;
    }

    internal static bool ApplyLayoutToDevMode(LayoutMetadata layout, ref Win32NativeMethods.DEVMODE devMode)
    {
        var originalFields = devMode.dmFields;
        var originalDisplayFlags = devMode.dmDisplayFlags;
        var originalDuplex = devMode.dmDuplex;

        var modified = false;

        if ((devMode.dmFields & (uint)Win32NativeMethods.DM_NUP) != 0)
        {
            devMode.dmFields &= ~(uint)Win32NativeMethods.DM_NUP;
            modified = true;
        }

        devMode.dmDisplayFlags = 0;

        if (layout.Kind == PrintLayoutKind.NUp)
        {
            devMode.dmFields |= (uint)Win32NativeMethods.DM_NUP;
            devMode.dmDisplayFlags = (uint)Math.Clamp(layout.NUpTileCount, 1, 16);
            modified = true;
        }
        else if (layout.Kind == PrintLayoutKind.Poster)
        {
            devMode.dmFields |= (uint)Win32NativeMethods.DM_NUP;
            devMode.dmDisplayFlags = (uint)Math.Max(1, layout.PosterTileCount);
            modified = true;
        }

        if (layout.Kind == PrintLayoutKind.Booklet)
        {
            var desiredDuplex = layout.BookletBindLongEdge
                ? Win32NativeMethods.DMDUP_HORIZONTAL
                : Win32NativeMethods.DMDUP_VERTICAL;

            if ((devMode.dmFields & (uint)Win32NativeMethods.DM_DUPLEX) == 0 || devMode.dmDuplex != desiredDuplex)
            {
                devMode.dmFields |= (uint)Win32NativeMethods.DM_DUPLEX;
                devMode.dmDuplex = desiredDuplex;
                modified = true;
            }
        }

        if (devMode.dmDisplayFlags != originalDisplayFlags)
        {
            modified = true;
        }

        if (devMode.dmFields != originalFields)
        {
            modified = true;
        }

        if (devMode.dmDuplex != originalDuplex)
        {
            modified = true;
        }

        return modified;
    }

    private static void DisposeBitmaps(IReadOnlyList<RenderTargetBitmap>? bitmaps)
    {
        if (bitmaps is null)
        {
            return;
        }

        foreach (var bitmap in bitmaps)
        {
            bitmap?.Dispose();
        }
    }

    private static async Task MonitorJobAsync(PrintSession session, string printerName, uint jobId, CancellationToken cancellationToken)
    {
        try
        {
            if (!Win32NativeMethods.OpenPrinter(printerName, out var handle, IntPtr.Zero))
            {
                PrintDiagnostics.Report(
                    DiagnosticsCategory,
                    "Monitor: OpenPrinter failed.",
                    context: new { Printer = printerName, JobId = jobId, Error = Win32NativeMethods.GetLastErrorMessage() });
                session.NotifyJobEvent(PrintJobEventKind.Failed, $"Unable to monitor job {jobId} on '{printerName}': OpenPrinter failed.");
                return;
            }

            using var printer = new PrinterHandle(handle);
            IntPtr buffer = IntPtr.Zero;
            IntPtr changeHandle = IntPtr.Zero;

            try
            {
                if (!TryGetJob(printer.DangerousGetHandle(), jobId, ref buffer, out var initialInfo))
                {
                    PrintDiagnostics.Report(
                        DiagnosticsCategory,
                        "Monitor: initial GetJob failed; assuming completion.",
                        context: new { Printer = printerName, JobId = jobId, Error = Win32NativeMethods.GetLastErrorMessage() });
                    session.NotifyJobEvent(PrintJobEventKind.Unknown, $"Unable to query job {jobId} on '{printerName}'.");
                    return;
                }

                LogJobStatus(session, printerName, jobId, initialInfo);
                if (IsJobComplete(initialInfo.Status) || IsJobError(initialInfo.Status))
                {
                    if (IsJobComplete(initialInfo.Status))
                    {
                        session.NotifyJobEvent(PrintJobEventKind.Completed, $"Job {jobId} on '{printerName}' completed.");
                    }
                    else if (IsJobError(initialInfo.Status))
                    {
                        session.NotifyJobEvent(PrintJobEventKind.Failed, $"Job {jobId} on '{printerName}' reported error state: {DescribeJobStatus(initialInfo.Status)}");
                    }
                    return;
                }

                changeHandle = Win32NativeMethods.FindFirstPrinterChangeNotification(printer.DangerousGetHandle(), Win32NativeMethods.PRINTER_CHANGE_JOB, 0, IntPtr.Zero);
                var notificationsEnabled = changeHandle != IntPtr.Zero && changeHandle.ToInt64() != -1;
                if (notificationsEnabled)
                {
                    PrintDiagnostics.Report(
                        DiagnosticsCategory,
                        "Monitor: using printer change notifications.",
                        context: new { Printer = printerName, JobId = jobId });
                    session.NotifyJobEvent(PrintJobEventKind.Unknown, $"Monitoring job {jobId} on '{printerName}'.");
                }

                var stopwatch = Stopwatch.StartNew();
                const int pollIntervalMs = 1000;
                const int maxDurationMs = 60000;

                while (!cancellationToken.IsCancellationRequested && stopwatch.ElapsedMilliseconds <= maxDurationMs)
                {
                    if (notificationsEnabled)
                    {
                        var waitResult = Win32NativeMethods.WaitForSingleObject(changeHandle, (uint)pollIntervalMs);
                        if (waitResult == Win32NativeMethods.WAIT_OBJECT_0)
                        {
                            if (!Win32NativeMethods.FindNextPrinterChangeNotification(changeHandle, out var change, IntPtr.Zero, IntPtr.Zero))
                            {
                                notificationsEnabled = false;
                            }
                            else if ((change & Win32NativeMethods.PRINTER_CHANGE_JOB) == 0)
                            {
                                continue;
                            }
                        }
                        else if (waitResult != Win32NativeMethods.WAIT_TIMEOUT)
                        {
                            notificationsEnabled = false;
                        }
                    }
                    else
                    {
                        await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
                    }

                    if (!TryGetJob(printer.DangerousGetHandle(), jobId, ref buffer, out var info))
                    {
                        PrintDiagnostics.Report(
                            DiagnosticsCategory,
                            "Monitor: GetJob failed; assuming completion.",
                            context: new { Printer = printerName, JobId = jobId, Error = Win32NativeMethods.GetLastErrorMessage() });
                        session.NotifyJobEvent(PrintJobEventKind.Unknown, $"Unable to continue monitoring job {jobId} on '{printerName}'.");
                        return;
                    }

                    LogJobStatus(session, printerName, jobId, info);

                    if (IsJobComplete(info.Status))
                    {
                        PrintDiagnostics.Report(
                            DiagnosticsCategory,
                            "Monitor: job completed.",
                            context: new { Printer = printerName, JobId = jobId });
                        session.NotifyJobEvent(PrintJobEventKind.Completed, $"Job {jobId} on '{printerName}' completed.");
                        return;
                    }

                    if (IsJobError(info.Status))
                    {
                        PrintDiagnostics.Report(
                            DiagnosticsCategory,
                            "Monitor: job error detected.",
                            context: new { Printer = printerName, JobId = jobId, StatusFlags = info.Status });
                        session.NotifyJobEvent(PrintJobEventKind.Failed, $"Job {jobId} on '{printerName}' reported error state: {DescribeJobStatus(info.Status)}");
                        return;
                    }
                }

                session.NotifyJobEvent(PrintJobEventKind.Unknown, $"Stopped monitoring job {jobId} on '{printerName}'.");
            }
            finally
            {
                if (changeHandle != IntPtr.Zero && changeHandle.ToInt64() != -1)
                {
                    Win32NativeMethods.FindClosePrinterChangeNotification(changeHandle);
                }

                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        catch (OperationCanceledException)
        {
            PrintDiagnostics.Report(
                DiagnosticsCategory,
                "Monitor: cancelled.",
                context: new { Printer = printerName, JobId = jobId });
            session.NotifyJobEvent(PrintJobEventKind.Cancelled, $"Monitoring cancelled for job {jobId} on '{printerName}'.");
        }
        catch (Exception ex)
        {
            PrintDiagnostics.Report(
                DiagnosticsCategory,
                "Monitor: exception raised.",
                ex,
                new { Printer = printerName, JobId = jobId });
            session.NotifyJobEvent(PrintJobEventKind.Failed, $"Exception while monitoring job {jobId} on '{printerName}': {ex.Message}");
        }
    }

    private static void LogJobStatus(PrintSession session, string printerName, uint jobId, Win32NativeMethods.JOB_INFO_2 info)
    {
        var statusDescription = DescribeJobStatus(info.Status);

        PrintDiagnostics.Report(
            DiagnosticsCategory,
            "Monitor: job status update.",
            context: new
            {
                Printer = printerName,
                JobId = jobId,
                StatusFlags = info.Status,
                info.PagesPrinted,
                info.TotalPages
            });

        var message = $"Job {jobId} on '{printerName}' status: {statusDescription} ({info.PagesPrinted}/{info.TotalPages}).";
        session.NotifyJobEvent(PrintJobEventKind.Unknown, message);
    }

    private static string DescribeJobStatus(uint status)
    {
        if (status == 0)
        {
            return "Pending";
        }

        var states = new List<string>();

        if ((status & JobStatusPrinted) != 0)
        {
            states.Add("Printed");
        }
        if ((status & JobStatusComplete) != 0)
        {
            states.Add("Complete");
        }
        if ((status & JobStatusError) != 0)
        {
            states.Add("Error");
        }
        if ((status & JobStatusBlocked) != 0)
        {
            states.Add("Blocked");
        }
        if ((status & JobStatusUserIntervention) != 0)
        {
            states.Add("Needs Attention");
        }

        return states.Count > 0 ? string.Join(", ", states) : $"0x{status:X}";
    }

    private static bool TryGetJob(IntPtr printerHandle, uint jobId, ref IntPtr buffer, out Win32NativeMethods.JOB_INFO_2 info)
    {
        info = default;
        const uint level = 2;

        if (buffer == IntPtr.Zero)
        {
            if (!Win32NativeMethods.GetJob(printerHandle, jobId, level, IntPtr.Zero, 0, out var bytesNeeded) && Marshal.GetLastWin32Error() != 122)
            {
                return false;
            }

            if (bytesNeeded == 0)
            {
                return false;
            }

            buffer = Marshal.AllocHGlobal((nint)bytesNeeded);
        }

        if (!Win32NativeMethods.GetJob(printerHandle, jobId, level, buffer, (uint)Marshal.SizeOf<Win32NativeMethods.JOB_INFO_2>(), out var required))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 122)
            {
                return false;
            }

            Marshal.FreeHGlobal(buffer);
            buffer = Marshal.AllocHGlobal((nint)required);
            if (!Win32NativeMethods.GetJob(printerHandle, jobId, level, buffer, required, out _))
            {
                return false;
            }
        }

        info = Marshal.PtrToStructure<Win32NativeMethods.JOB_INFO_2>(buffer);
        return true;
    }

    private static bool IsJobComplete(uint status) => (status & (JobStatusPrinted | JobStatusComplete)) != 0;

    private static bool IsJobError(uint status) => (status & (JobStatusError | JobStatusBlocked | JobStatusUserIntervention)) != 0;

    private const uint JobStatusError = 0x00000002;
    private const uint JobStatusPrinted = 0x00000080;
    private const uint JobStatusComplete = 0x00001000;
    private const uint JobStatusBlocked = 0x00000200;
    private const uint JobStatusUserIntervention = 0x00000400;

    private static bool TryBuildCapabilities(PrinterId printerId, out PrintCapabilities capabilities)
    {
        capabilities = PrintCapabilities.CreateDefault();

        try
        {
            if (!Win32NativeMethods.OpenPrinter(printerId.Value, out var handle, IntPtr.Zero))
            {
                PrintDiagnostics.Report(
                    DiagnosticsCategory,
                    "OpenPrinter for capabilities failed.",
                    context: new { Printer = printerId.Value, Error = Win32NativeMethods.GetLastErrorMessage() });
                return false;
            }

            using var printer = new PrinterHandle(handle);
            if (!TryGetDevMode(printer.DangerousGetHandle(), printerId.Value, out var devMode))
            {
                return false;
            }

            var baseCaps = PrintCapabilities.CreateDefault();
            var pageSizes = baseCaps.PageMediaSizes;

            var orientations = new List<PageOrientation>();
            orientations.Add(PageOrientation.Portrait);
            orientations.Add(PageOrientation.Landscape);

            var duplexSupport = DuplexingSupport.None;
            if ((devMode.dmFields & Win32NativeMethods.DM_DUPLEX) != 0)
            {
                switch (devMode.dmDuplex)
                {
                    case Win32NativeMethods.DMDUP_HORIZONTAL:
                        duplexSupport |= DuplexingSupport.LongEdge;
                        break;
                    case Win32NativeMethods.DMDUP_VERTICAL:
                        duplexSupport |= DuplexingSupport.ShortEdge;
                        break;
                }
            }

            var colorModes = new List<ColorMode>();
            if ((devMode.dmFields & Win32NativeMethods.DM_COLOR) != 0)
            {
                if (devMode.dmColor == Win32NativeMethods.DMCOLOR_COLOR)
                {
                    colorModes.Add(ColorMode.Color);
                    colorModes.Add(ColorMode.Monochrome);
                }
                else
                {
                    colorModes.Add(ColorMode.Monochrome);
                }
            }

            if (colorModes.Count == 0)
            {
                colorModes.Add(ColorMode.Auto);
            }

            var copies = new List<int>();
            if ((devMode.dmFields & Win32NativeMethods.DM_COPIES) != 0 && devMode.dmCopies > 0)
            {
                var maxCopies = Math.Clamp((int)devMode.dmCopies, 1, 999);
                for (var i = 1; i <= maxCopies; i++)
                {
                    copies.Add(i);
                }
            }
            else
            {
                copies.Add(1);
            }

            if (duplexSupport == DuplexingSupport.None)
            {
                duplexSupport = DuplexingSupport.None;
            }

            capabilities = new PrintCapabilities(
                pageSizes,
                new ReadOnlyCollection<PageOrientation>(orientations),
                duplexSupport,
                new ReadOnlyCollection<ColorMode>(colorModes),
                new ReadOnlyCollection<int>(copies));

            return true;
        }
        catch (Exception ex)
        {
            PrintDiagnostics.Report(
                DiagnosticsCategory,
                "Building capabilities failed.",
                ex,
                new { Printer = printerId.Value });
            return false;
        }
    }

    private static bool TryGetDevMode(IntPtr printerHandle, string printerName, out Win32NativeMethods.DEVMODE devMode)
    {
        devMode = default;

        int size = Win32NativeMethods.DocumentProperties(IntPtr.Zero, printerHandle, printerName, IntPtr.Zero, IntPtr.Zero, 0);
        if (size <= 0)
        {
            return false;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            int result = Win32NativeMethods.DocumentProperties(IntPtr.Zero, printerHandle, printerName, buffer, IntPtr.Zero, Win32NativeMethods.DM_OUT_BUFFER | Win32NativeMethods.DM_OUT_DEFAULT);
            if (result < 0)
            {
                return false;
            }

            devMode = Marshal.PtrToStructure<Win32NativeMethods.DEVMODE>(buffer);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
