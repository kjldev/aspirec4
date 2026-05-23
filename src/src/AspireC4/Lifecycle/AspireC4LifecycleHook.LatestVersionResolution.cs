using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>
	/// When a <see cref="LikeC4VersionProbeResource"/> is present in the model, fires a
	/// background task that watches its log output for a version line, then updates
	/// <see cref="ContainerWorkspaceOptions.HMRPortMode"/> and completes the
	/// <see cref="AspireC4Resource.HMRPortModeTcs"/> so the server container's <c>WithArgs</c> callback can
	/// proceed. If no probe resource is present the TCS was already pre-completed at
	/// registration time — nothing to do.
	/// </summary>
	void TryUpdateHmrPortModeFromLatestVersionAsync(
		DistributedApplicationModel model,
		CancellationToken cancellationToken
	)
	{
		var probe = model.Resources.OfType<LikeC4VersionProbeResource>().FirstOrDefault();
		if (probe is null)
			return;

		_ = WatchProbeLogsAndCompleteAsync(probe, cancellationToken);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Version probe log watching is best-effort; any failure falls back to FixedPort mode gracefully"
	)]
	async Task WatchProbeLogsAndCompleteAsync(LikeC4VersionProbeResource probe, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var logBatch in resourceLoggerService.WatchAsync(probe).WithCancellation(cancellationToken))
			{
				foreach (var logLine in logBatch)
				{
					if (LatestVersionResolver.TryExtractVersion(logLine.Content, out var version))
					{
						telemetry.ResolvedLatestContainerVersion(version);
						_resolvedLikeC4Version = version;
						workspaceOptions.Value.HMRPortMode = HMRPortCompatibility.Resolve(version);
						hmrPortModeTcs.TrySetResult(workspaceOptions.Value.HMRPortMode);
						return;
					}
				}
			}

			// Probe exited without a recognisable version line — fall back to fixed port.
			telemetry.FailedToResolveLatestContainerVersion();
			hmrPortModeTcs.TrySetResult(HMRPortMode.FixedPort);
		}
		catch (OperationCanceledException)
		{
			hmrPortModeTcs.TrySetResult(HMRPortMode.FixedPort);
		}
		catch (Exception ex)
		{
			resourceLoggerService
				.GetLogger(probe)
				.LogWarning(ex, "Unexpected error while watching version probe logs; falling back to fixed HMR port.");
			hmrPortModeTcs.TrySetResult(HMRPortMode.FixedPort);
		}
	}

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
