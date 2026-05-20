using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
public sealed class LikeC4StrictValidatorGenerator : IIncrementalGenerator
{
	const string AttributeNamespace = "Aspire.Hosting.AspireC4";
	const string AttributeShortName = "LikeC4RegistryAttribute";
	const string AttributeFullName = AttributeNamespace + "." + AttributeShortName;

	const string WithTagMethodName = "WithTag";
	const string WithKindMethodName = "WithKind";
	const string WithGroupMethodName = "WithLikeC4Group";

	// LikeC4RegistryType enum values (matching the injected enum)
	const int RegistryTypeTag = 0;
	const int RegistryTypeElementKind = 1;
	const int RegistryTypeRelationshipKind = 2;
	const int RegistryTypeGroup = 3;
	const int RegistryTypeMetadataKey = 4;

	/// <summary>
	/// Line-level patterns safe to apply globally. In LikeC4 DSL, these token sequences only
	/// appear inside <c>specification { }</c> blocks.
	/// </summary>
	static readonly Regex TagLinePattern = new(
		@"^\s*tag\s+([\w][\w-]*)",
		RegexOptions.Multiline | RegexOptions.Compiled
	);

	static readonly Regex ElementKindLinePattern = new(
		@"^\s*element\s+([\w][\w-]*)",
		RegexOptions.Multiline | RegexOptions.Compiled
	);

	static readonly Regex RelationshipKindLinePattern = new(
		@"^\s*relationship\s+([\w][\w-]*)",
		RegexOptions.Multiline | RegexOptions.Compiled
	);

	// --- Diagnostics ---

	/// <summary>Emitted when a <c>.WithTag()</c> argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredTag = new(
		id: "ASPIREC4001",
		title: "Undeclared LikeC4 tag",
		messageFormat: "Tag '{0}' is not declared. Add it to a 'specification {{ tag {0} }}' block in a .c4 additional file, "
			+ "or as 'public const string' in the 'Tags' nested class of your [LikeC4Registry] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All tags passed to WithTag() must be declared in the LikeC4 specification block of an additional "
			+ ".c4 file (when AspireC4Strict has a non-off severity), or as public const string fields in the Tags nested class "
			+ "of a [LikeC4Registry]-annotated class."
	);

	/// <summary>
	/// Emitted when a <c>.WithKind()</c> argument is not declared in any active element-kind or
	/// relationship-kind definition source.
	/// </summary>
	public static readonly DiagnosticDescriptor UndeclaredKind = new(
		id: "ASPIREC4002",
		title: "Undeclared LikeC4 element or relationship kind",
		messageFormat: "Kind '{0}' is not declared. Add it to a 'specification {{ element {0} }}' or "
			+ "'specification {{ relationship {0} }}' block in a .c4 additional file, "
			+ "or as 'public const string' in 'ElementKinds' or 'RelationshipKinds' nested class of your [LikeC4Registry] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All kinds passed to WithKind() must be declared in the LikeC4 specification block of an additional "
			+ ".c4 file (when AspireC4Strict has a non-off severity), or as public const string fields in the ElementKinds or "
			+ "RelationshipKinds nested class of a [LikeC4Registry]-annotated class."
	);

	/// <summary>Emitted when more than one class per assembly carries <c>[LikeC4Registry]</c>.</summary>
	public static readonly DiagnosticDescriptor MultipleDefinitionsClasses = new(
		id: "ASPIREC4003",
		title: "Multiple [LikeC4Registry] classes",
		messageFormat: "Only one class per assembly may carry [LikeC4Registry]. Duplicate found: '{0}'.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Only one class per assembly may be annotated with [LikeC4Registry]. "
			+ "Consolidate all tag, element-kind, relationship-kind, group, and metadata-key definitions into a single class."
	);

	/// <summary>Emitted when a <c>.WithLikeC4Group()</c> argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredGroup = new(
		id: "ASPIREC4004",
		title: "Undeclared LikeC4 group",
		messageFormat: "Group '{0}' is not declared. Add it as 'public const string' in the 'Groups' nested class of your [LikeC4Registry] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All group names passed to WithLikeC4Group() must be declared as public const string fields "
			+ "in the Groups nested class of a [LikeC4Registry]-annotated class."
	);

	/// <summary>Emitted when a <c>.WithMetadata()</c> key argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredMetadataKey = new(
		id: "ASPIREC4006",
		title: "Undeclared LikeC4 metadata key",
		messageFormat: "Metadata key '{0}' is not declared. Add it as 'public const string' in the 'MetadataKeys' nested class of your [LikeC4Registry] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All metadata keys passed to WithMetadata() must be declared as public const string fields "
			+ "in the MetadataKeys nested class of a [LikeC4Registry]-annotated class. "
			+ "Keys are compared after normalising whitespace and punctuation to underscores, and are case-insensitive."
	);

	/// <summary>
	/// Emitted when a registry type is declared both via a named nested class <em>and</em>
	/// via individual <c>[KnownType]</c> attributes on constants.
	/// </summary>
	public static readonly DiagnosticDescriptor DuplicateTypeDeclaration = new(
		id: "ASPIREC4005",
		title: "Duplicate LikeC4 registry type declaration",
		messageFormat: "Registry type '{0}' is declared both as a nested class and via [KnownType] attributes. Use only one declaration approach per type.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A registry type (e.g. Tag, Group) must be declared either as a nested static class "
			+ "(e.g. 'public static class Tags { ... }') OR via [KnownType] attributes on individual constants, not both."
	);

	// --- Injected attributes/enums source ---

	const string AttributeSource =
		"// <auto-generated />\n"
		+ "// Generated by AspireC4.SourceGenerators — do not edit manually.\n"
		+ "#nullable enable\n"
		+ "\n"
		+ "namespace Aspire.Hosting.AspireC4\n"
		+ "{\n"
		+ "    /// <summary>\n"
		+ "    /// Marks a static class as the single source of truth for LikeC4 registry values\n"
		+ "    /// (tags, element kinds, relationship kinds, groups, metadata keys).\n"
		+ "    /// Only one class per assembly may carry this attribute.\n"
		+ "    /// </summary>\n"
		+ "    /// <remarks>\n"
		+ "    /// Declare values as <c>public const string</c> fields inside nested static classes\n"
		+ "    /// named <c>Tags</c>, <c>ElementKinds</c>, <c>RelationshipKinds</c>, <c>Groups</c>,\n"
		+ "    /// or <c>MetadataKeys</c>, OR directly on the class with a <c>[KnownType]</c> attribute.\n"
		+ "    /// </remarks>\n"
		+ "    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]\n"
		+ "    internal sealed class LikeC4RegistryAttribute : System.Attribute\n"
		+ "    {\n"
		+ "        /// <summary>Registry-level diagnostic severity override. Default is <see cref=\"LikeC4Severity.Inherit\"/>.</summary>\n"
		+ "        public LikeC4Severity Strict { get; init; } = LikeC4Severity.Inherit;\n"
		+ "    }\n"
		+ "\n"
		+ "    /// <summary>Identifies which LikeC4 registry type a constant belongs to.</summary>\n"
		+ "    internal enum LikeC4RegistryType\n"
		+ "    {\n"
		+ "        /// <summary>The constant is a LikeC4 tag (used with <c>.WithTag()</c>).</summary>\n"
		+ "        Tag = 0,\n"
		+ "        /// <summary>The constant is a LikeC4 element kind (used with <c>.WithKind()</c>).</summary>\n"
		+ "        ElementKind = 1,\n"
		+ "        /// <summary>The constant is a LikeC4 relationship kind (used with <c>.WithKind()</c>).</summary>\n"
		+ "        RelationshipKind = 2,\n"
		+ "        /// <summary>The constant is a LikeC4 group name (used with <c>.WithLikeC4Group()</c>).</summary>\n"
		+ "        Group = 3,\n"
		+ "        /// <summary>The constant is a LikeC4 metadata key (used with <c>.WithMetadata()</c>).</summary>\n"
		+ "        MetadataKey = 4,\n"
		+ "    }\n"
		+ "\n"
		+ "    /// <summary>Controls the diagnostic severity for a registry class or type.</summary>\n"
		+ "    internal enum LikeC4Severity\n"
		+ "    {\n"
		+ "        /// <summary>Inherits severity from the parent scope (registry → MSBuild → default Suggestion when [LikeC4Registry] exists).</summary>\n"
		+ "        Inherit = 0,\n"
		+ "        /// <summary>Disables validation for this scope entirely.</summary>\n"
		+ "        Off = 1,\n"
		+ "        /// <summary>Emits an IDE suggestion (hidden diagnostic).</summary>\n"
		+ "        Suggestion = 2,\n"
		+ "        /// <summary>Emits a compiler warning.</summary>\n"
		+ "        Warning = 3,\n"
		+ "        /// <summary>Emits a compiler error, preventing build success.</summary>\n"
		+ "        Error = 4,\n"
		+ "    }\n"
		+ "\n"
		+ "    /// <summary>\n"
		+ "    /// Marks a <c>public const string</c> field as a known LikeC4 registry value of a specific type.\n"
		+ "    /// </summary>\n"
		+ "    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = false)]\n"
		+ "    internal sealed class KnownTypeAttribute : System.Attribute\n"
		+ "    {\n"
		+ "        public KnownTypeAttribute(LikeC4RegistryType type) { Type = type; }\n"
		+ "        /// <summary>The registry type this constant belongs to.</summary>\n"
		+ "        public LikeC4RegistryType Type { get; }\n"
		+ "        /// <summary>Per-type severity override. Default is <see cref=\"LikeC4Severity.Inherit\"/>.</summary>\n"
		+ "        public LikeC4Severity Strict { get; init; } = LikeC4Severity.Inherit;\n"
		+ "    }\n"
		+ "}\n";

	/// <inheritdoc />
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Always inject [LikeC4Registry], LikeC4RegistryType, LikeC4Severity, and [KnownType] so that
		// user code referencing these types compiles regardless of the disable flag.
		context.RegisterPostInitializationOutput(static ctx =>
			ctx.AddSource("LikeC4RegistryAttributes.g.cs", SourceText.From(AttributeSource, Encoding.UTF8))
		);

		// Opt-out: set <DisableAspireC4SourceGenerator>true</DisableAspireC4SourceGenerator> to skip all validation.
		var isDisabled = context.AnalyzerConfigOptionsProvider.Select(
			static (opts, _) =>
			{
				opts.GlobalOptions.TryGetValue("build_property.DisableAspireC4SourceGenerator", out var val);
				return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
			}
		);

		var globalStrict = context.AnalyzerConfigOptionsProvider.Select(
			static (opts, _) =>
			{
				opts.GlobalOptions.TryGetValue("build_property.AspireC4Strict", out var val);
				return ParseGlobalStrict(val);
			}
		);

		var dslDefinitions = context
			.AdditionalTextsProvider.Where(static f => IsDslFile(f.Path))
			.Select(static (f, ct) => ParseDslFile(f, ct))
			.Collect()
			.Select(static (parsed, _) => MergeDslDefinitions(parsed));

		// Mode 2: [LikeC4Registry] class-based definitions.
		var classDefinitions = context
			.SyntaxProvider.ForAttributeWithMetadataName(
				AttributeFullName,
				predicate: static (node, _) => node is ClassDeclarationSyntax,
				transform: static (ctx, ct) => ExtractClassDefinitions(ctx, ct)
			)
			.Collect();

		// Call-site string values to validate.
		var tagCallSites = CreateCallSiteProvider(context, WithTagMethodName).Collect();
		var kindCallSites = CreateCallSiteProvider(context, WithKindMethodName).Collect();
		var groupCallSites = CreateCallSiteProvider(context, WithGroupMethodName).Collect();
		var metadataCallSites = CreateMetadataCallSiteProvider(context).Collect();

		// Combine everything and validate.
		context.RegisterSourceOutput(
			isDisabled
				.Combine(globalStrict)
				.Combine(dslDefinitions)
				.Combine(classDefinitions)
				.Combine(tagCallSites)
				.Combine(kindCallSites)
				.Combine(groupCallSites)
				.Combine(metadataCallSites),
			static (ctx, data) =>
			{
				var (((((((isDisabled, globalStrict), dslDefs), classDefs), tags), kinds), groups), metadata) = data;
				if (isDisabled)
					return;
				Validate(ctx, globalStrict, dslDefs, classDefs, tags, kinds, groups, metadata);
			}
		);
	}

	// --- DSL file parsing ---

	static bool IsDslFile(string path) =>
		path.EndsWith(".c4", StringComparison.OrdinalIgnoreCase)
		|| path.EndsWith(".likec4", StringComparison.OrdinalIgnoreCase);

	static DslDefinitions ParseDslFile(AdditionalText file, CancellationToken ct) =>
		ExtractSpecificationItems(file.GetText(ct)?.ToString() ?? string.Empty);

	/// <summary>
	/// Extracts declared tags, element kinds, and relationship kinds from a LikeC4 DSL file.
	/// Exposed as <see langword="internal"/> for direct unit-testing.
	/// </summary>
	internal static DslDefinitions ExtractSpecificationItems(string text)
	{
		return new DslDefinitions(
			ExtractMatches(TagLinePattern, text),
			ExtractMatches(ElementKindLinePattern, text),
			ExtractMatches(RelationshipKindLinePattern, text)
		);
	}

	static ImmutableArray<string> ExtractMatches(Regex pattern, string text)
	{
		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (Match m in pattern.Matches(text))
			builder.Add(m.Groups[1].Value);
		return builder.ToImmutable();
	}

	static DslDefinitions MergeDslDefinitions(ImmutableArray<DslDefinitions> parsed)
	{
		if (parsed.IsEmpty)
			return DslDefinitions.Empty;

		var tags = ImmutableArray.CreateBuilder<string>();
		var elementKinds = ImmutableArray.CreateBuilder<string>();
		var relationshipKinds = ImmutableArray.CreateBuilder<string>();

		foreach (var d in parsed)
		{
			tags.AddRange(d.Tags);
			elementKinds.AddRange(d.ElementKinds);
			relationshipKinds.AddRange(d.RelationshipKinds);
		}

		return new DslDefinitions(tags.ToImmutable(), elementKinds.ToImmutable(), relationshipKinds.ToImmutable());
	}

	// --- Class-based definitions ---

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
		foreach (var nested in classSymbol.GetTypeMembers())
		{
			ct.ThrowIfCancellationRequested();

			int? registryType = nested.Name switch
			{
				"Tags" => RegistryTypeTag,
				"ElementKinds" => RegistryTypeElementKind,
				"RelationshipKinds" => RegistryTypeRelationshipKind,
				"Groups" => RegistryTypeGroup,
				"MetadataKeys" => RegistryTypeMetadataKey,
				_ => (int?)null,
			};

			if (registryType is null)
				continue;

			nestedClassTypes.Add(registryType.Value);
			var target = GetTargetList(
				registryType.Value,
				tags,
				elementKinds,
				relationshipKinds,
				groups,
				metadataKeys
			)!;

			foreach (var member in nested.GetMembers())
			{
				if (
					member is not IFieldSymbol field
					|| !field.IsConst
					|| field.DeclaredAccessibility != Accessibility.Public
					|| field.Type.SpecialType != SpecialType.System_String
					|| field.ConstantValue is not string value
				)
					continue;

				target.Add(value);
			}
		}

		// Step 2: scan top-level fields with [KnownType] attributes.
		foreach (var member in classSymbol.GetMembers())
		{
			ct.ThrowIfCancellationRequested();

			if (
				member is not IFieldSymbol field
				|| !field.IsConst
				|| field.Type.SpecialType != SpecialType.System_String
				|| field.ConstantValue is not string value
			)
				continue;

			var knownTypeAttr = field
				.GetAttributes()
				.FirstOrDefault(static a => a.AttributeClass?.Name == "KnownTypeAttribute");

			if (knownTypeAttr is null)
				continue;

			if (knownTypeAttr.ConstructorArguments.Length == 0)
				continue;

			var typeArg = knownTypeAttr.ConstructorArguments[0];
			if (typeArg.Kind != TypedConstantKind.Enum || typeArg.Value is not int registryTypeInt)
				continue;

			var strictArg = knownTypeAttr.NamedArguments.FirstOrDefault(static a => a.Key == "Strict");
			var fieldStrictMode =
				strictArg.Value.Kind == TypedConstantKind.Enum && strictArg.Value.Value is int strictInt
					? strictInt
					: ClassDefinitions.SeverityInherit;

			var fieldLocation = field.Locations.Length > 0 ? field.Locations[0] : null;

			if (!knownTypeFieldsByType.TryGetValue(registryTypeInt, out var fieldList))
				knownTypeFieldsByType[registryTypeInt] = fieldList = [];

			fieldList.Add((value, fieldStrictMode, fieldLocation));

			GetTargetList(registryTypeInt, tags, elementKinds, relationshipKinds, groups, metadataKeys)?.Add(value);
		}

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
			ImmutableArray.CreateRange(tags),
			ImmutableArray.CreateRange(elementKinds),
			ImmutableArray.CreateRange(relationshipKinds),
			ImmutableArray.CreateRange(groups),
			ImmutableArray.CreateRange(metadataKeys),
			registryStrictMode,
			ComputeTypeStrictMode(RegistryTypeTag),
			ComputeTypeStrictMode(RegistryTypeElementKind),
			ComputeTypeStrictMode(RegistryTypeRelationshipKind),
			ComputeTypeStrictMode(RegistryTypeGroup),
			ComputeTypeStrictMode(RegistryTypeMetadataKey),
			ImmutableArray.CreateRange(duplicates)
		);
	}

	static List<string>? GetTargetList(
		int registryType,
		List<string> tags,
		List<string> elementKinds,
		List<string> relationshipKinds,
		List<string> groups,
		List<string> metadataKeys
	) =>
		registryType switch
		{
			RegistryTypeTag => tags,
			RegistryTypeElementKind => elementKinds,
			RegistryTypeRelationshipKind => relationshipKinds,
			RegistryTypeGroup => groups,
			RegistryTypeMetadataKey => metadataKeys,
			_ => null,
		};

	// --- Call-site collection ---

	static IncrementalValuesProvider<CallSiteInfo> CreateCallSiteProvider(
		IncrementalGeneratorInitializationContext context,
		string methodName
	)
	{
		return context
			.SyntaxProvider.CreateSyntaxProvider(
				predicate: (node, _) => IsTargetInvocation(node, methodName),
				transform: (ctx, ct) => ExtractCallSiteInfo(ctx, ct)
			)
			.Where(static v => v.HasValue)
			.Select(static (v, _) => v!.Value);
	}

	/// <summary>
	/// Collects the first argument (key) of every <c>.WithMetadata(key, value)</c> call site.
	/// </summary>
	static IncrementalValuesProvider<CallSiteInfo> CreateMetadataCallSiteProvider(
		IncrementalGeneratorInitializationContext context
	)
	{
		return context
			.SyntaxProvider.CreateSyntaxProvider(
				predicate: static (node, _) => IsTargetInvocation(node, "WithMetadata"),
				transform: static (ctx, ct) => ExtractCallSiteInfo(ctx, ct)
			)
			.Where(static v => v.HasValue)
			.Select(static (v, _) => v!.Value);
	}

	static bool IsTargetInvocation(SyntaxNode node, string methodName) =>
		node
			is InvocationExpressionSyntax
			{
				ArgumentList.Arguments.Count: > 0,
				Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: var name },
			}
		&& name == methodName;

	static CallSiteInfo? ExtractCallSiteInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
	{
		var invocation = (InvocationExpressionSyntax)ctx.Node;
		var firstArg = invocation.ArgumentList.Arguments[0].Expression;
		var constant = ctx.SemanticModel.GetConstantValue(firstArg, ct);

		return constant.HasValue && constant.Value is string value
			? new CallSiteInfo(value, firstArg.GetLocation())
			: null;
	}

	// --- Validation ---

	static (DiagnosticSeverity? Severity, bool IncludesMetadata) ParseGlobalStrict(string? val)
	{
		if (string.IsNullOrWhiteSpace(val))
			return (null, false);

		var normalized = val!.Trim();
		if (normalized.Equals("off", StringComparison.OrdinalIgnoreCase))
			return (null, false);
		if (normalized.Equals("suggestion", StringComparison.OrdinalIgnoreCase))
			return (DiagnosticSeverity.Info, false);
		if (normalized.Equals("warning", StringComparison.OrdinalIgnoreCase))
			return (DiagnosticSeverity.Warning, false);
		if (
			normalized.Equals("error", StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("all", StringComparison.OrdinalIgnoreCase)
		)
			return (DiagnosticSeverity.Error, false);
		if (normalized.Equals("allincludingmetadata", StringComparison.OrdinalIgnoreCase))
			return (DiagnosticSeverity.Error, true);

		return (null, false);
	}

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

	static void Validate(
		SourceProductionContext ctx,
		(DiagnosticSeverity? Severity, bool IncludesMetadata) globalStrict,
		DslDefinitions dslDefs,
		ImmutableArray<ClassDefinitions> classDefs,
		ImmutableArray<CallSiteInfo> tagCallSites,
		ImmutableArray<CallSiteInfo> kindCallSites,
		ImmutableArray<CallSiteInfo> groupCallSites,
		ImmutableArray<CallSiteInfo> metadataCallSites
	)
	{
		if (classDefs.Length > 1)
		{
			for (int i = 1; i < classDefs.Length; i++)
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

		bool hasDslValidation = globalStrict.Severity is not null && dslDefs.HasAny;
		bool hasClassValidation = classDefs.Length > 0;

		if (!hasDslValidation && !hasClassValidation)
			return;

		var primaryDef = hasClassValidation ? classDefs[0] : null;
		int registryRaw = primaryDef?.RegistryStrictMode ?? ClassDefinitions.SeverityInherit;
		bool registryExplicit = registryRaw != ClassDefinitions.SeverityInherit;
		bool globalExplicit = globalStrict.Severity is not null;
		bool isExplicitlyEnabled = registryExplicit || globalExplicit;

		DiagnosticSeverity? registrySeverity = registryRaw switch
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

		int tagsTypeRaw = primaryDef?.TagsTypeStrictMode ?? ClassDefinitions.SeverityInherit;
		int kindsTypeRaw = CombineRaw(
			primaryDef?.ElementKindsTypeStrictMode ?? ClassDefinitions.SeverityInherit,
			primaryDef?.RelationshipKindsTypeStrictMode ?? ClassDefinitions.SeverityInherit
		);
		int groupsTypeRaw = primaryDef?.GroupsTypeStrictMode ?? ClassDefinitions.SeverityInherit;
		int metadataTypeRaw = primaryDef?.MetadataKeysTypeStrictMode ?? ClassDefinitions.SeverityInherit;

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
