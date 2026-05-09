namespace Aspire.Hosting.AspireC4;

/// <summary>Configuration options for the LikeC4 diagram generation.</summary>
public sealed class LikeC4DiagramOptions
{
	/// <summary>Title shown in the generated LikeC4 view. Defaults to "Architecture".</summary>
	public string Title { get; set; } = "Architecture";

	/// <summary>
	/// Directory where the generated <c>.c4</c> file is written.
	/// Defaults to <c>./likec4</c> relative to the AppHost working directory.
	/// </summary>
	public string OutputDirectory { get; set; } = "./likec4";

	/// <summary>
	/// Name of the generated <c>.c4</c> file (without extension).
	/// Defaults to "model".
	/// </summary>
	public string FileName { get; set; } = "model";

	/// <summary>
	/// The tag of the <c>ghcr.io/likec4/likec4</c> container image to use for the live server.
	/// Defaults to <c>null</c>, which resolves to <c>"latest"</c>.
	/// Set this to a specific version (e.g. <c>"1.56"</c>) to pin the LikeC4 server version.
	/// </summary>
	/// <remarks>
	/// Ignored when <see cref="ILikeC4VisualizationBuilder.WithLocalCli"/> is used.
	/// </remarks>
	public string? ContainerImageTag { get; set; }

	/// <summary>
	/// Enables automatic icon inference for known resource types and technologies.
	/// Defaults to <see langword="true" />.
	/// </summary>
	public bool AutoIconsEnabled { get; set; } = true;
}
