using System;
using FaultTrace.Oracle.Detection;
using Shouldly;
using Xunit;
using Driver = global::Oracle.ManagedDataAccess.Client;

namespace FaultTrace.Oracle.Tests.Detection;

public class OracleExceptionReaderTests
{
    [Fact]
    public void Find_ReadsNumberAndMessage()
    {
        var exception = new Driver.OracleException("ORA-01400: cannot insert NULL", 1400);

        var info = OracleExceptionReader.Find(exception);

        info.ShouldNotBeNull();
        info.Exception.ShouldBeSameAs(exception);
        info.Number.ShouldBe(1400);
        info.Message.ShouldBe("ORA-01400: cannot insert NULL");
    }

    [Fact]
    public void Find_RecognizesDerivedTypes()
    {
        var info = OracleExceptionReader.Find(new Driver.DerivedOracleException("ORA-00001: duplicate", 1));

        info.ShouldNotBeNull();
        info.Number.ShouldBe(1);
    }

    [Fact]
    public void Find_WalksInnerExceptions()
    {
        var oracle = new Driver.OracleException("ORA-00001: duplicate", 1);
        var wrapped = new InvalidOperationException("outer", new ApplicationException("middle", oracle));

        OracleExceptionReader.Find(wrapped)!.Exception.ShouldBeSameAs(oracle);
    }

    [Fact]
    public void Find_WalksAggregateExceptions()
    {
        var oracle = new Driver.OracleException("ORA-00001: duplicate", 1);
        var aggregate = new AggregateException(new InvalidOperationException("other"), oracle);

        OracleExceptionReader.Find(aggregate)!.Exception.ShouldBeSameAs(oracle);
    }

    [Fact]
    public void Find_ReturnsNullForNonOracleExceptions()
    {
        OracleExceptionReader.Find(new InvalidOperationException("Not a database error")).ShouldBeNull();
        OracleExceptionReader.Find(new AggregateException(new ArgumentException("x"))).ShouldBeNull();
        OracleExceptionReader.Find(null).ShouldBeNull();
    }

    [Fact]
    public void Find_StopsOnVeryDeepNesting()
    {
        Exception exception = new Driver.OracleException("ORA-00001: duplicate", 1);
        for (var i = 0; i < 100; i++)
        {
            exception = new InvalidOperationException("wrapper", exception);
        }

        OracleExceptionReader.Find(exception).ShouldBeNull();
    }

    [Fact]
    public void Find_BuildsMessageFromErrorsWhenMessageHasNoOracleText()
    {
        var errors = new[]
        {
            new Driver.OracleError(1400, "ORA-01400: cannot insert NULL"),
            new Driver.OracleError(6512, "ORA-06512: at line 1"),
        };

        var info = OracleExceptionReader.Find(new Driver.OracleException("Database failure", 1400, errors));

        info!.Message.ShouldBe("ORA-01400: cannot insert NULL\nORA-06512: at line 1");
    }

    [Fact]
    public void Find_SurvivesFailingDriverProperties()
    {
        var info = OracleExceptionReader.Find(new Driver.BrokenOracleException("ORA-00001: duplicate"));

        info.ShouldNotBeNull();
        info.Number.ShouldBeNull();
        info.Message.ShouldBe("ORA-00001: duplicate");
    }
}
