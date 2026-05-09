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
}
