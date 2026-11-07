namespace SerialPortLocator.LIB.Tests;

using System.IO.Ports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class SerialPortHelperTests
{
    [TestMethod]
    [DataRow(SerialPortHelper.DefaultSettings, 19_200, 8, Parity.None,
        StopBits.One, DisplayName = "Default Settings: "
            + $"\"{SerialPortHelper.DefaultSettings}\".")]
    [DataRow("9600/8-N-1", 9_600, 8, Parity.None,
        StopBits.One, DisplayName = "Settings: \"9600/8-N-1\".")]
    [DataRow("2400/7-O-1.5", 2_400, 7, Parity.Odd,
        StopBits.OnePointFive, DisplayName = "Settings: \"2400/7-O-1.5\".")]
    public void Test(string settings, int baudRate, int dataBits, Parity parity,
        StopBits stopBits)
    {
        var expectedResult = new SerialPortHelper.ParseResult(baudRate, dataBits,
            parity, stopBits);
        var actualResult = SerialPortHelper.ParseSettings(settings);

        Assert.AreEqual(expectedResult, actualResult);
        Assert.AreEqual(settings, SerialPortHelper.GetSettings(expectedResult));
        Assert.AreEqual(settings, SerialPortHelper.GetSettings(actualResult));
    }
}
