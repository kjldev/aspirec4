namespace Aspire.Hosting.AspireC4;

enum LikeC4HMRPortMode
{
	FixedPort,
	Configurable,
}

static class LikeC4HmrPortCompatibility
{
	static Version? ConfigurableHmrPortMinimumVersion => new(1, 56, 0);

	internal static LikeC4HMRPortMode Resolve(string? loadedVersionTag) =>
		Resolve(loadedVersionTag, ConfigurableHmrPortMinimumVersion);

	internal static LikeC4HMRPortMode Resolve(string? loadedVersionTag, Version? configurableHmrPortMinimumVersion)
	{
		return configurableHmrPortMinimumVersion is null ? LikeC4HMRPortMode.FixedPort
			: TryParseVersion(loadedVersionTag, out var loadedVersion)
			&& loadedVersion >= configurableHmrPortMinimumVersion
				? LikeC4HMRPortMode.Configurable
			: LikeC4HMRPortMode.FixedPort;
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
