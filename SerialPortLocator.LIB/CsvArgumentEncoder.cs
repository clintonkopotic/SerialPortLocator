namespace SerialPortLocator.LIB;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// A helper class for encoding strings into a comma separated string, while
/// preserving any commas within each string (see
/// <see cref="Encode(IEnumerable{string})"/>). The reverse can occur by
/// decoding of a comma separated string (see <see cref="Decode(string)"/>).
/// </summary>
public static partial class CsvArgumentEncoder
{
    /// <summary>
    /// The backslash (<c>'\'</c>) character. This is the escape character.
    /// </summary>
    /// <seealso cref="BackslashString"/>
    /// <seealso cref="EscapedBackslash"/>
    public const char Backslash = '\\';

    /// <summary>
    /// A <see cref="string"/> of the backslash (<c>'\'</c>) character (see
    /// <see cref="Backslash"/>).
    /// </summary>
    /// <seealso cref="Backslash"/>
    public static string BackslashString { get; } = $"{Backslash}";

    /// <summary>
    /// The comma (<c>','</c>) character. This character must be escaped when
    /// encoded (see <see cref="EscapedComma"/>).
    /// </summary>
    /// <seealso cref="CommaString"/>
    /// <seealso cref="EscapedComma"/>
    public const char Comma = ',';

    /// <summary>
    /// A <see cref="string"/> of the comma (<c>','</c>) character (see
    /// <see cref="Comma"/>).
    /// </summary>
    /// <seealso cref="Comma"/>
    public static string CommaString { get; } = $"{Comma}";

    /// <summary>
    /// A <see cref="string"/> of <c>"\\"</c>, which is the backslash
    /// (<c>'\'</c>) character escaped for use in an encoding.
    /// </summary>
    /// <seealso cref="Backslash"/>
    public static string EscapedBackslash { get; } = $"{Backslash}{Backslash}";

    /// <summary>
    /// A <see cref="string"/> of <c>"\,"</c>, which is the comma (<c>','</c>)
    /// character escaped for use in an encoding.
    /// </summary>
    /// <seealso cref="Backslash"/>
    /// <seealso cref="Comma"/>
    public static string EscapedComma { get; } = $"{Backslash}{Comma}";

    /// <summary>
    /// Decodes <paramref name="csvString"/> by splitting it along the
    /// <see cref="Comma"/> separator, unless it is escaped (see
    /// <see cref="EscapedComma"/>. Also, any escaped <see cref="Backslash"/>
    /// characters (see <see cref="EscapedBackslash"/>, will be replaced with
    /// just a single <see cref="Backslash"/> character.
    /// </summary>
    /// <param name="csvString">What to decode.</param>
    /// <returns></returns>
    public static string[] Decode(string csvString)
    {
        ArgumentNullException.ThrowIfNull(csvString);
        if (string.IsNullOrEmpty(csvString))
        {
            return [];
        }

        string[] fields = MatchCommaNotPrecededByBackslash().Split(csvString);
        List<string> decodedStrings = [];

        foreach (var field in fields)
        {
            decodedStrings.Add(field.Replace(EscapedComma, CommaString)
                .Replace(EscapedBackslash, BackslashString));
        }

        return [.. decodedStrings];
    }

    /// <summary>
    /// Encodes each item in <paramref name="fields"/> by
    /// escaping any special characters (see <see cref="Backslash"/> and
    /// <see cref="Comma"/>) and then separating them with the comma
    /// (<c>','</c>) character (see <see cref="Comma"/>).
    /// </summary>
    /// <param name="fields">What to separate with the comma
    /// (<c>','</c>) character (see <see cref="Comma"/>) after escaping any
    /// special characters.</param>
    /// <returns></returns>
    public static string Encode(IEnumerable<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        List<string> encodedStrings = [];

        foreach (var @string in fields)
        {
            encodedStrings.Add(@string.Replace(BackslashString, EscapedBackslash)
                .Replace(CommaString, EscapedComma));
        }

        return string.Join(Comma, encodedStrings);
    }

    [GeneratedRegex(@"(?<!\\),")]
    private static partial Regex MatchCommaNotPrecededByBackslash();
}
