namespace SerialPortLocator.LIB;

using System.Collections.Generic;

public record LoopbackAdapterModeOptions
{
    public IReadOnlySet<string> PortsToIgnore { get; set; }
        = new HashSet<string>();

    public bool RetryPortsOnException { get; set; }
}
