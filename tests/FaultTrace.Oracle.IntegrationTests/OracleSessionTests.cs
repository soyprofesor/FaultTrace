using System;
using System.Data;
using FaultTrace.Core.Model;
using FaultTrace.Oracle.IntegrationTests.Infrastructure;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace FaultTrace.Oracle.IntegrationTests;

[Collection(OracleCollection.Name)]
public class OracleSessionTests
{
    private readonly OracleTestDatabase _database;

    public OracleSessionTests(OracleTestDatabase database)
    {
        _database = database;
    }

    [OracleFact]
    public void RussianSession_GivesTheSameResult()
    {
        var fault = OracleTestDatabase.Call("REQUIRED_INSERT", prepare: Russian);

        fault.Code.ShouldBe(FaultCodes.RequiredField);
        fault.Provider!.NativeCode.ShouldBe("ORA-01400");
        fault.Provider.Details["table"].ShouldBe("FAULTTRACE_TEST_PARENT");
        fault.Provider.Details["column"].ShouldBe("NAME");
        fault.Provider.Origin!.Unit.ShouldBe(_database.Pack);
    }

    [OracleFact]
    public void RussianSession_FindsTheConstraint()
    {
        var fault = OracleTestDatabase.Call("DUPLICATE", prepare: Russian);

        fault.Code.ShouldBe(FaultCodes.Duplicate);
        fault.Provider!.Details["constraint"].ShouldBe("FAULTTRACE_TEST_PARENT_PK");
    }

    [OracleFact]
    public void CommandTimeout_IsATimeout()
    {
        var fault = OracleTestDatabase.Fails(OracleTestObjects.Pack + ".SLOW", CommandType.StoredProcedure, commandTimeout: 1);

        fault.Code.ShouldBe(FaultCodes.Timeout);
        fault.Status.ShouldBe(504);
        fault.IsTransient.ShouldBeTrue();
    }

    [OracleFact]
    public void NobodyListening_IsUnavailable()
    {
        var builder = new OracleConnectionStringBuilder(OracleTestDatabase.ConnectionString)
        {
            DataSource = "localhost:1/NOTHING",
            ConnectionTimeout = 5,
        };
        using var connection = new OracleConnection(builder.ConnectionString);

        var exception = Should.Throw<OracleException>(() => connection.Open());
        var fault = OracleTestDatabase.Translator().Translate(exception);

        fault.Code.ShouldBe(FaultCodes.Unavailable);
        fault.Status.ShouldBe(503);
        fault.IsTransient.ShouldBeTrue();
        fault.UserMessage.ShouldNotContain("ORA-");
        fault.UserMessage.ShouldNotContain("localhost");
    }

    [OracleFact]
    public void ExhaustedPool_IsUnavailable()
    {
        var builder = new OracleConnectionStringBuilder(OracleTestDatabase.ConnectionString)
        {
            MaxPoolSize = 1,
            MinPoolSize = 0,
            ConnectionTimeout = 2,
        };
        using var held = new OracleConnection(builder.ConnectionString);
        held.Open();
        using var connection = new OracleConnection(builder.ConnectionString);

        var exception = Should.Throw<OracleException>(() => connection.Open());
        var fault = OracleTestDatabase.Translator().Translate(exception);

        fault.Code.ShouldBe(FaultCodes.Unavailable);
        fault.IsTransient.ShouldBeTrue();
    }

    private static void Russian(OracleConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET NLS_LANGUAGE = RUSSIAN";
        command.ExecuteNonQuery();
    }
}
