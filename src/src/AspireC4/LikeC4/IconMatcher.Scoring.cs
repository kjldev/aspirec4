namespace Aspire.Hosting.AspireC4.LikeC4;

static partial class IconMatcher
{
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

			// Short query tokens (len < MinContainmentLength) cannot score via containment, so
			// they would only inflate the denominator without contributing.  Using the count of
			// "effective" tokens (those long enough to participate in containment) prevents this
			// while still letting short tokens score via exact match (e.g. "go" -> tech:go).
			var effectiveQueryLength = Math.Max(1, queryTokens.Count(t => t.Length >= MinContainmentLength));

			// score = matched weight / (effective query size + unmatched icon tokens)
			var score = matchSum / (effectiveQueryLength + unmatchedIconTokens);
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
}
