using System.Text.Json.Serialization;

namespace Aspire.Hosting.AspireC4.LikeC4.Icons;

[JsonSerializable(typeof(LikeC4IconManifest))]
sealed partial class IconMatcherJsonContext : JsonSerializerContext { }

sealed class LikeC4IconManifest
{
	[JsonPropertyName("generatedAt")]
	public string GeneratedAt { get; init; } = "";

	[JsonPropertyName("icons")]
	public Dictionary<string, string[]> Icons { get; init; } = [];
}
