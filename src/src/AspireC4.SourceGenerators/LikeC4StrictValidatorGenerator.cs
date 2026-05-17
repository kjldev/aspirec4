using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
///       <b>DSL file mode</b>: activated when <c>&lt;AspireC4Strict&gt;true&lt;/AspireC4Strict&gt;</c> is set
///       in the consuming project. <c>.c4</c>/<c>.likec4</c> additional files are parsed for
///       <c>specification</c> block declarations (<c>tag</c>, <c>element</c>, <c>relationship</c>).
///       All <c>.WithTag()</c> and <c>.WithKind()</c> call-site values are validated against those.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Class-based mode</b>: a class annotated with <c>[LikeC4Definitions]</c> (any accessibility,
///       any nesting level) provides <c>public const string</c> fields inside nested classes named
///       <c>Tags</c>, <c>ElementKinds</c>, and <c>RelationshipKinds</c>. Those constants are the
///       source of truth. Only one such class is allowed per assembly.
///     </description>
///   </item>
/// </list>
/// Both modes may be active simultaneously; allowed sets are merged.
/// </remarks>
[Generator]
public sealed class LikeC4StrictValidatorGenerator : IIncrementalGenerator
{
	const string AttributeNamespace = "Aspire.Hosting.AspireC4";
	const string AttributeShortName = "LikeC4DefinitionsAttribute";
	const string AttributeFullName = AttributeNamespace + "." + AttributeShortName;

	const string WithTagMethodName = "WithTag";
	const string WithKindMethodName = "WithKind";
	const string WithGroupMethodName = "WithLikeC4Group";

	/// <summary>
	/// Line-level patterns safe to apply globally. In LikeC4 DSL, these token sequences only
	/// appear inside <c>specification { }</c> blocks:
	/// <list type="bullet">
	///   <item><c>tag X</c> — in model blocks tags appear as <c>#tagname</c>, not <c>tag X</c></item>
	///   <item><c>element X</c> — in model blocks the form is <c>id = kind 'label'</c></item>
	///   <item><c>relationship X</c> — in model blocks the form is <c>source .kind target</c></item>
	/// </list>
	/// </summary>
	static readonly Regex TagLinePattern = new Regex(
		@"^\s*tag\s+([\w][\w-]*)",
		RegexOptions.Multiline | RegexOptions.Compiled
	);

	static readonly Regex ElementKindLinePattern = new Regex(
		@"^\s*element\s+([\w][\w-]*)",
		RegexOptions.Multiline | RegexOptions.Compiled
	);

	static readonly Regex RelationshipKindLinePattern = new Regex(
		@"^\s*relationship\s+([\w][\w-]*)",
		RegexOptions.Multiline | RegexOptions.Compiled
	);

	// --- Diagnostics ---

	/// <summary>Emitted when a <c>.WithTag()</c> argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredTag = new DiagnosticDescriptor(
		id: "ASPIREC4001",
		title: "Undeclared LikeC4 tag",
		messageFormat: "Tag '{0}' is not declared. Add it to a 'specification {{ tag {0} }}' block in a .c4 additional file, "
			+ "or as 'public const string' in the 'Tags' nested class of your [LikeC4Definitions] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "All tags passed to WithTag() must be declared in the LikeC4 specification block of an additional "
			+ ".c4 file (when AspireC4Strict=true), or as public const string fields in the Tags nested class "
			+ "of a [LikeC4Definitions]-annotated class."
	);

	/// <summary>
	/// Emitted when a <c>.WithKind()</c> argument is not declared in any active element-kind or
	/// relationship-kind definition source.
	/// </summary>
	public static readonly DiagnosticDescriptor UndeclaredKind = new DiagnosticDescriptor(
		id: "ASPIREC4002",
		title: "Undeclared LikeC4 element or relationship kind",
		messageFormat: "Kind '{0}' is not declared. Add it to a 'specification {{ element {0} }}' or "
			+ "'specification {{ relationship {0} }}' block in a .c4 additional file, "
			+ "or as 'public const string' in 'ElementKinds' or 'RelationshipKinds' nested class of your [LikeC4Definitions] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "All kinds passed to WithKind() must be declared in the LikeC4 specification block of an additional "
			+ ".c4 file (when AspireC4Strict=true), or as public const string fields in the ElementKinds or "
			+ "RelationshipKinds nested class of a [LikeC4Definitions]-annotated class."
	);

	/// <summary>Emitted when more than one class per assembly carries <c>[LikeC4Definitions]</c>.</summary>
	public static readonly DiagnosticDescriptor MultipleDefinitionsClasses = new DiagnosticDescriptor(
		id: "ASPIREC4003",
		title: "Multiple [LikeC4Definitions] classes",
		messageFormat: "Only one class per assembly may carry [LikeC4Definitions]. Duplicate found: '{0}'.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Only one class per assembly may be annotated with [LikeC4Definitions]. "
			+ "Consolidate all tag, element-kind, and relationship-kind definitions into a single class."
	);

	/// <summary>Emitted when a <c>.WithLikeC4Group()</c> argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredGroup = new DiagnosticDescriptor(
		id: "ASPIREC4004",
		title: "Undeclared LikeC4 group",
		messageFormat: "Group '{0}' is not declared. Add it as 'public const string' in the 'Groups' nested class of your [LikeC4Definitions] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "All group names passed to WithLikeC4Group() must be declared as public const string fields "
			+ "in the Groups nested class of a [LikeC4Definitions]-annotated class."
	);

	// --- Injected attribute ---

	const string AttributeSource =
		"// <auto-generated />\n"
		+ "// Generated by AspireC4.SourceGenerators — do not edit manually.\n"
		+ "#nullable enable\n"
		+ "\n"
		+ "namespace Aspire.Hosting.AspireC4\n"
		+ "{\n"
		+ "    /// <summary>\n"
		+ "    /// Marks a class as the single source of truth for LikeC4 definitions\n"
		+ "    /// (tags, element kinds, relationship kinds). Only one class per assembly\n"
		+ "    /// may carry this attribute.\n"
		+ "    /// </summary>\n"
		+ "    /// <remarks>\n"
		+ "    /// Declare allowed values as <c>public const string</c> fields inside\n"
		+ "    /// nested static classes named <c>Tags</c>, <c>ElementKinds</c>, and\n"
		+ "    /// <c>RelationshipKinds</c> within the annotated class.\n"
		+ "    /// The source generator will emit errors for any <c>.WithTag()</c> or\n"
		+ "    /// <c>.WithKind()</c> call-site value not present as a constant.\n"
		+ "    /// </remarks>\n"
		+ "    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]\n"
		+ "    internal sealed class LikeC4DefinitionsAttribute : System.Attribute { }\n"
		+ "}\n";

	/// <inheritdoc />
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Inject [LikeC4Definitions] into the user's compilation.
		context.RegisterPostInitializationOutput(static ctx =>
			ctx.AddSource("LikeC4DefinitionsAttribute.g.cs", SourceText.From(AttributeSource, Encoding.UTF8))
		);

		// Mode 1: DSL additional file definitions — opt-in via <AspireC4Strict>true</AspireC4Strict>.
		var isStrictMode = context.AnalyzerConfigOptionsProvider.Select(
			static (opts, _) =>
			{
				opts.GlobalOptions.TryGetValue("build_property.AspireC4Strict", out var val);
				return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
			}
		);

		var dslDefinitions = context
			.AdditionalTextsProvider.Where(static f => IsDslFile(f.Path))
			.Select(static (f, ct) => ParseDslFile(f, ct))
			.Collect()
			.Select(static (parsed, _) => MergeDslDefinitions(parsed));

		// Mode 2: [LikeC4Definitions] class-based definitions.
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

		// Combine everything and validate.
		context.RegisterSourceOutput(
			isStrictMode
				.Combine(dslDefinitions)
				.Combine(classDefinitions)
				.Combine(tagCallSites)
				.Combine(kindCallSites)
				.Combine(groupCallSites),
			static (ctx, data) =>
			{
				var (((((isStrict, dslDefs), classDefs), tags), kinds), groups) = data;
				Validate(ctx, isStrict, dslDefs, classDefs, tags, kinds, groups);
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
	/// Uses line-level regex patterns that are safe to apply globally across the file content
	/// because the matched token sequences only appear inside <c>specification { }</c> blocks.
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

		foreach (var nested in classSymbol.GetTypeMembers())
		{
			ct.ThrowIfCancellationRequested();

			List<string>? target;
			switch (nested.Name)
			{
				case "Tags":
					target = tags;
					break;
				case "ElementKinds":
					target = elementKinds;
					break;
				case "RelationshipKinds":
					target = relationshipKinds;
					break;
				case "Groups":
					target = groups;
					break;
				default:
					continue;
			}

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

		return new ClassDefinitions(
			displayName,
			location,
			tags.ToImmutableArray(),
			elementKinds.ToImmutableArray(),
			relationshipKinds.ToImmutableArray(),
			groups.ToImmutableArray()
		);
	}

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

		if (!constant.HasValue || constant.Value is not string value)
			return null;

		return new CallSiteInfo(value, firstArg.GetLocation());
	}

	// --- Validation ---

	static void Validate(
		SourceProductionContext ctx,
		bool isStrictMode,
		DslDefinitions dslDefs,
		ImmutableArray<ClassDefinitions> classDefs,
		ImmutableArray<CallSiteInfo> tagCallSites,
		ImmutableArray<CallSiteInfo> kindCallSites,
		ImmutableArray<CallSiteInfo> groupCallSites
	)
	{
		// Enforce single definitions class per assembly (report all duplicates beyond the first).
		if (classDefs.Length > 1)
		{
			for (int i = 1; i < classDefs.Length; i++)
			{
				ctx.ReportDiagnostic(
					Diagnostic.Create(MultipleDefinitionsClasses, classDefs[i].Location, classDefs[i].DisplayName)
				);
			}
		}

		bool hasDslValidation = isStrictMode && dslDefs.HasAny;
		bool hasClassValidation = classDefs.Length > 0;

		if (!hasDslValidation && !hasClassValidation)
			return;

		// Build allowed sets from all active definition sources.
		var allowedTags = BuildAllowedSet(
			hasDslValidation ? dslDefs.Tags.AsEnumerable() : Enumerable.Empty<string>(),
			hasClassValidation ? classDefs.SelectMany(static d => d.Tags) : Enumerable.Empty<string>()
		);

		var allowedKinds = BuildAllowedSet(
			hasDslValidation ? dslDefs.ElementKinds.Concat(dslDefs.RelationshipKinds) : Enumerable.Empty<string>(),
			hasClassValidation
				? classDefs.SelectMany(static d => d.ElementKinds.Concat(d.RelationshipKinds))
				: Enumerable.Empty<string>()
		);

		// Groups are class-based only (LikeC4 specification blocks have no group keyword).
		var allowedGroups = hasClassValidation
			? BuildAllowedSet(Enumerable.Empty<string>(), classDefs.SelectMany(static d => d.Groups))
			: new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var site in tagCallSites)
		{
			if (!allowedTags.Contains(site.Value))
				ctx.ReportDiagnostic(Diagnostic.Create(UndeclaredTag, site.Location, site.Value));
		}

		// WithKind() is used for both element kinds and relationship kinds; validate against the union.
		if (allowedKinds.Count > 0)
		{
			foreach (var site in kindCallSites)
			{
				if (!allowedKinds.Contains(site.Value))
					ctx.ReportDiagnostic(Diagnostic.Create(UndeclaredKind, site.Location, site.Value));
			}
		}

		// Validate group names only when the [LikeC4Definitions] class declares a Groups nested class.
		if (allowedGroups.Count > 0)
		{
			foreach (var site in groupCallSites)
			{
				if (!allowedGroups.Contains(site.Value))
					ctx.ReportDiagnostic(Diagnostic.Create(UndeclaredGroup, site.Location, site.Value));
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
}

// ---------------------------------------------------------------------------
// Supporting types
// ---------------------------------------------------------------------------

/// <summary>Definitions extracted from one or more LikeC4 DSL additional files.</summary>
readonly struct DslDefinitions : IEquatable<DslDefinitions>
{
	public static readonly DslDefinitions Empty = new DslDefinitions(
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty
	);

	public DslDefinitions(
		ImmutableArray<string> tags,
		ImmutableArray<string> elementKinds,
		ImmutableArray<string> relationshipKinds
	)
	{
		Tags = tags;
		ElementKinds = elementKinds;
		RelationshipKinds = relationshipKinds;
	}

	public ImmutableArray<string> Tags { get; }
	public ImmutableArray<string> ElementKinds { get; }
	public ImmutableArray<string> RelationshipKinds { get; }
	public bool HasAny => !Tags.IsEmpty || !ElementKinds.IsEmpty || !RelationshipKinds.IsEmpty;

	public bool Equals(DslDefinitions other) =>
		Tags.SequenceEqual(other.Tags, StringComparer.Ordinal)
		&& ElementKinds.SequenceEqual(other.ElementKinds, StringComparer.Ordinal)
		&& RelationshipKinds.SequenceEqual(other.RelationshipKinds, StringComparer.Ordinal);

	public override bool Equals(object obj) => obj is DslDefinitions d && Equals(d);

	public override int GetHashCode()
	{
		unchecked
		{
			int h = Tags.Length;
			h = (h * 397) ^ ElementKinds.Length;
			h = (h * 397) ^ RelationshipKinds.Length;
			return h;
		}
	}
}

/// <summary>Definitions extracted from a <c>[LikeC4Definitions]</c>-annotated class.</summary>
sealed class ClassDefinitions : IEquatable<ClassDefinitions>
{
	public static readonly ClassDefinitions Empty = new ClassDefinitions(
		string.Empty,
		null,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty
	);

	public ClassDefinitions(
		string displayName,
		Location? location,
		ImmutableArray<string> tags,
		ImmutableArray<string> elementKinds,
		ImmutableArray<string> relationshipKinds,
		ImmutableArray<string> groups
	)
	{
		DisplayName = displayName;
		Location = location;
		Tags = tags;
		ElementKinds = elementKinds;
		RelationshipKinds = relationshipKinds;
		Groups = groups;
	}

	public string DisplayName { get; }

	/// <summary>Location of the class declaration, used for <c>ASPIREC4003</c> diagnostics.</summary>
	public Location? Location { get; }

	public ImmutableArray<string> Tags { get; }
	public ImmutableArray<string> ElementKinds { get; }
	public ImmutableArray<string> RelationshipKinds { get; }
	public ImmutableArray<string> Groups { get; }

	public bool Equals(ClassDefinitions other)
	{
		if (other is null)
			return false;

		return DisplayName == other.DisplayName
			&& Tags.SequenceEqual(other.Tags, StringComparer.Ordinal)
			&& ElementKinds.SequenceEqual(other.ElementKinds, StringComparer.Ordinal)
			&& RelationshipKinds.SequenceEqual(other.RelationshipKinds, StringComparer.Ordinal)
			&& Groups.SequenceEqual(other.Groups, StringComparer.Ordinal);
	}

	public override bool Equals(object obj) => Equals(obj as ClassDefinitions);

	public override int GetHashCode()
	{
		unchecked
		{
			int h = DisplayName?.GetHashCode() ?? 0;
			h = (h * 397) ^ Tags.Length;
			h = (h * 397) ^ ElementKinds.Length;
			h = (h * 397) ^ RelationshipKinds.Length;
			h = (h * 397) ^ Groups.Length;
			return h;
		}
	}
}

/// <summary>A resolved constant string value from a call-site argument, with its source location.</summary>
readonly struct CallSiteInfo : IEquatable<CallSiteInfo>
{
	public CallSiteInfo(string value, Location location)
	{
		Value = value;
		Location = location;
	}

	public string Value { get; }

	/// <summary>The location of the argument expression, for diagnostic reporting.</summary>
	public Location Location { get; }

	public bool Equals(CallSiteInfo other) => Value == other.Value && Location.Equals(other.Location);

	public override bool Equals(object obj) => obj is CallSiteInfo c && Equals(c);

	public override int GetHashCode()
	{
		unchecked
		{
			int h = Value?.GetHashCode() ?? 0;
			h = (h * 397) ^ (Location?.GetHashCode() ?? 0);
			return h;
		}
	}
}
