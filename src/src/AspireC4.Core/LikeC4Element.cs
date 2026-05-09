namespace Aspire.Hosting.AspireC4;

/// <summary>Represents a single element (node) in the LikeC4 architecture model.</summary>
public sealed record LikeC4Element
{
	/// <summary>The resource name — used as the identifier in the DSL.</summary>
	public required string Name { get; init; }

	/// <summary>Human-readable display label. Defaults to <see cref="Name"/> when not specified.</summary>
	public required string Label { get; init; }

	/// <summary>LikeC4 element kind, e.g. "component", "container". See <see cref="LikeC4ElementKind"/>.</summary>
	public required string Kind { get; init; }

	/// <summary>Optional technology description shown on the element node.</summary>
	public string? Technology { get; init; }

	/// <summary>Optional longer description shown in element details.</summary>
	public string? Description { get; init; }

	/// <summary>Optional LikeC4 icon token or image reference shown on the element.</summary>
	public string? Icon { get; init; }

	/// <summary>Name of the parent resource when this element is nested (via <see cref="IResourceWithParent"/>).</summary>
	public string? ParentName { get; init; }

	/// <summary>Current runtime state of the resource — controls the diagram colour.</summary>
	public LikeC4ResourceState State { get; init; } = LikeC4ResourceState.Unknown;
}
