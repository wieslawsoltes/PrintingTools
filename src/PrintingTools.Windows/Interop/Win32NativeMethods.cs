using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;

namespace PrintingTools.Windows.Interop;

#pragma warning disable CS0649
internal static partial class Win32NativeMethods
{
    private const string WinSpool = "winspool.drv";

    [Flags]
    internal enum PrinterEnumFlags : uint
    {
        PRINTER_ENUM_LOCAL = 0x00000002,
        PRINTER_ENUM_CONNECTIONS = 0x00000004,
        PRINTER_ENUM_NETWORK = 0x00000040,
        PRINTER_ENUM_SHARED = 0x00000020
    }

    [Flags]
    internal enum PrinterAttributes : uint
    {
        PRINTER_ATTRIBUTE_DEFAULT = 0x00000004,
        PRINTER_ATTRIBUTE_NETWORK = 0x00000010,
        PRINTER_ATTRIBUTE_SHAREABLE = 0x00008000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTER_INFO_2
    {
        public string? pServerName;
        public string? pPrinterName;
        public string? pShareName;
        public string? pPortName;
        public string? pDriverName;
        public string? pComment;
        public string? pLocation;
        public IntPtr pDevMode;
        public string? pSepFile;
        public string? pPrintProcessor;
        public string? pDatatype;
        public string? pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DOC_INFO_1
    {
        public string? pDocName;
        public string? pOutputFile;
        public string? pDatatype;
    }

    internal unsafe struct DEVMODE
    {
        public const int CCHDEVICENAME = 32;
        public const int CCHFORMNAME = 32;

        public fixed char dmDeviceName[CCHDEVICENAME];
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public short dmOrientation;
        public short dmPaperSize;
        public short dmPaperLength;
        public short dmPaperWidth;
        public short dmScale;
        public short dmCopies;
        public short dmDefaultSource;
        public short dmPrintQuality;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        public fixed char dmFormName[CCHFORMNAME];
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PRINTPAGERANGE
    {
        public uint nFromPage;
        public uint nToPage;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PRINTER_INFO_9
    {
        public IntPtr pDevMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DEVNAMES
    {
        public ushort wDriverOffset;
        public ushort wDeviceOffset;
        public ushort wOutputOffset;
        public ushort wDefault;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PRINTDLGEX
    {
        public uint lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hDevMode;
        public IntPtr hDevNames;
        public IntPtr hDC;
        public uint Flags;
        public uint Flags2;
        public uint ExclusionFlags;
        public uint nPageRanges;
        public uint nMaxPageRanges;
        public IntPtr lpPageRanges;
        public uint nMinPage;
        public uint nMaxPage;
        public uint nCopies;
        public IntPtr hInstance;
        public IntPtr lpPrintTemplateName;
        public IntPtr lpCallback;
        public uint nPropertyPages;
        public IntPtr lphPropertyPages;
        public uint nStartPage;
        public uint dwResultAction;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct JOB_INFO_2
    {
        public int JobId;
        public string? pPrinterName;
        public string? pMachineName;
        public string? pUserName;
        public string? pDocument;
        public string? pDatatype;
        public string? pStatus;
        public uint Status;
        public uint Priority;
        public uint Position;
        public uint TotalPages;
        public uint PagesPrinted;
        public SYSTEMTIME Submitted;
        public uint Size;
        public IntPtr pDevMode;
        public IntPtr pDriverName;
        public IntPtr pPortName;
        public IntPtr pParameters;
        public IntPtr pSecurityDescriptor;
        public IntPtr pLogonId;
        public IntPtr pNotifyName;
        public IntPtr pPrintProcessor;
        public IntPtr pComment;
        public IntPtr pError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEMTIME
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;
    }

    [LibraryImport(WinSpool, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumPrinters(
        PrinterEnumFlags Flags,
        string? Name,
        uint Level,
        IntPtr pPrinterEnum,
        uint cbBuf,
        out uint pcbNeeded,
        out uint pcReturned);

    [LibraryImport(WinSpool, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [LibraryImport(WinSpool, SetLastError = true)]
    internal static partial int ClosePrinter(IntPtr hPrinter);

    [LibraryImport(WinSpool, EntryPoint = "StartDocPrinterW", SetLastError = true)]
    internal static partial int StartDocPrinter(IntPtr hPrinter, int Level, IntPtr pDocInfo);

    [LibraryImport(WinSpool, EntryPoint = "SetPrinterW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int Command);

    [LibraryImport(WinSpool, SetLastError = true)]
    internal static partial int EndDocPrinter(IntPtr hPrinter);

    [LibraryImport(WinSpool, SetLastError = true)]
    internal static partial int StartPagePrinter(IntPtr hPrinter);

    [LibraryImport(WinSpool, SetLastError = true)]
    internal static partial int EndPagePrinter(IntPtr hPrinter);

    [LibraryImport(WinSpool, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, uint dwCount, out uint dwWritten);

    [LibraryImport(WinSpool, EntryPoint = "GetJobW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetJob(IntPtr hPrinter, uint JobId, uint Level, IntPtr pJob, uint cbBuf, out uint pcbNeeded);

    [LibraryImport(WinSpool, EntryPoint = "DocumentPropertiesW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int DocumentProperties(IntPtr hwnd, IntPtr hPrinter, string pDeviceName, IntPtr pDevModeOutput, IntPtr pDevModeInput, int fMode);

    [LibraryImport("comdlg32.dll", EntryPoint = "PrintDlgExW", SetLastError = true)]
    internal static partial int PrintDlgEx(ref PRINTDLGEX dialog);

    [LibraryImport("kernel32.dll", EntryPoint = "GlobalLock", SetLastError = true)]
    internal static partial IntPtr GlobalLock(IntPtr hMem);

    [LibraryImport("kernel32.dll", EntryPoint = "GlobalUnlock", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalUnlock(IntPtr hMem);

    [LibraryImport("kernel32.dll", EntryPoint = "GlobalFree", SetLastError = true)]
    internal static partial IntPtr GlobalFree(IntPtr hMem);

    [LibraryImport("kernel32.dll", EntryPoint = "GlobalAlloc", SetLastError = true)]
    internal static partial IntPtr GlobalAlloc(uint uFlags, nuint dwBytes);

    [LibraryImport(WinSpool, EntryPoint = "FindFirstPrinterChangeNotification", SetLastError = true)]
    internal static partial IntPtr FindFirstPrinterChangeNotification(IntPtr hPrinter, uint fdwFlags, uint fdwOptions, IntPtr pPrinterNotifyOptions);

    [LibraryImport(WinSpool, EntryPoint = "FindNextPrinterChangeNotification", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FindNextPrinterChangeNotification(IntPtr hChange, out uint pdwChange, IntPtr pNotifyOptions, IntPtr ppdwData);

    [LibraryImport(WinSpool, EntryPoint = "FindClosePrinterChangeNotification", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FindClosePrinterChangeNotification(IntPtr hChange);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    internal static IReadOnlyList<Win32Printer> EnumeratePrinters()
    {
        var flags = PrinterEnumFlags.PRINTER_ENUM_LOCAL | PrinterEnumFlags.PRINTER_ENUM_CONNECTIONS | PrinterEnumFlags.PRINTER_ENUM_SHARED;
        const uint level = 2;

        if (!EnumPrinters(flags, null, level, IntPtr.Zero, 0, out var bytesNeeded, out _))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 122) // ERROR_INSUFFICIENT_BUFFER
            {
                throw new Win32Exception(error, "EnumPrinters failed to compute buffer size.");
            }
        }

        if (bytesNeeded == 0)
        {
            return Array.Empty<Win32Printer>();
        }

        var buffer = Marshal.AllocHGlobal((nint)bytesNeeded);
        try
        {
            if (!EnumPrinters(flags, null, level, buffer, bytesNeeded, out _, out var returned))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "EnumPrinters failed.");
            }

            var printers = new List<Win32Printer>((int)returned);
            var structSize = Marshal.SizeOf<PRINTER_INFO_2>();
            var current = buffer;
            for (var i = 0; i < returned; i++)
            {
                var info = Marshal.PtrToStructure<PRINTER_INFO_2>(current);
                if (info.pPrinterName is not null)
                {
                    printers.Add(new Win32Printer(
                        info.pPrinterName,
                        info.pDriverName ?? string.Empty,
                        info.pPortName ?? string.Empty,
                        info.pLocation ?? string.Empty,
                        info.pComment ?? string.Empty,
                        (PrinterAttributes)info.Attributes));
                }

                current = IntPtr.Add(current, structSize);
            }

            return printers;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static string GetLastErrorMessage()
    {
        var error = Marshal.GetLastWin32Error();
        return new Win32Exception(error).Message;
    }

    internal readonly record struct Win32Printer(
        string Name,
        string DriverName,
        string Port,
        string Location,
        string Comment,
        PrinterAttributes Attributes)
    {
        public bool IsDefault => Attributes.HasFlag(PrinterAttributes.PRINTER_ATTRIBUTE_DEFAULT);
        public bool IsNetwork => Attributes.HasFlag(PrinterAttributes.PRINTER_ATTRIBUTE_NETWORK);
    }

    internal const int DM_ORIENTATION = 0x00000001;
    internal const int DM_COPIES = 0x00000100;
    internal const int DM_COLOR = 0x00000800;
    internal const int DM_DUPLEX = 0x00001000;

    internal const short DMORIENT_PORTRAIT = 1;
    internal const short DMORIENT_LANDSCAPE = 2;

    internal const short DMDUP_SIMPLEX = 1;
    internal const short DMDUP_HORIZONTAL = 2;
    internal const short DMDUP_VERTICAL = 3;

    internal const short DMCOLOR_MONOCHROME = 1;
    internal const short DMCOLOR_COLOR = 2;

    internal const int DM_OUT_BUFFER = 0x00000002;
    internal const int DM_OUT_DEFAULT = 0x00000004;
    internal const int DM_IN_BUFFER = 0x00000008;
    internal const int DM_NUP = 0x00000040;

    internal const uint PD_ALLPAGES = 0x00000000;
    internal const uint PD_SELECTION = 0x00000001;
    internal const uint PD_PAGENUMS = 0x00000002;
    internal const uint PD_NOSELECTION = 0x00000004;
    internal const uint PD_PRINTTOFILE = 0x00000020;
    internal const uint PD_COLLATE = 0x00000010;
    internal const uint PD_USEDEVMODECOPIESANDCOLLATE = 0x00040000;
    internal const uint PD_RETURNDEFAULT = 0x00000400;
    internal const uint PD_RETURNDC = 0x00000100;

    internal const uint PD_RESULT_CANCEL = 0;
    internal const uint PD_RESULT_PRINT = 1;
    internal const uint PD_RESULT_APPLY = 2;

    internal const uint GMEM_FIXED = 0x0000;
    internal const uint GMEM_MOVEABLE = 0x0002;
    internal const uint GMEM_ZEROINIT = 0x0040;

    internal const uint START_PAGE_GENERAL = 0xFFFFFFFF;

    internal const uint WAIT_OBJECT_0 = 0x00000000;
    internal const uint WAIT_TIMEOUT = 0x00000102;
    internal const uint INFINITE = 0xFFFFFFFF;

    internal const uint PRINTER_CHANGE_ADD_JOB = 0x00000100;
    internal const uint PRINTER_CHANGE_SET_JOB = 0x00000200;
    internal const uint PRINTER_CHANGE_DELETE_JOB = 0x00000400;
    internal const uint PRINTER_CHANGE_WRITE_JOB = 0x00000800;
    internal const uint PRINTER_CHANGE_JOB = PRINTER_CHANGE_ADD_JOB | PRINTER_CHANGE_SET_JOB | PRINTER_CHANGE_DELETE_JOB | PRINTER_CHANGE_WRITE_JOB;
}
#pragma warning restore CS0649
