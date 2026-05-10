using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Annotation that assigns a resource to a named group in the generated LikeC4 diagram.
/// Resources sharing the same group label are emitted inside a
/// <c>group 'label' { include ... }</c> block in the generated view.
/// </summary>
public sealed class LikeC4GroupAnnotation : IResourceAnnotation
{
	/// <param name="groupName">The display label for the group as it appears in the diagram.</param>
	public LikeC4GroupAnnotation(string groupName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
		GroupName = groupName;
	}

	/// <summary>The display label for the group.</summary>
	public string GroupName { get; }
}
