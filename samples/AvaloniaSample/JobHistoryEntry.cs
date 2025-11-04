using System;
using PrintingTools.Core;

namespace AvaloniaSample;

public sealed record JobHistoryEntry(DateTimeOffset Timestamp, PrintJobEventKind Kind, string Message);
