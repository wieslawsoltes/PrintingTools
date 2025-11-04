using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PrintingTools.Core;

public readonly struct PrinterId : IEquatable<PrinterId>
{
    public PrinterId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Printer id cannot be null or whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public bool Equals(PrinterId other) =>
        StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);

    public override bool Equals(object? obj) =>
        obj is PrinterId other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(PrinterId left, PrinterId right) => left.Equals(right);

    public static bool operator !=(PrinterId left, PrinterId right) => !left.Equals(right);

    public static implicit operator PrinterId(string value) => new(value);
}

public sealed class PrinterInfo
{
    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase));

    public PrinterInfo(
        PrinterId id,
        string name,
        bool isDefault = false,
        bool isOnline = true,
        bool isLocal = true,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Printer name cannot be null or whitespace.", nameof(name));
        }

        Id = id;
        Name = name;
        IsDefault = isDefault;
        IsOnline = isOnline;
        IsLocal = isLocal;
        Attributes = attributes ?? EmptyAttributes;
    }

    public PrinterId Id { get; }

    public string Name { get; }

    public bool IsDefault { get; }

    public bool IsOnline { get; }

    public bool IsLocal { get; }

    public IReadOnlyDictionary<string, string> Attributes { get; }
}

public enum PageOrientation
{
    Portrait,
    Landscape
}

[Flags]
public enum DuplexingSupport
{
    None = 0,
    LongEdge = 1,
    ShortEdge = 2
}

public enum DuplexingMode
{
    OneSided,
    TwoSidedLongEdge,
    TwoSidedShortEdge
}

public enum ColorMode
{
    Auto,
    Monochrome,
    Color
}

public sealed class PageMediaSize : IEquatable<PageMediaSize>
{
    public PageMediaSize(string name, double width, double height, string unit = "Points")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Page media size name cannot be null or whitespace.", nameof(name));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit cannot be null or whitespace.", nameof(unit));
        }

        Name = name;
        Width = width;
        Height = height;
        Unit = unit;
    }

    public string Name { get; }

    public double Width { get; }

    public double Height { get; }

    public string Unit { get; }

    public bool Equals(PageMediaSize? other) =>
        other is not null && StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name);

    public override bool Equals(object? obj) =>
        obj is PageMediaSize other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    public override string ToString() =>
        $"{Name} ({Width:F0} x {Height:F0} {Unit})";
}

public static class CommonPageMediaSizes
{
    public static PageMediaSize Letter { get; } = new("Letter", 612d, 792d);

    public static PageMediaSize Legal { get; } = new("Legal", 612d, 1008d);

    public static PageMediaSize Tabloid { get; } = new("Tabloid", 792d, 1224d);

    public static PageMediaSize A4 { get; } = new("A4", 595d, 842d);
}

public sealed class PageMediaSizeInfo
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase));

    public PageMediaSizeInfo(PageMediaSize size, bool isDefault = false, IReadOnlyDictionary<string, string>? metadata = null)
    {
        Size = size ?? throw new ArgumentNullException(nameof(size));
        IsDefault = isDefault;
        Metadata = metadata ?? EmptyMetadata;
    }

    public PageMediaSize Size { get; }

    public bool IsDefault { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}

public readonly struct CapabilityWarning
{
    public CapabilityWarning(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Warning code cannot be null or whitespace.", nameof(code));
        }

        Code = code;
        Message = message ?? string.Empty;
    }

    public string Code { get; }

    public string Message { get; }

    public override string ToString() => $"{Code}: {Message}";

    public static CapabilityWarning Create(string code, string message) => new(code, message);
}

public sealed class PrintCapabilities
{
    public PrintCapabilities(
        IReadOnlyList<PageMediaSizeInfo> pageMediaSizes,
        IReadOnlyList<PageOrientation> orientations,
        DuplexingSupport duplexing,
        IReadOnlyList<ColorMode> colorModes,
        IReadOnlyList<int> supportedCopyCounts,
        IReadOnlyDictionary<string, string>? extensions = null)
    {
        PageMediaSizes = pageMediaSizes ?? throw new ArgumentNullException(nameof(pageMediaSizes));
        Orientations = orientations ?? throw new ArgumentNullException(nameof(orientations));
        Duplexing = duplexing;
        ColorModes = colorModes ?? throw new ArgumentNullException(nameof(colorModes));
        SupportedCopyCounts = supportedCopyCounts ?? throw new ArgumentNullException(nameof(supportedCopyCounts));
        Extensions = extensions ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyList<PageMediaSizeInfo> PageMediaSizes { get; }

    public IReadOnlyList<PageOrientation> Orientations { get; }

    public DuplexingSupport Duplexing { get; }

    public IReadOnlyList<ColorMode> ColorModes { get; }

    public IReadOnlyList<int> SupportedCopyCounts { get; }

    public IReadOnlyDictionary<string, string> Extensions { get; }

    public PageMediaSize? GetDefaultPageMediaSize() =>
        PageMediaSizes.FirstOrDefault(info => info.IsDefault)?.Size ?? PageMediaSizes.FirstOrDefault()?.Size;

    public PageOrientation GetDefaultOrientation() =>
        Orientations.Count > 0 ? Orientations[0] : PageOrientation.Portrait;

    public ColorMode GetDefaultColorMode() =>
        ColorModes.Count > 0 ? ColorModes[0] : ColorMode.Auto;

    public int GetDefaultCopies() =>
        SupportedCopyCounts.Count > 0 ? SupportedCopyCounts[0] : 1;

    public static PrintCapabilities CreateDefault()
    {
        var sizes = new[]
        {
            new PageMediaSizeInfo(CommonPageMediaSizes.Letter, isDefault: true),
            new PageMediaSizeInfo(CommonPageMediaSizes.A4),
            new PageMediaSizeInfo(CommonPageMediaSizes.Legal),
            new PageMediaSizeInfo(CommonPageMediaSizes.Tabloid)
        };

        var orientations = new[] { PageOrientation.Portrait, PageOrientation.Landscape };
        var colorModes = new[] { ColorMode.Color, ColorMode.Monochrome };
        var copies = Enumerable.Range(1, 99).ToArray();

        return new PrintCapabilities(
            new ReadOnlyCollection<PageMediaSizeInfo>(sizes),
            new ReadOnlyCollection<PageOrientation>(orientations),
            DuplexingSupport.LongEdge | DuplexingSupport.ShortEdge,
            new ReadOnlyCollection<ColorMode>(colorModes),
            new ReadOnlyCollection<int>(copies));
    }
}

public sealed class PrintTicketModel
{
    private readonly List<CapabilityWarning> _warnings = new();

    public PrintTicketModel()
        : this(CommonPageMediaSizes.Letter)
    {
    }

    public PrintTicketModel(PageMediaSize pageMediaSize)
    {
        PageMediaSize = pageMediaSize ?? throw new ArgumentNullException(nameof(pageMediaSize));
        Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public PageMediaSize PageMediaSize { get; set; }

    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    public DuplexingMode Duplex { get; set; } = DuplexingMode.OneSided;

    public ColorMode ColorMode { get; set; } = ColorMode.Auto;

    public int Copies { get; set; } = 1;

    public IDictionary<string, string> Extensions { get; }

    public IReadOnlyList<CapabilityWarning> Warnings => _warnings.AsReadOnly();

    public PrintTicketModel Clone()
    {
        var clone = new PrintTicketModel(PageMediaSize)
        {
            Orientation = Orientation,
            Duplex = Duplex,
            ColorMode = ColorMode,
            Copies = Copies
        };

        foreach (var kvp in Extensions)
        {
            clone.Extensions[kvp.Key] = kvp.Value;
        }

        clone._warnings.AddRange(_warnings);
        return clone;
    }

    public PrintTicketModel MergeWithCapabilities(PrintCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var merged = Clone();
        merged._warnings.Clear();

        if (!capabilities.PageMediaSizes.Any(info => info.Size.Equals(merged.PageMediaSize)))
        {
            if (capabilities.GetDefaultPageMediaSize() is { } fallbackSize)
            {
                merged.PageMediaSize = fallbackSize;
                merged._warnings.Add(CapabilityWarning.Create("PageMediaSizeUnsupported", $"Requested page size not supported. Using '{fallbackSize.Name}'."));
            }
        }

        if (capabilities.Orientations.Count > 0 && !capabilities.Orientations.Contains(merged.Orientation))
        {
            var fallbackOrientation = capabilities.GetDefaultOrientation();
            merged.Orientation = fallbackOrientation;
            merged._warnings.Add(CapabilityWarning.Create("OrientationUnsupported", $"Requested orientation not supported. Using {fallbackOrientation}."));
        }

        if (!IsDuplexSupported(merged.Duplex, capabilities.Duplexing))
        {
            merged.Duplex = DuplexingMode.OneSided;
            merged._warnings.Add(CapabilityWarning.Create("DuplexUnsupported", "Requested duplex setting not supported. Using one-sided."));
        }

        if (capabilities.ColorModes.Count > 0 && !capabilities.ColorModes.Contains(merged.ColorMode))
        {
            var fallbackColor = capabilities.GetDefaultColorMode();
            merged.ColorMode = fallbackColor;
            merged._warnings.Add(CapabilityWarning.Create("ColorModeUnsupported", $"Requested color mode not supported. Using {fallbackColor}."));
        }

        if (capabilities.SupportedCopyCounts.Count > 0 && !capabilities.SupportedCopyCounts.Contains(merged.Copies))
        {
            var fallbackCopies = capabilities.GetDefaultCopies();
            merged.Copies = fallbackCopies;
            merged._warnings.Add(CapabilityWarning.Create("CopiesUnsupported", $"Requested copy count not supported. Using {fallbackCopies}."));
        }

        return merged;
    }

    public static PrintTicketModel CreateDefault() => new();

    public void ClearWarnings() => _warnings.Clear();

    public void AddWarning(string code, string message) => _warnings.Add(CapabilityWarning.Create(code, message));

    public void AdoptWarningsFrom(PrintTicketModel ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        _warnings.Clear();
        _warnings.AddRange(ticket._warnings);
    }

    private static bool IsDuplexSupported(DuplexingMode mode, DuplexingSupport support) =>
        mode switch
        {
            DuplexingMode.TwoSidedLongEdge => support.HasFlag(DuplexingSupport.LongEdge),
            DuplexingMode.TwoSidedShortEdge => support.HasFlag(DuplexingSupport.ShortEdge),
            _ => true
        };
}
