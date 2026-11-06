namespace SerialPortLocator.LIB.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class LoopbackAdapterModeTesterTests
{
    [TestMethod]
    public void Test()
    {
        TextWriter writer = new StringWriter();
        _ = new LoopbackAdapterModeTester(options: new()
        {
            OriginalOrderOfPortNames = false,
            PortsToIgnore = new HashSet<string>(
                "COM1,COM2,COM3,COM4".Split(
                    separator: ',',
                    options: StringSplitOptions.RemoveEmptyEntries)),
            StatusWriter = writer,
        });

        Assert.AreEqual(
            actual: writer.ToString(),
            expected:
            """
              options.OriginalOrderOfPortNames: False.
              PortsToIgnore: "COM1,COM2,COM3,COM4".
              options.RetryPortsOnException: False.
              options.RetryPortsOnLocate: False.

            """);
    }
}
