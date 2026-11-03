using System;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Culture;
using FaultTrace.AspNetCore.Response;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FaultTrace.AspNetCore.Results;

/// <summary>
/// Returns a <see cref="Fault"/> from code that caught the error itself, in the same format as every other error response.
/// The fault was logged when it was created, so these don't log again.
/// </summary>
public static class FaultResultExtensions
{
    /// <summary>
    /// Creates the action result for a fault.
    /// </summary>
    /// <param name="controller">The controller.</param>
    /// <param name="fault">The fault, usually from <c>IFaultTranslator</c> or <c>IFaultFactory</c>.</param>
    /// <returns>The result to return from the action.</returns>
    public static IActionResult FaultResult(this ControllerBase controller, Fault fault)
    {
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        if (fault == null)
        {
            throw new ArgumentNullException(nameof(fault));
        }

        return new FaultActionResult(fault);
    }

    /// <summary>
    /// Writes the response for a fault, for code without a controller, such as a Minimal API handler or a middleware.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="fault">The fault, usually from <c>IFaultTranslator</c> or <c>IFaultFactory</c>.</param>
    /// <returns>A task that finishes when the response is written.</returns>
    public static Task WriteFaultAsync(this HttpContext context, Fault fault)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (fault == null)
        {
            throw new ArgumentNullException(nameof(fault));
        }

        var services = context.RequestServices;
        var culture = services.GetRequiredService<FaultCultureResolver>().Resolve(context);
        return services.GetRequiredService<ProblemResponseWriter>().WriteAsync(context, fault, null, culture);
    }

    private sealed class FaultActionResult : IActionResult
    {
        private readonly Fault _fault;

        public FaultActionResult(Fault fault)
        {
            _fault = fault;
        }

        public Task ExecuteResultAsync(ActionContext context) => context.HttpContext.WriteFaultAsync(_fault);
    }
}
