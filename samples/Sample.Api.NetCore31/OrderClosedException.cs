using System;
using System.Collections.Generic;
using FaultTrace.Core.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Sample.Api.NetCore31;

public sealed class OrderClosedException : Exception, IFaultException
{
    public int Status => StatusCodes.Status409Conflict;

    public string Code => "order_closed";

    public string? Detail => "This order is closed and can't be changed.";

    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors => null;
}
