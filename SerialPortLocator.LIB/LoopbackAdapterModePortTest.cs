namespace SerialPortLocator.LIB;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class LoopbackAdapterModePortTest(SerialPort serialPort,
    CancellationToken cancellationToken = default) : IDisposable
{
    private bool _disposedValue;

    private readonly Stopwatch _stopwatch = new();

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public SerialPort SerialPort { get; } = serialPort;

    public async Task<LoopbackAdapterModePortTestResult> Open()
    {
        try
        {
            CancellationToken.ThrowIfCancellationRequested();

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

    public async Task<LoopbackAdapterModePortTestResult> PreRead(
        int maxDurationInMilliseconds = 3_000,
        int sleepTimeoutInMilliseconds = 100)
    {
        try
        {
            CancellationToken.ThrowIfCancellationRequested();
            _stopwatch.Restart();

            while (_stopwatch.ElapsedMilliseconds < maxDurationInMilliseconds)
            {
                CancellationToken.ThrowIfCancellationRequested();
                var existing = SerialPort.ReadExisting();

                if (!string.IsNullOrEmpty(existing))
                {
                    _stopwatch.Stop();

                    return new LoopbackAdapterModePortTestResult.Success(
                        Step: LoopbackAdapterModePortTestStep.PreRead,
                        Data: existing);
                }

                await Task.Delay(sleepTimeoutInMilliseconds, CancellationToken);
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

    public async Task<LoopbackAdapterModePortTestResult> Close()
    {
        try
        {
            SerialPort.Close();
            CancellationToken.ThrowIfCancellationRequested();

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
