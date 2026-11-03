using System;
using System.Data;
using FaultTrace.Core.Model;
using FaultTrace.Oracle.IntegrationTests.Infrastructure;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace FaultTrace.Oracle.IntegrationTests;

[Collection(OracleCollection.Name)]
public class OracleErrorTests
{
    private readonly OracleTestDatabase _database;

    public OracleErrorTests(OracleTestDatabase database)
    {
        _database = database;
    }

    [OracleFact]
    public void DriverException_IsReadThroughReflection()
    {
        var exception = OracleTestDatabase.Catch(OracleTestObjects.Pack + ".BUSINESS", CommandType.StoredProcedure);

        var fault = OracleTestDatabase.Translator().Translate(exception);

        exception.Number.ShouldBe(20001);
        fault.Provider!.Provider.ShouldBe("Oracle");
        fault.Provider.NativeCode.ShouldBe("ORA-20001");
        fault.Provider.RawMessage.ShouldBe(exception.Message);
    }

    [OracleFact]
    public void WrappedDriverException_IsFound()
    {
        var exception = OracleTestDatabase.Catch(OracleTestObjects.Pack + ".BUSINESS", CommandType.StoredProcedure);

        var fault = OracleTestDatabase.Translator().Translate(new AggregateException(new InvalidOperationException("outer", exception)));

        fault.Code.ShouldBe(FaultCodes.Business);
    }

    [OracleFact]
    public void Business_ShowsTheTextWithoutCodes()
    {
        var fault = OracleTestDatabase.Call("BUSINESS");

        fault.Category.ShouldBe(FaultCategory.Business);
        fault.Code.ShouldBe(FaultCodes.Business);
        fault.Status.ShouldBe(400);
        fault.UserMessage.ShouldBe("Limit exceeded");
        ShouldBeSafe(fault);
    }

    [OracleFact]
    public void BusinessOnSeveralLines_KeepsThem()
    {
        OracleTestDatabase.Call("BUSINESS_MULTILINE").UserMessage.ShouldBe("First line\nSecond line");
    }

    [OracleFact]
    public void BusinessWithoutText_ShowsTheGenericText()
    {
        var fault = OracleTestDatabase.Call("BUSINESS_EMPTY", culture: "ru");

        fault.Code.ShouldBe(FaultCodes.Business);
        fault.UserMessage.ShouldBe("Не удалось выполнить запрос.");
    }

    [OracleFact]
    public void DuplicatedPrefix_IsRemoved()
    {
        OracleTestDatabase.Call("DUPLICATED_PREFIX").UserMessage.ShouldBe("Limit exceeded");
    }

    [OracleFact]
    public void SqlerrmInTheText_LosesOnlyTheCode()
    {
        var fault = OracleTestDatabase.Call("BUSINESS_WITH_SQLERRM");

        fault.Code.ShouldBe(FaultCodes.Business);
        fault.UserMessage.ShouldNotContain("ORA-");
        fault.UserMessage.ShouldStartWith("Failed: ");
    }

    [OracleFact]
    public void BusinessFromATrigger_ShowsOnlyTheText()
    {
        var fault = OracleTestDatabase.Call("GUARDED_INSERT");

        fault.Code.ShouldBe(FaultCodes.Business);
        fault.UserMessage.ShouldBe("Row is locked");
        ShouldBeSafe(fault);
    }

    [OracleFact]
    public void BusinessFromAFunction_IsTranslated()
    {
        var fault = OracleTestDatabase.Fails("DECLARE v NUMBER; BEGIN v := " + OracleTestObjects.Pack + ".BUSINESS_VALUE; END;");

        fault.Code.ShouldBe(FaultCodes.Business);
        fault.UserMessage.ShouldBe("Limit exceeded");
    }

    [OracleFact]
    public void BusinessFromAFunctionInASelect_IsTranslated()
    {
        var fault = OracleTestDatabase.Fails("SELECT " + OracleTestObjects.Pack + ".BUSINESS_VALUE FROM DUAL");

        fault.Code.ShouldBe(FaultCodes.Business);
        fault.UserMessage.ShouldBe("Limit exceeded");
        fault.Provider!.Origin!.ToString().ShouldBe(_database.Frame("function"));
    }

    [OracleFact]
    public void RequiredOnInsert_HasTheColumn()
    {
        var fault = OracleTestDatabase.Call("REQUIRED_INSERT");

        fault.Code.ShouldBe(FaultCodes.RequiredField);
        fault.Status.ShouldBe(400);
        fault.Provider!.NativeCode.ShouldBe("ORA-01400");
        fault.Provider.Details["schema"].ShouldBe(_database.Schema);
        fault.Provider.Details["table"].ShouldBe("FAULTTRACE_TEST_PARENT");
        fault.Provider.Details["column"].ShouldBe("NAME");
        ShouldBeSafe(fault);
    }

    [OracleFact]
    public void RequiredOnUpdate_HasTheColumn()
    {
        var fault = OracleTestDatabase.Call("REQUIRED_UPDATE");

        fault.Code.ShouldBe(FaultCodes.RequiredField);
        fault.Provider!.NativeCode.ShouldBe("ORA-01407");
        fault.Provider.Details["column"].ShouldBe("NAME");
        ShouldBeSafe(fault);
    }

    [OracleFact]
    public void MappedColumn_IsNamedForTheUser()
    {
        var fault = OracleTestDatabase.Call("REQUIRED_INSERT", options => options.MapColumn("NAME", new System.Collections.Generic.Dictionary<string, string> { ["en"] = "Customer name" }));

        fault.UserMessage.ShouldContain("Customer name");
        ShouldBeSafe(fault);
    }

    [OracleFact]
    public void TooLong_HasTheColumn()
    {
        var fault = OracleTestDatabase.Call("TOO_LONG");

        fault.Code.ShouldBe(FaultCodes.ValueTooLong);
        fault.Provider!.NativeCode.ShouldBe("ORA-12899");
        fault.Provider.Details["column"].ShouldBe("NAME");
        ShouldBeSafe(fault);
    }

    [OracleFact]
    public void Duplicate_HasTheConstraint()
    {
        var fault = OracleTestDatabase.Call("DUPLICATE");

        fault.Code.ShouldBe(FaultCodes.Duplicate);
        fault.Status.ShouldBe(409);
        fault.Provider!.Details["constraint"].ShouldBe("FAULTTRACE_TEST_PARENT_PK");
        ShouldBeSafe(fault);
    }

    [OracleFact]
    public void MissingParent_HasTheConstraint()
    {
        var fault = OracleTestDatabase.Call("MISSING_PARENT");

        fault.Code.ShouldBe(FaultCodes.ReferenceNotFound);
        fault.Provider!.Details["constraint"].ShouldBe("FAULTTRACE_TEST_CHILD_FK");
        ShouldBeSafe(fault);
    }

    [OracleFact]
    public void HasChildren_HasTheConstraint()
    {
        var fault = OracleTestDatabase.Call("HAS_CHILDREN");

        fault.Code.ShouldBe(FaultCodes.HasDependents);
        fault.Status.ShouldBe(409);
        fault.Provider!.Details["constraint"].ShouldBe("FAULTTRACE_TEST_CHILD_FK");
        ShouldBeSafe(fault);
    }

    [OracleFact]
    public void MappedConstraint_ShowsItsMessage()
    {
        var fault = OracleTestDatabase.Call("DUPLICATE", options => options.MapConstraint("FAULTTRACE_TEST_PARENT_PK", new System.Collections.Generic.Dictionary<string, string> { ["en"] = "This order already exists" }));

        fault.UserMessage.ShouldBe("This order already exists");
    }

    [OracleFact]
    public void TooLarge_IsAnInvalidFormat()
    {
        AssertTechnical(OracleTestDatabase.Call("TOO_LARGE"), FaultCodes.InvalidFormat, 400, "ORA-01438");
    }

    [OracleFact]
    public void BadNumber_IsAnInvalidFormat()
    {
        AssertTechnical(OracleTestDatabase.Call("BAD_NUMBER"), FaultCodes.InvalidFormat, 400, "ORA-01722");
    }

    [OracleFact]
    public void BadDate_IsAnInvalidFormat()
    {
        AssertTechnical(OracleTestDatabase.Call("BAD_DATE"), FaultCodes.InvalidFormat, 400, "ORA-01830");
    }

    [OracleFact]
    public void NoData_IsUnexpected()
    {
        AssertTechnical(OracleTestDatabase.Call("NO_DATA"), FaultCodes.Unexpected, 500, "ORA-01403");
    }

    [OracleFact]
    public void TooManyRows_IsUnexpected()
    {
        AssertTechnical(OracleTestDatabase.Call("TOO_MANY"), FaultCodes.Unexpected, 500, "ORA-01422");
    }

    [OracleFact]
    public void ValueError_IsUnexpected()
    {
        AssertTechnical(OracleTestDatabase.Call("VALUE_ERROR"), FaultCodes.Unexpected, 500, "ORA-06502");
    }

    [OracleFact]
    public void MissingTable_IsUnexpected()
    {
        AssertTechnical(OracleTestDatabase.Call("MISSING_TABLE"), FaultCodes.Unexpected, 500, "ORA-00942");
    }

    [OracleFact]
    public void PlainSqlOnAMissingTable_IsUnexpected()
    {
        AssertTechnical(OracleTestDatabase.Fails("DELETE FROM FAULTTRACE_TEST_MISSING"), FaultCodes.Unexpected, 500, "ORA-00942");
    }

    [OracleFact]
    public void BlockThatDoesNotCompile_IsUnexpected()
    {
        AssertTechnical(OracleTestDatabase.Fails("BEGIN FAULTTRACE_TEST_MISSING; END;"), FaultCodes.Unexpected, 500, "ORA-06550");
    }

    [OracleFact]
    public void UnknownCode_IsMarkedUnmapped()
    {
        var fault = OracleTestDatabase.Call("DIVIDE_BY_ZERO");

        AssertTechnical(fault, FaultCodes.Unexpected, 500, "ORA-01476");
        fault.Provider!.IsUnmapped.ShouldBeTrue();
    }

    private void AssertTechnical(Fault fault, string code, int status, string nativeCode)
    {
        fault.Code.ShouldBe(code);
        fault.Status.ShouldBe(status);
        fault.Provider!.NativeCode.ShouldBe(nativeCode);
        ShouldBeSafe(fault);
    }

    private void ShouldBeSafe(Fault fault)
    {
        fault.UserMessage.ShouldNotBeNullOrWhiteSpace();
        fault.UserMessage.ShouldNotContain("ORA-");
        fault.UserMessage.ShouldNotContain("docs.oracle.com");
        fault.UserMessage.ShouldNotContain("FAULTTRACE_TEST");
        fault.UserMessage.ShouldNotContain(_database.Schema + ".");
        fault.UserMessage.ShouldNotContain("\"" + _database.Schema + "\"");
    }
}
