using System.Collections.Generic;

namespace FaultTrace.Core.Model;

/// <summary>
/// What a project's exception means. Returned by the delegate given to <see cref="Options.FaultTraceOptions.MapException{TException}"/>.
/// </summary>
public sealed class FaultProblem
{
    /// <summary>
    /// Creates a description.
    /// </summary>
    /// <param name="status">The HTTP status, from 400 to 599. It also decides the <see cref="FaultCategory"/>.</param>
    /// <param name="code">The stable machine key. It can be a <see cref="FaultCodes"/> value or the project's own.</param>
    /// <param name="detail">The text the user sees, shown as written. If empty, the library-generated text for the code is used.</param>
    /// <param name="errors">Field to messages. If set, the fault counts as <see cref="FaultCategory.Validation"/>.</param>
    public FaultProblem(int status, string code, string? detail = null, IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
    {
        Status = status;
        Code = code;
        Detail = detail;
        Errors = errors;
    }

    /// <summary>The HTTP status.</summary>
    public int Status { get; }

    /// <summary>The stable machine key.</summary>
    public string Code { get; }

    /// <summary>The text the user sees, or <see langword="null"/> for the library-generated text.</summary>
    public string? Detail { get; }

    /// <summary>Field to messages, or <see langword="null"/> if there are none.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors { get; }
}
