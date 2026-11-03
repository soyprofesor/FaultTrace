using System;
using FaultTrace.AspNetCore.Handling;
using FaultTrace.AspNetCore.Response;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FaultTrace.AspNetCore.DependencyInjection;

/// <summary>
/// Adds FaultTrace to the request pipeline.
/// </summary>
public static class FaultTraceApplicationBuilderExtensions
{
    /// <summary>
    /// Turns every exception into a problem response. Call this first in the pipeline, so it covers everything after it.
    /// </summary>
    /// <param name="app">The pipeline.</param>
    /// <returns>The same pipeline.</returns>
    /// <exception cref="InvalidOperationException"><c>AddFaultTrace()</c> wasn't called.</exception>
    public static IApplicationBuilder UseFaultTrace(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        if (app.ApplicationServices.GetService<ProblemResponseWriter>() == null)
        {
            throw new InvalidOperationException("Call services.AddFaultTrace() before app.UseFaultTrace().");
        }

        return app.UseMiddleware<FaultTraceMiddleware>();
    }
}
