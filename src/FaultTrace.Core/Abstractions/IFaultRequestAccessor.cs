namespace FaultTrace.Core.Abstractions;

/// <summary>
/// Supplies the request being handled, so a fault translated anywhere inside it, including in a local
/// <c>catch</c>, is logged with the same context. A host integration implements this.
/// </summary>
public interface IFaultRequestAccessor
{
    /// <summary>
    /// Gets the current request. Called only on the error path.
    /// </summary>
    /// <returns>The request, or <see langword="null"/> if there is none.</returns>
    FaultRequest? GetCurrent();
}
