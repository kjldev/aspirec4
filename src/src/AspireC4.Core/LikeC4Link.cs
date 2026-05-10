namespace Aspire.Hosting.AspireC4;

/// <summary>Represents a hyperlink that can be attached to an element or relationship in the LikeC4 diagram.</summary>
/// <param name="Uri">The URL of the link. May be absolute or relative to the <c>.c4</c> file.</param>
/// <param name="Title">Optional display text shown in the diagram. When <see langword="null"/>, only the URL is emitted.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
public sealed record LikeC4Link(string Uri, string? Title = null);
