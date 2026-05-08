using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LikeC4;

/// <summary>
/// Annotation that excludes a resource from the generated LikeC4 diagram.
/// Apply via <c>builder.ExcludeFromLikeC4()</c>.
/// </summary>
public sealed class ExcludeFromLikeC4Annotation : IResourceAnnotation;
