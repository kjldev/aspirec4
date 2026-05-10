namespace Aspire.Hosting.AspireC4;

/// <summary>Represents a directed relationship between two elements in the LikeC4 architecture model.</summary>
public sealed record LikeC4Relationship
{
	/// <summary>Name of the source element.</summary>
	public required string SourceName { get; init; }

	/// <summary>Name of the target element.</summary>
	public required string TargetName { get; init; }

	/// <summary>Optional short label shown on the relationship arrow.</summary>
	public string? Label { get; init; }

	/// <summary>Optional technology or protocol (e.g., "HTTP/2", "gRPC", "AMQP").</summary>
	public string? Technology { get; init; }

	/// <summary>Optional longer description of the relationship.</summary>
	public string? Description { get; init; }

	/// <summary>
	/// Optional LikeC4 relationship kind (e.g. "async", "sync", "grpc"). When set, the kind is
	/// declared in the <c>specification</c> block and the relationship is emitted with the
	/// <c>-[KIND]-&gt;</c> typed syntax.
	/// </summary>
	public string? Kind { get; init; }

	/// <summary>Tags applied to this relationship (e.g. <c>"external"</c>). Declared in the specification block as <c>tag NAME</c>.</summary>
	public IReadOnlyList<string> Tags { get; init; } = [];

	/// <summary>Links attached to this relationship — emitted as <c>link URL ['title']</c> in the relationship body.</summary>
	public IReadOnlyList<LikeC4Link> Links { get; init; } = [];

	/// <summary>Metadata key-value pairs emitted as a <c>metadata { key "value" }</c> block in the relationship body.</summary>
	public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
