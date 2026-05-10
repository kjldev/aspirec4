using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// An Aspire executable resource that runs the LikeC4 live server via a local JavaScript
/// package manager CLI (npx, pnpm, yarn, or bun). Used as a fallback when Docker is not
/// available or when explicitly requested via <see cref="IAspireC4Builder.WithLocalCli"/>.
/// </summary>
public sealed class LikeC4LocalServerResource : ExecutableResource
{
	/// <summary>The name of the HTTP endpoint exposed by the LikeC4 server.</summary>
	public const string HttpEndpointName = "http";

	internal const int DefaultPort = 5173;

	internal LikeC4LocalServerResource(string name, string command, string workingDirectory)
		: base(name, command, workingDirectory) { }
}
