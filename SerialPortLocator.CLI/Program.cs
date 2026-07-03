namespace SerialPortLocator.CLI;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Reflection;
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
                parseResult.InvocationConfiguration.Output,
                _cancellationTokenSource.Token),
            _ => throw new NotImplementedException("The following mode is not "
                + $"implemented: {locatorMode}."),
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

    static int ModeLoopbackAdapter(TextWriter? output = null,
        CancellationToken cancellationToken = default)
    {
        output ??= TextWriter.Null;
        output.WriteLine("Starting Loopback Adapter mode...");
        output.WriteLine("  (Press CTRL+C to exit)");
        
        Queue<string> portNames = [];
        SerialPort? serialPort = null;
        bool closeSerialPort = false;

        while (true)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (portNames.Count <= 0)
                {
                    var _portNames = SerialPort.GetPortNames();
                    var numberOfPorts = _portNames.Length;

                    if (numberOfPorts <= 0)
                    {
                        output.WriteLine("No ports avaiable.");

                        return 1;
                    }

                    output.Write($"Preparing to test {numberOfPorts} port"
                        + (numberOfPorts != 1 ? "s" : string.Empty) + "...");

                    foreach (var _portName in _portNames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        portNames.Enqueue(_portName);
                    }

                    output.WriteLine("READY");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var portName = portNames.Dequeue();
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
                    output.WriteLine("Press ENTER to continue or CTRL+C to "
                        + "exit.");

                    while (true)
                    {
                        while (Console.KeyAvailable)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var consoleKeyInfo = Console.ReadKey(true);

                            if (consoleKeyInfo.Key == ConsoleKey.Enter)
                            {
                                break;
                            }
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        Thread.Sleep(100);
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
}
