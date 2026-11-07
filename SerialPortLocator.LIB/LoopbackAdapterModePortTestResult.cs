namespace SerialPortLocator.LIB;

using System;

public abstract record LoopbackAdapterModePortTestResult
{
    private LoopbackAdapterModePortTestResult() { }

    public sealed record Failure(LoopbackAdapterModePortTestStep Step,
        string? Data = null, Exception? Exception = null)
        : LoopbackAdapterModePortTestResult;

    public sealed record Success(LoopbackAdapterModePortTestStep Step,
        string Data) : LoopbackAdapterModePortTestResult;
}
