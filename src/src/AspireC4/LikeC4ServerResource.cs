using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// An Aspire container resource that runs the LikeC4 live server via the official
/// <c>ghcr.io/likec4/likec4</c> Docker image, providing a hot-reloading interactive
/// architecture diagram.
/// </summary>
/// <remarks>
/// To use a local Node.js CLI instead of Docker, call
/// <see cref="ILikeC4VisualizationBuilder.WithLocalCli"/> on the returned builder.
/// </remarks>
public sealed class LikeC4ServerResource : ContainerResource
{
	/// <summary>The name of the HTTP endpoint exposed by the LikeC4 server.</summary>
	public const string HttpEndpointName = "http";

	/// <summary>The container registry hosting the LikeC4 image.</summary>
	internal const string DefaultRegistry = "ghcr.io";

	/// <summary>The container image name (without registry prefix).</summary>
	internal const string DefaultImage = "likec4/likec4";

	/// <summary>The default image tag used when <see cref="LikeC4DiagramOptions.ContainerImageTag"/> is not set.</summary>
	internal const string DefaultTag = "latest";

	/// <summary>The container port exposed by <c>likec4 serve</c>.</summary>
	internal const int DefaultContainerServePort = 5173;

	internal const int DefaultContainerUpdatePort = 24678;

	/// <summary>The path inside the container where <c>.c4</c> source files are mounted.</summary>
	internal const string WorkspacePath = "/data";

	internal LikeC4ServerResource(string name) : base(name) { }
}
