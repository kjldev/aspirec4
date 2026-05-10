using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Matches resource names and types to LikeC4 icon IDs using token-overlap scoring
/// against the bundled icon manifest generated from the LikeC4 GitHub repository.
/// </summary>
/// <remarks>
/// The manifest is loaded once from embedded resources and cached for the lifetime
/// of the process. Refresh it by running <c>node scripts/generate-icon-manifest.mjs</c>
/// (or <c>just refresh-icons</c>) and rebuilding the project.
/// </remarks>
static class LikeC4IconMatcher
{
	const float MinScore = 0.35f;
	const float ExactMatchScore = 1.0f;
	const float ContainmentMatchScore = 0.8f;
	const int MinContainmentLength = 3;

	static readonly Lazy<LikeC4IconManifest?> _manifest = new(
		LoadManifest,
		LazyThreadSafetyMode.ExecutionAndPublication
	);

	// Cloud collection names and the keyword tokens that identify a resource as belonging there.
	static readonly (string Collection, HashSet<string> Markers)[] CloudCollections =
	[
		("azure", new HashSet<string>(StringComparer.Ordinal) { "azure" }),
		("aws", new HashSet<string>(StringComparer.Ordinal) { "aws", "amazon" }),
		("gcp", new HashSet<string>(StringComparer.Ordinal) { "gcp", "google" }),
	];

	// Tokens that are never meaningful discriminators for icon matching (appear in .NET class
	// names but not in icon names).
	static readonly HashSet<string> QueryStopTokens = new(StringComparer.Ordinal) { "resource" };

	// Canonical form for tokens that normalise to an ambiguous short string.
	// ".NET" → token "net" → preferred canonical icon token "dotnet".
	static readonly Dictionary<string, string> TokenAliases = new(StringComparer.Ordinal) { ["net"] = "dotnet" };

	/// <summary>
	/// Tries to infer a LikeC4 icon ID from the supplied string candidates.
	/// </summary>
	/// <param name="candidates">
	/// Strings to score, in priority order (technology label, inferred technology, resource type,
	/// class name, resource name). <see langword="null"/> entries are silently skipped.
	/// </param>
	/// <returns>
	/// A LikeC4 icon string such as <c>tech:redis</c> or <c>azure:azure-managed-redis</c>,
	/// or <see langword="null"/> if no confident match was found.
	/// </returns>
	public static string? TryInferIcon(string?[] candidates)
	{
		var manifest = _manifest.Value;
		if (manifest is null)
		{
			return null;
		}

		var allCloudMarkers = CloudCollections.SelectMany(static c => c.Markers).ToHashSet(StringComparer.Ordinal);

		// Phase 1: cloud collections — try every candidate that carries a cloud marker.
		// Cloud matching runs first so a resource named "azure-redis" beats a plain tech match.
		foreach (var (collection, markers) in CloudCollections)
		{
			if (!manifest.Icons.TryGetValue(collection, out var collectionIcons))
			{
				continue;
			}

			foreach (var candidate in candidates)
			{
				var tokens = Tokenize(candidate);
				if (tokens.Length == 0)
				{
					continue;
				}

				// Skip candidates that don't contain any marker for this collection.
				if (!tokens.Any(t => markers.Contains(t)))
				{
					continue;
				}

				// Remove cloud markers and generic stop-tokens from the query.
				var queryTokens = tokens.Where(t => !markers.Contains(t) && !QueryStopTokens.Contains(t)).ToArray();

				if (queryTokens.Length == 0)
				{
					continue;
				}

				var (score, icon) = BestMatch(queryTokens, collectionIcons, collection);
				if (score >= MinScore)
				{
					return icon;
				}
			}
		}

		// Phase 2: tech collection — try candidates that contain no cloud markers.
		if (!manifest.Icons.TryGetValue("tech", out var techIcons))
		{
			return null;
		}

		foreach (var candidate in candidates)
		{
			var tokens = Tokenize(candidate);
			if (tokens.Length == 0)
			{
				continue;
			}

			// Skip candidates already handled by the cloud phase.
			if (tokens.Any(t => allCloudMarkers.Contains(t)))
			{
				continue;
			}

			var queryTokens = tokens.Where(t => !QueryStopTokens.Contains(t)).ToArray();
			if (queryTokens.Length == 0)
			{
				continue;
			}

			var (score, icon) = BestMatch(queryTokens, techIcons, "tech");
			if (score >= MinScore)
			{
				return icon;
			}
		}

		return null;
	}

	// ── Scoring ─────────────────────────────────────────────────────────────────────────────────

	static (float Score, string Icon) BestMatch(string[] queryTokens, string[] iconNames, string prefix)
	{
		var bestScore = 0f;
		var bestIcon = string.Empty;

		foreach (var iconName in iconNames)
		{
			// Tokenise the icon (kebab-case), stripping the collection prefix token if present.
			var iconTokens = iconName.Split('-').Where(t => t != prefix && t.Length > 0).ToArray();

			if (iconTokens.Length == 0)
			{
				continue;
			}

			// For each query token, find its best-matching icon token and accumulate scores.
			var matchSum = 0f;
			foreach (var qt in queryTokens)
			{
				var best = 0f;
				foreach (var it in iconTokens)
				{
					var s = TokenSimilarity(qt, it);
					if (s > best)
					{
						best = s;
					}
				}

				matchSum += best;
			}

			// Jaccard-style score: matched weight / union size.
			var score = matchSum / Math.Max(queryTokens.Length, iconTokens.Length);
			if (score > bestScore)
			{
				bestScore = score;
				bestIcon = $"{prefix}:{iconName}";
			}
		}

		return (bestScore, bestIcon);
	}

	static float TokenSimilarity(string qt, string it)
	{
		if (string.Equals(qt, it, StringComparison.Ordinal))
		{
			return ExactMatchScore;
		}

		// Containment handles common abbreviation/stem mismatches:
		//   "node"     ↔ "nodejs"      (query contained in icon)
		//   "postgres" ↔ "postgresql"  (query contained in icon)
		//   "net"      ↔ "dotnet"      (query contained in icon)
		if (qt.Length >= MinContainmentLength && it.Contains(qt, StringComparison.Ordinal))
		{
			return ContainmentMatchScore;
		}

		if (it.Length >= MinContainmentLength && qt.Contains(it, StringComparison.Ordinal))
		{
			return ContainmentMatchScore;
		}

		return 0f;
	}

	// ── Tokenisation ────────────────────────────────────────────────────────────────────────────

	static string[] Tokenize(string? value)
	{
		var normalized = NormalizeForIconLookup(value);
		if (string.IsNullOrEmpty(normalized))
		{
			return [];
		}

		return normalized
			.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Select(t => TokenAliases.TryGetValue(t, out var alias) ? alias : t)
			.ToArray();
	}

	// Splits PascalCase / kebab-case / dotted strings into lowercase space-delimited tokens.
	// E.g. "AzureRedisCacheResource" → "azure redis cache resource"
	//      "Azure.Redis"             → "azure redis"
	//      "library/postgres:latest" → "library postgres latest"
	static string NormalizeForIconLookup(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		var sb = new System.Text.StringBuilder(value.Length * 2);
		var previousWasSeparator = true;

		for (var i = 0; i < value.Length; i++)
		{
			var current = value[i];

			if (
				i > 0
				&& char.IsUpper(current)
				&& (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]))
				&& !previousWasSeparator
			)
			{
				sb.Append(' ');
				previousWasSeparator = true;
			}

			if (char.IsLetterOrDigit(current))
			{
				sb.Append(char.ToLowerInvariant(current));
				previousWasSeparator = false;
				continue;
			}

			if (!previousWasSeparator)
			{
				sb.Append(' ');
				previousWasSeparator = true;
			}
		}

		return sb.ToString().Trim();
	}

	// ── Manifest loading ────────────────────────────────────────────────────────────────────────

	static LikeC4IconManifest? LoadManifest()
	{
		var assembly = typeof(LikeC4IconMatcher).Assembly;
		const string ResourceName = "Aspire.Hosting.AspireC4.Resources.likec4-icons.json";
		using var stream = assembly.GetManifestResourceStream(ResourceName);
		if (stream is null)
		{
			return null;
		}

		return JsonSerializer.Deserialize(stream, LikeC4IconMatcherJsonContext.Default.LikeC4IconManifest);
	}
}

[JsonSerializable(typeof(LikeC4IconManifest))]
partial class LikeC4IconMatcherJsonContext : JsonSerializerContext { }

sealed class LikeC4IconManifest
{
	[JsonPropertyName("generatedAt")]
	public string GeneratedAt { get; init; } = "";

	[JsonPropertyName("icons")]
	public Dictionary<string, string[]> Icons { get; init; } = [];
}
