using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>
	/// When running in local CLI mode (<c>WithLocalCLI()</c>), resolves the exact installed
	/// LikeC4 version by running <c>&lt;command&gt; [prefix…] --version</c> via the package
	/// manager. The version is always determinable for local CLI installations because the
	/// package manager resolves it from the local or cached package metadata.
	/// </summary>
	async Task TryResolveLocalCLIVersionAsync(CancellationToken cancellationToken)
	{
		var runtime = workspaceOptions.Value.LocalCLIRuntime;
		if (runtime is null)
			return;

		var (command, prefix) = AspireC4Builder.BuildLikeC4CLIPrefix(runtime.Value);

		var resolvedVersion = await LatestVersionResolver.TryResolveFromLocalCLIAsync(
			command,
			prefix,
			options.Value.ExternalProcessTimeoutSeconds,
			cancellationToken
		);

		if (resolvedVersion is null)
		{
			telemetry.FailedToResolveLatestContainerVersion();
			return;
		}

		telemetry.ResolvedLatestContainerVersion(resolvedVersion);
		_resolvedLikeC4Version = resolvedVersion;
	}
}

