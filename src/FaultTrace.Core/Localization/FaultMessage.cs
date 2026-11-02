using FaultTrace.Core.Model;

namespace FaultTrace.Core.Localization;

/// <summary>
/// The library-generated texts for one fault code in one culture.
/// </summary>
public sealed class FaultMessage
{
    /// <summary>
    /// Creates a message.
    /// </summary>
    /// <param name="title">The short title.</param>
    /// <param name="detail">The generic text, without any database names.</param>
    /// <param name="fieldDetail">The text used when a friendly field name is known, with <c>{0}</c> in place of the name.</param>
    public FaultMessage(string title, string detail, string? fieldDetail = null)
    {
        Title = Guard.NotEmpty(title, nameof(title));
        Detail = Guard.NotEmpty(detail, nameof(detail));
        FieldDetail = fieldDetail;
    }

    /// <summary>The short title.</summary>
    public string Title { get; }

    /// <summary>The generic text, without any database names.</summary>
    public string Detail { get; }

    /// <summary>The text used when a friendly field name is known, or <see langword="null"/> if there is none.</summary>
    public string? FieldDetail { get; }
}
