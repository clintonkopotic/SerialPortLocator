namespace SerialPortLocator.CLI;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using SerialPortLocator.LIB;

internal class Program
{
    /// <summary>
    /// Entry point.
    /// </summary>
    /// <param name="args">The string arguments to parse.</param>
    /// <returns>Process exit code.</returns>
    static int Main(string[] args) => new Program().Execute(args);

    bool _actionRan = false;

    bool _canceling = false;

    readonly CancellationTokenSource _cancellationTokenSource = new();

    Option<string> PortsToIgnoreOption { get; }
        = new("--ports-to-ignore")
        {
            Description = "A comma (\',\') separated values (CSV) string of "
                + "ports to ignore while locating. Use the backslash (\'\\\') "
                + "character to escape comma or backslash characters that are "
                + "to be preserved. For example, \"COM3,COM4\".",
            DefaultValueFactory = _ => string.Empty,
            HelpName = "\"CSV\""
        };

    Option<bool> RetryPortsOnExceptionOption { get; }
        = new("--retry-ports-on-exception")
        {
            Description = "When a port encounters an exception, it will be "
                + "retried. The default behaviour is to ignore them after they "
                + "encounter an exception.",
        };

    Option<bool> RetryPortsOnLocate { get; }
        = new("--retry-ports-on-locate")
        {
            Description = "When a port is located, it will continue to be "
                + "located. The default behaviour is to ignore it after it is "
                + "located.",
        };

    Option<bool> UseNullModemCableOption { get; }
        = new("--use-nullmodem-cable")
        {
            Description = "Instead of the default behavior of using a "
                + "loop-back adapter (i.e., a device that connects the Receive "
                + "pin with the Transmit pin within the same port), the use of "
                + "a null-modem cable between two ports to assist with "
                + "locating ports.",
        };

    int Execute(string[] args)
    {
        RootCommand rootCommand = [];
        var executingAssembly = Assembly.GetExecutingAssembly();
        var descriptionAttribute = executingAssembly
            .GetCustomAttribute<AssemblyDescriptionAttribute>();
        rootCommand.Description ??= descriptionAttribute?.Description;
        rootCommand.SetAction(_ => _actionRan = true);
        rootCommand.Add(PortsToIgnoreOption);
        rootCommand.Add(RetryPortsOnExceptionOption);
        rootCommand.Add(RetryPortsOnLocate);
        // TODO: Add option when NullModemCable mode is implemented.
        //rootCommand.Add(UseNullModemCableOption);
        CustomHelpAction.UpdateWith(rootCommand);
        ParseResult parseResult = rootCommand.Parse(args);
        var result = parseResult.Invoke();

        if (!_actionRan || (result != 0))
        {
            return result;
        }

        Console.CancelKeyPress += Console_CancelKeyPress;
        var portsToIgnoreOptionResult = parseResult.GetResult(PortsToIgnoreOption);
        var portsToIgnoreCsvString
            = portsToIgnoreOptionResult is not null
            && !portsToIgnoreOptionResult.Implicit
            ? parseResult.GetValue(PortsToIgnoreOption)
            : string.Empty;
        var portsToIgnoreArray = CsvArgumentEncoder.Decode(
            portsToIgnoreCsvString ?? string.Empty);
        HashSet<string> portsToIgnore = [];

        foreach (var portToIgnore in portsToIgnoreArray)
        {
            _ = portsToIgnore.Add(portToIgnore);
        }

        var retryPortsOnExceptionOptionResult = parseResult.GetResult(
            RetryPortsOnExceptionOption);
        var retryPortsOnException
            = retryPortsOnExceptionOptionResult is not null
            && !retryPortsOnExceptionOptionResult.Implicit
            && parseResult.GetValue(RetryPortsOnExceptionOption);

        var retryPortsOnLocateOptionResult = parseResult.GetResult(
            RetryPortsOnLocate);
        var retryPortsOnLocate = retryPortsOnLocateOptionResult is not null
            && !retryPortsOnLocateOptionResult.Implicit
            && parseResult.GetValue(RetryPortsOnLocate);

        var useNullModemCableOptionResult = parseResult.GetResult(
            UseNullModemCableOption);
        // Only use the NullModemCable mode if the option is specified on the
        // command line, otherwise use the default LoopbackAdapter mode.
        var locatorMode = (useNullModemCableOptionResult is not null
            && !useNullModemCableOptionResult.Implicit
            && parseResult.GetValue(UseNullModemCableOption))
            ? LocatorMode.NullModemCable
            : LocatorMode.LoopbackAdapter;

        return locatorMode switch
        {
            LocatorMode.LoopbackAdapter => ModeLoopbackAdapter(
                new LoopbackAdapterModeOptions()
                {
                    PortsToIgnore = portsToIgnore,
                    RetryPortsOnException = retryPortsOnException,
                    RetryPortsOnLocate = retryPortsOnLocate,
                },
                parseResult.InvocationConfiguration.Output,
                _cancellationTokenSource.Token),
            _ => throw new NotImplementedException("The following mode is not "
                + "implemented: "
                + $"{Format(locatorMode, nameWithIndent: false)}."),
        };
    }

    private void Console_CancelKeyPress(object? sender,
        ConsoleCancelEventArgs e)
    {
        e.Cancel = true;

        if (!_canceling)
        {
            _canceling = true;
            Console.WriteLine();
            Console.WriteLine("* CANCELLATION REQUESTED *");
            Console.WriteLine();
            _cancellationTokenSource.Cancel();
        }
    }

    static int ModeLoopbackAdapter(LoopbackAdapterModeOptions options,
        TextWriter? output = null,
        CancellationToken cancellationToken = default)
    {
        output ??= TextWriter.Null;
        output.WriteLine("Loopback Adapter mode.");
        HashSet<string> portsToIgnore = [.. options.PortsToIgnore];
        output.WriteLine(Format(portsToIgnore));

        var retryPortsOnException = options.RetryPortsOnException;
        output.WriteLine(Format(retryPortsOnException));

        var retryPortsOnLocate = options.RetryPortsOnLocate;
        output.WriteLine(Format(retryPortsOnLocate));

        Queue<string> portNames = [];
        SerialPort? serialPort = null;
        bool closeSerialPort = false;

        while (true)
        {
            string portName = string.Empty;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (portNames.Count <= 0)
                {
                    output.WriteLine("Remember, press CTRL+C to exit.");
                    Thread.Sleep(1_000);
                    var _portNames = SerialPort.GetPortNames();
                    var numberOfPorts = _portNames.Length;

                    if (numberOfPorts <= 0)
                    {
                        output.WriteLine("No ports avaiable.");

                        return 1;
                    }

                    output.Write($"Preparing to test {numberOfPorts} port"
                        + (numberOfPorts != 1 ? "s" : string.Empty) + "...");
                    portNames = new(_portNames);
                    output.WriteLine("READY");
                    output.WriteLine(Format(portNames));
                    output.WriteLine(Format(portsToIgnore));
                }

                cancellationToken.ThrowIfCancellationRequested();
                portName = portNames.Dequeue();

                if (portsToIgnore.Contains(portName))
                {
                    output.WriteLine($"Ignoring port: \"{portName}\".");

                    continue;
                }

                output.WriteLine($"Testing port: \"{portName}\":");
                output.Write("  Attempting to open with (19200/8-N-1)...");
                serialPort = new SerialPort(portName, baudRate: 19_200,
                    parity: Parity.None, dataBits: 8)
                {
                    Encoding = Encoding.ASCII,
                    NewLine = "\n",
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                };

                if (serialPort.IsOpen)
                {
                    output.WriteLine("Already open. Skipping.");

                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                serialPort.Open();
                closeSerialPort = true;
                output.WriteLine("DONE");
                cancellationToken.ThrowIfCancellationRequested();

                output.Write("  Listening to ensure nothing read (press CTRL+C "
                    + "to exit)...");
                var stopwatch = Stopwatch.StartNew();

                while (stopwatch.ElapsedMilliseconds < 3_000)
                {
                    var existing = serialPort.ReadExisting();

                    if (!string.IsNullOrEmpty(existing))
                    {
                        output.WriteLine($"Read {existing.Length} byte"
                            + (existing.Length != 1 ? "s" : string.Empty)
                            + ". Skipping.");

                        break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(100);
                }

                stopwatch.Stop();
                output.WriteLine("DONE");
                cancellationToken.ThrowIfCancellationRequested();

                var message = Guid.NewGuid().ToString();
                output.Write($"  Writting message \"{message}\"...");
                serialPort.WriteLine(message);
                output.WriteLine("DONE");
                cancellationToken.ThrowIfCancellationRequested();

                output.Write("  Listening for read message...");
                var success = false;
                stopwatch.Restart();

                while (stopwatch.ElapsedMilliseconds < 3_000)
                {
                    try
                    {
                        var readMessage = serialPort.ReadLine();

                        if (!string.IsNullOrEmpty(readMessage)
                            && readMessage == message)
                        {
                            output.WriteLine("SUCCESS. Read message: "
                                + $"\"{readMessage}\".");
                            success = true;

                            break;
                        }
                    }
                    catch (TimeoutException) { }

                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(100);
                }

                stopwatch.Stop();

                if (!success)
                {
                    output.WriteLine("FAILED. Skipping.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                output.Write("  Closing...");
                CloseSerialPort();
                output.WriteLine("DONE");
                cancellationToken.ThrowIfCancellationRequested();

                if (success)
                {
                    output.WriteLine("** Loopback Adapter found on port: "
                        + $"\"{portName}\". **");

                    if (!retryPortsOnLocate)
                    {
                        output.WriteLine("Going forward, ignoring port: "
                            + $"\"{portName}\".");
                        _ = portsToIgnore.Add(portName);
                    }

                    output.WriteLine("Press ENTER to continue or CTRL+C to "
                        + "exit.");

                    while (true)
                    {
                        while (!Console.KeyAvailable)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Thread.Sleep(100);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        var consoleKeyInfo = Console.ReadKey(true);

                        if (consoleKeyInfo.Key == ConsoleKey.Enter
                            && consoleKeyInfo.Modifiers == ConsoleModifiers.None)
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                output.WriteLine($"Exception. {ex.GetType().FullName}: "
                    + ex.Message);

                if (!retryPortsOnException
                    && !string.IsNullOrWhiteSpace(portName))
                {
                    output.WriteLine("Going forward, ignoring port: "
                        + $"\"{portName}\".");
                    _ = portsToIgnore.Add(portName);
                }
            }
            finally
            {
                CloseSerialPort();
            }
        }

        // Ensure the current serial port is closed and disposed correctly.
        void CloseSerialPort()
        {
            if (closeSerialPort && serialPort is not null && serialPort.IsOpen)
            {
                serialPort.Close();
                serialPort.Dispose();
                serialPort = null;
                closeSerialPort = false;
            }
        }
    }

    static string Format(IReadOnlyCollection<string> collection,
        [CallerArgumentExpression(nameof(collection))] string?
        paramName = null, string indent = "  ", bool nameWithIndent = true)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndent(paramName, indent));
        }

        if (collection.Count > 0)
        {
            output.Append($"\"{CsvArgumentEncoder.Encode(collection)}\"");
        }
        else
        {
            output.Append("<none>");
        }

        if (nameWithIndent)
        {
            output.Append('.');
        }

        return output.ToString();
    }

    static string Format(object @object,
        [CallerArgumentExpression(nameof(@object))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndent(paramName, indent));
        }

        _ = output.Append(@object);

        if (nameWithIndent)
        {
            _ = output.Append('.');
        }

        return output.ToString();
    }

    static string Format<TEnum>(TEnum @enum,
        [CallerArgumentExpression(nameof(@enum))] string?
        paramName = null, string indent = "  ", bool nameWithIndent = true)
        where TEnum : struct, Enum
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndent(paramName, indent));
        }

        _ = output.Append(Enum.IsDefined(@enum) ? @enum.ToString()
            : "<undefined>");

        var underlyingType = Enum.GetUnderlyingType(typeof(TEnum));
        object? underlyingValue = null;

        if (underlyingType == typeof(int))
        {
            underlyingValue = (int)(object)@enum;
        }
        else if (underlyingType == typeof(byte))
        {
            underlyingValue = (byte)(object)@enum;
        }
        else if (underlyingType == typeof(sbyte))
        {
            underlyingValue = (sbyte)(object)@enum;
        }
        else if (underlyingType == typeof(short))
        {
            underlyingValue = (short)(object)@enum;
        }
        else if (underlyingType == typeof(ushort))
        {
            underlyingValue = (ushort)(object)@enum;
        }
        else if (underlyingType == typeof(uint))
        {
            underlyingValue = (uint)(object)@enum;
        }
        else if (underlyingType == typeof(long))
        {
            underlyingValue = (long)(object)@enum;
        }
        else if (underlyingType == typeof(ulong))
        {
            underlyingValue = (ulong)(object)@enum;
        }

        _ = output.AppendFormat(" ({0})", underlyingValue is not null
            ? underlyingValue.ToString() : "null");

        if (nameWithIndent)
        {
            output.Append('.');
        }

        return output.ToString();
    }

    static string FormatNameWithIndent(string? paramName, string indent = "  ")
        => $"{indent}{paramName}: ";
}
