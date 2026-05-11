using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// An Aspire container resource that runs the LikeC4 live server via the official
/// <c>ghcr.io/likec4/likec4</c> Docker image, providing a hot-reloading interactive
/// architecture diagram.
/// </summary>
/// <remarks>
/// To use a local Node.js CLI instead of Docker, call
/// <see cref="IAspireC4Builder.WithLocalCLI"/> on the returned builder.
/// </remarks>
public sealed class LikeC4ServerResource : ContainerResource
{
	/// <summary>The name of the HTTP endpoint exposed by the LikeC4 server.</summary>
	public const string HttpEndpointName = "http";

	/// <summary>The name of the HTTP endpoint used by LikeC4's Vite HMR channel.</summary>
	public const string HmrEndpointName = "http-updates";

	/// <summary>The container registry hosting the LikeC4 image.</summary>
	internal const string DefaultRegistry = "ghcr.io";

	/// <summary>The container image name (without registry prefix).</summary>
	internal const string DefaultImage = "likec4/likec4";

	/// <summary>The default image tag used when <see cref="AspireC4DiagramOptions.ContainerImageTag"/> is not set.</summary>
	internal const string DefaultTag = "latest";

	/// <summary>The container port exposed by <c>likec4 serve</c>.</summary>
	internal const int DefaultContainerServePort = 5173;

	/// <summary>The container port used by LikeC4's Vite HMR channel.</summary>
	internal const int DefaultContainerUpdatePort = 24678;

	/// <summary>
	/// Root path inside the container that LikeC4 watches. Additional DSL folders and image-alias
	/// folders are bind-mounted as subdirectories here so that live edits reach the server directly,
	/// without going through the named volume.
	/// </summary>
	internal const string WorkspacePath = "/data";

	/// <summary>
	/// Sub-path inside the container where the named Docker volume is mounted. Only auto-generated
	/// files (the <c>.c4</c> model and <c>likec4.config.json</c>) live here.
	/// Keeping generated files in a subdirectory separate from the workspace root ensures that
	/// bind-mounts for additional DSL folders at <c>/data/ext/…</c> and image assets at
	/// <c>/data/img/…</c> are <em>outside</em> the named volume and are therefore not shadowed by it.
	/// </summary>
	internal const string GeneratedPath = "/data/output";

	internal static string GetImageReference(string imageTag) => $"{DefaultRegistry}/{DefaultImage}:{imageTag}";

	internal LikeC4ServerResource(string name)
		: base(name) { }
}
