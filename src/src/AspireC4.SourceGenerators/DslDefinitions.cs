using System.Collections.Immutable;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

/// <summary>Definitions extracted from one or more LikeC4 DSL additional files.</summary>
readonly struct DSLDefinitions(
	ImmutableArray<string> tags,
	ImmutableArray<string> elementKinds,
	ImmutableArray<string> relationshipKinds
) : IEquatable<DSLDefinitions>
{
	public static readonly DSLDefinitions Empty = new([], [], []);

	public ImmutableArray<string> Tags { get; } = tags;

	public ImmutableArray<string> ElementKinds { get; } = elementKinds;

	public ImmutableArray<string> RelationshipKinds { get; } = relationshipKinds;

	public bool HasAny => !Tags.IsEmpty || !ElementKinds.IsEmpty || !RelationshipKinds.IsEmpty;

	public bool Equals(DSLDefinitions other) =>
		Tags.SequenceEqual(other.Tags, StringComparer.Ordinal)
		&& ElementKinds.SequenceEqual(other.ElementKinds, StringComparer.Ordinal)
		&& RelationshipKinds.SequenceEqual(other.RelationshipKinds, StringComparer.Ordinal);

	public override bool Equals(object? obj) => obj is DSLDefinitions d && Equals(d);

	public override int GetHashCode()
	{
		unchecked
		{
			var h = Tags.Length;
			h = (h * 397) ^ ElementKinds.Length;
			h = (h * 397) ^ RelationshipKinds.Length;

			return h;
		}
	}
}
