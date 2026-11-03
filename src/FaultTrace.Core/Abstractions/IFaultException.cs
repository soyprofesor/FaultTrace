using System.Collections.Generic;
using FaultTrace.Core.Model;

namespace FaultTrace.Core.Abstractions;

/// <summary>
/// Implement this on a project's own exception, and it is picked up automatically.
/// It has no effect on any other type: only exceptions are ever translated.
/// </summary>
public interface IFaultException
{
    /// <summary>The HTTP status, from 400 to 599. It also decides the <see cref="FaultCategory"/>.</summary>
    int Status { get; }

    /// <summary>The stable machine key. It can be a <see cref="FaultCodes"/> value or the project's own.</summary>
    string Code { get; }

    /// <summary>
    /// The text the user sees, shown as written, so it must be safe to show.
    /// If empty, the library-generated text for the code is used.
    /// </summary>
    string? Detail { get; }

    /// <summary>Field to messages. If set, the fault counts as <see cref="FaultCategory.Validation"/>.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors { get; }
}
