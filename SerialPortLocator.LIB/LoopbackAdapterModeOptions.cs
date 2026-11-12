namespace SerialPortLocator.LIB;

using System;
using System.Collections.Generic;
using System.IO;
using NaturalSort.Extension;

public record LoopbackAdapterModeOptions
{
    public IFormatHelper FormatHelper { get; set; } = new FormatHelper();

    public IReadOnlySet<string> PortsToIgnore { get; set; }
        = new HashSet<string>();

    public IReadOnlySet<string> PortsToTest { get; set; }
        = new HashSet<string>();

    public bool KeepOriginalOrderOfPortNames { get; set; }

    public bool RetryPortsOnException { get; set; }

    public bool RetryPortsOnLocate { get; set; }

    public IComparer<string> StringComparerForSort { get; set; }
        = StringComparison.OrdinalIgnoreCase.WithNaturalSort();

    public TextWriter StatusWriter { get; set; } = TextWriter.Null;
}
