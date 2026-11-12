namespace SerialPortLocator.LIB;

using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public sealed class NullModemCableModePortTest(SerialPort serialPort)
    : IDisposable
{
    private bool _disposedValue;

    private readonly Stopwatch _stopwatch = new();

    public SerialPort SerialPort { get; } = serialPort;

    public async Task<NullModemCableModePortTestResult> OpenAsync(
        CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.Now;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SerialPort.IsOpen)
            {
                return new NullModemCableModePortTestResult.Failure(
                    PortName: SerialPort.PortName,
                    Step: NullModemCableModePortTestStep.Open,
                    Start: start, End: DateTimeOffset.Now);
            }

            SerialPort.Open();

            return new NullModemCableModePortTestResult.Success(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.Open,
                Data: string.Empty, Start: start, End: DateTimeOffset.Now);
        }
        catch (OperationCanceledException)
        {
            return new NullModemCableModePortTestResult.Canceled(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.Open);
        }
        catch (Exception ex)
        {
            return new NullModemCableModePortTestResult.Failure(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.Open,
                Exception: ex, Start: start, End: DateTimeOffset.Now);
        }
    }

    public async Task<NullModemCableModePortTestResult> PreReadAsync(
        int maxDurationInMilliseconds = 3_000,
        int sleepTimeoutInMilliseconds = 100,
        CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.Now;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stopwatch.Restart();

            while (_stopwatch.ElapsedMilliseconds < maxDurationInMilliseconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var existing = SerialPort.ReadExisting();

                if (!string.IsNullOrEmpty(existing))
                {
                    _stopwatch.Stop();

                    return new NullModemCableModePortTestResult.Failure(
                        PortName: SerialPort.PortName,
                        Step: NullModemCableModePortTestStep.PreRead,
                        Data: existing,
                        Start: start,
                        End: DateTimeOffset.Now);
                }

                await Task.Delay(sleepTimeoutInMilliseconds, cancellationToken);
            }

            _stopwatch.Stop();

            return new NullModemCableModePortTestResult.Success(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.PreRead,
                Data: string.Empty,
                Start: start,
                End: DateTimeOffset.Now);
        }
        catch (OperationCanceledException)
        {
            return new NullModemCableModePortTestResult.Canceled(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.PreRead);
        }
        catch (Exception ex)
        {
            return new NullModemCableModePortTestResult.Failure(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.PreRead,
                Exception: ex,
                Start: start,
                End: DateTimeOffset.Now);
        }
    }

    public async Task<NullModemCableModePortTestResult> WriteAsync(
        CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.Now;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var message = Guid.NewGuid().ToString();
            SerialPort.WriteLine(message);

            return new NullModemCableModePortTestResult.Success(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.Write,
                Data: message,
                Start: start,
                End: DateTimeOffset.Now);
        }
        catch (OperationCanceledException)
        {
            return new NullModemCableModePortTestResult.Canceled(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.Write);
        }
        catch (Exception ex)
        {
            return new NullModemCableModePortTestResult.Failure(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.Write,
                Exception: ex,
                Start: start,
                End: DateTimeOffset.Now);
        }
    }

    public async Task<NullModemCableModePortTestResult> ReadAsync(
        int maxDurationInMilliseconds = 3_000,
        int sleepTimeoutInMilliseconds = 100,
        CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.Now;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stopwatch.Restart();

            while (_stopwatch.ElapsedMilliseconds < maxDurationInMilliseconds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var readLine = SerialPort.ReadLine();

                    if (!string.IsNullOrEmpty(readLine))
                    {
                        _stopwatch.Stop();

                        return new NullModemCableModePortTestResult.Success(
                            PortName: SerialPort.PortName,
                            Step: NullModemCableModePortTestStep.Read,
                            Data: readLine,
                            Start: start,
                            End: DateTimeOffset.Now);
                    }
                }
                catch (TimeoutException)
                { }

                await Task.Delay(sleepTimeoutInMilliseconds, cancellationToken);
            }

            _stopwatch.Stop();

            return new NullModemCableModePortTestResult.Success(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.Read,
                Data: string.Empty,
                Start: start,
                End: DateTimeOffset.Now);
        }
        catch (OperationCanceledException)
        {
            return new NullModemCableModePortTestResult.Canceled(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.Read);
        }
        catch (Exception ex)
        {
            return new NullModemCableModePortTestResult.Failure(
                PortName: SerialPort.PortName,
                Step: NullModemCableModePortTestStep.Read,
                Exception: ex,
                Start: start,
                End: DateTimeOffset.Now);
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (SerialPort.IsOpen)
                {
                    SerialPort.Close();
                }

                SerialPort.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in
        // 'Dispose(bool disposing)' method.
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
