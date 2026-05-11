namespace Aspire.Hosting.AspireC4;

sealed class LikeC4ContainerWorkspaceOptions
{
	public string VolumeName { get; set; } = "";

	public string ContainerImageReference { get; set; } = "";

	public string ContainerRuntimeExecutable { get; set; } = "";

	public LikeC4HMRPortMode HMRPortMode { get; set; } = LikeC4HMRPortMode.FixedPort;

	/// <summary>
	/// When true, the host-side TCP relay listens on port <see cref="LikeC4ServerResource.DefaultContainerUpdatePort"/>
	/// and bridges incoming HMR connections to the dynamically-allocated Docker host port.
	/// Always true for FixedPort images; also true on Windows to avoid Hyper-V port reservation issues.
	/// </summary>
	public bool UseHMRRelay { get; set; }

	/// <summary>
	/// Absolute paths of additional DSL files that are covered by a container bind mount.
	/// The lifecycle hook skips volume-syncing these files to avoid duplicate definitions
	/// in the container (the bind mount already makes the files accessible).
	/// </summary>
#pragma warning disable IDE0028 // Simplify collection initialization
	public HashSet<string> BindMountedSourceFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028 // Simplify collection initialization
}
