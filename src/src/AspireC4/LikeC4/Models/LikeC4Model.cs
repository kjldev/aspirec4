namespace Aspire.Hosting.AspireC4.LikeC4.Models;

/// <summary>The complete in-memory model used to generate a LikeC4 DSL diagram.</summary>
public sealed record LikeC4Model
{
	public required IReadOnlyList<LikeC4Element> Elements { get; init; }

	public required IReadOnlyList<LikeC4Relationship> Relationships { get; init; }

	public static readonly LikeC4Model Empty = new() { Elements = [], Relationships = [] };
}
