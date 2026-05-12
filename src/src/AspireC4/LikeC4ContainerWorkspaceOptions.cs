namespace Aspire.Hosting.AspireC4;

sealed class LikeC4ContainerWorkspaceOptions
{
	public LikeC4HMRPortMode HMRPortMode { get; set; } = LikeC4HMRPortMode.FixedPort;

	/// <summary>
	/// When true, the host-side TCP relay listens on port <see cref="LikeC4ServerResource.DefaultContainerUpdatePort"/>
	/// and bridges incoming HMR connections to the dynamically-allocated Docker host port.
	/// Always true for FixedPort images; also true on Windows to avoid Hyper-V port reservation issues.
	/// </summary>
	public bool UseHMRRelay { get; set; }

	/// <summary>
	/// The resolved local CLI runtime when <c>WithLocalCLI()</c> was called.
	/// <see langword="null"/> means Docker container mode — host-side CLI invocations
	/// (format, validate) fall back to <c>npx</c>.
	/// </summary>
	public LikeC4LocalCLIRuntime? LocalCLIRuntime { get; set; }

	/// <summary>
	/// The absolute path inside the container at which <c>likec4 start</c> is pointed.
	/// Equals <c><see cref="LikeC4ServerResource.WorkspacePath"/>/{relative-output-dir}</c>
	/// and is set by <see cref="AspireC4LifecycleHook"/> during <c>BeforeStartEvent</c>.
	/// </summary>
	public string ContainerServePath { get; set; } = LikeC4ServerResource.WorkspacePath;
}
