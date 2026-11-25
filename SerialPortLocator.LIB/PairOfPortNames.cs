namespace SerialPortLocator.LIB;

using System;
using System.Collections.Generic;
using NaturalSort.Extension;

public sealed record PairOfPortNames : IComparable<PairOfPortNames>
{
    private static readonly NaturalSortComparer PortNameComparer
        = StringComparison.OrdinalIgnoreCase.WithNaturalSort();

    public string PortNameA { get; }

    public string PortNameB { get; }

    public PairOfPortNames(string portName, string otherPortName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        ArgumentException.ThrowIfNullOrWhiteSpace(otherPortName);

        List<string> names = [portName.Trim(), otherPortName.Trim()];
        names.Sort(PortNameComparer);

        PortNameA = names[0];
        PortNameB = names[1];
    }

    public int CompareTo(PairOfPortNames? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (other is null)
        {
            return 1;
        }

        var firstComparison = PortNameComparer.Compare(PortNameA,
            other.PortNameA);

        if (firstComparison != 0)
        {
            return firstComparison;
        }

        return PortNameComparer.Compare(PortNameB, other.PortNameB);
    }
}
