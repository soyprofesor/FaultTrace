using System.Linq;
using FaultTrace.Oracle.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace FaultTrace.Oracle.IntegrationTests;

// Expects exact package lines, which pins down how Oracle orders the stack.
[Collection(OracleCollection.Name)]
public class OracleStackTests
{
    private readonly OracleTestDatabase _database;

    public OracleStackTests(OracleTestDatabase database)
    {
        _database = database;
    }

    [OracleFact]
    public void Business_HasItsOwnLine()
    {
        OracleTestDatabase.Call("BUSINESS").Provider!.Origin!.ToString().ShouldBe(_database.Frame("business"));
    }

    [OracleFact]
    public void PlainStack_FindsOriginAndEntry()
    {
        var details = OracleTestDatabase.Call("PLAIN_STACK").Provider!;

        details.Origin!.ToString().ShouldBe(_database.Frame("level3"));
        details.Entry!.ToString().ShouldBe(_database.Frame("level1"));
    }

    [OracleFact]
    public void NestedRaise_StartsAtTheLastRaiseAndKeepsTheRealLineInTheChain()
    {
        var details = OracleTestDatabase.Call("NESTED_RAISE").Provider!;

        details.NativeCode.ShouldBe("ORA-01400");

        // Plain RAISE leaves one flat list that starts at the last RAISE, so the real line is only found in the chain.
        details.Origin!.ToString().ShouldBe(_database.Frame("nested1"));
        details.Chain.Single().Frames.Select(frame => frame.ToString()).ShouldContain(_database.Frame("level3"));
        details.Entry!.Unit.ShouldBe(_database.Pack);
    }

    [OracleFact]
    public void KeepErrors_TakesOriginFromTheTechnicalError()
    {
        var fault = OracleTestDatabase.Call("KEEP_ERRORS");

        fault.Code.ShouldBe("business");
        fault.UserMessage.ShouldBe("Could not save");
        fault.Provider!.Origin!.ToString().ShouldBe(_database.Frame("keep_insert"));
        fault.Provider.Chain.Select(segment => segment.NativeCode).ShouldContain("ORA-01400");
    }

    [OracleFact]
    public void WithoutKeepErrors_OriginIsTheRaiseLine()
    {
        var fault = OracleTestDatabase.Call("DROP_ERRORS");

        fault.UserMessage.ShouldBe("Could not save");
        fault.Provider!.Origin!.ToString().ShouldBe(_database.Frame("drop_raise"));
    }

    [OracleFact]
    public void LabelledBacktrace_RecoversTheOrigin()
    {
        var fault = OracleTestDatabase.Call("BACKTRACE_LABELLED");

        fault.Provider!.Origin!.ToString().ShouldBe(_database.Frame("labelled_insert"));
        fault.UserMessage.ShouldBe("Could not save");
    }

    [OracleFact]
    public void PlainBacktrace_RecoversTheOrigin()
    {
        var fault = OracleTestDatabase.Call("BACKTRACE_PLAIN");

        fault.Provider!.Origin!.ToString().ShouldBe(_database.Frame("plain_insert"));
        fault.UserMessage.ShouldBe("Could not save");
    }

    [OracleFact]
    public void IgnoredPackage_IsLeftOutOfOriginButNotOutOfTheRawMessage()
    {
        var details = OracleTestDatabase.Call("THROUGH_HELPER", options => options.IgnoredPackages.Add(OracleTestObjects.Helper)).Provider!;

        details.Origin!.ToString().ShouldBe(_database.Frame("helper_call"));
        details.RawMessage!.ShouldContain(OracleTestObjects.Helper);
    }

    [OracleFact]
    public void Trigger_IsTheOrigin()
    {
        var details = OracleTestDatabase.Call("GUARDED_INSERT").Provider!;

        details.Origin!.Unit.ShouldBe(_database.Trigger);
        details.Entry!.Unit.ShouldBe(_database.Pack);
    }

    [OracleFact]
    public void TechnicalErrorInATrigger_HasTheTriggerAsItsOrigin()
    {
        var fault = OracleTestDatabase.Call("GUARDED_TECHNICAL");

        fault.Code.ShouldBe("required_field");
        fault.Provider!.Origin!.Unit.ShouldBe(_database.Trigger);
        fault.Provider.Entry!.Unit.ShouldBe(_database.Pack);
    }
}
