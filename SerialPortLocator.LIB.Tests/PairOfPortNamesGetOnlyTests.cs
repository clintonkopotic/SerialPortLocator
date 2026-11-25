namespace SerialPortLocator.LIB.Tests;

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class PairOfPortNamesTests
{
    [TestMethod]
    public void Constructor_OrdersPortNamesUsingNaturalSort()
    {
        var pair = new PairOfPortNames("COM10", "COM2");

        Assert.AreEqual("COM2", pair.PortNameA);
        Assert.AreEqual("COM10", pair.PortNameB);
    }

    [TestMethod]
    public void Constructor_SortsCaseInsensitivelyWhilePreservingTrimmedCasing()
    {
        var pair = new PairOfPortNames("  com10 \t", "\n COM2  ");

        Assert.AreEqual("COM2", pair.PortNameA);
        Assert.AreEqual("com10", pair.PortNameB);
    }

    [TestMethod]
    public void Constructor_WhenFirstPortNameIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new PairOfPortNames(null!, "COM1"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\t\r\n")]
    public void Constructor_WhenFirstPortNameIsEmptyOrWhitespace_ThrowsArgumentException(string invalidPortName)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new PairOfPortNames(invalidPortName, "COM1"));
    }

    [TestMethod]
    public void Constructor_WhenSecondPortNameIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new PairOfPortNames("COM1", null!));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\t\r\n")]
    public void Constructor_WhenSecondPortNameIsEmptyOrWhitespace_ThrowsArgumentException(string invalidPortName)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new PairOfPortNames("COM1", invalidPortName));
    }

    [TestMethod]
    public void Equality_ReversedInputsWithDifferentSpacingAndNaturalOrder_AreEqualWithMatchingHashCode()
    {
        var first = new PairOfPortNames("COM10", "COM2");
        var second = new PairOfPortNames("  COM2  ", "  COM10  ");

        Assert.AreEqual(first, second);
        Assert.IsTrue(first == second);
        Assert.IsFalse(first != second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equality_DifferentPortNames_AreNotEqual()
    {
        var first = new PairOfPortNames("COM1", "COM2");
        var second = new PairOfPortNames("COM1", "COM3");

        Assert.AreNotEqual(first, second);
        Assert.IsFalse(first == second);
        Assert.IsTrue(first != second);
    }

    [TestMethod]
    public void CompareTo_SameInstanceOrNull_ReturnsExpectedSign()
    {
        var pair = new PairOfPortNames("COM1", "COM2");

        Assert.AreEqual(0, pair.CompareTo(pair));
        Assert.IsGreaterThan(0, pair.CompareTo(null));
    }

    [TestMethod]
    public void CompareTo_ComparesFirstPortNaturallyThenSecondPortNaturally()
    {
        var com1Com10 = new PairOfPortNames("COM10", "COM1");
        var com2Com3 = new PairOfPortNames("COM3", "COM2");
        var com2Com10 = new PairOfPortNames("COM10", "COM2");

        Assert.IsLessThan(0, com1Com10.CompareTo(com2Com3));
        Assert.IsGreaterThan(0, com2Com3.CompareTo(com1Com10));

        Assert.IsLessThan(0, com2Com3.CompareTo(com2Com10));
        Assert.IsGreaterThan(0, com2Com10.CompareTo(com2Com3));
    }

    [TestMethod]
    public void CompareTo_CaseInsensitiveNaturalComparison_ReturnsZero()
    {
        var first = new PairOfPortNames("com10", "COM2");
        var second = new PairOfPortNames("COM2", "com10");

        Assert.AreEqual(0, first.CompareTo(second));
    }

    [TestMethod]
    public void ListSort_UsesIComparableNaturalOrder()
    {
        var pairC = new PairOfPortNames("COM2", "COM10");
        var pairA = new PairOfPortNames("COM1", "COM5");
        var pairB = new PairOfPortNames("COM2", "COM3");

        List<PairOfPortNames> pairs = [pairC, pairA, pairB];
        pairs.Sort();

        CollectionAssert.AreEqual(new[] { pairA, pairB, pairC }, pairs);
    }
}
