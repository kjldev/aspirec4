namespace Aspire.Hosting.AspireC4;

/// <summary>Configuration options for the LikeC4 diagram generation.</summary>
public sealed class AspireC4DiagramOptions
{
	public const string SectionName = "AspireC4";

	/// <summary>Title shown in the generated LikeC4 view. Defaults to "Architecture".</summary>
	public string Title { get; set; } = "Architecture";

	/// <summary>
	/// Directory where the generated <c>.c4</c> file is written.
	/// Defaults to <c>./likec4</c> relative to the AppHost working directory.
	/// </summary>
	public string OutputDirectory { get; set; } = "./likec4";

	/// <summary>
	/// Name of the generated <c>.c4</c> file (without extension).
	/// Defaults to "model.gen".
	/// </summary>
	public string FileName { get; set; } = "model.gen";

	/// <summary>
	/// Disables the Hot Module Replacement (HMR) channel between the LikeC4 server and the browser, which provides
	/// dynamic/ live updates to the diagram. Defaults to <see langword="false"/> (HMR enabled).
	/// </summary>
	public bool DisableHMR { get; set; }

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

	/// <summary>
	/// When <see langword="true"/>, hides the LikeC4 server resource from the Aspire dashboard
	/// and instead surfaces the diagram URL as a link and command on all project resources.
	/// Defaults to <see langword="false"/>.
	/// </summary>
	public bool HideFromDashboard { get; set; }

	/// <summary>
	/// Display name used for the architecture diagram link and command when
	/// <see cref="HideFromDashboard"/> is <see langword="true"/>.
	/// Defaults to <c>"Architecture Diagram"</c>.
	/// </summary>
	public string DashboardLinkDisplayName { get; set; } = "Architecture Diagram";

	/// <summary>
	/// Controls the DSL syntax used to emit typed relationships in the generated <c>.c4</c> file.
	/// <list type="bullet">
	///   <item><description><see cref="LikeC4RelationshipKindSyntax.Dot"/> — <c>SOURCE .KIND TARGET</c> (default, preferred).</description></item>
	///   <item><description><see cref="LikeC4RelationshipKindSyntax.Bracket"/> — <c>SOURCE -[KIND]-&gt; TARGET</c>.</description></item>
	/// </list>
	/// </summary>
	public LikeC4RelationshipKindSyntax RelationshipKindSyntax { get; set; } = LikeC4RelationshipKindSyntax.Dot;

	/// <summary>
	/// When <see langword="true"/>, runs <c>npx likec4 validate --json --no-layout</c> against the output
	/// directory after generating the <c>.c4</c> file. Any validation errors are logged as warnings;
	/// the application continues to start regardless of the result.
	/// Defaults to <see langword="false"/>.
	/// </summary>
	public bool ValidateBeforeStart { get; set; }

	/// <summary>
	/// Custom element kind specifications emitted in the <c>specification { }</c> block.
	/// Each entry may include optional style tokens (shape, color, icon, border, opacity),
	/// a notation string, and a default technology label.
	/// <para>
	/// These are additive — kinds listed here but not present in the model are still declared.
	/// When a kind in the model matches an entry here, the full body (style, notation, technology)
	/// is emitted rather than a bare <c>element KIND</c> line.
	/// </para>
	/// </summary>
	public List<LikeC4ElementKindSpec> ElementKindSpecs { get; set; } = [];

	/// <summary>
	/// Additional user-managed <c>.c4</c> source files that are copied into the output directory
	/// (and synced to the Docker volume when in container mode) alongside the generated model file.
	/// LikeC4 automatically discovers all <c>.c4</c> files in the project directory, so these files
	/// are included in the diagram without any further configuration.
	/// </summary>
	/// <remarks>
	/// Each entry should be an absolute path or a path relative to the AppHost working directory.
	/// </remarks>
	public List<string> AdditionalDslFiles { get; set; } = [];
}
