using FaultTrace.Core.Abstractions;

namespace Sample.Api.Net8;

public sealed class OrderClosedException : Exception, IFaultException
{
    public int Status => StatusCodes.Status409Conflict;

    public string Code => "order_closed";

    public string? Detail => "This order is closed and can't be changed.";

    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors => null;
}
