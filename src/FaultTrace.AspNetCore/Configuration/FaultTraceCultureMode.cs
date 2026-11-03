using Microsoft.AspNetCore.Localization;

namespace FaultTrace.AspNetCore.Configuration;

/// <summary>
/// How the response culture is found.
/// </summary>
public enum FaultTraceCultureMode
{
    /// <summary>Use <see cref="IRequestCultureFeature"/> if ASP.NET request localization ran, otherwise the FaultTrace sources.</summary>
    Auto,

    /// <summary>Use only <see cref="IRequestCultureFeature"/>.</summary>
    RequestLocalization,

    /// <summary>Use only the FaultTrace sources, listed in <see cref="FaultTraceCultureOptions"/>.</summary>
    Sources,
}
