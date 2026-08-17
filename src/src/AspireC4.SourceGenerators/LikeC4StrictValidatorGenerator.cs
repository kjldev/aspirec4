using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Aspire.Hosting.AspireC4.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

/// <summary>
/// Validates LikeC4 call-site string arguments against pre-declared definitions.
/// </summary>
/// <remarks>
/// Two validation modes:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>DSL file mode</b>: activated when <c>&lt;AspireC4Strict&gt;...&lt;/AspireC4Strict&gt;</c> is set
///       in the consuming project to a non-off severity. <c>.c4</c>/<c>.likec4</c> additional files are parsed for
///       <c>specification</c> block declarations (<c>tag</c>, <c>element</c>, <c>relationship</c>).
///       All <c>.WithTag()</c> and <c>.WithKind()</c> call-site values are validated against those.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Class-based mode</b>: a class annotated with <c>[LikeC4Registry]</c> (any accessibility,
///       any nesting level) provides <c>public const string</c> fields inside nested static classes
///       named <c>Tags</c>, <c>ElementKinds</c>, <c>RelationshipKinds</c>, <c>Groups</c>, and/or
///       <c>MetadataKeys</c>, or directly on the class via <c>[KnownType(LikeC4RegistryType.X)]</c>.
///       Only one such class is allowed per assembly.
///     </description>
///   </item>
/// </list>
/// Both modes may be active simultaneously; allowed sets are merged.
/// </remarks>
[Generator]
public sealed partial class LikeC4StrictValidatorGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(ctx =>
		{
			_logger?.Info("Adding the following types:");
			foreach (var (HintName, Source) in MarkAttributeEmitter.EmitMarkAttribute())
			{
				_logger?.Info($"- {HintName}", 1);
				ctx.AddSource(HintName, Source);
			}
		});

		// Combine everything and validate.
		var pipeline = SourceGenHelper.CreateGenerationPipeline(context, _logger);
		context.RegisterSourceOutput(
			pipeline,
			static (ctx, model) =>
			{
				if (model.IsDisabled)
				{
					model.Context.Debug(
						"LikeC4StrictValidatorGenerator is disabled via <DisableAspireC4SourceGenerator> property."
					);

					return;
				}

				Validate(ctx, model);
			}
		);
	}

	static ClassDefinitions ExtractClassDefinitions(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
	{
		if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
			return ClassDefinitions.Empty;

		var displayName = classSymbol.ToDisplayString();
		var location = classSymbol.Locations.Length > 0 ? classSymbol.Locations[0] : null;

		var tags = new List<string>();
		var elementKinds = new List<string>();
		var relationshipKinds = new List<string>();
		var groups = new List<string>();
		var metadataKeys = new List<string>();

		// Track which registry types are declared via named nested classes vs [KnownType] fields.
		var nestedClassTypes = new HashSet<int>();
		var knownTypeFieldsByType = new Dictionary<int, List<(string Value, int StrictMode, Location? Loc)>>();

		// Step 1: scan named nested classes (Tags, ElementKinds, RelationshipKinds, Groups, MetadataKeys).
		ScanNestedClasses(
			classSymbol,
			tags,
			elementKinds,
			relationshipKinds,
			groups,
			metadataKeys,
			nestedClassTypes,
			ct
		);

		// Step 2: scan top-level fields with [KnownType] attributes.
		ScanForKnownTypes(
			classSymbol,
			tags,
			elementKinds,
			relationshipKinds,
			groups,
			metadataKeys,
			knownTypeFieldsByType,
			ct
		);

		// Step 3: compute per-type severity from [KnownType] fields (highest severity wins; Off suppresses).
		int ComputeTypeStrictMode(int registryType) =>
			knownTypeFieldsByType.TryGetValue(registryType, out var fields)
				? fields.Aggregate(ClassDefinitions.SeverityInherit, static (acc, f) => Math.Max(acc, f.StrictMode))
				: ClassDefinitions.SeverityInherit;

		// Step 4: read registry-level severity from [LikeC4Registry(Strict = ...)] (ctx.Attributes[0]).
		var registryAttr = ctx.Attributes.Length > 0 ? ctx.Attributes[0] : null;
		var registryStrictMode = ClassDefinitions.SeverityInherit;
		if (registryAttr is not null)
		{
			var strictArg = registryAttr.NamedArguments.FirstOrDefault(static a => a.Key == "Strict");
			if (strictArg.Value.Kind == TypedConstantKind.Enum && strictArg.Value.Value is int strictInt)
				registryStrictMode = strictInt;
		}

		// Step 5: detect duplicate type declarations (nested class + [KnownType] for same type).
		var duplicates = new List<(string TypeName, Location? Location)>();
		foreach (var kvp in knownTypeFieldsByType)
		{
			if (!nestedClassTypes.Contains(kvp.Key))
				continue;

			var typeName = kvp.Key switch
			{
				RegistryTypeTag => "Tag",
				RegistryTypeElementKind => "ElementKind",
				RegistryTypeRelationshipKind => "RelationshipKind",
				RegistryTypeGroup => "Group",
				RegistryTypeMetadataKey => "MetadataKey",
				_ => kvp.Key.ToString(CultureInfo.InvariantCulture),
			};

			duplicates.Add((typeName, kvp.Value.Count > 0 ? kvp.Value[0].Loc : null));
		}

		return new ClassDefinitions(
			displayName,
			location,
			[.. tags],
			[.. elementKinds],
			[.. relationshipKinds],
			[.. groups],
			[.. metadataKeys],
			registryStrictMode,
			ComputeTypeStrictMode(RegistryTypeTag),
			ComputeTypeStrictMode(RegistryTypeElementKind),
			ComputeTypeStrictMode(RegistryTypeRelationshipKind),
			ComputeTypeStrictMode(RegistryTypeGroup),
			ComputeTypeStrictMode(RegistryTypeMetadataKey),
			[.. duplicates]
		);
	}

	//static void ScanForKnownTypes(
	//	INamedTypeSymbol classSymbol,
	//	List<string> tags,
	//	List<string> elementKinds,
	//	List<string> relationshipKinds,
	//	List<string> groups,
	//	List<string> metadataKeys,
	//	Dictionary<int, List<(string Value, int StrictMode, Location? Loc)>> knownTypeFieldsByType,
	//	CancellationToken ct
	//)
	//{
	//	foreach (var member in classSymbol.GetMembers())
	//	{
	//		ct.ThrowIfCancellationRequested();

	//		if (
	//			member is not IFieldSymbol field
	//			|| !field.IsConst
	//			|| field.Type.SpecialType != SpecialType.System_String
	//			|| field.ConstantValue is not string value
	//		)
	//			continue;

	//		var knownTypeAttr = field
	//			.GetAttributes()
	//			.FirstOrDefault(static a => a.AttributeClass?.Name == "KnownTypeAttribute");

	//		if (knownTypeAttr is null)
	//			continue;

	//		if (knownTypeAttr.ConstructorArguments.Length == 0)
	//			continue;

	//		var typeArg = knownTypeAttr.ConstructorArguments[0];
	//		if (typeArg.Kind != TypedConstantKind.Enum || typeArg.Value is not int registryTypeInt)
	//			continue;

	//		var strictArg = knownTypeAttr.NamedArguments.FirstOrDefault(static a => a.Key == "Strict");
	//		var fieldStrictMode =
	//			strictArg.Value.Kind == TypedConstantKind.Enum && strictArg.Value.Value is int strictInt
	//				? strictInt
	//				: ClassDefinitions.SeverityInherit;

	//		var fieldLocation = field.Locations.Length > 0 ? field.Locations[0] : null;

	//		if (!knownTypeFieldsByType.TryGetValue(registryTypeInt, out var fieldList))
	//			knownTypeFieldsByType[registryTypeInt] = fieldList = [];

	//		fieldList.Add((value, fieldStrictMode, fieldLocation));

	//		GetTargetList(registryTypeInt, tags, elementKinds, relationshipKinds, groups, metadataKeys)?.Add(value);
	//	}
	//}

	//static void ScanNestedClasses(
	//	INamedTypeSymbol classSymbol,
	//	List<string> tags,
	//	List<string> elementKinds,
	//	List<string> relationshipKinds,
	//	List<string> groups,
	//	List<string> metadataKeys,
	//	HashSet<int> nestedClassTypes,
	//	CancellationToken ct
	//)
	//{
	//	foreach (var nested in classSymbol.GetTypeMembers())
	//	{
	//		ct.ThrowIfCancellationRequested();

	//		var registryType = nested.Name switch
	//		{
	//			"Tags" => RegistryTypeTag,
	//			"ElementKinds" => RegistryTypeElementKind,
	//			"RelationshipKinds" => RegistryTypeRelationshipKind,
	//			"Groups" => RegistryTypeGroup,
	//			"MetadataKeys" => RegistryTypeMetadataKey,
	//			_ => (int?)null,
	//		};

	//		if (registryType is null)
	//			continue;

	//		nestedClassTypes.Add(registryType.Value);
	//		var target = GetTargetList(
	//			registryType.Value,
	//			tags,
	//			elementKinds,
	//			relationshipKinds,
	//			groups,
	//			metadataKeys
	//		)!;

	//		foreach (var member in nested.GetMembers())
	//		{
	//			if (
	//				member is not IFieldSymbol field
	//				|| !field.IsConst
	//				|| field.DeclaredAccessibility != Accessibility.Public
	//				|| field.Type.SpecialType != SpecialType.System_String
	//				|| field.ConstantValue is not string value
	//			)
	//				continue;

	//			target.Add(value);
	//		}
	//	}
	//}

	//static List<string>? GetTargetList(
	//	int registryType,
	//	List<string> tags,
	//	List<string> elementKinds,
	//	List<string> relationshipKinds,
	//	List<string> groups,
	//	List<string> metadataKeys
	//) =>
	//	registryType switch
	//	{
	//		RegistryTypeTag => tags,
	//		RegistryTypeElementKind => elementKinds,
	//		RegistryTypeRelationshipKind => relationshipKinds,
	//		RegistryTypeGroup => groups,
	//		RegistryTypeMetadataKey => metadataKeys,
	//		_ => null,
	//	};

	//static (DiagnosticSeverity? Severity, bool IncludesMetadata) ParseGlobalStrict(string? val)
	//{
	//	if (string.IsNullOrWhiteSpace(val))
	//		return (null, false);

	//	var normalized = val.Trim();
	//	if (normalized.Equals("off", StringComparison.OrdinalIgnoreCase))
	//		return (null, false);
	//	if (normalized.Equals("suggestion", StringComparison.OrdinalIgnoreCase))
	//		return (DiagnosticSeverity.Info, false);
	//	if (normalized.Equals("warning", StringComparison.OrdinalIgnoreCase))
	//		return (DiagnosticSeverity.Warning, false);
	//	if (
	//		normalized.Equals("error", StringComparison.OrdinalIgnoreCase)
	//		|| normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
	//		|| normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
	//		|| normalized.Equals("all", StringComparison.OrdinalIgnoreCase)
	//	)
	//		return (DiagnosticSeverity.Error, false);
	//	if (normalized.Equals("allincludingmetadata", StringComparison.OrdinalIgnoreCase))
	//		return (DiagnosticSeverity.Error, true);

	//	return (null, false);
	//}

	static DiagnosticDescriptor WithSeverity(DiagnosticDescriptor descriptor, DiagnosticSeverity severity) =>
		severity == descriptor.DefaultSeverity
			? descriptor
			: new DiagnosticDescriptor(
				descriptor.Id,
				descriptor.Title,
				descriptor.MessageFormat,
				descriptor.Category,
				severity,
				descriptor.IsEnabledByDefault,
				descriptor.Description,
				descriptor.HelpLinkUri
			);

	[SuppressMessage(
		"Maintainability",
		"CA1502:Avoid excessive complexity",
		Justification = "I will come back to this at somepoint."
	)]
	static void Validate(
		SourceProductionContext ctx,
		(DiagnosticSeverity? Severity, bool IncludesMetadata) globalStrict,
		DSLDefinitions dslDefs,
		ImmutableArray<ClassDefinitions> classDefs,
		ImmutableArray<CallSiteInfo> tagCallSites,
		ImmutableArray<CallSiteInfo> kindCallSites,
		ImmutableArray<CallSiteInfo> groupCallSites,
		ImmutableArray<CallSiteInfo> metadataCallSites
	)
	{
		if (classDefs.Length > 1)
		{
			for (var i = 1; i < classDefs.Length; i++)
			{
				ctx.ReportDiagnostic(
					Diagnostic.Create(MultipleDefinitionsClasses, classDefs[i].Location, classDefs[i].DisplayName)
				);
			}
		}

		foreach (var def in classDefs)
		{
			foreach (var (typeName, dupLocation) in def.DuplicateTypeDeclarations)
				ctx.ReportDiagnostic(Diagnostic.Create(DuplicateTypeDeclaration, dupLocation, typeName));
		}

		var hasDslValidation = globalStrict.Severity is not null && dslDefs.HasAny;
		var hasClassValidation = classDefs.Length > 0;

		if (!hasDslValidation && !hasClassValidation)
			return;

		var primaryDef = hasClassValidation ? classDefs[0] : null;
		var registryRaw = primaryDef?.RegistryStrictMode ?? ClassDefinitions.SeverityInherit;
		var registryExplicit = registryRaw != ClassDefinitions.SeverityInherit;
		var globalExplicit = globalStrict.Severity is not null;
		var isExplicitlyEnabled = registryExplicit || globalExplicit;

		var registrySeverity = registryRaw switch
		{
			ClassDefinitions.SeverityOff => null,
			ClassDefinitions.SeverityInherit => globalStrict.Severity
				?? (hasClassValidation ? DiagnosticSeverity.Info : null),
			ClassDefinitions.SeveritySuggestion => DiagnosticSeverity.Info,
			ClassDefinitions.SeverityWarning => DiagnosticSeverity.Warning,
			ClassDefinitions.SeverityError => DiagnosticSeverity.Error,
			_ => null,
		};

		DiagnosticSeverity? ResolveTypeSeverity(int typeRaw) =>
			typeRaw switch
			{
				ClassDefinitions.SeverityOff => null,
				ClassDefinitions.SeverityInherit => registrySeverity,
				ClassDefinitions.SeveritySuggestion => DiagnosticSeverity.Info,
				ClassDefinitions.SeverityWarning => DiagnosticSeverity.Warning,
				ClassDefinitions.SeverityError => DiagnosticSeverity.Error,
				_ => registrySeverity,
			};

		int CombineRaw(int a, int b) =>
			a == ClassDefinitions.SeverityOff || b == ClassDefinitions.SeverityOff
				? ClassDefinitions.SeverityOff
				: Math.Max(a, b);

		bool ShouldValidate(HashSet<string> allowedSet, DiagnosticSeverity? severity) =>
			severity is not null && (allowedSet.Count > 0 || isExplicitlyEnabled);

		var allowedTags = BuildAllowedSet(
			hasDslValidation ? dslDefs.Tags.AsEnumerable() : [],
			hasClassValidation ? classDefs.SelectMany(static d => d.Tags) : []
		);

		var allowedKinds = BuildAllowedSet(
			hasDslValidation ? dslDefs.ElementKinds.Concat(dslDefs.RelationshipKinds) : [],
			hasClassValidation ? classDefs.SelectMany(static d => d.ElementKinds.Concat(d.RelationshipKinds)) : []
		);

#pragma warning disable IDE0028
		var allowedGroups = hasClassValidation
			? BuildAllowedSet([], classDefs.SelectMany(static d => d.Groups))
			: new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		// Normalise declared keys so that "Azure SKU", "Azure_SKU", and "azure sku" all map to
		// the same normalised form and are matched case-insensitively at the call site.
		var allowedMetadata = hasClassValidation
			? BuildAllowedSet(
				[],
				classDefs.SelectMany(static d => d.MetadataKeys).Select(NormaliseMetadataKeyForComparison)
			)
			: new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028

		var tagsTypeRaw = primaryDef?.TagsTypeStrictMode ?? ClassDefinitions.SeverityInherit;
		var kindsTypeRaw = CombineRaw(
			primaryDef?.ElementKindsTypeStrictMode ?? ClassDefinitions.SeverityInherit,
			primaryDef?.RelationshipKindsTypeStrictMode ?? ClassDefinitions.SeverityInherit
		);
		var groupsTypeRaw = primaryDef?.GroupsTypeStrictMode ?? ClassDefinitions.SeverityInherit;
		var metadataTypeRaw = primaryDef?.MetadataKeysTypeStrictMode ?? ClassDefinitions.SeverityInherit;

		var tagsSeverity = ResolveTypeSeverity(tagsTypeRaw);
		var kindsSeverity = ResolveTypeSeverity(kindsTypeRaw);
		var groupsSeverity = ResolveTypeSeverity(groupsTypeRaw);
		var metadataSeverity =
			metadataTypeRaw != ClassDefinitions.SeverityInherit
				? ResolveTypeSeverity(metadataTypeRaw)
				: (globalStrict.IncludesMetadata ? registrySeverity : null);

		if (ShouldValidate(allowedTags, tagsSeverity) && tagsSeverity is { } tagSeverity)
		{
			var descriptor = WithSeverity(UndeclaredTag, tagSeverity);
			foreach (var site in tagCallSites)
			{
				if (!allowedTags.Contains(site.Value))
					ctx.ReportDiagnostic(Diagnostic.Create(descriptor, site.Location, site.Value));
			}
		}

		if (ShouldValidate(allowedKinds, kindsSeverity) && kindsSeverity is { } kindSeverity)
		{
			var descriptor = WithSeverity(UndeclaredKind, kindSeverity);
			foreach (var site in kindCallSites)
			{
				if (!allowedKinds.Contains(site.Value))
					ctx.ReportDiagnostic(Diagnostic.Create(descriptor, site.Location, site.Value));
			}
		}

		if (ShouldValidate(allowedGroups, groupsSeverity) && groupsSeverity is { } groupSeverity)
		{
			var descriptor = WithSeverity(UndeclaredGroup, groupSeverity);
			foreach (var site in groupCallSites)
			{
				if (!allowedGroups.Contains(site.Value))
					ctx.ReportDiagnostic(Diagnostic.Create(descriptor, site.Location, site.Value));
			}
		}

		if (ShouldValidate(allowedMetadata, metadataSeverity) && metadataSeverity is { } metaSeverity)
		{
			var descriptor = WithSeverity(UndeclaredMetadataKey, metaSeverity);
			foreach (var site in metadataCallSites)
			{
				// Normalise the call-site key the same way the registry keys were normalised
				// so that "Azure SKU", "azure sku", "AZURE_sku" all match "Azure_SKU".
				var normalised = NormaliseMetadataKeyForComparison(site.Value);
				if (!allowedMetadata.Contains(normalised))
					ctx.ReportDiagnostic(Diagnostic.Create(descriptor, site.Location, site.Value));
			}
		}
	}

	static HashSet<string> BuildAllowedSet(IEnumerable<string> primary, IEnumerable<string> secondary)
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var v in primary)
			set.Add(v);
		foreach (var v in secondary)
			set.Add(v);
		return set;
	}

	/// <summary>
	/// Normalises a metadata key for registry comparison by replacing every character that is
	/// not a letter, digit, hyphen, or underscore with <c>_</c>.  The resulting string is then
	/// compared case-insensitively, so <c>"Azure SKU"</c>, <c>"azure sku"</c>, <c>"AZURE_sku"</c>,
	/// and <c>"Azure_SKU"</c> all resolve to the same key.
	/// Mirrors the runtime logic in <c>ModelBuilder.NormaliseMetadataKey</c>.
	/// </summary>
	internal static string NormaliseMetadataKeyForComparison(string key)
	{
		if (string.IsNullOrEmpty(key))
			return key;

		var chars = key.ToCharArray();
		for (var i = 0; i < chars.Length; i++)
		{
			var c = chars[i];
			if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
				chars[i] = '_';
		}

		return new string(chars);
	}
}
