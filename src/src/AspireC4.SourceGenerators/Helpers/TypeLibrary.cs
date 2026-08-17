using Aspire.Hosting.AspireC4.SourceGenerators.Models;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

static class TypeLibrary
{
	public const string AspireC4Namespace = "Aspire.Hosting.AspireC4";

	public const string LikeC4RegistryAttributeFullname = AspireC4Namespace + "." + nameof(LikeC4RegistryAttribute);
	public const string KnownTypeAttributeFullname = AspireC4Namespace + "." + nameof(KnownTypeAttribute);

	public const string LikeC4RegistryTypeFullname = AspireC4Namespace + "." + nameof(LikeC4RegistryType);

	public static readonly TypeValueObject LikeC4RegistryAttribute = new(
		nameof(LikeC4RegistryAttribute),
		AspireC4Namespace
	);

	public static readonly TypeValueObject KnownTypeAttribute = new(nameof(KnownTypeAttribute), AspireC4Namespace);

	public static readonly TypeValueObject LikeC4RegistryType = new(nameof(LikeC4RegistryType), AspireC4Namespace);

	public static readonly TypeValueObject LikeC4Severity = new(nameof(LikeC4Severity), AspireC4Namespace);

	public static readonly TypeValueObject LikeC4StrictValidatorGenerator =
		TypeValueObject.Create<LikeC4StrictValidatorGenerator>();

	public static class SeverityValues
	{
		public static readonly SeverityDefinition Inherit = new(nameof(Inherit), 0);

		public static readonly SeverityDefinition Off = new(nameof(Off), 1);

		public static readonly SeverityDefinition Suggestion = new(nameof(Suggestion), 2);

		public static readonly SeverityDefinition Warning = new(nameof(Warning), 3);

		public static readonly SeverityDefinition Error = new(nameof(Error), 4);
	}

	public static class RegistryTypeValues
	{
		public static readonly RegistryTypeDefinition Tag = new(nameof(Tag), 0, ["Tag", "Tags"]);

		public static readonly RegistryTypeDefinition ElementKind = new(
			nameof(ElementKind),
			1,
			["ElementKind", "ElementKinds", "Element", "Elements"]
		);

		public static readonly RegistryTypeDefinition RelationshipKind = new(
			nameof(RelationshipKind),
			2,
			["RelationshipKind", "RelationshipKinds", "Relationship", "Relationships"]
		);

		public static readonly RegistryTypeDefinition Group = new(nameof(Group), 3, ["Group", "Groups"]);

		public static readonly RegistryTypeDefinition MetadataKey = new(
			nameof(MetadataKey),
			4,
			["MetadataKey", "MetadataKeys"]
		);

		public static RegistryTypeDefinition GetByName(string name)
		{
			if (Tag.FullName == name || Tag.ValidTypeNames.Any(m => m == name))
				return Tag;
			if (ElementKind.FullName == name || ElementKind.ValidTypeNames.Any(m => m == name))
				return ElementKind;
			if (RelationshipKind.FullName == name || RelationshipKind.ValidTypeNames.Any(m => m == name))
				return RelationshipKind;
			if (Group.FullName == name || Group.ValidTypeNames.Any(m => m == name))
				return Group;
			if (MetadataKey.FullName == name || MetadataKey.ValidTypeNames.Any(m => m == name))
				return MetadataKey;

			return RegistryTypeDefinition.Empty;
		}
	}
}
