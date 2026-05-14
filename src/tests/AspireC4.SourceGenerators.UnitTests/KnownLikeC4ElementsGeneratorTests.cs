using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

public sealed class KnownLikeC4ElementsGeneratorTests
{
	[Test]
	public async Task ToPascalCase_WithSingleWord_ReturnsCapitalized()
	{
		// Arrange
		const string name = "starting";

		// Act
		var result = KnownLikeC4ElementsGenerator.ToPascalCase(name);

		// Assert
		await Assert.That(result).IsEqualTo("Starting");
	}

	[Test]
	public async Task ToPascalCase_WithDashSeparated_ReturnsPascalCase()
	{
		// Arrange
		const string name = "aspire-run-state-starting";

		// Act
		var result = KnownLikeC4ElementsGenerator.ToPascalCase(name);

		// Assert
		await Assert.That(result).IsEqualTo("AspireRunStateStarting");
	}

	[Test]
	public async Task ToPascalCase_WithUnderscoreSeparated_ReturnsPascalCase()
	{
		// Arrange
		const string name = "runtime_unhealthy";

		// Act
		var result = KnownLikeC4ElementsGenerator.ToPascalCase(name);

		// Assert
		await Assert.That(result).IsEqualTo("RuntimeUnhealthy");
	}

	[Test]
	public async Task ToPascalCase_WithAlreadyPascalCase_ReturnsUnchanged()
	{
		// Arrange
		const string name = "Starting";

		// Act
		var result = KnownLikeC4ElementsGenerator.ToPascalCase(name);

		// Assert
		await Assert.That(result).IsEqualTo("Starting");
	}

	[Test]
	public async Task ToPascalCase_WithEmptyString_ReturnsEmptyString()
	{
		// Arrange
		const string name = "";

		// Act
		var result = KnownLikeC4ElementsGenerator.ToPascalCase(name);

		// Assert
		await Assert.That(result).IsEqualTo(string.Empty);
	}

	[Test]
	public async Task RunGenerator_WithTagRegistryClass_GeneratesTagsNestedClass()
	{
		// Arrange
		var source = CreateTagRegistrySource();

		// Act
		var (result, _) = RunGenerator(source);
		var generatedSource = GetGeneratedSource(result, "KnownLikeC4Elements.g.cs");

		// Assert
		await Assert.That(generatedSource).IsNotNull();
		await Assert.That(generatedSource!).Contains("public static class Tags");
		await Assert.That(generatedSource).Contains("public const string Starting = \"aspire-run-state-starting\";");
		await Assert
			.That(generatedSource)
			.Contains("public const string RuntimeUnhealthy = \"aspire-run-state-runtimeunhealthy\";");
	}

	[Test]
	public async Task RunGenerator_WithElementKindRegistryClass_GeneratesElementKindsNestedClass()
	{
		// Arrange
		var source = CreateElementKindRegistrySource();

		// Act
		var (result, _) = RunGenerator(source);
		var generatedSource = GetGeneratedSource(result, "KnownLikeC4Elements.g.cs");

		// Assert
		await Assert.That(generatedSource).IsNotNull();
		await Assert.That(generatedSource!).Contains("public static class ElementKinds");
		await Assert.That(generatedSource).Contains("public const string Component = \"component\";");
		await Assert.That(generatedSource).Contains("public const string DataBase = \"database\";");
	}

	[Test]
	public async Task RunGenerator_WithBothRegistryClasses_GeneratesBothNestedClasses()
	{
		// Arrange
		var source = CreateBothRegistrySource();

		// Act
		var (result, _) = RunGenerator(source);
		var generatedSource = GetGeneratedSource(result, "KnownLikeC4Elements.g.cs");

		// Assert
		await Assert.That(generatedSource).IsNotNull();
		await Assert.That(generatedSource!).Contains("public static class Tags");
		await Assert.That(generatedSource).Contains("public static class ElementKinds");
	}

	[Test]
	public async Task RunGenerator_WithEmptyRegistryClass_GeneratesEmptyNestedClass()
	{
		// Arrange
		var source = CreateEmptyTagRegistrySource();

		// Act
		var (result, _) = RunGenerator(source);
		var generatedSource = GetGeneratedSource(result, "KnownLikeC4Elements.g.cs");

		// Assert
		await Assert.That(generatedSource is null).IsTrue();
	}

	[Test]
	public async Task RunGenerator_AlwaysEmitsAttributeSource()
	{
		// Arrange
		const string source = "namespace TestAssembly;";

		// Act
		var (result, _) = RunGenerator(source);
		var generatedSource = GetGeneratedSource(result, "KnownLikeC4Attributes.g.cs");

		// Assert
		await Assert.That(generatedSource).IsNotNull();
		await Assert.That(generatedSource!).Contains("KnownLikeC4TagRegistryAttribute");
		await Assert.That(generatedSource).Contains("KnownLikeC4ElementKindRegistryAttribute");
	}

	private static KnownLikeC4ElementsGenerator CreateSut()
	{
		return new KnownLikeC4ElementsGenerator();
	}

	private static (GeneratorDriverRunResult Result, Compilation OutputCompilation) RunGenerator(string source)
	{
		var inputCompilation = CreateCompilation(source);
		var generator = CreateSut();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out _);
		return (driver.GetRunResult(), outputCompilation);
	}

	private static Compilation CreateCompilation(string source)
	{
		return CSharpCompilation.Create(
			assemblyName: "TestAssembly",
			syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
			references: CreateMetadataReferences(),
			options: CreateCompilationOptions()
		);
	}

	private static CSharpCompilationOptions CreateCompilationOptions()
	{
		return new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
	}

	private static IEnumerable<MetadataReference> CreateMetadataReferences()
	{
		return AppDomain
			.CurrentDomain.GetAssemblies()
			.Where(static assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
			.Select(static assembly => MetadataReference.CreateFromFile(assembly.Location));
	}

	private static string? GetGeneratedSource(GeneratorDriverRunResult result, string hintName)
	{
		return result
			.Results.SelectMany(static generatorResult => generatorResult.GeneratedSources)
			.Where(generatedSource => string.Equals(generatedSource.HintName, hintName, StringComparison.Ordinal))
			.Select(static generatedSource => generatedSource.SourceText.ToString())
			.SingleOrDefault();
	}

	private static string CreateTagRegistrySource()
	{
		return CreateSource(
			"""
			[KnownLikeC4TagRegistry]
			internal static class TestTags
			{
			    public const string starting = "aspire-run-state-starting";
			    public const string runtime_unhealthy = "aspire-run-state-runtimeunhealthy";
			}
			"""
		);
	}

	private static string CreateElementKindRegistrySource()
	{
		return CreateSource(
			"""
			[KnownLikeC4ElementKindRegistry]
			internal static class TestElementKinds
			{
			    public const string COMPONENT = "component";
			    public const string data_base = "database";
			}
			"""
		);
	}

	private static string CreateBothRegistrySource()
	{
		return CreateSource(
			"""
			[KnownLikeC4TagRegistry]
			internal static class TestTags
			{
			    public const string starting = "aspire-run-state-starting";
			}

			[KnownLikeC4ElementKindRegistry]
			internal static class TestElementKinds
			{
			    public const string COMPONENT = "component";
			}
			"""
		);
	}

	private static string CreateEmptyTagRegistrySource()
	{
		return CreateSource(
			"""
			[KnownLikeC4TagRegistry]
			internal static class TestTags
			{
			}
			"""
		);
	}

	private static string CreateSource(string registryDeclarations)
	{
		return """
				namespace Aspire.Hosting.AspireC4.LikeC4.Models;

				[System.AttributeUsage(System.AttributeTargets.Class)]
				internal sealed class KnownLikeC4TagRegistryAttribute : System.Attribute { }

				[System.AttributeUsage(System.AttributeTargets.Class)]
				internal sealed class KnownLikeC4ElementKindRegistryAttribute : System.Attribute { }

				""" + registryDeclarations;
	}
}
