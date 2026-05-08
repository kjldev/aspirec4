using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Annotation that customises how a resource appears as a node in the generated LikeC4 diagram.
/// </summary>
public sealed class LikeC4NodeDetailsAnnotation : IResourceAnnotation
{
    public LikeC4NodeDetailsAnnotation(string label, string? technology = null, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Label = label;
        Technology = technology;
        Description = description;
    }

    public string Label { get; }
    public string? Technology { get; }
    public string? Description { get; }
}
