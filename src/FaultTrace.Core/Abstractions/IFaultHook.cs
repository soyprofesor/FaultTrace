using System;
using FaultTrace.Core.Model;

namespace FaultTrace.Core.Abstractions;

/// <summary>
/// Runs after a fault is handled. The extension point for sinks like metrics or notifications.
/// </summary>
public interface IFaultHook
{
    /// <summary>
    /// Called once per fault on the error path. Keep it fast and queue slow work elsewhere.
    /// A failing hook is logged and doesn't affect the response.
    /// </summary>
    /// <param name="fault">The fault.</param>
    /// <param name="exception">The exception it came from.</param>
    void OnFault(Fault fault, Exception exception);
}
