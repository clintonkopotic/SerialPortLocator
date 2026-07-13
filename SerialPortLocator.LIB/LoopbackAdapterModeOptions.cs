namespace SerialPortLocator.LIB;

using System.Collections.Generic;

public record LoopbackAdapterModeOptions
{
    public IReadOnlySet<string> PortsToIgnore { get; set; }
        = new HashSet<string>();

    public IReadOnlySet<string> PortsToTest { get; set; }
        = new HashSet<string>();

    public bool RetryPortsOnException { get; set; }

    public bool RetryPortsOnLocate { get; set; }

    public bool OriginalOrderOfPortNames { get; set; }
}
