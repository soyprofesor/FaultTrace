using System;
using System.Collections.Generic;
using System.Data;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.DependencyInjection;
using FaultTrace.Core.Model;
using FaultTrace.Oracle.DependencyInjection;
using FaultTrace.Oracle.Options;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace FaultTrace.Oracle.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class OracleCollection : ICollectionFixture<OracleTestDatabase>
{
    public const string Name = "Oracle";
}

// Creates the test objects once for all tests and drops them at the end. Only objects named FAULTTRACE_TEST_* are touched.
public sealed class OracleTestDatabase : IDisposable
{
    public const string SkipReason = "Set " + VariableName + " to run the tests against a test database.";

    private const string VariableName = "FAULTTRACE_TEST_ORACLE_CONNECTION";

    private readonly Dictionary<string, int> _lines;

    public OracleTestDatabase()
    {
        _lines = OracleTestObjects.FindLines();
        if (!IsConfigured)
        {
            return;
        }

        Drop();
        foreach (var statement in OracleTestObjects.Statements)
        {
            Execute(statement);
        }

        Schema = (string)Scalar("SELECT USER FROM DUAL");
    }

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    public static string? ConnectionString => Environment.GetEnvironmentVariable(VariableName);

    public string Schema { get; } = string.Empty;

    public string Pack => Schema + "." + OracleTestObjects.Pack;

    public string Helper => Schema + "." + OracleTestObjects.Helper;

    public string Trigger => Schema + "." + OracleTestObjects.Trigger;

    public string Frame(string marker) => Pack + ":" + _lines[marker];

    public Fault Call(string procedure, Action<OracleFaultOptions>? configure = null, string? culture = null, Action<OracleConnection>? prepare = null)
    {
        return Fails(
            OracleTestObjects.Pack + "." + procedure,
            CommandType.StoredProcedure,
            configure,
            culture,
            prepare);
    }

    public Fault Fails(
        string text,
        CommandType type = CommandType.Text,
        Action<OracleFaultOptions>? configure = null,
        string? culture = null,
        Action<OracleConnection>? prepare = null,
        int? commandTimeout = null)
    {
        var exception = Catch(text, type, prepare, commandTimeout);
        return Translator(configure).Translate(exception, culture);
    }

    public OracleException Catch(string text, CommandType type = CommandType.Text, Action<OracleConnection>? prepare = null, int? commandTimeout = null)
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();
        prepare?.Invoke(connection);

        using var command = connection.CreateCommand();
        command.CommandText = text;
        command.CommandType = type;
        if (commandTimeout != null)
        {
            command.CommandTimeout = commandTimeout.Value;
        }

        try
        {
            using var reader = command.ExecuteReader();
            reader.Read();
        }
        catch (OracleException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(text + " did not fail.");
    }

    public static IFaultTranslator Translator(Action<OracleFaultOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFaultTraceCore().AddOracle(configure);
        return services.BuildServiceProvider().GetRequiredService<IFaultTranslator>();
    }

    public void Execute(string text)
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = text;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (IsConfigured)
        {
            Drop();
        }
    }

    private object Scalar(string text)
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = text;
        return command.ExecuteScalar();
    }

    private void Drop()
    {
        foreach (var statement in OracleTestObjects.DropStatements)
        {
            try
            {
                Execute(statement);
            }
            catch (OracleException exception) when (exception.Number == 942 || exception.Number == 4043 || exception.Number == 4080)
            {
                // Already gone: the object doesn't exist.
            }
        }
    }
}
