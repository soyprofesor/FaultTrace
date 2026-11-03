using System.Collections.Generic;
using FaultTrace.Core.Model;

namespace FaultTrace.Core.Abstractions;

/// <summary>
/// Creates a <see cref="Fault"/> when there is no exception, for example a 404 response or failed validation.
/// The fault is logged and passed to the hooks, like a translated one.
/// </summary>
public interface IFaultFactory
{
    /// <summary>
    /// Creates a fault. Without a <paramref name="detail"/>, it gets the library-generated text for its code or, if the
    /// code is a project's own, for its category.
    /// </summary>
    /// <param name="category">The broad kind of the fault.</param>
    /// <param name="code">The stable machine key. See <see cref="FaultCodes"/>.</param>
    /// <param name="status">The suggested HTTP status, from 400 to 599.</param>
    /// <param name="culture">The culture for the message, or <see langword="null"/> for the default culture.</param>
    /// <param name="errors">Field to messages. Only allowed for <see cref="FaultCategory.Validation"/>.</param>
    /// <param name="detail">The text the user sees, shown as written, so it must be safe to show.</param>
    /// <returns>The fault. Invalid arguments are logged and give a generic unexpected fault.</returns>
    Fault Create(
        FaultCategory category,
        string code,
        int status,
        string? culture = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null,
        string? detail = null);
}
