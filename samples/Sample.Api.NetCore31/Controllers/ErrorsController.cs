using System;
using System.Collections.Generic;
using FaultTrace.AspNetCore.Results;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace Sample.Api.NetCore31.Controllers;

[ApiController]
[Route("errors")]
public sealed class ErrorsController : ControllerBase
{
    // Anonymous blocks, so the sample needs no objects in the database.
    private static readonly Dictionary<string, string> OracleErrors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["business"] = "BEGIN RAISE_APPLICATION_ERROR(-20001, 'The limit is exceeded'); END;",
        ["no-data"] = "DECLARE v NUMBER; BEGIN SELECT 1 INTO v FROM DUAL WHERE 1 = 0; END;",
        ["bad-number"] = "DECLARE v NUMBER; BEGIN SELECT TO_NUMBER('abc') INTO v FROM DUAL; END;",
        ["missing-table"] = "DELETE FROM FAULTTRACE_SAMPLE_MISSING",
    };

    private readonly IFaultTranslator _translator;
    private readonly IFaultFactory _faults;
    private readonly IConfiguration _configuration;

    public ErrorsController(IFaultTranslator translator, IFaultFactory faults, IConfiguration configuration)
    {
        _translator = translator;
        _faults = faults;
        _configuration = configuration;
    }

    // Any exception nobody catches becomes the generic "unexpected" response.
    [HttpGet("bug")]
    public IActionResult Bug() => throw new InvalidOperationException("Internal details the user never sees");

    // A project's own exception, recognized through IFaultException.
    [HttpGet("project")]
    public IActionResult Project() => throw new OrderClosedException();

    // A project's own exception, mapped in Startup.cs without changing its class.
    [HttpGet("mapped")]
    public IActionResult Mapped() => throw new KeyNotFoundException();

    // Caught locally, because a decision depends on it, and still returned in the same format.
    [HttpGet("caught")]
    public IActionResult Caught()
    {
        try
        {
            throw new TimeoutException();
        }
        catch (Exception exception)
        {
            var fault = _translator.Translate(exception);
            return this.FaultResult(fault);
        }
    }

    // Needs ConnectionStrings__Oracle. The Oracle message never reaches a production response.
    [HttpGet("oracle/{name}")]
    public IActionResult Oracle(string name)
    {
        if (!OracleErrors.TryGetValue(name, out var sql))
        {
            return NotFound();
        }

        var connectionString = _configuration.GetConnectionString("Oracle");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return this.FaultResult(_faults.Create(
                FaultCategory.Transient,
                FaultCodes.Unavailable,
                StatusCodes.Status503ServiceUnavailable,
                detail: "Set ConnectionStrings__Oracle to run the Oracle examples."));
        }

        using var connection = new OracleConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();

        return NoContent();
    }
}
