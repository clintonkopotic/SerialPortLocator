namespace SerialPortLocator.CLI;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SerialPortLocator.LIB;

internal partial class Program
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

    Option<string> PortsToTestOption { get; }
        = new("--ports-to-test")
        {
            Description = "A comma (\',\') separated values (CSV) string of "
                + "ports to test. The default behavior is to to query for a "
                + "list of valid serial port names. Use the backslash (\'\\\') "
                + "character to escape comma or backslash characters that are "
                + "to be preserved. For example, \"COM3,COM4,COM6,COM7\".",
            DefaultValueFactory = _ => string.Empty,
            HelpName = "\"CSV\""
        };

    Option<bool> RetryPortsOnExceptionOption { get; }
        = new("--retry-ports-on-exception")
        {
            Description = "When a port encounters an exception, it will be "
                + "retried. The default behavior is to ignore them after they "
                + "encounter an exception.",
        };

    Option<bool> RetryPortsOnLocateOption { get; }
        = new("--retry-ports-on-locate")
        {
            Description = "When a port is located, it will continue to be "
                + "located. The default behavior is to ignore it after it is "
                + "located.",
        };

    Option<bool> KeepOriginalOrderOfPortNamesOption { get; }
        = new("--keep-original-order-of-port-names")
        {
            Description = "Keep the original order of the port names. The "
                + "default behavior is to sort the port names.",
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

    Option<bool> ListAvailablePortNamesOption { get; }
        = new("--list-port-names")
        {
            Description = "Lists the currently available port names, each on a "
                + "separate line and in the original order and then exits.",
        };

    int Execute(string[] args)
    {
        var launchedFromGui = LaunchedFromGui.Detect();

        try
        {
            RootCommand rootCommand = [];
            var executingAssembly = Assembly.GetExecutingAssembly();
            var descriptionAttribute = executingAssembly
                .GetCustomAttribute<AssemblyDescriptionAttribute>();

            if (descriptionAttribute is not null
                && !string.IsNullOrWhiteSpace(descriptionAttribute.Description))
            {
                rootCommand.Description = descriptionAttribute.Description;
            }

            rootCommand.SetAction(_ => _actionRan = true);
            rootCommand.Add(PortsToIgnoreOption);
            rootCommand.Add(PortsToTestOption);
            rootCommand.Add(RetryPortsOnExceptionOption);
            rootCommand.Add(RetryPortsOnLocateOption);
            rootCommand.Add(KeepOriginalOrderOfPortNamesOption);
            // TODO: Add option when NullModemCable mode is implemented.
            rootCommand.Add(UseNullModemCableOption);
            rootCommand.Add(ListAvailablePortNamesOption);
            CustomHelpAction.UpdateWith(rootCommand,
                packageFunc: ListPackages);
            ParseResult parseResult = rootCommand.Parse(args);
            var result = parseResult.Invoke();

            if (!_actionRan || (result != 0))
            {
                return result;
            }

            var listAvailablePortNamesBool = GetOptionStructValue(parseResult,
                ListAvailablePortNamesOption);
            var listAvailablePortNames = listAvailablePortNamesBool is not null
                && listAvailablePortNamesBool.Value;

            if (listAvailablePortNames)
            {
                foreach (var portName in SerialPort.GetPortNames())
                {
                    Console.WriteLine(portName);
                }

                return 0;
            }

            Console.CancelKeyPress += Console_CancelKeyPress;

            var output = parseResult.InvocationConfiguration.Output;
            output.WriteLine("{0}: {1}",
                arg0: CustomHelpAction.DefaultVersionSectionTitle,
                arg1: GetExecutableVersion());
            output.WriteLine("Runtime: {0}",
                arg0: RuntimeInformation.FrameworkDescription);
            _ = ListPackages(output);

            var portsToIgnoreCsvString = GetOptionClassValue(parseResult,
                PortsToIgnoreOption);
            var portsToIgnoreArray = CsvArgumentEncoder.Decode(
                portsToIgnoreCsvString ?? string.Empty);
            HashSet<string> portsToIgnore = [.. portsToIgnoreArray];

            var portsToTestCsvString = GetOptionClassValue(parseResult,
                PortsToTestOption);
            var portsToTestArray = CsvArgumentEncoder.Decode(
                portsToTestCsvString ?? string.Empty);
            HashSet<string> portsToTest = [.. portsToTestArray];

            var retryPortsOnExceptionBool = GetOptionStructValue(parseResult,
                RetryPortsOnExceptionOption);
            var retryPortsOnException = retryPortsOnExceptionBool is not null
                && retryPortsOnExceptionBool.Value;

            var retryPortsOnLocateBool = GetOptionStructValue(parseResult,
                RetryPortsOnLocateOption);
            var retryPortsOnLocate = retryPortsOnLocateBool is not null
                && retryPortsOnLocateBool.Value;

            var keepOriginalOrderOfPortNamesBool = GetOptionStructValue(
                parseResult, KeepOriginalOrderOfPortNamesOption);
            var keepOriginalOrderOfPortNames
                = keepOriginalOrderOfPortNamesBool is not null
                && keepOriginalOrderOfPortNamesBool.Value;

            var useNullModemCableOptionResult = parseResult.GetResult(
                UseNullModemCableOption);
            // Only use the NullModemCable mode if the option is specified on the
            // command line, otherwise use the default LoopbackAdapter mode.
            var locatorMode = (useNullModemCableOptionResult is not null
                && !useNullModemCableOptionResult.Implicit
                && parseResult.GetValue(UseNullModemCableOption))
                ? LocatorMode.NullModemCable
                : LocatorMode.LoopbackAdapter;
            FormatHelper formatHelper = new();
            output.WriteLine(formatHelper.Format(locatorMode,
                indent: string.Empty));

            return locatorMode switch
            {
                LocatorMode.LoopbackAdapter => ModeLoopbackAdapter(
                    options: new LoopbackAdapterModeOptions()
                    {
                        PortsToIgnore = portsToIgnore,
                        PortsToTest = portsToTest,
                        RetryPortsOnException = retryPortsOnException,
                        RetryPortsOnLocate = retryPortsOnLocate,
                        KeepOriginalOrderOfPortNames
                            = keepOriginalOrderOfPortNames,
                        StatusWriter = output,
                    },
                    cancellationToken: _cancellationTokenSource.Token),
                LocatorMode.NullModemCable => ModeNullModemCable(
                    options: new NullModemCableModeOptions()
                    {
                        PortsToIgnore = portsToIgnore,
                        PortsToTest = portsToTest,
                        RetryPortsOnException = retryPortsOnException,
                        RetryPortsOnLocate = retryPortsOnLocate,
                        KeepOriginalOrderOfPortNames
                            = keepOriginalOrderOfPortNames,
                        StatusWriter = output,
                    },
                    cancellationToken: _cancellationTokenSource.Token),
                _ => throw new NotImplementedException("The following mode is "
                    + "not implemented: "
                    + formatHelper.Format(locatorMode, nameWithIndent: false)
                    + '.'),
            };
        }
        finally
        {
            if (launchedFromGui)
            {
                Console.WriteLine();
                Console.Write("Press any key to close this window . . .");
                _ = Console.ReadKey(true);
            }
        }
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
        CancellationToken cancellationToken = default)
    {
        var output = options.StatusWriter;

        var keepOriginalOrderOfPortNames = options.KeepOriginalOrderOfPortNames;
        output.WriteLine(Format(keepOriginalOrderOfPortNames));

        var sortComparer = options.StringComparerForSort;
        ISet<string> portsToTest = keepOriginalOrderOfPortNames
            ? new HashSet<string>(options.PortsToTest)
            : new SortedSet<string>(options.PortsToTest, sortComparer);

        if (portsToTest.Count > 0)
        {
            output.WriteLine(Format(portsToTest));
        }

        ISet<string> portsToIgnore = keepOriginalOrderOfPortNames
            ? new HashSet<string>(options.PortsToIgnore)
            : new SortedSet<string>(options.PortsToIgnore, sortComparer);
        output.WriteLine(Format(portsToIgnore));
        Dictionary<string, string> errorsForTestedPorts = [];

        foreach (var port in portsToIgnore)
        {
            errorsForTestedPorts[port] = "Commandline provided option to "
                + "ignore.";
        }

        var retryPortsOnException = options.RetryPortsOnException;
        output.WriteLine(Format(retryPortsOnException));

        var retryPortsOnLocate = options.RetryPortsOnLocate;
        output.WriteLine(Format(retryPortsOnLocate));

        Dictionary<string, string> notesForTestedPorts = [];
        var notesCount = 0;
        Queue<string> portNamesLeftToBeTested = [];
        SerialPort? serialPort = null;
        ISet<string> portsTested = keepOriginalOrderOfPortNames
            ? new HashSet<string>()
            : new SortedSet<string>(sortComparer);

        try
        {
            while (true)
            {
                string portName = string.Empty;

                try
                {
                    ThrowIfCancellationRequested();

                    if (portNamesLeftToBeTested.Count <= 0)
                    {
                        output.WriteLine("Remember, press CTRL+C to exit.");
                        Thread.Sleep(1_000);
                        var _portsToTest = portsToTest;

                        if (_portsToTest.Count <= 0)
                        {
                            output.Write("Asking for a list of valid serial "
                                + "port names...");
                            var portNamesArray = SerialPort.GetPortNames();
                            _portsToTest = keepOriginalOrderOfPortNames
                                ? new HashSet<string>(portNamesArray)
                                : new SortedSet<string>(portNamesArray,
                                    sortComparer);
                            output.WriteLine("DONE");
                        }

                        var _actualPortsToTest = new HashSet<string>(
                            _portsToTest);
                        _actualPortsToTest.ExceptWith(portsToIgnore);
                        var numberOfPorts = _actualPortsToTest.Count;

                        if (numberOfPorts <= 0)
                        {
                            output.WriteLine("No ports to test:");
                            output.WriteLine(Format(_portsToTest,
                                paramName: "portsToTest"));
                            output.WriteLine(Format(portsToIgnore));
                            output.WriteLine(Format(portNamesLeftToBeTested));

                            return 1;
                        }

                        output.Write($"Preparing to test {numberOfPorts} port"
                            + (numberOfPorts != 1 ? "s" : string.Empty)
                            + "...");
                        portNamesLeftToBeTested = new(_actualPortsToTest);
                        output.WriteLine("READY");
                        output.WriteLine(Format(_portsToTest,
                            paramName: $"{nameof(portsToTest)}"));
                        output.WriteLine(Format(portsToIgnore));
                        output.WriteLine(Format(portNamesLeftToBeTested));
                    }

                    ThrowIfCancellationRequested();
                    portName = portNamesLeftToBeTested.Dequeue();

                    if (portsToIgnore.Contains(portName))
                    {
                        output.WriteLine($"Ignoring port: \"{portName}\".");

                        continue;
                    }

                    output.WriteLine($"Testing port: \"{portName}\":");
                    _ = portsTested.Add(portName);
                    var settings = SerialPortHelper.DefaultSettings;
                    output.Write($"  Attempting to open with ({settings})...");
                    var parsedSettings = SerialPortHelper.ParseSettings(
                        settings);
                    serialPort = new SerialPort(portName,
                        baudRate: parsedSettings.BaudRate,
                        dataBits: parsedSettings.DataBits,
                        parity: parsedSettings.Parity,
                        stopBits: parsedSettings.StopBits)
                    {
                        Encoding = Encoding.ASCII,
                        NewLine = "\n",
                        ReadTimeout = 500,
                        WriteTimeout = 500,
                    };

                    using LoopbackAdapterModePortTest test = new(serialPort);
                    var result = test.OpenAsync(cancellationToken).GetAwaiter()
                        .GetResult();

                    if (result is LoopbackAdapterModePortTestResult.Failure
                        openFailure)
                    {
                        if (openFailure.Exception is not null)
                        {
                            throw openFailure.Exception;
                        }

                        output.WriteLine("Already open. Skipping.");

                        continue;
                    }

                    output.WriteLine("DONE");
                    ThrowIfCancellationRequested();

                    output.Write("  Listening to ensure nothing read (press "
                        + "CTRL+C to exit)...");
                    var somethingRead = false;
                    var escapeKeyPressed = false;
                    using var preReadCancellationTokenSource
                        = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken);
                    var preReadCancellationToken
                        = preReadCancellationTokenSource.Token;
                    result = Task
                        .WhenAny(
                            test.PreReadAsync(
                                cancellationToken: preReadCancellationToken),
                            KeyPressedAsnc(
                                ConsoleKey.Escape,
                                cancellationToken: preReadCancellationToken))
                        .GetAwaiter().GetResult()
                        .GetAwaiter().GetResult();
                    preReadCancellationTokenSource.Cancel();

                    if (result is LoopbackAdapterModePortTestResult.Failure
                        preReadFailure)
                    {
                        if (preReadFailure.Exception is not null)
                        {
                            throw preReadFailure.Exception;
                        }
                    }
                    else if (result is LoopbackAdapterModePortTestResult.Success
                        preReadSuccess)
                    {
                        if (preReadSuccess.Step == LoopbackAdapterModePortTestStep
                            .PreRead)
                        {
                            var preReadData = preReadSuccess.Data;

                            if (!string.IsNullOrEmpty(preReadData))
                            {
                                somethingRead = true;
                                output.WriteLine($"Read {preReadData.Length} "
                                    + "byte" + (preReadData.Length != 1 ? "s"
                                        : string.Empty)
                                    + ':');
                                output.WriteLine("    As ASCII encoded: "
                                    + $"\"{preReadData}\".");
                                output.WriteLine("    As hexadecimal bytes: "
                                    + Format(Encoding.ASCII.GetBytes(
                                        preReadData))
                                    + '.');
                            }
                        }
                        else if (preReadSuccess.Step == LoopbackAdapterModePortTestStep
                            .None)
                        {
                            escapeKeyPressed = true;
                        }
                    }

                    ThrowIfCancellationRequested();

                    if (somethingRead)
                    {
                        var continueToTest = PromptYesOrNo(
                            prompt: "  There was communication read on this "
                                + "port. Do you want to continue to test this "
                                + "port? (y/[N]) ",
                            cancellationToken: cancellationToken,
                            output: output);
                        ThrowIfCancellationRequested();

                        if (!continueToTest)
                        {
                            output.WriteLine("Received response \"no\". Going "
                                + $"forward, ignoring port: \"{portName}\".");
                            _ = portsToIgnore.Add(portName);
                            errorsForTestedPorts[portName] = "User reqested to "
                                + "ignore after response on port received.";

                            continue;
                        }

                        output.WriteLine("Received response: \"yes\". "
                            + "Continuing to test.");
                    }
                    else if (escapeKeyPressed)
                    {
                        output.WriteLine("ESCAPED");
                        var ignore = PromptYesOrNo(
                            prompt: "Would you like to ignore this port going "
                                + "forward? (y/[N]) ",
                            cancellationToken: cancellationToken,
                            output: output);

                        if (ignore)
                        {
                            output.WriteLine("Received response \"yes\". Going "
                                + $"forward, ignoring port: \"{portName}\".");
                            _ = portsToIgnore.Add(portName);
                            errorsForTestedPorts[portName]
                                = "User reqested to ignore.";

                            continue;
                        }

                        output.WriteLine("Received response \"no\". Will "
                            + $"continue to test port: \"{portName}\".");
                    }
                    else
                    {
                        output.WriteLine("DONE");
                    }

                    ThrowIfCancellationRequested();

                    var messageChannel = Channel.CreateUnbounded<string>(
                        options: new UnboundedChannelOptions()
                        {
                            SingleReader = true,
                            SingleWriter = true,
                        });
                    using var writeCancellationTokenSource
                        = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken);
                    var writeCancellationToken
                        = writeCancellationTokenSource.Token;
                    result = Task
                        .WhenAny(
                            test.WriteAsync(messageChannel.Writer,
                                cancellationToken: writeCancellationToken),
                            WaitForWriteMessageAsync(messageChannel.Reader,
                                output: output,
                                cancellationToken: writeCancellationToken))
                        .GetAwaiter().GetResult()
                        .GetAwaiter().GetResult();
                    writeCancellationTokenSource.Cancel();
                    var message = string.Empty;

                    if (result is LoopbackAdapterModePortTestResult.Failure
                        writeFailure)
                    {
                        if (writeFailure.Exception is not null)
                        {
                            throw writeFailure.Exception;
                        }

                        throw new InvalidOperationException("Unhandled write "
                            + "failure.");
                    }
                    else if (result is LoopbackAdapterModePortTestResult.Success
                        writeSuccess)
                    {
                        if (writeSuccess.Step
                            != LoopbackAdapterModePortTestStep.Write
                            || string.IsNullOrEmpty(writeSuccess.Data))
                        {
                            throw new InvalidOperationException("Unhandled "
                                + "write success.");
                        }

                        message = writeSuccess.Data;
                        output.WriteLine("DONE");
                    }
                    else
                    {
                        throw new InvalidOperationException("Unhandled write "
                            + "result.");
                    }

                    ThrowIfCancellationRequested();

                    output.Write("  Listening for message...");
                    var success = false;
                    escapeKeyPressed = false;
                    using var readCancellationTokenSource
                        = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken);
                    var readCancellationToken
                        = readCancellationTokenSource.Token;
                    result = Task
                        .WhenAny(
                            test.ReadAsync(
                                cancellationToken: readCancellationToken),
                            KeyPressedAsnc(
                                ConsoleKey.Escape,
                                cancellationToken: readCancellationToken))
                        .GetAwaiter().GetResult()
                        .GetAwaiter().GetResult();
                    readCancellationTokenSource.Cancel();

                    if (result is LoopbackAdapterModePortTestResult.Failure
                        readFailure && readFailure.Exception is not null)
                    {
                        throw readFailure.Exception;
                    }
                    else if (result is LoopbackAdapterModePortTestResult.Success
                        readSuccess)
                    {
                        if (readSuccess.Step == LoopbackAdapterModePortTestStep
                            .Read)
                        {
                            var readData = readSuccess.Data;

                            if (!string.IsNullOrEmpty(readData)
                                && readData == message)
                            {
                                output.WriteLine("SUCCESS. Read message: "
                                    + $"\"{readData}\".");
                                success = true;
                            }
                        }
                        else if (readSuccess.Step == LoopbackAdapterModePortTestStep
                            .None)
                        {
                            escapeKeyPressed = true;
                        }
                    }

                    ThrowIfCancellationRequested();

                    if (escapeKeyPressed)
                    {
                        output.WriteLine("ESCAPED. Skipping.");
                    }
                    else if (!success)
                    {
                        output.WriteLine("FAILED. Skipping.");
                    }

                    ThrowIfCancellationRequested();
                    output.Write("  Closing...");
                    result = test.CloseAsync(cancellationToken).GetAwaiter()
                        .GetResult();

                    if (result is LoopbackAdapterModePortTestResult.Failure
                        closeFailure && closeFailure.Exception is not null)
                    {
                        throw closeFailure.Exception;
                    }

                    output.WriteLine("DONE");
                    ThrowIfCancellationRequested();

                    if (escapeKeyPressed)
                    {
                        var ignore = PromptYesOrNo(
                            prompt: "Would you like to ignore this port going "
                                + "forward? (y/[N]) ",
                            cancellationToken: cancellationToken,
                            output: output);

                        if (ignore)
                        {
                            output.WriteLine("Received response \"yes\". Going "
                                + $"forward, ignoring port: \"{portName}\".");
                            _ = portsToIgnore.Add(portName);
                            errorsForTestedPorts[portName]
                                = "User reqested to ignore.";
                        }

                        output.WriteLine("Received response \"no\". Will "
                            + $"continue to test port: \"{portName}\".");
                    }
                    else if (success)
                    {
                        output.WriteLine("** Loopback Adapter found on port: "
                            + $"\"{portName}\". **");

                        if (!retryPortsOnLocate)
                        {
                            output.WriteLine("Going forward, ignoring port: "
                                + $"\"{portName}\".");
                            _ = portsToIgnore.Add(portName);
                        }

                        ThrowIfCancellationRequested();
                        var noteAlreadyAssociated = true;

                        if (!notesForTestedPorts.TryGetValue(portName,
                            out var note))
                        {
                            noteAlreadyAssociated = false;
                            notesCount++;
                            note = $"Port {notesCount}";
                        }

                        var changeNote = false;

                        while (true)
                        {
                            ThrowIfCancellationRequested();
                            output.WriteLine("The following will be the note "
                                + "associated with this port, "
                                + $"\"{portName}\":");
                            output.WriteLine($"  \"{note}\".");
                            Console.Write("Would you like to change the note? "
                                + "(y/[N]) ");
                            var response = Console.ReadLine();

                            if (string.IsNullOrWhiteSpace(response))
                            {
                                response = "n";
                            }

                            response = response.Trim().ToLower();

                            if (response == "y")
                            {
                                changeNote = true;

                                break;
                            }
                            else if (response == "n")
                            {
                                changeNote = false;

                                break;
                            }
                            else
                            {
                                Console.WriteLine("Unrecognized response. "
                                    + "Please try again.");
                            }
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        if (changeNote)
                        {
                            Console.Write("Enter the note to associate with "
                                + "this port: ");
                            note = (Console.ReadLine() ?? string.Empty).Trim();
                            noteAlreadyAssociated = false;
                        }

                        if (!noteAlreadyAssociated)
                        {
                            notesForTestedPorts[portName] = note;
                        }

                        ThrowIfCancellationRequested();
                        output.WriteLine("Press ENTER to continue or CTRL+C to "
                            + "exit.");

                        while (true)
                        {
                            while (!Console.KeyAvailable)
                            {
                                ThrowIfCancellationRequested();
                                Thread.Sleep(100);
                            }

                            ThrowIfCancellationRequested();
                            var consoleKeyInfo = Console.ReadKey(true);

                            if (consoleKeyInfo.Key == ConsoleKey.Enter
                                && consoleKeyInfo.Modifiers
                                    == ConsoleModifiers.None)
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
                    var typeAndMessage = $"Exception. {ex.GetType().FullName}: "
                        + ex.Message;
                    output.WriteLine(typeAndMessage);

                    if (!retryPortsOnException
                        && !string.IsNullOrWhiteSpace(portName))
                    {
                        output.WriteLine("Going forward, ignoring port: "
                            + $"\"{portName}\".");
                        _ = portsToIgnore.Add(portName);
                        errorsForTestedPorts[portName] = typeAndMessage;
                    }
                }
            }
        }
        finally
        {
            const string Asterisk = "*";
            const string Error = "Error";
            const string Note = "Note";
            const string Port = "Port";
            List<(string port, string note, bool noteIsError)> portsAndNotes
                = [];
            var maxPortLength = Port.Length;
            var maxNoteLength = Note.Length;

            foreach (var port in portsTested)
            {
                if (errorsForTestedPorts.TryGetValue(port, out var error))
                {
                    portsAndNotes.Add((port, error, noteIsError: true));

                    if (port.Length > maxPortLength)
                    {
                        maxPortLength = port.Length;
                    }

                    if (Asterisk.Length > maxNoteLength)
                    {
                        maxNoteLength = Asterisk.Length;
                    }
                }
                else if (notesForTestedPorts.TryGetValue(port, out var note))
                {
                    portsAndNotes.Add((port, note, noteIsError: false));

                    if (port.Length > maxPortLength)
                    {
                        maxPortLength = port.Length;
                    }

                    if (note.Length > maxNoteLength)
                    {
                        maxNoteLength = note.Length;
                    }
                }
            }

            if (portsAndNotes.Count > 0 && maxPortLength > 0)
            {
                Console.WriteLine();
                var line = $"  {{0,{-maxPortLength}}} | {{1,{-maxNoteLength}}} "
                    + $"| {{2}}";
                Console.WriteLine(line, Port, Note, Error);
                Console.WriteLine($"  {new string('-', maxPortLength)} +"
                    + $" {new string('-', maxNoteLength)} + ------");

                foreach (var (port, note, noteIsError) in portsAndNotes)
                {
                    if (noteIsError)
                    {
                        Console.WriteLine(line, port,
                            PadCenterSpan(Asterisk, maxNoteLength),
                            note);
                    }
                    else
                    {
                        Console.WriteLine(line, port, note, string.Empty);
                    }
                }

                if (!keepOriginalOrderOfPortNames)
                {
                    List<(string note, string port)> notesAndPorts = [];
                    List<(string port, string error)> portsAndErrors = [];

                    foreach (var (port, note, noteIsError) in portsAndNotes)
                    {
                        if (noteIsError)
                        {
                            portsAndErrors.Add((port, note));
                        }
                        else
                        {
                            notesAndPorts.Add((note, port));
                        }
                    }

                    if (notesAndPorts.Count > 0)
                    {
                        notesAndPorts.Sort((noteAndPortA, noteAndPortB) =>
                        {
                            var (noteA, portA) = noteAndPortA;
                            var (noteB, portB) = noteAndPortB;

                            var noteComparison = sortComparer.Compare(noteA,
                                noteB);

                            if (noteComparison != 0)
                            {
                                return noteComparison;
                            }

                            return sortComparer.Compare(portA, portB);
                        });
                        portsAndErrors.Sort((portAndErrorA, portAndErrorB) =>
                        {
                            var (portA, errorA) = portAndErrorA;
                            var (portB, errorB) = portAndErrorB;

                            var portComparison = sortComparer.Compare(portA,
                                portB);

                            if (portComparison != 0)
                            {
                                return portComparison;
                            }

                            return sortComparer.Compare(errorA, errorB);
                        });

                        Console.WriteLine();
                        line = $"  {{0,{-maxNoteLength}}} | {{1,{-maxPortLength}}} "
                            + $"| {{2}}";
                        Console.WriteLine(line,
                            PadCenterSpan(Note, maxNoteLength),
                            PadCenterSpan(Port, maxPortLength),
                            Error);
                        Console.WriteLine($"  {new string('-', maxNoteLength)} +"
                            + $" {new string('-', maxPortLength)} + ------");

                        foreach (var (note, port) in notesAndPorts)
                        {
                            Console.WriteLine(line, note, port, string.Empty);
                        }

                        foreach (var (port, error) in portsAndErrors)
                        {
                            Console.WriteLine(line,
                                PadCenterSpan(Asterisk, maxNoteLength),
                                port, error);
                        }
                    }
                }
            }
        }

        // Local functions:
        string Format(object? @object,
            [CallerArgumentExpression(nameof(@object))]
                string? paramName = null,
            string indent = "  ", bool nameWithIndent = true)
            => options.FormatHelper.Format(@object, paramName, indent,
                nameWithIndent);

        void ThrowIfCancellationRequested()
            => cancellationToken.ThrowIfCancellationRequested();
    }

    static int ModeNullModemCable(NullModemCableModeOptions options,
        CancellationToken cancellationToken = default)
    {
        var output = options.StatusWriter;

        var keepOriginalOrderOfPortNames = options.KeepOriginalOrderOfPortNames;
        output.WriteLine(Format(keepOriginalOrderOfPortNames));

        var sortComparer = options.StringComparerForSort;
        ISet<string> portsToTest = keepOriginalOrderOfPortNames
            ? new HashSet<string>(options.PortsToTest)
            : new SortedSet<string>(options.PortsToTest, sortComparer);

        if (portsToTest.Count > 0)
        {
            output.WriteLine(Format(portsToTest));
        }

        ISet<string> portsToIgnore = keepOriginalOrderOfPortNames
            ? new HashSet<string>(options.PortsToIgnore)
            : new SortedSet<string>(options.PortsToIgnore, sortComparer);
        output.WriteLine(Format(portsToIgnore));
        Dictionary<string, string> errorsForTestedPorts = [];

        foreach (var port in portsToIgnore)
        {
            errorsForTestedPorts[port] = "Commandline provided option to "
                + "ignore.";
        }

        var retryPortsOnException = options.RetryPortsOnException;
        output.WriteLine(Format(retryPortsOnException));

        var retryPortsOnLocate = options.RetryPortsOnLocate;
        output.WriteLine(Format(retryPortsOnLocate));

        Dictionary<string, string> notesForTestedPorts = [];
        //var notesCount = 0;
        List<string> portNamesLeftToBeTested = [];
        ISet<string> portsTested = keepOriginalOrderOfPortNames
            ? new HashSet<string>()
            : new SortedSet<string>(sortComparer);
        Stopwatch stopwatch = new();
        var pluralSuffixS = string.Empty;

        try
        {
            while (true)
            {
                try
                {
                    ThrowIfCancellationRequested();
                    output.WriteLine("Remember, press CTRL+C to exit.");
                    Thread.Sleep(1_000);
                    var _portsToTest = portsToTest;

                    if (portsToTest.Count <= 0)
                    {
                        output.Write("Asking for a list of valid serial port "
                            + "names...");
                        var portNamesArray = SerialPort.GetPortNames();
                        _portsToTest = keepOriginalOrderOfPortNames
                            ? new HashSet<string>(portNamesArray)
                            : new SortedSet<string>(portNamesArray,
                                sortComparer);
                        output.WriteLine("DONE");
                        ThrowIfCancellationRequested();
                    }

                    var _actualPortsToTest = new HashSet<string>(
                        _portsToTest);
                    _actualPortsToTest.ExceptWith(portsToIgnore);
                    portNamesLeftToBeTested = [.. _actualPortsToTest];
                    var numberOfPorts = portNamesLeftToBeTested.Count;

                    if (numberOfPorts <= 0)
                    {
                        output.WriteLine("No ports to test:");
                        output.WriteLine(Format(_portsToTest,
                            paramName: "portsToTest"));
                        output.WriteLine(Format(portsToIgnore));
                        output.WriteLine(Format(portNamesLeftToBeTested));

                        return 1;
                    }

                    pluralSuffixS = numberOfPorts != 1 ? "s" : string.Empty;
                    output.Write($"Preparing to test {numberOfPorts} port"
                        + $"{pluralSuffixS}...");
                    var portSettings = SerialPortHelper.DefaultSettings;
                    using NullModemCableModePortTestsRunner testsRunner = new(
                        portNames: portNamesLeftToBeTested,
                        portSettings: portSettings);
                    output.WriteLine("READY");
                    output.WriteLine(Format(_portsToTest,
                        paramName: $"{nameof(portsToTest)}"));
                    output.WriteLine(Format(portsToIgnore));
                    output.WriteLine(Format(portNamesLeftToBeTested));
                    output.WriteLine(Format(portSettings));
                    ThrowIfCancellationRequested();

                    output.Write("Opening...");
                    stopwatch.Restart();
                    var openResults = testsRunner.OpenAsync(cancellationToken)
                        .GetAwaiter().GetResult();
                    List<string> failedToOpenPortNames = [];

                    foreach (var openResult in openResults)
                    {
                        if (openResult
                            is NullModemCableModePortTestResult.Failure failure
                            && failure.Exception is not null)
                        {
                            failedToOpenPortNames.Add(failure.PortName);
                            errorsForTestedPorts[failure.PortName]
                                = Format(failure, nameWithIndent: false);
                            portsToIgnore.Add(failure.PortName);
                        }
                    }

                    ThrowIfCancellationRequested();
                    output.Write("DONE (");
                    var failedToOpenCount = failedToOpenPortNames.Count;
                    pluralSuffixS = failedToOpenCount != 1 ? "s" : string.Empty;
                    stopwatch.Stop();

                    if (failedToOpenCount > 0)
                    {
                        output.Write($"{failedToOpenCount} failure"
                            + $"{pluralSuffixS} | ");
                    }

                    output.WriteLine($"Duration: {stopwatch.Elapsed})");

                    if (failedToOpenCount > 0)
                    {
                        output.WriteLine(Format(failedToOpenPortNames,
                            paramName: $"Failed Port{pluralSuffixS}"));
                    }

                    output.Write("Listening to ensure nothing read (press "
                        + "CTRL+C to exit)...");
                    stopwatch.Restart();
                    var preReadResults = testsRunner.PreReadAsync(
                        cancellationToken: cancellationToken)
                        .GetAwaiter().GetResult();
                    List<string> failedToPreReadPortNames = [];

                    foreach (var preReadResult in preReadResults)
                    {
                        if (preReadResult
                            is NullModemCableModePortTestResult.Failure failure
                            && failure.Exception is not null)
                        {
                            failedToPreReadPortNames.Add(failure.PortName);
                            errorsForTestedPorts[failure.PortName]
                                = Format(failure, nameWithIndent: false);
                            portsToIgnore.Add(failure.PortName);
                        }
                    }

                    ThrowIfCancellationRequested();
                    output.Write("DONE (");
                    var failedToPreReadCount = failedToPreReadPortNames.Count;
                    pluralSuffixS = failedToPreReadCount != 1 ? "s"
                        : string.Empty;
                    stopwatch.Stop();

                    if (failedToPreReadCount > 0)
                    {
                        output.Write($"{failedToPreReadCount} failure"
                            + $"{pluralSuffixS} | ");
                    }

                    output.WriteLine($"Duration: {stopwatch.Elapsed})");

                    if (failedToPreReadCount > 0)
                    {
                        output.WriteLine(Format(failedToOpenPortNames,
                            paramName: $"Failed Port{pluralSuffixS}"));
                    }

                    pluralSuffixS = testsRunner.Count != 1 ? "s" : string.Empty;
                    output.Write($"Writting message{pluralSuffixS}...");
                    stopwatch.Restart();
                    var writeResults = testsRunner.WriteAsync(cancellationToken)
                        .GetAwaiter().GetResult();
                    List<string> failedToWritePortNames = [];

                    foreach (var writeResult in writeResults)
                    {
                        if (writeResult
                            is NullModemCableModePortTestResult.Failure failure
                            && failure.Exception is not null)
                        {
                            failedToWritePortNames.Add(failure.PortName);
                            errorsForTestedPorts[failure.PortName]
                                = Format(failure, nameWithIndent: false);
                            portsToIgnore.Add(failure.PortName);
                        }
                    }

                    ThrowIfCancellationRequested();
                    output.Write("DONE (");
                    var failedToWriteCount = failedToWritePortNames.Count;
                    pluralSuffixS = failedToWriteCount != 1 ? "s"
                        : string.Empty;
                    stopwatch.Stop();

                    if (failedToWriteCount > 0)
                    {
                        output.Write($"{failedToWriteCount} failure"
                            + $"{pluralSuffixS} | ");
                    }

                    output.WriteLine($"Duration: {stopwatch.Elapsed})");

                    if (failedToWriteCount > 0)
                    {
                        output.WriteLine(Format(failedToWritePortNames,
                            paramName: $"Failed Port{pluralSuffixS}"));
                    }

                    pluralSuffixS = testsRunner.Count != 1 ? "s"
                        : string.Empty;
                    output.Write($"Reading message{pluralSuffixS}...");
                    stopwatch.Restart();
                    var readResults = testsRunner.ReadAsync(
                        cancellationToken: cancellationToken)
                        .GetAwaiter().GetResult();
                    List<string> failedToReadPortNames = [];

                    foreach (var readResult in readResults)
                    {
                        if (readResult
                            is NullModemCableModePortTestResult.Failure failure
                            && failure.Exception is not null)
                        {
                            failedToReadPortNames.Add(failure.PortName);
                            errorsForTestedPorts[failure.PortName]
                                = Format(failure, nameWithIndent: false);
                            portsToIgnore.Add(failure.PortName);
                        }
                    }

                    ThrowIfCancellationRequested();
                    output.Write("DONE (");
                    var failedToReadCount = failedToReadPortNames.Count;
                    pluralSuffixS = failedToReadCount != 1 ? "s" : string.Empty;
                    stopwatch.Stop();

                    if (failedToReadCount > 0)
                    {
                        output.Write($"{failedToReadCount} failure"
                            + $"{pluralSuffixS} | ");
                    }

                    output.WriteLine($"Duration: {stopwatch.Elapsed})");

                    if (failedToReadCount > 0)
                    {
                        output.WriteLine(Format(failedToWritePortNames,
                            paramName: $"Failed Port{pluralSuffixS}"));
                    }

                    // TODO: Locate any loopback adapters or null-modem cables.
                    throw new NotImplementedException();
                }
                catch (OperationCanceledException)
                {
                    return 0;
                }
                catch (Exception ex)
                {
                    output.WriteLine(Format(ex, nameWithIndent: false));

                    return 1;
                }
            }
        }
        finally
        {
            const string Asterisk = "*";
            const string Error = "Error";
            const string Note = "Note";
            const string Port = "Port";
            List<(string port, string note, bool noteIsError)> portsAndNotes
                = [];
            var maxPortLength = Port.Length;
            var maxNoteLength = Note.Length;

            foreach (var port in portsTested)
            {
                if (errorsForTestedPorts.TryGetValue(port, out var error))
                {
                    portsAndNotes.Add((port, error, noteIsError: true));

                    if (port.Length > maxPortLength)
                    {
                        maxPortLength = port.Length;
                    }

                    if (Asterisk.Length > maxNoteLength)
                    {
                        maxNoteLength = Asterisk.Length;
                    }
                }
                else if (notesForTestedPorts.TryGetValue(port, out var note))
                {
                    portsAndNotes.Add((port, note, noteIsError: false));

                    if (port.Length > maxPortLength)
                    {
                        maxPortLength = port.Length;
                    }

                    if (note.Length > maxNoteLength)
                    {
                        maxNoteLength = note.Length;
                    }
                }
            }

            if (portsAndNotes.Count > 0 && maxPortLength > 0)
            {
                Console.WriteLine();
                var line = $"  {{0,{-maxPortLength}}} | {{1,{-maxNoteLength}}} "
                    + $"| {{2}}";
                Console.WriteLine(line, Port, Note, Error);
                Console.WriteLine($"  {new string('-', maxPortLength)} +"
                    + $" {new string('-', maxNoteLength)} + ------");

                foreach (var (port, note, noteIsError) in portsAndNotes)
                {
                    if (noteIsError)
                    {
                        Console.WriteLine(line, port,
                            PadCenterSpan(Asterisk, maxNoteLength),
                            note);
                    }
                    else
                    {
                        Console.WriteLine(line, port, note, string.Empty);
                    }
                }

                if (!keepOriginalOrderOfPortNames)
                {
                    List<(string note, string port)> notesAndPorts = [];
                    List<(string port, string error)> portsAndErrors = [];

                    foreach (var (port, note, noteIsError) in portsAndNotes)
                    {
                        if (noteIsError)
                        {
                            portsAndErrors.Add((port, note));
                        }
                        else
                        {
                            notesAndPorts.Add((note, port));
                        }
                    }

                    if (notesAndPorts.Count > 0)
                    {
                        notesAndPorts.Sort((noteAndPortA, noteAndPortB) =>
                        {
                            var (noteA, portA) = noteAndPortA;
                            var (noteB, portB) = noteAndPortB;

                            var noteComparison = sortComparer.Compare(noteA,
                                noteB);

                            if (noteComparison != 0)
                            {
                                return noteComparison;
                            }

                            return sortComparer.Compare(portA, portB);
                        });
                        portsAndErrors.Sort((portAndErrorA, portAndErrorB) =>
                        {
                            var (portA, errorA) = portAndErrorA;
                            var (portB, errorB) = portAndErrorB;

                            var portComparison = sortComparer.Compare(portA,
                                portB);

                            if (portComparison != 0)
                            {
                                return portComparison;
                            }

                            return sortComparer.Compare(errorA, errorB);
                        });

                        Console.WriteLine();
                        line = $"  {{0,{-maxNoteLength}}} | {{1,{-maxPortLength}}} "
                            + $"| {{2}}";
                        Console.WriteLine(line,
                            PadCenterSpan(Note, maxNoteLength),
                            PadCenterSpan(Port, maxPortLength),
                            Error);
                        Console.WriteLine($"  {new string('-', maxNoteLength)} +"
                            + $" {new string('-', maxPortLength)} + ------");

                        foreach (var (note, port) in notesAndPorts)
                        {
                            Console.WriteLine(line, note, port, string.Empty);
                        }

                        foreach (var (port, error) in portsAndErrors)
                        {
                            Console.WriteLine(line,
                                PadCenterSpan(Asterisk, maxNoteLength),
                                port, error);
                        }
                    }
                }
            }
        }

        // Local functions:
        string Format(object? @object,
            [CallerArgumentExpression(nameof(@object))]
                string? paramName = null,
            string indent = "  ", bool nameWithIndent = true)
            => options.FormatHelper.Format(@object, paramName, indent,
                nameWithIndent);

        void ThrowIfCancellationRequested()
            => cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Adapted from <see cref="VersionOption"/>.
    /// </summary>
    /// <returns>The <see cref="Assembly"/> from
    /// <see cref="Assembly.GetEntryAssembly()"/> or
    /// <see cref="Assembly.GetExecutingAssembly()"/> if called from unmanged
    /// code then the .</returns>
    static Assembly GetExecutableAssembly()
        => Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

    /// <summary>
    /// Adapted from <see cref="VersionOption"/>.
    /// </summary>
    /// <returns>The version of the <see cref="Assembly"/>.</returns>
    static string GetExecutableVersion()
    {
        var assembly = GetExecutableAssembly();
        var customAttribute = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        return customAttribute is not null
            ? customAttribute.InformationalVersion
            : assembly.GetName().Version?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Only gets the parsed value of the <paramref name="option"/> from the
    /// <paramref name="parseResult"/> if it was specified on the command line,
    /// otherwise <see langword="null"/> is returned. This overrides the use of
    /// default value (see <see cref="Option.GetDefaultValue()"/>) when calling
    /// <see cref="ParseResult.GetValue{T}(Option{T})"/>.
    /// </summary>
    /// <typeparam name="T">A <see langword="class"/>.</typeparam>
    /// <param name="parseResult">The result of parsing the command line
    /// input (see
    /// <see cref="Command.Parse(IReadOnlyList{string},
    /// ParserConfiguration?)"/>).</param>
    /// <param name="option">The value to get from.</param>
    /// <returns></returns>
    static T? GetOptionClassValue<T>(ParseResult parseResult, Option<T> option)
        where T : class
    {
        var optionResult = parseResult.GetResult(option);

        return optionResult is not null && !optionResult.Implicit
            ? parseResult.GetValue(option) : null;
    }

    /// <summary>
    /// Only gets the parsed value of the <paramref name="option"/> from the
    /// <paramref name="parseResult"/> if it was specified on the command line,
    /// otherwise <see langword="null"/> is returned. This overrides the use of
    /// default value (see <see cref="Option.GetDefaultValue()"/>) when calling
    /// <see cref="ParseResult.GetValue{T}(Option{T})"/>.
    /// </summary>
    /// <typeparam name="T">A <see langword="struct"/>.</typeparam>
    /// <param name="parseResult">The result of parsing the command line
    /// input (see
    /// <see cref="Command.Parse(IReadOnlyList{string},
    /// ParserConfiguration?)"/>).</param>
    /// <param name="option">The value to get from.</param>
    /// <returns></returns>
    static T? GetOptionStructValue<T>(ParseResult parseResult, Option<T> option)
        where T : struct
    {
        var optionResult = parseResult.GetResult(option);

        return optionResult is not null && !optionResult.Implicit
            ? parseResult.GetValue(option) : null;
    }

    static bool KeyPressed(ConsoleKey key,
        ConsoleModifiers modifiers = ConsoleModifiers.None,
        bool intercept = true, bool readLast = true)
    {
        ConsoleKeyInfo consoleKeyInfo = default;

        while (Console.KeyAvailable)
        {
            consoleKeyInfo = Console.ReadKey(intercept: intercept);

            if (!readLast)
            {
                return _KeyPressed();
            }
        }

        return _KeyPressed();

        bool _KeyPressed()
        {
            return consoleKeyInfo.Key == key
                && consoleKeyInfo.Modifiers == modifiers;
        }
    }

    static async Task<LoopbackAdapterModePortTestResult> KeyPressedAsnc(
        ConsoleKey key, ConsoleModifiers modifiers = ConsoleModifiers.None,
        bool intercept = true, bool readLast = true,
        int sleepTimeoutInMilliseconds = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            while (!KeyPressed(key, modifiers, intercept, readLast))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(sleepTimeoutInMilliseconds, cancellationToken);
            }

            return new LoopbackAdapterModePortTestResult.Success(
                LoopbackAdapterModePortTestStep.None, true.ToString());
        }
        catch (OperationCanceledException)
        {
            return new LoopbackAdapterModePortTestResult.Failure(
                LoopbackAdapterModePortTestStep.None, Data: false.ToString());
        }
        catch (Exception ex)
        {
            return new LoopbackAdapterModePortTestResult.Failure(
                LoopbackAdapterModePortTestStep.None, Exception: ex);
        }
    }

    static int ListPackages(TextWriter output, string indent = "  ",
        bool extraLineSpacing = false)
    {
        string[] nugets =
            [
                "System.CommandLine",
                "System.IO.Ports",
                "NaturalSort.Extension",
            ];

        var assembly = GetExecutableAssembly();
        var assemblyName = assembly.GetName().Name ?? string.Empty;
        var assemblyNameStartsWith = assemblyName.Split('.').First();

        var maxInternalsNameLength = 0;
        var maxPackagesNameLength = 0;
        SortedDictionary<string, string> internals = [];
        SortedDictionary<string, string> packages = [];
        Queue<AssemblyName> referencedAssemblies = new(
            collection: assembly.GetReferencedAssemblies());

        while (referencedAssemblies.Count > 0)
        {
            var referencedAssemblyName = referencedAssemblies.Dequeue();
            var name = referencedAssemblyName.Name;
            var version = referencedAssemblyName.Version?.ToString();
            var referencedAssembly = Assembly.Load(referencedAssemblyName);
            var assemblyInformationalVersionAttribute = referencedAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            if (assemblyInformationalVersionAttribute is not null)
            {
                version = assemblyInformationalVersionAttribute
                    .InformationalVersion;
            }

            if (string.IsNullOrEmpty(name) || version is null)
            {
                continue;
            }
            else if (!string.IsNullOrEmpty(assemblyNameStartsWith)
                && name.StartsWith(assemblyNameStartsWith))
            {
                internals[name] = version;

                if (name.Length > maxInternalsNameLength)
                {
                    maxInternalsNameLength = name.Length;
                }

                try
                {
                    foreach (var internalReferencedAssembly
                        in referencedAssembly.GetReferencedAssemblies())
                    {
                        referencedAssemblies.Enqueue(internalReferencedAssembly);
                    }
                }
                catch
                { }
            }
            else if (nugets.Contains(name))
            {
                packages[name] = version;

                if (name.Length > maxPackagesNameLength)
                {
                    maxPackagesNameLength = name.Length;
                }
            }
        }

        if (packages.Count > 0)
        {
            output.WriteLine("NuGet Packages:");

            foreach (var (name, version) in packages)
            {
                string dotLeader = new(c: '.',
                    count: maxPackagesNameLength - name.Length);
                output.WriteLine($"{indent}{name} .{dotLeader}. v{version}");
            }

            if (extraLineSpacing && internals.Count > 0)
            {
                output.WriteLine();
            }
        }

        if (internals.Count > 0)
        {
            output.WriteLine("Internal Libraries:");

            foreach (var (name, version) in internals)
            {
                string dotLeader = new(c: '.',
                    count: maxInternalsNameLength - name.Length);
                output.WriteLine($"{indent}{name} .{dotLeader}. v{version}");
            }
        }

        return 0;
    }

    static string PadCenterSpan(string text, int totalWidth,
        char paddingChar = ' ')
    {
        if (string.IsNullOrEmpty(text))
        {
            return new string(paddingChar, totalWidth);
        }

        if (text.Length >= totalWidth)
        {
            return text;
        }

        var leftPadding = (totalWidth - text.Length) / 2;

        return string.Create(
            length: totalWidth,
            state: (text, leftPadding, paddingChar),
            action: (span, state) =>
            {
                // Fill the left side with padding characters
                span[..state.leftPadding].Fill(state.paddingChar);

                // Copy the actual text content into the center
                state.text.AsSpan().CopyTo(span[state.leftPadding..]);

                // Fill the remaining right side with padding characters
                span[(state.leftPadding + state.text.Length)..]
                    .Fill(state.paddingChar);
            });
    }

    static bool PromptYesOrNo(string prompt,
        CancellationToken? cancellationToken = null, bool defaultIsNo = true,
        TextWriter? output = null)
    {
        cancellationToken ??= CancellationToken.None;
        output ??= Console.Out;

        while (true)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            output.Write(prompt);
            var response = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(response))
            {
                response = defaultIsNo ? "n" : "y";
            }

            response = response.Trim().ToLower();

            if (response == "y" || response == "yes")
            {
                return true;
            }
            else if (response == "n" || response == "no")
            {
                return false;
            }
            else
            {
                output.WriteLine("Unrecognized response. Please try again.");
            }
        }
    }

    static async Task<LoopbackAdapterModePortTestResult>
        WaitForWriteMessageAsync(ChannelReader<string> messageReader,
        TextWriter? output = null,
        CancellationToken cancellationToken = default)
    {
        output ??= TextWriter.Null;

        try
        {
            var message = await messageReader.ReadAsync(cancellationToken);
            output.Write($"  Writting message \"{message}\"...");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new LoopbackAdapterModePortTestResult.Success(
                LoopbackAdapterModePortTestStep.None, string.Empty);
        }
        catch (OperationCanceledException)
        {
            return new LoopbackAdapterModePortTestResult.Failure(
                LoopbackAdapterModePortTestStep.None);
        }
        catch (Exception ex)
        {
            return new LoopbackAdapterModePortTestResult.Failure(
                LoopbackAdapterModePortTestStep.None, Exception: ex);
        }
    }
}
