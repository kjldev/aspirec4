using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.Lifecycle;

namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

sealed class ContainerWorkspaceOptions
{
	public HMRPortMode HMRPortMode { get; set; } = HMRPortMode.FixedPort;

	/// <summary>
	/// The resolved HMR port to use for the LikeC4 server, both as the container target port
	/// and as the value passed via <c>--hmr-port</c> in Configurable mode.
	/// Defaults to <see cref="LikeC4ServerResource.DefaultContainerHMRPort"/> (24678).
	/// Set during <c>AddAspireC4</c> from <c>AspireC4DiagramOptions.HMRPort</c> if configured.
	/// </summary>
	public int ResolvedHMRPort { get; set; } = LikeC4ServerResource.DefaultContainerHMRPort;

	/// <summary>
	/// The resolved local CLI runtime when <c>WithLocalCLI()</c> was called.
	/// <see langword="null"/> means Docker container mode — host-side CLI invocations
	/// (format, validate) fall back to <c>npx</c>.
	/// </summary>
	public LocalCLIRuntime? LocalCLIRuntime { get; set; }

	/// <summary>
	/// The absolute path inside the container at which <c>likec4 start</c> is pointed.
	/// Equals <c><see cref="LikeC4ServerResource.WorkspacePath"/>/{relative-output-dir}</c>
	/// and is set by <see cref="AspireC4LifecycleHook"/> during <c>BeforeStartEvent</c>.
	/// </summary>
	public string ContainerServePath { get; set; } = LikeC4ServerResource.WorkspacePath;
}
