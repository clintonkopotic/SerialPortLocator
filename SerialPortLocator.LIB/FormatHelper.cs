namespace SerialPortLocator.LIB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

public class FormatHelper : IFormatHelper
{
    public const string None = "<none>";

    public const string Null = $"<{NullCore}>";

    public const string NullCore = "null";

    public const string Undefined = "<undefined>";

    public string Format(object? @object,
        [CallerArgumentExpression(nameof(@object))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true)
        => FormatCore(@object, paramName, indent, nameWithIndent);

    public string FormatNameWithIndent(string? paramName,
        string indent = "  ")
        => FormatNameWithIndentCore(paramName, indent);

    public static string Format(Enum @enum,
        [CallerArgumentExpression(nameof(@enum))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndentCore(paramName, indent));
        }

        var type = @enum.GetType();
        _ = output.Append(Enum.IsDefined(type, @enum)
            ? @enum.ToString() : Undefined);
        var underlyingType = Enum.GetUnderlyingType(type);
        object? underlyingValue = null;

        if (underlyingType == typeof(int))
        {
            underlyingValue = (int)(object)@enum;
        }
        else if (underlyingType == typeof(byte))
        {
            underlyingValue = (byte)(object)@enum;
        }
        else if (underlyingType == typeof(sbyte))
        {
            underlyingValue = (sbyte)(object)@enum;
        }
        else if (underlyingType == typeof(short))
        {
            underlyingValue = (short)(object)@enum;
        }
        else if (underlyingType == typeof(ushort))
        {
            underlyingValue = (ushort)(object)@enum;
        }
        else if (underlyingType == typeof(uint))
        {
            underlyingValue = (uint)(object)@enum;
        }
        else if (underlyingType == typeof(long))
        {
            underlyingValue = (long)(object)@enum;
        }
        else if (underlyingType == typeof(ulong))
        {
            underlyingValue = (ulong)(object)@enum;
        }

        _ = output.AppendFormat(" ({0})", underlyingValue is not null
            ? underlyingValue.ToString() : NullCore);

        if (nameWithIndent)
        {
            output.Append('.');
        }

        return output.ToString();
    }

    public static string Format(Exception exception,
        [CallerArgumentExpression(nameof(exception))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndentCore(paramName, indent));
        }

        _ = output.AppendFormat("Exception. {0}: {1}", exception.GetType(),
            exception.Message);

        if (nameWithIndent)
        {
            output.Append('.');
        }

        return output.ToString();
    }

    public static string Format(ICollection<string> strings,
        [CallerArgumentExpression(nameof(strings))] string?
        paramName = null, string indent = "  ", bool nameWithIndent = true)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndentCore(paramName, indent));
        }

        if (strings.Count > 0)
        {
            output.Append($"\"{CsvArgumentEncoder.Encode(strings)}\"");
        }
        else
        {
            output.Append(None);
        }

        if (nameWithIndent)
        {
            output.Append('.');
        }

        return output.ToString();
    }

    public static string FormatCore(IEnumerable<byte> bytes,
        [CallerArgumentExpression(nameof(bytes))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true,
        string separator = " ", bool lowercaseHex = false)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndentCore(paramName, indent));
        }

        _ = output.Append(string.Join(separator,
            bytes.Select(b => b.ToString(lowercaseHex ? "x2" : "X2"))));

        if (nameWithIndent)
        {
            _ = output.Append('.');
        }

        return output.ToString();
    }

    public static string Format(
        NullModemCableModePortTestResult.Failure failure,
        [CallerArgumentExpression(nameof(failure))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndentCore(paramName, indent));
        }

        if (failure.Exception is not null)
        {
            _ = output.AppendFormat("Exception at step {0}. {1}: {2}",
                Format(failure.Step, nameWithIndent: false),
                failure.Exception.GetType().FullName,
                failure.Exception.Message);
        }

        if (nameWithIndent)
        {
            _ = output.Append('.');
        }

        return output.ToString();
    }

    public static string FormatCore(object? @object,
        [CallerArgumentExpression(nameof(@object))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndentCore(paramName, indent));
        }

        if (@object is null)
        {
            _ = output.Append(Null);
        }
        else if (@object is string @string)
        {
            _ = output.Append(Format(@string, indent: string.Empty,
                nameWithIndent: false));
        }
        else if (@object is Enum @enum)
        {
            _ = output.Append(Format(@enum, indent: string.Empty,
                nameWithIndent: false));
        }
        else if (@object is IEnumerable<byte> enumerableBytes)
        {
            _ = output.Append(FormatCore(enumerableBytes,
                indent: string.Empty, nameWithIndent: false));
        }
        else if (@object is Queue<string> queueOfStrings)
        {
            _ = output.Append(Format(queueOfStrings,
                indent: string.Empty, nameWithIndent: false));
        }
        else if (@object is ICollection<string> collectionOfStrings)
        {
            _ = output.Append(Format(collectionOfStrings,
                indent: string.Empty, nameWithIndent: false));
        }
        else if (@object is Exception exception)
        {
            _ = output.Append(Format(exception,
                indent: string.Empty, nameWithIndent: false));
        }
        else if (@object is NullModemCableModePortTestResult.Failure failure)
        {
            _ = output.Append(Format(failure,
                indent: string.Empty, nameWithIndent: false));
        }
        else
        {
            _ = output.Append(@object);
        }

        if (nameWithIndent)
        {
            _ = output.Append('.');
        }

        return output.ToString();
    }

    public static string Format(Queue<string> collection,
        [CallerArgumentExpression(nameof(collection))] string?
        paramName = null, string indent = "  ", bool nameWithIndent = true)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndentCore(paramName, indent));
        }

        if (collection.Count > 0)
        {
            output.Append($"\"{CsvArgumentEncoder.Encode(collection)}\"");
        }
        else
        {
            output.Append(None);
        }

        if (nameWithIndent)
        {
            output.Append('.');
        }

        return output.ToString();
    }

    public static string Format(string @string,
        [CallerArgumentExpression(nameof(@string))] string? paramName = null,
        string indent = "  ", bool nameWithIndent = true)
    {
        StringBuilder output = new();

        if (nameWithIndent)
        {
            _ = output.Append(FormatNameWithIndentCore(paramName, indent));
        }

        _ = output.Append($"\"{@string}\"");

        if (nameWithIndent)
        {
            output.Append('.');
        }

        return output.ToString();
    }

    public static string Format<TEnum>(TEnum @enum,
        [CallerArgumentExpression(nameof(@enum))] string?
        paramName = null, string indent = "  ", bool nameWithIndent = true)
        where TEnum : struct, Enum
        => Format(@enum as Enum, paramName, indent, nameWithIndent);

    public static string FormatNameWithIndentCore(string? paramName,
        string indent = "  ")
        => $"{indent}{paramName}: ";
}
