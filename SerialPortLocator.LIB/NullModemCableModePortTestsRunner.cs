namespace SerialPortLocator.LIB;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public sealed class NullModemCableModePortTestsRunner : IDisposable
{
    private bool _disposedValue;

    private readonly Dictionary<string, NullModemCableModePortTest> _dictionary;

    public int Count => _dictionary.Count;

    public IEnumerable<string> PortNames => _dictionary.Keys;

    public IEnumerable<NullModemCableModePortTest> Tests => _dictionary.Values;

    public SerialPortHelper.ParseResult Settings { get; }

    public NullModemCableModePortTestsRunner(
        IEnumerable<string> portNames, string portSettings)
    {
        Settings = SerialPortHelper.ParseSettings(portSettings);
        _dictionary = [];

        foreach (var portName in portNames)
        {
            if (_dictionary.ContainsKey(portName))
            {
                continue;
            }

            _dictionary[portName] = new NullModemCableModePortTest(
                serialPort: new SerialPort(portName, Settings.BaudRate,
                    Settings.Parity, Settings.DataBits, Settings.StopBits)
                {
                    Encoding = Encoding.ASCII,
                    NewLine = "\n",
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                });
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                DisposeTests(Tests);
            }

            _dictionary.Clear();
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in
        // 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async Task<IEnumerable<NullModemCableModePortTestResult>>
        OpenAsync(CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.Now;
        List<NullModemCableModePortTestResult> testResults = [];
        var innerCancellationTokenSource = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        var innerCancellationToken = innerCancellationTokenSource.Token;

        await foreach (var portTestTask in Task.WhenEach(
            from test in Tests select test.OpenAsync(innerCancellationToken)))
        {
            var taskResult = await portTestTask;
            testResults.Add(taskResult);
        }

        RemoveFailedTests(testResults);
        var end = DateTimeOffset.Now;
        var duration = end - start;

        return testResults;
    }

    public async Task<IEnumerable<NullModemCableModePortTestResult>>
        PreReadAsync(int maxDurationInMilliseconds = 3_000,
        int sleepTimeoutInMilliseconds = 100,
        CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.Now;
        List<NullModemCableModePortTestResult> testResults = [];
        var innerCancellationTokenSource = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        var innerCancellationToken = innerCancellationTokenSource.Token;

        await foreach (var portTestTask in Task.WhenEach(
            from test in Tests
            select test.PreReadAsync(
                maxDurationInMilliseconds, sleepTimeoutInMilliseconds,
                innerCancellationToken)))
        {
            var taskResult = await portTestTask;
            testResults.Add(taskResult);
        }

        RemoveFailedTests(testResults);
        var end = DateTimeOffset.Now;
        var duration = end - start;

        return testResults;
    }

    public async Task<IEnumerable<NullModemCableModePortTestResult>>
        WriteAsync(CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.Now;
        List<NullModemCableModePortTestResult> testResults = [];
        var innerCancellationTokenSource = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        var innerCancellationToken = innerCancellationTokenSource.Token;

        await foreach (var portTestTask in Task.WhenEach(
            from test in Tests select test.WriteAsync(innerCancellationToken)))
        {
            var taskResult = await portTestTask;
            testResults.Add(taskResult);
        }

        RemoveFailedTests(testResults);
        var end = DateTimeOffset.Now;
        var duration = end - start;

        return testResults;
    }

    public async Task<IEnumerable<NullModemCableModePortTestResult>>
        ReadAsync(int maxDurationInMilliseconds = 3_000,
        int sleepTimeoutInMilliseconds = 100,
        CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.Now;
        List<NullModemCableModePortTestResult> testResults = [];
        var innerCancellationTokenSource = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        var innerCancellationToken = innerCancellationTokenSource.Token;

        await foreach (var portTestTask in Task.WhenEach(
            from test in Tests
            select test.ReadAsync(
                maxDurationInMilliseconds, sleepTimeoutInMilliseconds,
                innerCancellationToken)))
        {
            var taskResult = await portTestTask;
            testResults.Add(taskResult);
        }

        RemoveFailedTests(testResults);
        var end = DateTimeOffset.Now;
        var duration = end - start;

        return testResults;
    }

    private void RemoveFailedTests(
        IReadOnlyList<NullModemCableModePortTestResult> testResults)
    {
        foreach (var testResult in testResults)
        {
            if (testResult is NullModemCableModePortTestResult.Failure
                && _dictionary.TryGetValue(testResult.PortName, out var test))
            {
                test.Dispose();
                _dictionary.Remove(testResult.PortName);
            }
        }
    }

    private static void DisposeTests(
        IEnumerable<NullModemCableModePortTest> tests)
    {
        foreach (var test in tests)
        {
            test.Dispose();
        }
    }
}
