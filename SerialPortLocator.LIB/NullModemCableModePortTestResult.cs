namespace SerialPortLocator.LIB;

using System;

public abstract record NullModemCableModePortTestResult
{
    public string? Data { get; }

    public TimeSpan Duration { get; }

    public DateTimeOffset End { get; }

    public bool IsFailure { get; }

    public string PortName { get; }

    public DateTimeOffset Start { get; }

    public NullModemCableModePortTestStep Step { get; }

    private NullModemCableModePortTestResult(string portName,
        NullModemCableModePortTestStep step, string? data, bool isFailure,
        DateTimeOffset start, DateTimeOffset end)
    {
        PortName = portName;
        Step = step;
        Data = data;
        IsFailure = isFailure;
        Start = start;
        End = end;
        Duration = End - Start;
    }

    public sealed record Canceled(string PortName,
        NullModemCableModePortTestStep Step, string? Data = null,
        DateTimeOffset Start = default, DateTimeOffset End = default)
        : NullModemCableModePortTestResult(PortName, Step, Data, true, Start,
            End)
    {
        public new TimeSpan Duration { get; } = End - Start;
    }

    public sealed record Failure(string PortName,
        NullModemCableModePortTestStep Step, string? Data = null,
        Exception? Exception = null, DateTimeOffset Start = default,
        DateTimeOffset End = default)
        : NullModemCableModePortTestResult(PortName, Step, Data, true, Start,
            End)
    {
        public new TimeSpan Duration { get; } = End - Start;
    }

    public sealed record Success(string PortName,
        NullModemCableModePortTestStep Step, string? Data = null,
        DateTimeOffset Start = default, DateTimeOffset End = default)
        : NullModemCableModePortTestResult(PortName, Step, Data, false, Start,
            End)
    {
        public new TimeSpan Duration { get; } = End - Start;
    }
}
