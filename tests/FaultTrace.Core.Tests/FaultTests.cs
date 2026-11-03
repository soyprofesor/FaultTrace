using System;
using System.Collections.Generic;
using FaultTrace.Core.Model;
using Shouldly;
using Xunit;

namespace FaultTrace.Core.Tests;

public class FaultTests
{
    [Fact]
    public void Constructor_KeepsAllValues()
    {
        var provider = new ProviderFaultDetails("Oracle");
        var errors = Errors("amount", "Required");

        var fault = new Fault(
            "M3QD-7HZA",
            FaultCategory.Validation,
            FaultCodes.Validation,
            "Invalid input",
            400,
            isTransient: true,
            traceId: "trace",
            errors: errors,
            provider: provider);

        fault.ErrorId.ShouldBe("M3QD-7HZA");
        fault.Category.ShouldBe(FaultCategory.Validation);
        fault.Code.ShouldBe("validation");
        fault.UserMessage.ShouldBe("Invalid input");
        fault.Status.ShouldBe(400);
        fault.IsTransient.ShouldBeTrue();
        fault.TraceId.ShouldBe("trace");
        fault.Errors!["amount"].ShouldBe(["Required"]);
        fault.Provider.ShouldBeSameAs(provider);
    }

    [Fact]
    public void Constructor_OptionalValuesDefaultToEmpty()
    {
        var fault = Create();

        fault.IsTransient.ShouldBeFalse();
        fault.TraceId.ShouldBeNull();
        fault.Errors.ShouldBeNull();
        fault.Provider.ShouldBeNull();
    }

    [Theory]
    [InlineData("", "code", "message")]
    [InlineData(" ", "code", "message")]
    [InlineData("id", "", "message")]
    [InlineData("id", "code", "")]
    public void Constructor_RejectsEmptyRequiredText(string errorId, string code, string message)
    {
        Should.Throw<ArgumentException>(() => new Fault(errorId, FaultCategory.Internal, code, message, 500));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(399)]
    [InlineData(600)]
    public void Constructor_RejectsNonErrorStatus(int status)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Create(status: status));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(499)]
    [InlineData(599)]
    public void Constructor_AcceptsErrorStatus(int status)
    {
        Create(status: status).Status.ShouldBe(status);
    }

    [Fact]
    public void Constructor_RejectsErrorsOnNonValidationFault()
    {
        Should.Throw<ArgumentException>(() => new Fault(
            "id", FaultCategory.Internal, "code", "message", 500, errors: Errors("amount", "Required")));
    }

    [Fact]
    public void Constructor_TreatsEmptyErrorsAsNone()
    {
        var fault = new Fault(
            "id",
            FaultCategory.Internal,
            "code",
            "message",
            500,
            errors: new Dictionary<string, IReadOnlyList<string>>());

        fault.Errors.ShouldBeNull();
    }

    [Fact]
    public void Errors_AreNotAffectedByLaterChangesToTheInput()
    {
        var messages = new List<string> { "Required" };
        var errors = new Dictionary<string, IReadOnlyList<string>> { ["amount"] = messages };

        var fault = new Fault("id", FaultCategory.Validation, "code", "message", 400, errors: errors);
        messages.Add("Added later");
        errors["other"] = messages;

        fault.Errors!.Count.ShouldBe(1);
        fault.Errors["amount"].ShouldBe(["Required"]);
    }

    private static Fault Create(int status = 500) =>
        new("id", FaultCategory.Internal, FaultCodes.Unexpected, "message", status);

    private static Dictionary<string, IReadOnlyList<string>> Errors(string field, string message) =>
        new()
        { [field] = [message] };
}
