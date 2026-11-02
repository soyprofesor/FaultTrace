using System;
using FaultTrace.Core.Model;

namespace FaultTrace.Core.Abstractions;

/// <summary>
/// Translates the exceptions of one technology, for example a database. New providers plug in here without changing Core.
/// </summary>
public interface IFaultProvider
{
    /// <summary>
    /// Translates an exception if this provider recognizes it. Must not throw.
    /// </summary>
    /// <param name="exception">The exception to translate.</param>
    /// <param name="context">The values the fault must carry.</param>
    /// <returns>The fault, or <see langword="null"/> if the exception doesn't belong to this provider.</returns>
    Fault? TryTranslate(Exception exception, FaultContext context);
}
