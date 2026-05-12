namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Identifies the container runtime in use on the host.
/// </summary>
/// <remarks>
/// Used by <see cref="AspireC4Builder.NormalizeBindMountPath"/> to format bind-mount
/// source paths correctly for the daemon that will receive them.  Each runtime has
/// different expectations for how Windows paths are expressed.
/// </remarks>
enum ContainerRuntime
{
	/// <summary>
	/// Any Linux-native runtime (Docker or Podman running directly on Linux or macOS).
	/// Bind-mount paths are already in Linux format — no conversion needed.
	/// </summary>
	Linux,

	/// <summary>
	/// Docker Desktop for Windows (official).
	/// Its Windows-native daemon understands <c>C:\…</c> paths and maps them into
	/// the VM automatically — no conversion needed from AspireC4.
	/// </summary>
	DockerDesktop,

	/// <summary>
	/// Rancher Desktop on Windows.
	/// Runs a standard Linux <c>dockerd</c> inside WSL2 that cannot resolve
	/// Windows paths.  Windows drives are mounted at <c>/mnt/&lt;drive&gt;/…</c>
	/// inside the WSL2 distribution, so AspireC4 converts paths to that format.
	/// </summary>
	RancherDesktop,

	/// <summary>
	/// Podman on Windows (activated via <c>ASPIRE_CONTAINER_RUNTIME=podman</c>).
	/// <c>podman.exe</c> translates Windows paths to <c>/mnt/&lt;drive&gt;/…</c>
	/// internally via its own <c>ConvertWinMountPath</c> logic before sending them
	/// to the Podman Machine VM — no conversion needed from AspireC4.
	/// </summary>
	Podman,
}
