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
	/// Strict mode constants: 0 = Inherit (use global MSBuild property), 1 = Enable, 2 = Disable.
	/// </summary>
	public const int StrictInherit = 0;
	public const int StrictEnable = 1;
	public const int StrictDisable = 2;

	public static readonly ClassDefinitions Empty = new(
		string.Empty,
		null,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty,
		StrictInherit,
		StrictInherit,
		StrictInherit,
		StrictInherit,
		StrictInherit,
		StrictInherit,
		ImmutableArray<(string, Location?)>.Empty
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
	/// Registry-wide strict override from <c>[LikeC4Registry(Strict = LikeC4StrictMode.X)]</c>.
	/// 0 = Inherit, 1 = Enable, 2 = Disable.
	/// </summary>
	public int RegistryStrictMode { get; } = registryStrictMode;

	/// <summary>Per-type strict overrides from <c>[KnownType(..., Strict = LikeC4StrictMode.X)]</c>.</summary>
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
			var h = DisplayName?.GetHashCode() ?? 0;
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
