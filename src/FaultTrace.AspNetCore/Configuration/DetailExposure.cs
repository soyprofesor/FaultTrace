namespace FaultTrace.AspNetCore.Configuration;

/// <summary>
/// When a response may contain the <c>debug</c> block, which holds raw messages and database names.
/// </summary>
public enum DetailExposure
{
    /// <summary>Never.</summary>
    Never,

    /// <summary>Only when the host environment is Development.</summary>
    DevelopmentOnly,

    /// <summary>Always, unless <see cref="FaultTraceAspNetCoreOptions.DetailExposurePredicate"/> says otherwise.</summary>
    Always,
}
