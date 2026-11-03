using FaultTrace.AspNetCore.Users;
using FaultTrace.Core.Abstractions;
using Microsoft.AspNetCore.Http;

namespace FaultTrace.AspNetCore.Handling;

internal sealed class HttpFaultRequestAccessor : IFaultRequestAccessor
{
    private readonly IHttpContextAccessor _contexts;
    private readonly FaultUserResolver _users;

    public HttpFaultRequestAccessor(IHttpContextAccessor contexts, FaultUserResolver users)
    {
        _contexts = contexts;
        _users = users;
    }

    public FaultRequest? GetCurrent()
    {
        var context = _contexts.HttpContext;
        if (context == null)
        {
            return null;
        }

        // PathString.ToString() keeps the URL encoding, so a decoded line break in the path can't forge log lines.
        return new FaultRequest(
            context.Request.PathBase.Add(context.Request.Path).ToString(),
            context.Request.Method,
            _users.Resolve(context),
            context.TraceIdentifier,
            context.RequestAborted.IsCancellationRequested);
    }
}
