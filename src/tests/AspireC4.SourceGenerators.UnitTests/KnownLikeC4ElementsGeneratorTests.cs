using System;
using System.Collections.Generic;
using System.Linq;
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
	public async Task RunGenerator_WithWithTagCallSite_GeneratesTagsNestedClass()
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithTag(\"external\")");

		// Act
		var result = RunGenerator(source);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public static class Tags");
		await Assert.That(output).Contains("public const string External = \"external\";");
	}

	[Test]
	public async Task RunGenerator_WithWithKindCallSite_GeneratesElementKindsNestedClass()
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithKind(\"async\")");

		// Act
		var result = RunGenerator(source);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public static class ElementKinds");
		await Assert.That(output).Contains("public const string Async = \"async\";");
	}

	[Test]
	public async Task RunGenerator_WithConstReference_FollowsConstToExtractValue()
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
		var result = RunGenerator(source);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert
			.That(output!)
			.Contains("public const string AspireRunStateStarting = \"aspire-run-state-starting\";");
	}

	[Test]
	public async Task RunGenerator_WithDuplicateCallSites_DeduplicatesValues()
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithTag(\"external\")", ".WithTag(\"external\")");

		// Act
		var result = RunGenerator(source);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		var count = CountOccurrences(output!, "External = \"external\"");
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task RunGenerator_WithNoCallSites_ProducesNoOutput()
	{
		// Arrange
		const string source = "namespace TestApp;";

		// Act
		var result = RunGenerator(source);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNull();
	}

	[Test]
	public async Task RunGenerator_WithBothTagAndKindCallSites_GeneratesBothNestedClasses()
	{
		// Arrange
		var source = CreateSourceWithCallSites(".WithTag(\"external\")", ".WithKind(\"async\")");

		// Act
		var result = RunGenerator(source);
		var output = GetGeneratedSource(result, "KnownLikeC4Registry.g.cs");

		// Assert
		await Assert.That(output).IsNotNull();
		await Assert.That(output!).Contains("public static class Tags");
		await Assert.That(output).Contains("public static class ElementKinds");
	}

	static KnownLikeC4RegistryGenerator CreateSut()
	{
		return new KnownLikeC4RegistryGenerator();
	}

	static GeneratorDriverRunResult RunGenerator(string source)
	{
		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[CSharpSyntaxTree.ParseText(source)],
			GetMetadataReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		var generator = CreateSut();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

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
