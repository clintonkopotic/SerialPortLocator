namespace SerialPortLocator.LIB;

using System.Runtime.CompilerServices;

public interface IFormatHelper
{
    string Format(object? @object,
        [CallerArgumentExpression(nameof(@object))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true);

    string FormatNameWithIndent(string? paramName, string indent);
}
