namespace Aspire.Hosting.AspireC4.LikeC4.Models;

/// <summary>The complete in-memory model used to generate a LikeC4 DSL diagram.</summary>
public sealed record LikeC4Model
{
	/// <summary>The ordered list of elements (nodes) in the diagram.</summary>
	public required IReadOnlyList<LikeC4Element> Elements { get; init; }

	/// <summary>The ordered list of relationships (edges) between elements in the diagram.</summary>
	public required IReadOnlyList<LikeC4Relationship> Relationships { get; init; }

	/// <summary>A pre-built empty model containing no elements or relationships.</summary>
	public static readonly LikeC4Model Empty = new() { Elements = [], Relationships = [] };
}
