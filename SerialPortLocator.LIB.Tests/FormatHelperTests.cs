namespace SerialPortLocator.LIB.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NaturalSort.Extension;

[TestClass]
public sealed class FormatHelperTests
{
    [TestMethod]
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible "
        + "for improved performance", Justification = "Testing purposes.")]
    public void Test()
    {
        IFormatHelper helper = new FormatHelper();
        Assert.AreEqual(
            expected: "  nullVariable: <null>.",
            actual: helper.Format(null, "nullVariable"));

        var sortComparer = StringComparison.OrdinalIgnoreCase.WithNaturalSort();

        var locatorMode = LocatorMode.LoopbackAdapter;

        Assert.AreEqual(
            expected: "locatorMode: LoopbackAdapter (1).",
            actual: helper.Format(locatorMode, indent: string.Empty));

        var originalOrderOfPortNames = false;

        Assert.AreEqual(
            expected: "  originalOrderOfPortNames: False.",
            actual: helper.Format(originalOrderOfPortNames));

        ISet<string> portsToTest = new SortedSet<string>(
            collection: "COM3,COM4,COM5,COM6,COM7".Split(
                separator:',',
                options: StringSplitOptions.RemoveEmptyEntries),
            comparer: sortComparer);

        Assert.AreEqual(
            expected: "  portsToTest: \"COM3,COM4,COM5,COM6,COM7\".",
            actual: helper.Format(portsToTest));

        ISet<string> portsToIgnore = new SortedSet<string>(
            collection: "".Split(
                separator: ',',
                options: StringSplitOptions.RemoveEmptyEntries),
            comparer: sortComparer);

        Assert.AreEqual(
            expected: "  portsToIgnore: <none>.",
            actual: helper.Format(portsToIgnore));


        var portNames = new Queue<string>(portsToTest);

        Assert.AreEqual(
            expected: "  portNames: \"COM3,COM4,COM5,COM6,COM7\".",
            actual: helper.Format(portNames));
    }
}
