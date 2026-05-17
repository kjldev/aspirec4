using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

public sealed class KnownLikeC4RegistryGeneratorTests
{
	[Test]
	public async Task ToPascalCase_WithSingleWord_ReturnsCapitalized()
	{
		// Arrange
		const string input = "starting";

		// Act
		var result = KnownLikeC4RegistryGenerator.ToPascalCase(input);

		// Assert
		await Assert.That(result).IsEqualTo("Starting");
	}

	[Test]
	public async Task ToPascalCase_WithDashSeparated_ReturnsPascalCase()
	{
		// Arrange
		const string input = "aspire-run-state-starting";

		// Act
		var result = KnownLikeC4RegistryGenerator.ToPascalCase(input);

		// Assert
		await Assert.That(result).IsEqualTo("AspireRunStateStarting");
	}

	[Test]
	public async Task ToPascalCase_WithUnderscoreSeparated_ReturnsPascalCase()
	{
		// Arrange
		const string input = "runtime_unhealthy";

		// Act
		var result = KnownLikeC4RegistryGenerator.ToPascalCase(input);

		// Assert
		await Assert.That(result).IsEqualTo("RuntimeUnhealthy");
	}

	[Test]
	public async Task ToPascalCase_WithAlreadyPascalCase_ReturnsUnchanged()
	{
		// Arrange
		const string input = "Starting";

		// Act
		var result = KnownLikeC4RegistryGenerator.ToPascalCase(input);

		// Assert
		await Assert.That(result).IsEqualTo("Starting");
	}

	[Test]
	public async Task ToPascalCase_WithEmptyString_ReturnsEmpty()
	{
		// Arrange
		const string input = "";

		// Act
		var result = KnownLikeC4RegistryGenerator.ToPascalCase(input);

		// Assert
		await Assert.That(result).IsEqualTo(string.Empty);
	}

	[Test]
	public async Task RunGenerator_WithWithTagCallSite_GeneratesTagsNestedClass(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithTag(\"external\")");

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public static class Tags");
		await Assert.That(output).Contains("public const string External = \"external\";");
	}

	[Test]
	public async Task RunGenerator_WithWithKindCallSite_GeneratesElementKindsNestedClass(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithKind(\"async\")");

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public static class ElementKinds");
		await Assert.That(output).Contains("public const string Async = \"async\";");
	}

	[Test]
	public async Task RunGenerator_WithConstReference_FollowsConstToExtractValue(CancellationToken cancellationToken)
	{
		// Arrange
		var source = """
			namespace TestApp;
			class Annotation
			{
				public Annotation WithTag(string t) => this;
			}
			class Setup
			{
				public const string StateTag = "aspire-run-state-starting";
				static void Configure(Annotation a) => a.WithTag(StateTag);
			}
			""";

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert
			.That(output!)
			.Contains("public const string AspireRunStateStarting = \"aspire-run-state-starting\";");
	}

	[Test]
	public async Task RunGenerator_WithDuplicateCallSites_DeduplicatesValues(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithTag(\"external\")", ".WithTag(\"external\")");

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		var count = CountOccurrences(output!, "External = \"external\"");
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task RunGenerator_WithNoCallSites_ProducesNoOutput(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = "namespace TestApp;";

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNull();
	}

	[Test]
	public async Task RunGenerator_WithBothTagAndKindCallSites_GeneratesBothNestedClasses(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithTag(\"external\")", ".WithKind(\"async\")");

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public static class Tags");
		await Assert.That(output).Contains("public static class ElementKinds");
	}

	[Test]
	public async Task RunGenerator_WithWithLikeC4GroupCallSite_GeneratesGroupsNestedClass(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithLikeC4Group(\"Frontend\")");

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public static class Groups");
		await Assert.That(output).Contains("public const string Frontend = \"Frontend\";");
	}

	[Test]
	public async Task RunGenerator_WithMultiWordGroupName_GeneratesPascalCasedConstant(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithLikeC4Group(\"local-dev-services\")");

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public const string LocalDevServices = \"local-dev-services\";");
	}

	[Test]
	public async Task RunGenerator_WithDuplicateGroupCallSites_DeduplicatesValues(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithLikeC4Group(\"Frontend\")", ".WithLikeC4Group(\"Frontend\")");

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		var count = CountOccurrences(output!, "Frontend = \"Frontend\"");
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task RunGenerator_WithGroupCallSiteOnly_ProducesOutputWithGroupsClass(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithLikeC4Group(\"Backend\")");

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public static class Groups");
	}

	[Test]
	public async Task RunGenerator_WithAllThreeCallSiteTypes_GeneratesAllThreeNestedClasses(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateSourceWithCallSites(
			".WithTag(\"external\")",
			".WithKind(\"async\")",
			".WithLikeC4Group(\"Frontend\")"
		);

		// Act
		var result = RunGenerator(source, cancellationToken);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public static class Tags");
		await Assert.That(output).Contains("public static class ElementKinds");
		await Assert.That(output).Contains("public static class Groups");
	}

	static KnownLikeC4RegistryGenerator CreateSut() => new();

	static GeneratorDriverRunResult RunGenerator(string source, CancellationToken cancellationToken)
	{
		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
			GetMetadataReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		var generator = CreateSut();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cancellationToken);

		return driver.GetRunResult();
	}

	static IEnumerable<MetadataReference> GetMetadataReferences()
	{
		return AppDomain
			.CurrentDomain.GetAssemblies()
			.Where(static a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
			.Select(static a => MetadataReference.CreateFromFile(a.Location));
	}

	static string? GetGeneratedSource(GeneratorDriverRunResult result, string hintName)
	{
		return result
			.Results.SelectMany(static r => r.GeneratedSources)
			.Where(s => string.Equals(s.HintName, hintName, StringComparison.Ordinal))
			.Select(static s => s.SourceText.ToString())
			.SingleOrDefault();
	}

	static string CreateSourceWithCallSites(params string[] invocations)
	{
		var body = string.Join("\n        ", invocations.Select((inv, i) => $"var a{i} = new object(); a{i}{inv};"));

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

	static int CountOccurrences(string text, string pattern)
	{
		int count = 0,
			index = 0;
		while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += pattern.Length;
		}

		return count;
	}
}
