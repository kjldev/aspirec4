using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.ApplicationModel;

/// <summary>
/// An Aspire container resource that runs the LikeC4 live server via the official
/// <c>ghcr.io/likec4/likec4</c> Docker image, providing a hot-reloading interactive
/// architecture diagram.
/// </summary>
/// <remarks>
/// This is an implementation detail of <see cref="AspireC4Resource"/>. To switch to a local
/// Node.js CLI instead, call <c>.WithLocalCLI()</c> on the
/// <c>IResourceBuilder&lt;AspireC4Resource&gt;</c> returned by <c>AddAspireC4()</c>.
/// </remarks>
public sealed class LikeC4ServerResource : ContainerResource
{
	/// <summary>The container registry hosting the LikeC4 image.</summary>
	public const string DefaultRegistry = "ghcr.io";

	/// <summary>The container image name (without registry prefix).</summary>
	public const string DefaultImage = "likec4/likec4";

	/// <summary>
	/// The default image tag used when <see cref="AspireC4DiagramOptions.ContainerImageTag"/> is not set.
	/// </summary>
	/// <remarks>To use the dynamic HMR ports, make sure the minimum version is 1.57.</remarks>
	public const string DefaultTag = "latest";

	/// <summary>
	/// Root path inside the container where the host directory tree is bind-mounted.
	/// <c>likec4 start</c> is pointed at a subdirectory of this root that corresponds to the
	/// output directory on the host; the full path is computed at startup and stored in
	/// <see cref="ContainerWorkspaceOptions.ContainerServePath"/>.
	/// </summary>
	internal const string WorkspacePath = "/data";

	internal static string GetImageReference(string imageTag) => $"{DefaultRegistry}/{DefaultImage}:{imageTag}";

	internal LikeC4ServerResource(string name)
		: base(name) { }
}
