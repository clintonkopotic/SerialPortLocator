namespace SerialPortLocator.LIB;

using System;
using System.Globalization;
using System.IO.Ports;

public static class SerialPortHelper
{
    public const string BaudRateSeparator = "/";

    public const string DefaultSettings = "19200/8-N-1";

    public const string SettingsSeparator = "-";

    public static string GetSettings(ParseResult parseResult)
    {
        var baudRate = parseResult.BaudRate.ToString(
            CultureInfo.InvariantCulture);
        var dataBits = parseResult.DataBits.ToString(
            CultureInfo.InvariantCulture);
        var parity = parseResult.Parity switch
        {
            Parity.None => "N",
            Parity.Odd => "O",
            Parity.Even => "E",
            Parity.Mark => "M",
            Parity.Space => "S",
            _ => "?",
        };
        var stopBits = parseResult.StopBits switch
        {
            StopBits.None => "0",
            StopBits.One => "1",
            StopBits.Two => "2",
            StopBits.OnePointFive => "1.5",
            _ => "?",
        };

        return $"{baudRate}{BaudRateSeparator}{dataBits}{SettingsSeparator}"
            + $"{parity}{SettingsSeparator}{stopBits}";
    }

    public static string GetSettings(SerialPort serialPort)
        => GetSettings(new ParseResult(serialPort.BaudRate, serialPort.DataBits,
            serialPort.Parity, serialPort.StopBits));

    public static ParseResult ParseSettings(string settings)
    {
        var baudRateAndSettings = settings.Split(BaudRateSeparator,
            StringSplitOptions.TrimEntries);
        var baudRate = int.Parse(baudRateAndSettings[0]);

        var otherSettings = baudRateAndSettings[1].Split(SettingsSeparator,
            StringSplitOptions.TrimEntries);
        var dataBits = int.Parse(otherSettings[0]);
        var parity = otherSettings[1] switch
        {
            "N" => Parity.None,
            "O" => Parity.Odd,
            "E" => Parity.Even,
            "M" => Parity.Mark,
            "S" => Parity.Space,
            _ => throw new InvalidOperationException(
                message: $"Could not parse \"{otherSettings[1]}\" as "
                    + $"{nameof(Parity)}."),
        };
        var stopBits = otherSettings[2] switch
        {
            "0" => StopBits.None,
            "1" => StopBits.One,
            "1.5" => StopBits.OnePointFive,
            "2" => StopBits.None,
            _ => throw new InvalidOperationException(
                message: $"Could not parse \"{otherSettings[2]}\" as "
                    + $"{nameof(StopBits)}."),
        };

        return new ParseResult(baudRate, dataBits, parity, stopBits);
    }

    public sealed record ParseResult(int BaudRate, int DataBits, Parity Parity,
        StopBits StopBits);
}
