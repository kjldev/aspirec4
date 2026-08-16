using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

/// <summary>Definitions extracted from a <c>[LikeC4Registry]</c>-annotated class.</summary>
sealed class ClassDefinitions(
	string displayName,
	Location? location,
	ImmutableArray<string> tags,
	ImmutableArray<string> elementKinds,
	ImmutableArray<string> relationshipKinds,
	ImmutableArray<string> groups,
	ImmutableArray<string> metadataKeys,
	int registryStrictMode,
	int tagsTypeStrictMode,
	int elementKindsTypeStrictMode,
	int relationshipKindsTypeStrictMode,
	int groupsTypeStrictMode,
	int metadataKeysTypeStrictMode,
	ImmutableArray<(string TypeName, Location? DuplicateLocation)> duplicateTypeDeclarations
) : IEquatable<ClassDefinitions>
{
	/// <summary>
	/// Severity constants: 0 = Inherit, 1 = Off, 2 = Suggestion, 3 = Warning, 4 = Error.
	/// </summary>
	public const int SeverityInherit = 0;
	public const int SeverityOff = 1;
	public const int SeveritySuggestion = 2;
	public const int SeverityWarning = 3;
	public const int SeverityError = 4;

	public static readonly ClassDefinitions Empty = new(
		string.Empty,
		null,
		[],
		[],
		[],
		[],
		[],
		SeverityInherit,
		SeverityInherit,
		SeverityInherit,
		SeverityInherit,
		SeverityInherit,
		SeverityInherit,
		[]
	);

	public string DisplayName { get; } = displayName;

	/// <summary>Location of the class declaration, used for <c>ASPIREC4003</c> diagnostics.</summary>
	public Location? Location { get; } = location;

	public ImmutableArray<string> Tags { get; } = tags;
	public ImmutableArray<string> ElementKinds { get; } = elementKinds;
	public ImmutableArray<string> RelationshipKinds { get; } = relationshipKinds;
	public ImmutableArray<string> Groups { get; } = groups;
	public ImmutableArray<string> MetadataKeys { get; } = metadataKeys;

	/// <summary>
	/// Registry-wide severity override from <c>[LikeC4Registry(Strict = LikeC4Severity.X)]</c>.
	/// 0 = Inherit, 1 = Off, 2 = Suggestion, 3 = Warning, 4 = Error.
	/// </summary>
	public int RegistryStrictMode { get; } = registryStrictMode;

	/// <summary>Per-type severity overrides from <c>[KnownType(..., Strict = LikeC4Severity.X)]</c>.</summary>
	public int TagsTypeStrictMode { get; } = tagsTypeStrictMode;
	public int ElementKindsTypeStrictMode { get; } = elementKindsTypeStrictMode;
	public int RelationshipKindsTypeStrictMode { get; } = relationshipKindsTypeStrictMode;
	public int GroupsTypeStrictMode { get; } = groupsTypeStrictMode;
	public int MetadataKeysTypeStrictMode { get; } = metadataKeysTypeStrictMode;

	/// <summary>Types declared both via named nested class and <c>[KnownType]</c> field; used for ASPIREC4005.</summary>
	public ImmutableArray<(string TypeName, Location? DuplicateLocation)> DuplicateTypeDeclarations { get; } =
		duplicateTypeDeclarations;

	public bool Equals(ClassDefinitions? other)
	{
		return other is not null
			&& DisplayName == other.DisplayName
			&& Tags.SequenceEqual(other.Tags, StringComparer.Ordinal)
			&& ElementKinds.SequenceEqual(other.ElementKinds, StringComparer.Ordinal)
			&& RelationshipKinds.SequenceEqual(other.RelationshipKinds, StringComparer.Ordinal)
			&& Groups.SequenceEqual(other.Groups, StringComparer.Ordinal)
			&& MetadataKeys.SequenceEqual(other.MetadataKeys, StringComparer.Ordinal)
			&& RegistryStrictMode == other.RegistryStrictMode
			&& TagsTypeStrictMode == other.TagsTypeStrictMode
			&& ElementKindsTypeStrictMode == other.ElementKindsTypeStrictMode
			&& RelationshipKindsTypeStrictMode == other.RelationshipKindsTypeStrictMode
			&& GroupsTypeStrictMode == other.GroupsTypeStrictMode
			&& MetadataKeysTypeStrictMode == other.MetadataKeysTypeStrictMode;
		// Note: DuplicateTypeDeclarations contains Location which is not value-comparable; excluded from equality.
	}

	public override bool Equals(object? obj) => Equals(obj as ClassDefinitions);

	public override int GetHashCode()
	{
		unchecked
		{
			var h = DisplayName?.GetHashCode(StringComparison.Ordinal) ?? 0;
			h = (h * 397) ^ Tags.Length;
			h = (h * 397) ^ ElementKinds.Length;
			h = (h * 397) ^ RelationshipKinds.Length;
			h = (h * 397) ^ Groups.Length;
			h = (h * 397) ^ MetadataKeys.Length;
			h = (h * 397) ^ RegistryStrictMode;
			return h;
		}
	}
}
