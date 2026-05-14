using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspire.Hosting.AspireC4.LikeC4;

static partial class IconMatcher
{
	static LikeC4IconManifest? LoadManifest()
	{
		var assembly = typeof(IconMatcher).Assembly;
		const string resourceName = $"{AssemblyInfo.RootNamespace}.BuildResources.likec4-icons.json";
		using var stream = assembly.GetManifestResourceStream(resourceName);
		return stream is null
			? null
			: JsonSerializer.Deserialize(stream, IconMatcherJsonContext.Default.LikeC4IconManifest);
	}
}
