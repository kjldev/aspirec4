using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>
	/// When the configured image tag is <c>"latest"</c> and
	/// <see cref="AspireC4DiagramOptions.CheckLatestImageVersion"/> is enabled, runs a throwaway
	/// container (<c>docker run --rm &lt;image&gt;:latest likec4 --version</c>) to discover the
	/// actual version pulled by the container runtime, then updates
	/// <see cref="ContainerWorkspaceOptions.HMRPortMode"/> accordingly.
	/// <para>
	/// This ensures version-gated features (such as configurable HMR port) work correctly even
	/// when the user has not pinned to a specific image tag. The update must happen before the
	/// <c>WithArgs</c> callback reads <c>HMRPortMode</c> at container start time.
	/// </para>
	/// </summary>
	async Task TryUpdateHmrPortModeFromLatestVersionAsync(CancellationToken cancellationToken)
	{
		var opts = options.Value;
		var effectiveTag = opts.ContainerImageTag ?? LikeC4ServerResource.DefaultTag;

		if (
			!opts.CheckLatestImageVersion
			|| !string.Equals(effectiveTag, LikeC4ServerResource.DefaultTag, StringComparison.OrdinalIgnoreCase)
		)
		{
			return;
		}

		var containerExe = GetContainerRuntimeExecutable();
		var imageRef = LikeC4ServerResource.GetImageReference(effectiveTag);

		var resolvedVersion = await LatestVersionResolver.TryResolveAsync(
			containerExe,
			imageRef,
			opts.ExternalProcessTimeoutSeconds,
			cancellationToken
		);

		if (resolvedVersion is null)
		{
			telemetry.FailedToResolveLatestContainerVersion();
			return;
		}

		telemetry.ResolvedLatestContainerVersion(resolvedVersion);
		_resolvedLikeC4Version = resolvedVersion;
		workspaceOptions.Value.HMRPortMode = HMRPortCompatibility.Resolve(resolvedVersion);
	}
}
