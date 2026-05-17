using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

public sealed class LikeC4StrictValidatorGeneratorTests
{
	// -----------------------------------------------------------------------
	// ExtractSpecificationItems — direct unit tests (no Roslyn pipeline needed)
	// -----------------------------------------------------------------------

	[Test]
	public async Task ExtractSpecificationItems_WithTagDeclaration_ExtractsTags()
	{
		// Arrange
		const string dsl = """
			specification {
			  element container
			  tag my-tag
			  tag external
			}
			""";

		// Act
		var result = LikeC4StrictValidatorGenerator.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.Tags).Contains("my-tag");
		await Assert.That(result.Tags).Contains("external");
	}

	[Test]
	public async Task ExtractSpecificationItems_WithElementDeclaration_ExtractsElementKinds()
	{
		// Arrange
		const string dsl = """
			specification {
			  element container
			  element executable
			  element service
			}
			""";

		// Act
		var result = LikeC4StrictValidatorGenerator.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.ElementKinds).Contains("container");
		await Assert.That(result.ElementKinds).Contains("executable");
		await Assert.That(result.ElementKinds).Contains("service");
	}

	[Test]
	public async Task ExtractSpecificationItems_WithRelationshipDeclaration_ExtractsRelationshipKinds()
	{
		// Arrange
		const string dsl = """
			specification {
			  relationship async
			  relationship RESP
			  relationship tcp-ip
			}
			""";

		// Act
		var result = LikeC4StrictValidatorGenerator.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.RelationshipKinds).Contains("async");
		await Assert.That(result.RelationshipKinds).Contains("RESP");
		await Assert.That(result.RelationshipKinds).Contains("tcp-ip");
	}

	[Test]
	public async Task ExtractSpecificationItems_WithEmptyText_ReturnsEmptyDefinitions()
	{
		// Arrange
		const string dsl = "";

		// Act
		var result = LikeC4StrictValidatorGenerator.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.Tags).IsEmpty();
		await Assert.That(result.ElementKinds).IsEmpty();
		await Assert.That(result.RelationshipKinds).IsEmpty();
	}

	[Test]
	public async Task ExtractSpecificationItems_WithFullGeneratedFile_ExtractsAllDeclarations()
	{
		// Arrange
		const string dsl = """
			specification {
			  element container
			  element executable
			  relationship RESP
			  relationship tcp-ip
			  tag aspire-run-state-finished
			  tag aspire-run-state-running
			  tag local-dev
			}

			model {
			  redis = container 'redis' {
			    #local-dev
			    link https://redis.io/ 'Redis'
			  }
			  redis -> container_other 'Connects'
			}
			""";

		// Act
		var result = LikeC4StrictValidatorGenerator.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.Tags).Contains("aspire-run-state-finished");
		await Assert.That(result.Tags).Contains("aspire-run-state-running");
		await Assert.That(result.Tags).Contains("local-dev");
		await Assert.That(result.ElementKinds).Contains("container");
		await Assert.That(result.ElementKinds).Contains("executable");
		await Assert.That(result.RelationshipKinds).Contains("RESP");
		await Assert.That(result.RelationshipKinds).Contains("tcp-ip");
	}

	[Test]
	public async Task ExtractSpecificationItems_WithExtendBlockInModel_DoesNotFalselyExtract()
	{
		// Arrange — model block with #tag (hash-prefix) should not be picked up as a declaration
		const string dsl = """
			model {
			  extend azure_redis {
			    link https://redis.io/ 'Redis'
			    metadata {
			      team 'Platform'
			    }
			  }
			}
			""";

		// Act
		var result = LikeC4StrictValidatorGenerator.ExtractSpecificationItems(dsl);

		// Assert — nothing from the model block should be extracted
		await Assert.That(result.Tags).IsEmpty();
		await Assert.That(result.ElementKinds).IsEmpty();
		await Assert.That(result.RelationshipKinds).IsEmpty();
	}

	// -----------------------------------------------------------------------
	// HasAny on DslDefinitions
	// -----------------------------------------------------------------------

	[Test]
	public async Task DslDefinitions_Empty_HasAnyIsFalse()
	{
		// Arrange / Act
		var empty = DslDefinitions.Empty;

		// Assert
		await Assert.That(empty.HasAny).IsFalse();
	}

	[Test]
	public async Task DslDefinitions_WithTags_HasAnyIsTrue()
	{
		// Arrange / Act
		var defs = new DslDefinitions(
			ImmutableArray.Create("my-tag"),
			ImmutableArray<string>.Empty,
			ImmutableArray<string>.Empty
		);

		// Assert
		await Assert.That(defs.HasAny).IsTrue();
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — attribute injection
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_Always_InjectsLikeC4DefinitionsAttribute(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = "namespace TestApp;";

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);
		var attributeSource = GetGeneratedSource(result, "LikeC4DefinitionsAttribute.g.cs");

		// Assert
		await Assert.That(attributeSource).IsNotNull();
		await Assert.That(attributeSource!).Contains("LikeC4DefinitionsAttribute");
		await Assert.That(attributeSource).Contains("namespace Aspire.Hosting.AspireC4");
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — DSL strict mode (ASPIREC4001 / ASPIREC4002)
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithStrictModeAndDeclaredTag_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string dsl = """
			specification {
			  tag external
			}
			""";
		var source = BuildSourceWithCallSites(".WithTag(\"external\")");

		// Act
		var result = RunGenerator(
			source,
			additionalFiles: [new TestAdditionalText("model.c4", dsl)],
			strictMode: true,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4001")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndUndeclaredTag_EmitsUndeclaredTagDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string dsl = """
			specification {
			  tag existing-tag
			}
			""";
		var source = BuildSourceWithCallSites(".WithTag(\"unknown-tag\")");

		// Act
		var result = RunGenerator(
			source,
			additionalFiles: [new TestAdditionalText("model.c4", dsl)],
			strictMode: true,
			cancellationToken: cancellationToken
		);

		// Assert
		var diagnostics = GetDiagnostics(result, "ASPIREC4001");
		await Assert.That(diagnostics.Count).IsGreaterThan(0);
		await Assert.That(diagnostics[0].GetMessage()).Contains("unknown-tag");
	}

	[Test]
	public async Task RunGenerator_WithStrictModeDisabledAndUndeclaredTag_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — strict mode is off even though there are DSL files
		const string dsl = "specification { tag existing-tag }";
		var source = BuildSourceWithCallSites(".WithTag(\"unknown-tag\")");

		// Act
		var result = RunGenerator(
			source,
			additionalFiles: [new TestAdditionalText("model.c4", dsl)],
			strictMode: false,
			cancellationToken: cancellationToken
		);

		// Assert — no validation without strict mode
		await Assert.That(GetDiagnostics(result, "ASPIREC4001")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndNoDslFiles_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange — strict mode is on but no DSL additional files are provided
		var source = BuildSourceWithCallSites(".WithTag(\"any-value\")");

		// Act
		var result = RunGenerator(source, additionalFiles: [], strictMode: true, cancellationToken: cancellationToken);

		// Assert — no DSL definitions = nothing to validate against
		await Assert.That(GetDiagnostics(result, "ASPIREC4001")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndDeclaredKind_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string dsl = "specification { element service }";
		var source = BuildSourceWithCallSites(".WithKind(\"service\")");

		// Act
		var result = RunGenerator(
			source,
			additionalFiles: [new TestAdditionalText("spec.c4", dsl)],
			strictMode: true,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4002")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndUndeclaredKind_EmitsUndeclaredKindDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string dsl = """
			specification {
			  element container
			}
			""";
		var source = BuildSourceWithCallSites(".WithKind(\"unknown-kind\")");

		// Act
		var result = RunGenerator(
			source,
			additionalFiles: [new TestAdditionalText("spec.c4", dsl)],
			strictMode: true,
			cancellationToken: cancellationToken
		);

		// Assert
		var diagnostics = GetDiagnostics(result, "ASPIREC4002");
		await Assert.That(diagnostics.Count).IsGreaterThan(0);
		await Assert.That(diagnostics[0].GetMessage()).Contains("unknown-kind");
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndRelationshipKindDeclared_EmitsNoDiagnosticForWithKind(
		CancellationToken cancellationToken
	)
	{
		// Arrange — relationship kinds are also valid for WithKind()
		const string dsl = """
			specification {
			  relationship async
			}
			""";
		var source = BuildSourceWithCallSites(".WithKind(\"async\")");

		// Act
		var result = RunGenerator(
			source,
			additionalFiles: [new TestAdditionalText("spec.c4", dsl)],
			strictMode: true,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4002")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithConstReferenceToValidTag_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange — const reference should be resolved to its value
		const string dsl = "specification { tag my-tag }";
		var source = """
			namespace TestApp;
			class Setup
			{
			    const string MyTag = "my-tag";
			    static object Create(object a) => a;
			    static void Configure()
			    {
			        var a = Create(null);
			        a.WithTag(MyTag);
			    }
			}
			""";

		// Act
		var result = RunGenerator(
			source,
			additionalFiles: [new TestAdditionalText("spec.c4", dsl)],
			strictMode: true,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4001")).IsEmpty();
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — class-based mode (ASPIREC4001 / ASPIREC4002)
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndValidTag_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("External", "external")],
			callSites: [".WithTag(\"external\")"]
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4001")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndUndeclaredTag_EmitsUndeclaredTagDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("External", "external")],
			callSites: [".WithTag(\"unknown\")"]
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostics = GetDiagnostics(result, "ASPIREC4001");
		await Assert.That(diagnostics.Count).IsGreaterThan(0);
		await Assert.That(diagnostics[0].GetMessage()).Contains("unknown");
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndValidElementKind_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			elementKindConstants: [("Service", "service")],
			callSites: [".WithKind(\"service\")"]
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4002")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndValidRelationshipKind_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			relationshipKindConstants: [("Async", "async")],
			callSites: [".WithKind(\"async\")"]
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4002")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndUndeclaredKind_EmitsUndeclaredKindDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			elementKindConstants: [("Container", "container")],
			callSites: [".WithKind(\"unknown-kind\")"]
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostics = GetDiagnostics(result, "ASPIREC4002");
		await Assert.That(diagnostics.Count).IsGreaterThan(0);
		await Assert.That(diagnostics[0].GetMessage()).Contains("unknown-kind");
	}

	[Test]
	public async Task RunGenerator_WithPrivateNestedDefinitionsClass_DiscoverDefinitions(
		CancellationToken cancellationToken
	)
	{
		// Arrange — [LikeC4Definitions] class can be private and/or nested
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("MyTag", "my-tag")],
			callSites: [".WithTag(\"my-tag\")"],
			classAccessibility: "private"
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4001")).IsEmpty();
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — multiple [LikeC4Definitions] classes (ASPIREC4003)
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithMultipleDefinitionsClasses_EmitsMultipleDefinitionsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — two [LikeC4Definitions] classes in the same assembly
		var source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Definitions]
			class FirstDefinitions
			{
			    public static class Tags { public const string External = "external"; }
			}

			[LikeC4Definitions]
			class SecondDefinitions
			{
			    public static class Tags { public const string Internal = "internal"; }
			}
			""";

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostics = GetDiagnostics(result, "ASPIREC4003");
		await Assert.That(diagnostics.Count).IsGreaterThan(0);
	}

	[Test]
	public async Task RunGenerator_WithNoDefinitionsAndNoStrictMode_EmitsNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithCallSites(".WithTag(\"anything\")", ".WithKind(\"anything\")");

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert — no validation active when no definitions are present
		await Assert.That(GetDiagnostics(result, "ASPIREC4001")).IsEmpty();
		await Assert.That(GetDiagnostics(result, "ASPIREC4002")).IsEmpty();
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — group validation (ASPIREC4004)
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithDefinitionsClassGroupsAndDeclaredGroup_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			groupConstants: [("Frontend", "Frontend")],
			callSites: [".WithLikeC4Group(\"Frontend\")"]
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4004")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassGroupsAndUndeclaredGroup_EmitsUndeclaredGroupDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			groupConstants: [("Frontend", "Frontend")],
			callSites: [".WithLikeC4Group(\"Backend\")"]
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostics = GetDiagnostics(result, "ASPIREC4004");
		await Assert.That(diagnostics.Count).IsGreaterThan(0);
		await Assert.That(diagnostics[0].GetMessage()).Contains("Backend");
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassGroupsAndCaseMismatch_EmitsUndeclaredGroupDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — "frontend" (lowercase) ≠ "Frontend" declared → catches case-typo bugs
		var source = BuildSourceWithDefinitionsClass(
			groupConstants: [("Frontend", "Frontend")],
			callSites: [".WithLikeC4Group(\"frontend\")"]
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4004")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassWithoutGroupsNestedClass_EmitsNoDiagnosticForGroup(
		CancellationToken cancellationToken
	)
	{
		// Arrange — [LikeC4Definitions] exists but has no Groups nested class → no group validation
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("External", "external")],
			callSites: [".WithLikeC4Group(\"anything\")"]
		);

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert — group validation is opt-in via declaring a Groups nested class
		await Assert.That(GetDiagnostics(result, "ASPIREC4004")).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithNoDefinitionsClassAndGroupCallSite_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — no [LikeC4Definitions] at all
		var source = BuildSourceWithCallSites(".WithLikeC4Group(\"Frontend\")");

		// Act
		var result = RunGenerator(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(result, "ASPIREC4004")).IsEmpty();
	}

	// -----------------------------------------------------------------------
	// Helpers
	// -----------------------------------------------------------------------

	static LikeC4StrictValidatorGenerator CreateSut() => new LikeC4StrictValidatorGenerator();

	static GeneratorDriverRunResult RunGenerator(
		string source,
		TestAdditionalText[]? additionalFiles = null,
		bool strictMode = false,
		CancellationToken cancellationToken = default
	)
	{
		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
			GetMetadataReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		var generator = CreateSut();
		var additionalTexts = additionalFiles?.Cast<AdditionalText>().ToArray() ?? [];

		AnalyzerConfigOptionsProvider? optionsProvider = strictMode
			? new TestAnalyzerConfigOptionsProvider("build_property.AspireC4Strict", "true")
			: null;

		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			generators: [generator.AsSourceGenerator()],
			additionalTexts: additionalTexts.ToImmutableArray(),
			parseOptions: CSharpParseOptions.Default,
			optionsProvider: optionsProvider
		);

		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cancellationToken);
		return driver.GetRunResult();
	}

	static IEnumerable<MetadataReference> GetMetadataReferences() =>
		AppDomain
			.CurrentDomain.GetAssemblies()
			.Where(static a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
			.Select(static a => MetadataReference.CreateFromFile(a.Location));

	static string? GetGeneratedSource(GeneratorDriverRunResult result, string hintName) =>
		result
			.Results.SelectMany(static r => r.GeneratedSources)
			.Where(s => string.Equals(s.HintName, hintName, StringComparison.Ordinal))
			.Select(static s => s.SourceText.ToString())
			.SingleOrDefault();

	static IReadOnlyList<Diagnostic> GetDiagnostics(GeneratorDriverRunResult result, string diagnosticId) =>
		result.Diagnostics.Where(d => d.Id == diagnosticId).ToList();

	static string BuildSourceWithCallSites(params string[] invocations)
	{
		var body = string.Join(
			"\n        ",
			invocations.Select(static (inv, i) => $"var a{i} = new object(); a{i}{inv};")
		);

		return $$"""
			namespace TestApp;
			class Setup
			{
			    static void Configure()
			    {
			        {{body}}
			    }
			}
			""";
	}

	static string BuildSourceWithDefinitionsClass(
		(string Name, string Value)[]? tagsConstants = null,
		(string Name, string Value)[]? elementKindConstants = null,
		(string Name, string Value)[]? relationshipKindConstants = null,
		(string Name, string Value)[]? groupConstants = null,
		string[]? callSites = null,
		string classAccessibility = "internal"
	)
	{
		static string BuildNestedClass(string className, (string Name, string Value)[]? constants)
		{
			if (constants is null || constants.Length == 0)
				return string.Empty;

			var fields = string.Join(
				"\n            ",
				constants.Select(static c => $"public const string {c.Name} = \"{c.Value}\";")
			);
			return @$"""
			    public static class {className}
			        {{
			            {fields}
			        }}
			""";
		}

		var tagsClass = BuildNestedClass("Tags", tagsConstants);
		var elementKindsClass = BuildNestedClass("ElementKinds", elementKindConstants);
		var relationshipKindsClass = BuildNestedClass("RelationshipKinds", relationshipKindConstants);
		var groupsClass = BuildNestedClass("Groups", groupConstants);

		var body = callSites is null
			? string.Empty
			: string.Join("\n        ", callSites.Select(static (inv, i) => $"var a{i} = new object(); a{i}{inv};"));

		return $$"""
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Definitions]
			{{classAccessibility}} class MyDiagramDefinitions
			{
			{{tagsClass}}
			{{elementKindsClass}}
			{{relationshipKindsClass}}
			{{groupsClass}}
			}

			class Setup
			{
			    static void Configure()
			    {
			        {{body}}
			    }
			}
			""";
	}

	// -----------------------------------------------------------------------
	// Test infrastructure — AdditionalText and AnalyzerConfigOptionsProvider
	// -----------------------------------------------------------------------

	sealed class TestAdditionalText(string path, string content) : AdditionalText
	{
		public override string Path { get; } = path;

		public override SourceText? GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
	}

	sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
	{
		readonly TestAnalyzerConfigOptions _options;

		public TestAnalyzerConfigOptionsProvider(string key, string value)
		{
			_options = new TestAnalyzerConfigOptions(key, value);
		}

		public override AnalyzerConfigOptions GlobalOptions => _options;

		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
	}

	sealed class TestAnalyzerConfigOptions(string key, string value) : AnalyzerConfigOptions
	{
		public override bool TryGetValue(string requestedKey, out string? optionValue)
		{
			if (string.Equals(requestedKey, key, StringComparison.OrdinalIgnoreCase))
			{
				optionValue = value;
				return true;
			}

			optionValue = null;
			return false;
		}
	}
}
