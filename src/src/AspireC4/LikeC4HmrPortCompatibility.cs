namespace Aspire.Hosting.AspireC4;

enum LikeC4HmrPortMode
{
	FixedPort,
	Configurable,
}

static class LikeC4HmrPortCompatibility
{
	static Version? ConfigurableHmrPortMinimumVersion => new(1, 56, 0);

	internal static LikeC4HmrPortMode Resolve(string? loadedVersionTag) =>
		Resolve(loadedVersionTag, ConfigurableHmrPortMinimumVersion);

	internal static LikeC4HmrPortMode Resolve(string? loadedVersionTag, Version? configurableHmrPortMinimumVersion)
	{
		if (configurableHmrPortMinimumVersion is null)
		{
			return LikeC4HmrPortMode.FixedPort;
		}

		return
			TryParseVersion(loadedVersionTag, out var loadedVersion)
			&& loadedVersion >= configurableHmrPortMinimumVersion
			? LikeC4HmrPortMode.Configurable
			: LikeC4HmrPortMode.FixedPort;
	}

	internal static bool TryParseVersion(string? loadedVersionTag, out Version loadedVersion)
	{
		loadedVersion = default!;

		if (string.IsNullOrWhiteSpace(loadedVersionTag))
		{
			return false;
		}

		var normalized = loadedVersionTag.Trim();
		if (normalized.StartsWith('v'))
		{
			normalized = normalized[1..];
		}

		var suffixIndex = normalized.IndexOfAny(['-', '+']);
		if (suffixIndex >= 0)
		{
			normalized = normalized[..suffixIndex];
		}

		if (!Version.TryParse(normalized, out var parsedVersion) || parsedVersion is null)
		{
			return false;
		}

		loadedVersion = parsedVersion;
		return true;
	}
}
