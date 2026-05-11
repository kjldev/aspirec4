using System.Text.Json;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Unit tests for <see cref="LikeC4ConfigGenerator"/>.
/// </summary>
public sealed class LikeC4ConfigGeneratorTests
{
	[Test]
	public async Task Generate_MinimalConfig_ContainsSchemaNameAndTitle()
	{
		var json = LikeC4ConfigGenerator.Generate(
			"my-project",
			"My Architecture",
			[],
			new Dictionary<string, string>()
		);

		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		await Assert.That(root.GetProperty("$schema").GetString()).IsEqualTo("https://likec4.dev/schemas/config.json");
		await Assert.That(root.GetProperty("name").GetString()).IsEqualTo("my-project");
		await Assert.That(root.GetProperty("title").GetString()).IsEqualTo("My Architecture");
	}

	[Test]
	public async Task Generate_NoIncludePaths_DoesNotEmitIncludeBlock()
	{
		var json = LikeC4ConfigGenerator.Generate("proj", "Title", [], new Dictionary<string, string>());

		using var doc = JsonDocument.Parse(json);
		await Assert.That(doc.RootElement.TryGetProperty("include", out _)).IsFalse();
	}

	[Test]
	public async Task Generate_WithIncludePaths_EmitsIncludeBlock()
	{
		var json = LikeC4ConfigGenerator.Generate(
			"proj",
			"Title",
			["../extra", "../other"],
			new Dictionary<string, string>()
		);

		using var doc = JsonDocument.Parse(json);
		var include = doc.RootElement.GetProperty("include");
		var paths = include.GetProperty("paths");

		await Assert.That(paths.GetArrayLength()).IsEqualTo(2);
		await Assert.That(paths[0].GetString()).IsEqualTo("../extra");
		await Assert.That(paths[1].GetString()).IsEqualTo("../other");
	}

	[Test]
	public async Task Generate_NoImageAliases_DoesNotEmitImageAliasesBlock()
	{
		var json = LikeC4ConfigGenerator.Generate("proj", "Title", [], new Dictionary<string, string>());

		using var doc = JsonDocument.Parse(json);
		await Assert.That(doc.RootElement.TryGetProperty("imageAliases", out _)).IsFalse();
	}

	[Test]
	public async Task Generate_WithImageAliases_EmitsImageAliasesBlock()
	{
		var aliases = new Dictionary<string, string> { ["@icons"] = "../assets/icons" };
		var json = LikeC4ConfigGenerator.Generate("proj", "Title", [], aliases);

		using var doc = JsonDocument.Parse(json);
		var imageAliases = doc.RootElement.GetProperty("imageAliases");

		await Assert.That(imageAliases.GetProperty("@icons").GetString()).IsEqualTo("../assets/icons");
	}

	[Test]
	public async Task Generate_FullConfig_IsValidJson()
	{
		var aliases = new Dictionary<string, string> { ["@icons"] = "../icons", ["@logos"] = "../logos" };
		var json = LikeC4ConfigGenerator.Generate("aspirec4", "Architecture", ["../ext/abc", "../ext/def"], aliases);

		// Should round-trip through JsonDocument without throwing.
		using var doc = JsonDocument.Parse(json);
		await Assert.That(doc.RootElement.GetProperty("name").GetString()).IsEqualTo("aspirec4");
	}
}
