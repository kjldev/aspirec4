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

	static readonly Lazy<LikeC4IconManifest?> Manifest = new(
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

	// Penalty multiplier applied when a query token is found inside an icon token but not at
	// the start (infix/suffix containment is weaker evidence than prefix containment).
	const float InfixPenalty = 0.75f;

	// Tokens that are never meaningful discriminators for icon matching (appear in .NET class
	// names, Docker image paths, or container tags but not in icon names).
	static readonly HashSet<string> QueryStopTokens = new(StringComparer.Ordinal)
	{
		"resource", // e.g. "AzureRedisResource" → strip "resource"
		"library", // e.g. "library/redis" Docker image namespace
		"latest", // e.g. ":latest" Docker image tag
		"app", // e.g. "NodeAppResource" → strip "app"
		"installer", // e.g. "node-app-installer" → installer is not part of the icon name
		"aspire", // e.g. "Aspire.Hosting.Azure.*" → namespace token, not an icon category
		"hosting", // e.g. "Aspire.Hosting.Azure.*" → namespace token, not an icon category
	};

	// Canonical form for tokens that normalise to an ambiguous short string.
	// ".NET"        → token "net"        → preferred canonical icon token "dotnet".
	// "JavaScript"  → tokens "java"+"script" (CamelCase split) merged to "javascript"
	//               → preferred canonical icon token "node" (avoids matching tech:java).
	static readonly Dictionary<string, string> TokenAliases = new(StringComparer.Ordinal)
	{
		["net"] = "dotnet",
		["javascript"] = "node",
	};

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
		var manifest = Manifest.Value;
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

				// Cloud icons have verbose, category-structured names (e.g. "azure-database-postgre-sql-server").
				// Using queryTokens.Length as the sole denominator prevents those extra categorical tokens
				// from burying the score below MinScore; within a cloud collection we pick the best
				// match anyway, so false positives across providers are not a concern.
				var (score, icon) = BestMatch(
					queryTokens,
					collectionIcons,
					collection,
					penalizeUnmatchedIconTokens: false
				);
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

	static (float Score, string Icon) BestMatch(
		string[] queryTokens,
		string[] iconNames,
		string prefix,
		bool penalizeUnmatchedIconTokens = true
	)
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

			// Penalise icons that have tokens matched by no query token.
			var unmatchedIconTokens = penalizeUnmatchedIconTokens
				? iconTokens.Count(it => queryTokens.All(qt => TokenSimilarity(qt, it) == 0f))
				: 0;

			// score = matched weight / (query size + unmatched icon tokens)
			var score = matchSum / (queryTokens.Length + unmatchedIconTokens);
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

		// Containment handles abbreviation/stem mismatches with length-proportional scoring:
		//   "node"     in "nodejs"      -> prefix match:  0.8 * 4/6        = 0.533
		//   "node"     in "linode"      -> infix match:   0.8 * 4/6 * 0.75 = 0.400
		//   "postgres" in "postgresql"  -> prefix match:  0.8 * 8/10       = 0.640
		//   "postgres" in "postgraphile"-> not prefix:    0.8 * 8/12 * 0.75= 0.400
		if (qt.Length >= MinContainmentLength && it.Contains(qt, StringComparison.Ordinal))
		{
			var ratio = (float)qt.Length / it.Length;
			return it.StartsWith(qt, StringComparison.Ordinal)
				? ContainmentMatchScore * ratio
				: ContainmentMatchScore * ratio * InfixPenalty;
		}

		if (it.Length >= MinContainmentLength && qt.Contains(it, StringComparison.Ordinal))
		{
			var ratio = (float)it.Length / qt.Length;
			return qt.StartsWith(it, StringComparison.Ordinal)
				? ContainmentMatchScore * ratio
				: ContainmentMatchScore * ratio * InfixPenalty;
		}

		return 0f;
	}

	// ── Tokenisation ────────────────────────────────────────────────────────────────────────────

	static string[] Tokenize(string? value)
	{
		var normalized = NormalizeForIconLookup(value);
		if (string.IsNullOrEmpty(normalized))
			return [];

		var rawTokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		// Merge "java" + "script" bigrams into "javascript" so that the class name
		// "JavaScriptInstallerResource" (Aspire.Hosting.JavaScript namespace) does not produce
		// a "java" token that exactly matches tech:java.  The "javascript" alias redirects to
		// "node", yielding tech:nodejs instead.
		return
		[
			.. MergeJavaScriptBigrams(rawTokens).Select(t => TokenAliases.TryGetValue(t, out var alias) ? alias : t),
		];
	}

	static IEnumerable<string> MergeJavaScriptBigrams(string[] tokens)
	{
		for (var i = 0; i < tokens.Length; i++)
		{
			if (tokens[i] == "java" && i + 1 < tokens.Length && tokens[i + 1] == "script")
			{
				yield return "javascript";
				i++; // skip "script"
			}
			else
			{
				yield return tokens[i];
			}
		}
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
		const string resourceName = $"{AssemblyInfo.RootNamespace}.Resources.likec4-icons.json";
		using var stream = assembly.GetManifestResourceStream(resourceName);
		return stream is null
			? null
			: JsonSerializer.Deserialize(stream, LikeC4IconMatcherJsonContext.Default.LikeC4IconManifest);
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
