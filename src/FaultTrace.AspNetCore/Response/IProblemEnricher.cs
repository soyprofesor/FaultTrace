using System.Collections.Generic;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;

namespace FaultTrace.AspNetCore.Response;

/// <summary>
/// Adds project fields to an error response. Register as many as needed in dependency injection.
/// </summary>
public interface IProblemEnricher
{
    /// <summary>
    /// Adds fields. The standard fields can't be removed or renamed: a key that matches one is ignored.
    /// A failing enricher, or a value that can't be serialized, is logged and leaves the rest of the response as it was.
    /// Don't copy <see cref="Fault.Provider"/> here: it can reveal database names.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="fault">The fault.</param>
    /// <param name="extensions">The fields to add. Keys are written exactly as given.</param>
    void Enrich(HttpContext context, Fault fault, IDictionary<string, object?> extensions);
}
