using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Annotation that customises how a relationship appears as an edge in the generated LikeC4 diagram.
/// Keyed by <see cref="TargetName"/>; attach multiple instances to a source resource — one per
/// target — to override different relationships independently.
/// </summary>
public sealed class LikeC4RelationshipDetailsAnnotation : IResourceAnnotation
{
	/// <param name="targetName">
	/// The <see cref="IResource.Name"/> of the relationship target. Used to match this override to the
	/// correct <see cref="ResourceRelationshipAnnotation"/> when building the diagram model.
	/// </param>
	/// <param name="label">Short label shown on the relationship arrow.</param>
	/// <param name="technology">Technology or protocol used by the relationship (e.g., "HTTP/2", "gRPC").</param>
	/// <param name="description">Longer description of the relationship.</param>
	public LikeC4RelationshipDetailsAnnotation(
		string targetName,
		string? label,
		string? technology,
		string? description
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

		TargetName = targetName;
		Label = label;
		Technology = technology;
		Description = description;
	}

	/// <summary>The <see cref="IResource.Name"/> of the relationship target resource.</summary>
	public string TargetName { get; }

	/// <summary>Short label shown on the relationship arrow.</summary>
	public string? Label { get; }

	/// <summary>Technology or protocol used by the relationship (e.g., "HTTP/2", "gRPC", "AMQP").</summary>
	public string? Technology { get; }

	/// <summary>Longer description of the relationship.</summary>
	public string? Description { get; }
}
