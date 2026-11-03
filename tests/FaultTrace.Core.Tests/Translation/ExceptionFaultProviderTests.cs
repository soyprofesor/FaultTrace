using System;
using System.Collections.Generic;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using FaultTrace.Core.Options;
using FaultTrace.Core.Translation;
using Shouldly;
using Xunit;

namespace FaultTrace.Core.Tests.Translation;

public class ExceptionFaultProviderTests
{
    private readonly CapturingLogger _logger = new CapturingLogger();
    private readonly FaultTraceOptions _options = new FaultTraceOptions();

    [Theory]
    [InlineData(400, FaultCategory.Business)]
    [InlineData(401, FaultCategory.Unauthorized)]
    [InlineData(403, FaultCategory.Forbidden)]
    [InlineData(404, FaultCategory.NotFound)]
    [InlineData(410, FaultCategory.NotFound)]
    [InlineData(429, FaultCategory.Transient)]
    [InlineData(409, FaultCategory.Conflict)]
    [InlineData(422, FaultCategory.Business)]
    [InlineData(499, FaultCategory.ClientClosed)]
    [InlineData(500, FaultCategory.Internal)]
    [InlineData(502, FaultCategory.Internal)]
    [InlineData(503, FaultCategory.Transient)]
    [InlineData(504, FaultCategory.Timeout)]
    public void Status_DecidesTheCategory(int status, FaultCategory category)
    {
        var fault = Translate(new ProjectException(status, "own"));

        fault.ShouldNotBeNull();
        fault!.Category.ShouldBe(category);
        fault.Status.ShouldBe(status);
    }

    [Theory]
    [InlineData(503, true)]
    [InlineData(504, true)]
    [InlineData(400, false)]
    [InlineData(500, false)]
    public void OnlyTemporaryStatuses_AreTransient(int status, bool transient)
    {
        Translate(new ProjectException(status, "own"))!.IsTransient.ShouldBe(transient);
    }

    [Fact]
    public void Errors_MakeItAValidationFault()
    {
        var errors = new Dictionary<string, IReadOnlyList<string>> { ["amount"] = new[] { "Too large" } };

        var fault = Translate(new ProjectException(404, "own", "Check it", errors));

        fault!.Category.ShouldBe(FaultCategory.Validation);
        fault.Errors!["amount"].ShouldBe(new[] { "Too large" });
    }

    [Fact]
    public void Detail_IsShownAsWritten()
    {
        Translate(new ProjectException(400, "own", "Order is closed"))!.UserMessage.ShouldBe("Order is closed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void EmptyDetailWithABuiltInCode_UsesItsText(string? detail)
    {
        Translate(new ProjectException(409, FaultCodes.Duplicate, detail), "ru")!.UserMessage.ShouldBe("Такая запись уже существует.");
    }

    [Fact]
    public void EmptyDetailWithAnOwnCode_UsesTheTextOfTheCategory()
    {
        Translate(new ProjectException(404, "own"), "ru")!.UserMessage.ShouldBe("Запрошенные данные не найдены.");
    }

    [Fact]
    public void InnerException_IsFound()
    {
        var fault = Translate(new InvalidOperationException("outer", new ProjectException(400, "own")));

        fault!.Code.ShouldBe("own");
    }

    [Fact]
    public void AggregateException_IsSearched()
    {
        var fault = Translate(new AggregateException(new InvalidOperationException(), new ProjectException(400, "own")));

        fault!.Code.ShouldBe("own");
    }

    [Fact]
    public void OtherException_IsNotRecognized()
    {
        Translate(new InvalidOperationException()).ShouldBeNull();
    }

    [Fact]
    public void Mapping_WinsOverTheInterface()
    {
        _options.MapException<ProjectException>(_ => new FaultProblem(400, "mapped"));

        Translate(new ProjectException(400, "own"))!.Code.ShouldBe("mapped");
    }

    [Fact]
    public void Mapping_CoversDerivedTypes()
    {
        _options.MapException<InvalidOperationException>(_ => new FaultProblem(400, "base"));

        Translate(new ObjectDisposedException("x"))!.Code.ShouldBe("base");
    }

    [Fact]
    public void MostSpecificMapping_Wins()
    {
        _options.MapException<InvalidOperationException>(_ => new FaultProblem(400, "base"));
        _options.MapException<ObjectDisposedException>(_ => new FaultProblem(400, "derived"));

        Translate(new ObjectDisposedException("x"))!.Code.ShouldBe("derived");
    }

    [Fact]
    public void MappingThatReturnsNull_LeavesItToTheInterface()
    {
        _options.MapException<ProjectException>(_ => null);

        Translate(new ProjectException(400, "own"))!.Code.ShouldBe("own");
    }

    [Fact]
    public void FailingMapping_IsLoggedAndLeavesItToTheInterface()
    {
        _options.MapException<ProjectException>(_ => throw new NotSupportedException("mapping failure"));

        Translate(new ProjectException(400, "own"))!.Code.ShouldBe("own");
        _logger.Entries.ShouldContain(entry => entry.Exception is NotSupportedException);
    }

    [Fact]
    public void InvalidStatus_IsLoggedAndNotRecognized()
    {
        Translate(new ProjectException(200, "own")).ShouldBeNull();
        _logger.Entries.ShouldContain(entry => entry.Exception is ArgumentOutOfRangeException);
    }

    private Fault? Translate(Exception exception, string? culture = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(_options);
        var provider = new ExceptionFaultProvider(options, new DefaultFaultMessageProvider(options), _logger);

        return provider.TryTranslate(exception, new FaultContext("ABCD-EFGH", culture: culture));
    }

    private sealed class ProjectException : Exception, IFaultException
    {
        public ProjectException(int status, string code, string? detail = null, IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
        {
            Status = status;
            Code = code;
            Detail = detail;
            Errors = errors;
        }

        public int Status { get; }

        public string Code { get; }

        public string? Detail { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors { get; }
    }
}
