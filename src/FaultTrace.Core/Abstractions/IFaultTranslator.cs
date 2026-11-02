using System;
using FaultTrace.Core.Model;

namespace FaultTrace.Core.Abstractions;

/// <summary>
/// Turns any exception into a <see cref="Fault"/>. Use it in workers and in local <c>catch</c> blocks.
/// </summary>
public interface IFaultTranslator
{
    /// <summary>
    /// Translates an exception.
    /// </summary>
    /// <param name="exception">The exception to translate.</param>
    /// <param name="culture">The culture for library-generated messages, or <see langword="null"/> for the default culture.</param>
    /// <returns>The fault. An exception no provider recognizes becomes a generic unexpected fault.</returns>
    Fault Translate(Exception exception, string? culture = null);
}
