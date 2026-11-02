using System;
using System.Collections.Generic;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Model;
using Shouldly;
using Xunit;

namespace FaultTrace.Core.Tests;

public class ProviderFaultDetailsTests
{
    [Fact]
    public void Constructor_KeepsAllValues()
    {
        var origin = new FaultFrame("APP.ORDER_REQ", 1749);
        var entry = new FaultFrame("APP.ORDER_PACK", 775);
        var segment = new FaultSegment("ORA-01400", "message", new[] { origin, entry });

        var details = new ProviderFaultDetails(
            "Oracle",
            "ORA-01400",
            origin,
            entry,
            new[] { segment },
            new Dictionary<string, string> { ["column"] = "TREASURY_CODE" },
            "raw");

        details.Provider.ShouldBe("Oracle");
        details.NativeCode.ShouldBe("ORA-01400");
        details.Origin.ShouldBeSameAs(origin);
        details.Entry.ShouldBeSameAs(entry);
        details.Chain.ShouldBe(new[] { segment });
        details.Chain[0].Frames.ShouldBe(new[] { origin, entry });
        details.Details["column"].ShouldBe("TREASURY_CODE");
        details.RawMessage.ShouldBe("raw");
    }

    [Fact]
    public void Constructor_CollectionsAreNeverNull()
    {
        var details = new ProviderFaultDetails("Oracle");

        details.Chain.ShouldBeEmpty();
        details.Details.ShouldBeEmpty();
        new FaultSegment("ORA-00001").Frames.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_CopiesCollections()
    {
        var chain = new List<FaultSegment> { new FaultSegment("ORA-00001") };
        var values = new Dictionary<string, string> { ["table"] = "ORDERS" };

        var details = new ProviderFaultDetails("Oracle", chain: chain, details: values);
        chain.Clear();
        values.Clear();

        details.Chain.Count.ShouldBe(1);
        details.Details.Count.ShouldBe(1);
    }

    [Fact]
    public void Constructor_RejectsEmptyProvider()
    {
        Should.Throw<ArgumentException>(() => new ProviderFaultDetails(""));
    }

    [Fact]
    public void Segment_RejectsEmptyNativeCode()
    {
        Should.Throw<ArgumentException>(() => new FaultSegment(""));
    }

    [Theory]
    [InlineData("APP.ORDER_REQ", 1749, "APP.ORDER_REQ:1749")]
    [InlineData(null, 1, "anonymous:1")]
    public void Frame_ToString_ReturnsUnitAndLine(string? unit, int line, string expected)
    {
        new FaultFrame(unit, line).ToString().ShouldBe(expected);
    }

    [Fact]
    public void Context_KeepsAllValues()
    {
        var context = new FaultContext("M3QD-7HZA", "trace", "uz-Latn");

        context.ErrorId.ShouldBe("M3QD-7HZA");
        context.TraceId.ShouldBe("trace");
        context.Culture.ShouldBe("uz-Latn");
    }

    [Fact]
    public void Context_RejectsEmptyErrorId()
    {
        Should.Throw<ArgumentException>(() => new FaultContext(" "));
    }
}
