namespace SerialPortLocator.LIB;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public sealed class LoopbackAdapterModePortTest(SerialPort serialPort)
    : IDisposable
{
    private bool _disposedValue;

    private readonly Stopwatch _stopwatch = new();

    public SerialPort SerialPort { get; } = serialPort;

    public async Task<LoopbackAdapterModePortTestResult> OpenAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SerialPort.IsOpen)
            {
                return new LoopbackAdapterModePortTestResult.Failure(
                    Step: LoopbackAdapterModePortTestStep.Open);
            }

            SerialPort.Open();

            return new LoopbackAdapterModePortTestResult.Success(
                Step: LoopbackAdapterModePortTestStep.Open,
                Data: string.Empty);
        }
        catch (Exception ex)
        {
            return new LoopbackAdapterModePortTestResult.Failure(
                Step: LoopbackAdapterModePortTestStep.Open,
                Exception: ex);
        }
    }

    public async Task<LoopbackAdapterModePortTestResult> PreReadAsync(
        int maxDurationInMilliseconds = 3_000,
        int sleepTimeoutInMilliseconds = 100,
        CancellationToken cancellationToken = default)
    {
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

                    return new LoopbackAdapterModePortTestResult.Success(
                        Step: LoopbackAdapterModePortTestStep.PreRead,
                        Data: existing);
                }

                await Task.Delay(sleepTimeoutInMilliseconds, cancellationToken);
            }

            _stopwatch.Stop();

            return new LoopbackAdapterModePortTestResult.Success(
                Step: LoopbackAdapterModePortTestStep.PreRead,
                Data: string.Empty);
        }
        catch (Exception ex)
        {
            return new LoopbackAdapterModePortTestResult.Failure(
                Step: LoopbackAdapterModePortTestStep.PreRead,
                Exception: ex);
        }
    }

    public async Task<LoopbackAdapterModePortTestResult> WriteAsync(
        ChannelWriter<string>? messageWriter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var message = Guid.NewGuid().ToString();

            if (messageWriter is not null)
            {
                await messageWriter.WriteAsync(message, cancellationToken);
            }

            SerialPort.WriteLine(message);

            return new LoopbackAdapterModePortTestResult.Success(
                Step: LoopbackAdapterModePortTestStep.Write,
                Data: message);
        }
        catch (Exception ex)
        {
            return new LoopbackAdapterModePortTestResult.Failure(
                Step: LoopbackAdapterModePortTestStep.Write,
                Exception: ex);
        }
    }

    public async Task<LoopbackAdapterModePortTestResult> ReadAsync(
        int maxDurationInMilliseconds = 3_000,
        int sleepTimeoutInMilliseconds = 100,
        CancellationToken cancellationToken = default)
    {
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

                        return new LoopbackAdapterModePortTestResult.Success(
                            Step: LoopbackAdapterModePortTestStep.Read,
                            Data: readLine);
                    }
                }
                catch (TimeoutException)
                { }

                await Task.Delay(sleepTimeoutInMilliseconds, cancellationToken);
            }

            _stopwatch.Stop();

            return new LoopbackAdapterModePortTestResult.Success(
                Step: LoopbackAdapterModePortTestStep.Read,
                Data: string.Empty);
        }
        catch (Exception ex)
        {
            return new LoopbackAdapterModePortTestResult.Failure(
                Step: LoopbackAdapterModePortTestStep.Read,
                Exception: ex);
        }
    }

    public async Task<LoopbackAdapterModePortTestResult> CloseAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            SerialPort.Close();
            cancellationToken.ThrowIfCancellationRequested();

            return new LoopbackAdapterModePortTestResult.Success(
                Step: LoopbackAdapterModePortTestStep.Close,
                Data: string.Empty);
        }
        catch (Exception ex)
        {
            return new LoopbackAdapterModePortTestResult.Failure(
                Step: LoopbackAdapterModePortTestStep.Close,
                Exception: ex);
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
