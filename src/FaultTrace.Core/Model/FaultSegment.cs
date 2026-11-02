using System;
using System.Collections.Generic;
using System.Linq;

namespace FaultTrace.Core.Model;

/// <summary>
/// One error in a chain, followed by the frames it passed through.
/// A new segment starts at each re-raise boundary.
/// </summary>
public sealed class FaultSegment
{
    /// <summary>
    /// Creates a segment.
    /// </summary>
    /// <param name="nativeCode">The provider's own error code.</param>
    /// <param name="message">The error text as the provider wrote it.</param>
    /// <param name="frames">The frames of this segment, in the order the provider listed them.</param>
    /// <exception cref="ArgumentException"><paramref name="nativeCode"/> is empty.</exception>
    public FaultSegment(string nativeCode, string? message = null, IEnumerable<FaultFrame>? frames = null)
    {
        NativeCode = Guard.NotEmpty(nativeCode, nameof(nativeCode));
        Message = message;
        Frames = Array.AsReadOnly(frames?.ToArray() ?? Array.Empty<FaultFrame>());
    }

    /// <summary>The provider's own error code.</summary>
    public string NativeCode { get; }

    /// <summary>The error text as the provider wrote it, or <see langword="null"/> if unknown.</summary>
    public string? Message { get; }

    /// <summary>The frames of this segment, in the order the provider listed them. Never <see langword="null"/>.</summary>
    public IReadOnlyList<FaultFrame> Frames { get; }
}
