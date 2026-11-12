namespace SerialPortLocator.LIB;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class LoopbackAdapterModeTester
{
    private readonly Dictionary<string, string> _errorsForTestedPorts = [];

    private readonly Dictionary<string, string> _notesForTestedPorts = [];

    private readonly ISet<string> _portsTested;

    public IFormatHelper FormatHelper { get; }

    public LoopbackAdapterModeOptions Options { get; }

    public ISet<string> PortsToIgnore { get; }

    public ISet<string> PortsToTest { get; }

    public TextWriter StatusWriter { get; }

    public LoopbackAdapterModeTester() : this(
        cancellationToken: CancellationToken.None)
    { }

    public LoopbackAdapterModeTester(CancellationToken cancellationToken)
        : this(options: new LoopbackAdapterModeOptions(),
              cancellationToken: cancellationToken)
    { }

    public LoopbackAdapterModeTester(LoopbackAdapterModeOptions options,
        CancellationToken cancellationToken = default)
    {
        Options = options;

        StatusWriter = options.StatusWriter;
        FormatHelper = options.FormatHelper;
        cancellationToken.ThrowIfCancellationRequested();
        StatusFormatAndWriteLine(options.KeepOriginalOrderOfPortNames);
        PortsToTest = options.KeepOriginalOrderOfPortNames
            ? new HashSet<string>(options.PortsToTest)
            : new SortedSet<string>(
                collection: options.PortsToTest,
                comparer: options.StringComparerForSort);

        if (PortsToTest.Count > 0)
        {
            StatusFormatAndWriteLine(PortsToTest);
        }

        cancellationToken.ThrowIfCancellationRequested();
        PortsToIgnore = options.KeepOriginalOrderOfPortNames
            ? new HashSet<string>(options.PortsToIgnore)
            : new SortedSet<string>(
                collection: options.PortsToIgnore,
                comparer: options.StringComparerForSort);
        StatusFormatAndWriteLine(PortsToIgnore);

        foreach (var portToIgnore in PortsToIgnore)
        {
            _errorsForTestedPorts[portToIgnore] = "Provided option to ignore.";
        }

        cancellationToken.ThrowIfCancellationRequested();
        StatusFormatAndWriteLine(options.RetryPortsOnException);
        StatusFormatAndWriteLine(options.RetryPortsOnLocate);
        _portsTested = options.KeepOriginalOrderOfPortNames
            ? new HashSet<string>()
            : new SortedSet<string>(
                comparer: options.StringComparerForSort);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task GetAvailablePorts(
        CancellationToken cancellationToken = default)
    {

    }

    void StatusFormatAndWrite(object? @object,
        [CallerArgumentExpression(nameof(@object))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true)
        => StatusWriter.Write(FormatHelper.Format(@object, paramName,
            indent, nameWithIndent));

    void StatusFormatAndWriteLine(object? @object,
        [CallerArgumentExpression(nameof(@object))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true)
        => StatusWriter.WriteLine(FormatHelper.Format(@object, paramName,
            indent, nameWithIndent));
}
