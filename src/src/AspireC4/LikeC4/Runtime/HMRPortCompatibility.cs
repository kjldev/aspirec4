namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

static class HMRPortCompatibility
{
	// Hopefully the PR will be in for 1.57.0, but I can't tell so I've set the number really
	// high for now to avoid accidentally enabling it before it's actually available.
	static Version? ConfigurableHmrPortMinimumVersion => new(100, 57, 0);

	internal static HMRPortMode Resolve(string? loadedVersionTag) =>
		Resolve(loadedVersionTag, ConfigurableHmrPortMinimumVersion);

	internal static HMRPortMode Resolve(string? loadedVersionTag, Version? configurableHmrPortMinimumVersion)
	{
		return configurableHmrPortMinimumVersion is null ? HMRPortMode.FixedPort
			: TryParseVersion(loadedVersionTag, out var loadedVersion)
			&& loadedVersion >= configurableHmrPortMinimumVersion
				? HMRPortMode.Configurable
			: HMRPortMode.FixedPort;
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
