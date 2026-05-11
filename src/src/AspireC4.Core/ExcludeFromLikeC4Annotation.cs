using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Annotation that excludes a resource from the generated LikeC4 diagram.
/// Apply via <c>builder.ExcludeFromLikeC4()</c>.
/// <param name="Exclude">Indicates whether the resource should be excluded from the LikeC4 diagram. Defaults to <c>true</c>.</param>
/// </summary>
public sealed class ExcludeFromLikeC4Annotation(bool Exclude = true) : IResourceAnnotation;
