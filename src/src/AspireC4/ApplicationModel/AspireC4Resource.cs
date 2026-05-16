namespace Aspire.Hosting.AspireC4.ApplicationModel;

/// <summary>
/// The top-level Aspire resource for the LikeC4 visualization sidecar.
/// </summary>
/// <remarks>
/// <para>
/// By default the visualization is backed by the official <c>ghcr.io/likec4/likec4</c> Docker
/// container (<see cref="LikeC4ServerResource"/>).  Calling
/// <c>.WithLocalCLI()</c> on the returned builder replaces the container with a local
/// Node.js CLI executable (<see cref="LikeC4LocalServerResource"/>).
/// </para>
/// <para>
/// In either case the inner resource is wired up automatically; consumers interact only with
/// <c>IResourceBuilder&lt;AspireC4Resource&gt;</c> extension methods.
/// </para>
/// </remarks>
[AspireExport]
public sealed class AspireC4Resource : Resource
{
	internal AspireC4Resource(string name, string outputDirectory)
		: base(name)
	{
		OutputDirectory = outputDirectory;
	}

	/// <summary>The absolute host path where the generated <c>.c4</c> file is written.</summary>
	internal string OutputDirectory { get; }

	/// <summary>
	/// The underlying server resource — either a <see cref="LikeC4ServerResource"/> (Docker container,
	/// the default) or a <see cref="LikeC4LocalServerResource"/> (local CLI, after calling
	/// <c>.WithLocalCLI()</c>).
	/// </summary>
	internal IResource? InnerResource { get; set; }
}
