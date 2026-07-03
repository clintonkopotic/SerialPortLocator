namespace SerialPortLocator.LIB.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SerialPortLocator.LIB;

[TestClass]
public sealed class CsvArgumentEncoderTests
{
    [TestMethod]
    [DataRow("", 0, DisplayName = "Empty")]
    [DataRow(" \t", 1, DisplayName = "White-space characters")]
    [DataRow("COM1,COM2", 2, DisplayName = "\"COM1,COM2\"")]
    [DataRow("COM1\\,COM2\\\\", 1, DisplayName = "\"COM1\\,COM2\\\\\"")]
    public void RoundTripTest(string whatToDecodeAndThenEncode,
        int expectedNumberOfFields)
    {
        var fields = CsvArgumentEncoder.Decode(whatToDecodeAndThenEncode);
        Assert.HasCount(expectedNumberOfFields, fields);
        Assert.AreEqual(whatToDecodeAndThenEncode,
            CsvArgumentEncoder.Encode(fields));
    }

    [TestMethod]
    public void NullTest()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            CsvArgumentEncoder.Decode(null!);
        });
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            CsvArgumentEncoder.Encode(null!);
        });
    }
}
