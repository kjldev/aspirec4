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
	/// <param name="kind">
	/// Optional LikeC4 relationship kind identifier (e.g. "async", "sync"). Must be a valid LikeC4
	/// identifier (letters, digits, hyphens, underscores; cannot start with a digit). When set, the
	/// kind is declared in the <c>specification</c> block and the typed <c>-[kind]-&gt;</c> syntax is used.
	/// </param>
	public LikeC4RelationshipDetailsAnnotation(
		string targetName,
		string? label,
		string? technology,
		string? description,
		string? kind = null
	)
		: this(
			targetName,
			label,
			technology,
			description,
			kind,
			tags: [],
			links: [],
			metadata: new Dictionary<string, string>()
		) { }

	public LikeC4RelationshipDetailsAnnotation(
		string targetName,
		string? label,
		string? technology,
		string? description,
		string? kind,
		IReadOnlyList<string> tags,
		IReadOnlyList<LikeC4Link> links,
		IReadOnlyDictionary<string, string> metadata
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

		TargetName = targetName;
		Label = label;
		Technology = technology;
		Description = description;
		Kind = kind;
		Tags = tags ?? [];
		Links = links ?? [];
		Metadata = metadata ?? new Dictionary<string, string>();
	}

	/// <summary>The <see cref="IResource.Name"/> of the relationship target resource.</summary>
	public string TargetName { get; }

	/// <summary>Short label shown on the relationship arrow.</summary>
	public string? Label { get; }

	/// <summary>Technology or protocol used by the relationship (e.g., "HTTP/2", "gRPC", "AMQP").</summary>
	public string? Technology { get; }

	/// <summary>Longer description of the relationship.</summary>
	public string? Description { get; }

	/// <summary>
	/// Optional LikeC4 relationship kind identifier (e.g. "async", "sync"). When set, the kind is
	/// declared in the <c>specification</c> block and the typed <c>-[kind]-&gt;</c> syntax is used.
	/// </summary>
	public string? Kind { get; }

	/// <summary>Tags applied to this relationship in the diagram.</summary>
	public IReadOnlyList<string> Tags { get; }

	/// <summary>Links attached to this relationship in the diagram.</summary>
	public IReadOnlyList<LikeC4Link> Links { get; }

	/// <summary>Metadata key-value pairs for this relationship.</summary>
	public IReadOnlyDictionary<string, string> Metadata { get; }
}
