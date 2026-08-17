using Microsoft.CodeAnalysis.Text;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

static class MarkAttributeEmitter
{
	public static IEnumerable<(string HintName, SourceText Source)> EmitMarkAttribute()
	{
		yield return (GetHintName(TypeLibrary.LikeC4RegistryAttribute), LikeC4RegistryAttribute());
		yield return (GetHintName(TypeLibrary.KnownTypeAttribute), KnownTypeAttribute());
		yield return (GetHintName(TypeLibrary.SeverityAttribute), SeverityAttribute());
		yield return (GetHintName(TypeLibrary.LikeC4RegistryType), LikeC4RegistryType());
		yield return (GetHintName(TypeLibrary.LikeC4Severity), LikeC4Severity());
	}

	static string GetHintName(TypeValueObject type) => $"{type.TypeName}.g.cs";

	static SourceText LikeC4RegistryAttribute()
	{
		var writer = CreateCodeWriter(TypeLibrary.LikeC4RegistryAttribute);

		writer
			.XmlSummary(
				"Marks a static class as the single source of truth for LikeC4 registry values",
				"(tags, element kinds, relationship kinds, groups, metadata keys).",
				"Only one class per assembly may carry this attribute."
			)
			.XmlRemarks(
				"Declare values as <c>public const string</c> fields inside nested static classes",
				"named <c>Tags</c>, <c>ElementKinds</c>, <c>RelationshipKinds</c>, <c>Groups</c>,",
				$"or <c>MetadataKeys</c>, OR directly on the class with {CodeWriter.XmlSee(TypeLibrary.KnownTypeAttribute)}."
			)
			.WriteAttributeClass(
				new(TypeLibrary.LikeC4RegistryAttribute),
				AttributeTargets.Class,
				bodyWriter =>
					bodyWriter
						.XmlSummary(
							$"Registry-level diagnostic severity override. Default is {CodeWriter.XmlSee(TypeLibrary.LikeC4Severity.StaticMember(TypeLibrary.SeverityValues.Inherit.Name))}."
						)
						.WriteProperty(
							new("Strict", TypeLibrary.LikeC4Severity)
							{
								Accessibility = TypeDeclarationAccessibility.Public,
								IsInitOnly = true,
								Initializer = TypeLibrary.LikeC4Severity.StaticMember(
									TypeLibrary.SeverityValues.Inherit.Name
								),
							}
						)
			);

		return writer;
	}

	static SourceText KnownTypeAttribute()
	{
		var writer = CreateCodeWriter(TypeLibrary.KnownTypeAttribute);
		return writer
			.XmlSummary(
				"Marks a <c>public string constant</c> field as a known LikeC4 registry value of a specific type."
			)
			.WriteAttributeClass(
				new(TypeLibrary.KnownTypeAttribute)
				{
					PrimaryConstructorParameters = [new("type", TypeLibrary.LikeC4RegistryType)],
				},
				AttributeTargets.Field,
				bodyWriter =>
				{
					writer
						.XmlSummary("The registry type this constant belongs to.")
						.WriteProperty(
							new("Type", TypeLibrary.LikeC4RegistryType)
							{
								Accessibility = TypeDeclarationAccessibility.Public,
								Initializer = "type",
							}
						);

					writer
						.XmlSummary(
							"Per-type severity override.",
							$"Default is {CodeWriter.XmlSee(TypeLibrary.LikeC4Severity.StaticMember(TypeLibrary.SeverityValues.Inherit.Name))}."
						)
						.WriteProperty(
							new("Strict", TypeLibrary.LikeC4Severity)
							{
								Accessibility = TypeDeclarationAccessibility.Public,
								IsInitOnly = true,
								Initializer = TypeLibrary.LikeC4Severity.StaticMember(
									TypeLibrary.SeverityValues.Inherit.Name
								),
							}
						);
				}
			);
	}

	static SourceText SeverityAttribute()
	{
		var writer = CreateCodeWriter(TypeLibrary.SeverityAttribute);
		return writer
			.XmlSummary(
				$"Marks a registry class, nested under another with {CodeWriter.XmlSee(TypeLibrary.LikeC4RegistryAttribute)}, with a default {CodeWriter.XmlSee(TypeLibrary.LikeC4Severity)}."
			)
			.WriteAttributeClass(
				new(TypeLibrary.SeverityAttribute)
				{
					PrimaryConstructorParameters = [new("severity", TypeLibrary.LikeC4Severity)],
				},
				AttributeTargets.Class,
				bodyWriter =>
					writer
						.XmlSummary("The diagnostic severity for this registry class.")
						.WriteProperty(
							new("Severity", TypeLibrary.LikeC4Severity)
							{
								Accessibility = TypeDeclarationAccessibility.Public,
								Initializer = "severity",
							}
						)
			);
	}

	static SourceText LikeC4RegistryType()
	{
		var writer = CreateCodeWriter(TypeLibrary.LikeC4RegistryType);
		return writer
			.XmlSummary("Identifies which LikeC4 registry type a constant belongs to.")
			.WriteEnum(
				new(TypeLibrary.LikeC4RegistryType),
				[
					new(TypeLibrary.RegistryTypeValues.Tag.Name, TypeLibrary.RegistryTypeValues.Tag.Value)
					{
						XmlSummary = ["The constant is a LikeC4 tag (used with <c>.WithTag()</c>)."],
					},
					new(
						TypeLibrary.RegistryTypeValues.ElementKind.Name,
						TypeLibrary.RegistryTypeValues.ElementKind.Value
					)
					{
						XmlSummary = ["The constant is a LikeC4 element kind (used with <c>.WithKind()</c>)."],
					},
					new(
						TypeLibrary.RegistryTypeValues.RelationshipKind.Name,
						TypeLibrary.RegistryTypeValues.RelationshipKind.Value
					)
					{
						XmlSummary = ["The constant is a LikeC4 relationship kind (used with <c>.WithKind()</c>)."],
					},
					new(TypeLibrary.RegistryTypeValues.Group.Name, TypeLibrary.RegistryTypeValues.Group.Value)
					{
						XmlSummary = ["The constant is a LikeC4 group (used with <c>.WithLikeC4Group()</c>)."],
					},
					new(
						TypeLibrary.RegistryTypeValues.MetadataKey.Name,
						TypeLibrary.RegistryTypeValues.MetadataKey.Value
					)
					{
						XmlSummary = ["The constant is a LikeC4 metadata key (used with <c>.WithMetadata()</c>)."],
					},
				]
			);
	}

	static SourceText LikeC4Severity()
	{
		var writer = CreateCodeWriter(TypeLibrary.LikeC4Severity);
		return writer
			.XmlSummary("Controls the diagnostic severity for a registry class or type.")
			.WriteEnum(
				new(TypeLibrary.LikeC4Severity),
				[
					new(TypeLibrary.SeverityValues.Inherit.Name, TypeLibrary.SeverityValues.Inherit.Value)
					{
						XmlSummary =
						[
							"Inherits severity from the parent scope (registry → MSBuild → default Suggestion",
							$"when [{TypeLibrary.LikeC4RegistryAttribute}] exists).",
						],
					},
					new(TypeLibrary.SeverityValues.Off.Name, TypeLibrary.SeverityValues.Off.Value)
					{
						XmlSummary = ["Disables validation for this scope entirely."],
					},
					new(TypeLibrary.SeverityValues.Suggestion.Name, TypeLibrary.SeverityValues.Suggestion.Value)
					{
						XmlSummary = ["Emits an IDE suggestion (hidden diagnostic)."],
					},
					new(TypeLibrary.SeverityValues.Warning.Name, TypeLibrary.SeverityValues.Warning.Value)
					{
						XmlSummary = ["Emits a compiler warning."],
					},
					new(TypeLibrary.SeverityValues.Error.Name, TypeLibrary.SeverityValues.Error.Value)
					{
						XmlSummary = ["Emits a compiler error."],
					},
				]
			);
	}

	static CodeWriter CreateCodeWriter(TypeValueObject type)
	{
		CodeWriter writer = new(TypeLibrary.LikeC4StrictValidatorGenerator.MetadataFullName, AssemblyInfo.Version);

		return writer.WriteAutoGeneratedHeader().WriteFileScopedNamespace(type);
	}
}
