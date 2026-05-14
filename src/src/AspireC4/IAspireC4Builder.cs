using Aspire.Hosting.AspireC4.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Provides a fluent interface for configuring the LikeC4 visualization after calling
/// <see cref="AspireC4DistributedApplicationBuilderExtensions.AddAspireC4"/>.
/// </summary>
public interface IAspireC4Builder
{
	/// <summary>The underlying distributed application builder.</summary>
	IDistributedApplicationBuilder ApplicationBuilder { get; }

	/// <summary>
	/// The resource builder for the LikeC4 server resource.
	/// <para>
	/// In the default (Docker) mode this is an <see cref="IResourceBuilder{T}"/> of
	/// <see cref="LikeC4ServerResource"/>. After calling <see cref="WithLocalCLI"/> it becomes
	/// an <see cref="IResourceBuilder{T}"/> of <see cref="LikeC4LocalServerResource"/>.
	/// Both are assignable here because <c>IResourceBuilder&lt;out T&gt;</c> is covariant.
	/// </para>
	/// </summary>
	IResourceBuilder<IResource> LikeC4ResourceBuilder { get; }

	/// <summary>
	/// Switches the LikeC4 server from the default Docker container to a local JavaScript
	/// package manager CLI (<c>npx</c>, <c>pnpm exec</c>, <c>yarn dlx</c>, or <c>bunx</c>).
	/// </summary>
	/// <remarks>
	/// Use this when Docker is not available or you prefer a local Node.js-based workflow.
	/// The selected runtime must be installed and accessible on the system PATH.
	/// </remarks>
	/// <param name="runtime">
	/// The CLI runtime to use. Defaults to <see cref="LocalCLIRuntime.Auto"/>,
	/// which detects the first available runtime in the order: npx → pnpm → yarn → bun.
	/// </param>
	/// <returns>An updated <see cref="IAspireC4Builder"/> with the local server resource.</returns>
	IAspireC4Builder WithLocalCLI(LocalCLIRuntime runtime = LocalCLIRuntime.Auto);

	/// <summary>
	/// Hides the LikeC4 server resource from the Aspire dashboard and instead surfaces
	/// the diagram as a URL link and command button on every project resource row.
	/// </summary>
	/// <remarks>
	/// When enabled, the <c>likec4-visualization</c> resource is removed from the dashboard
	/// resource list. Once the server is running, each <see cref="ProjectResource"/>
	/// gains a clickable link and a command button that opens the live diagram.
	/// </remarks>
	/// <param name="displayName">
	/// The text shown for the link and command button. Defaults to <c>"Architecture Diagram"</c>.
	/// </param>
	/// <returns>The same <see cref="IAspireC4Builder"/> for further configuration.</returns>
	IAspireC4Builder WithHideFromDashboard(string displayName = "Architecture Diagram");

	/// <summary>
	/// Registers an additional <c>.c4</c> source file that will be copied to the LikeC4
	/// output directory (and synced to the Docker volume workspace, if applicable) alongside
	/// the auto-generated model file.
	/// </summary>
	/// <remarks>
	/// Use this to include hand-authored LikeC4 files — custom views, styles, or extra model
	/// elements — that complement the auto-generated output.  The file is copied verbatim; its
	/// name must form a valid LikeC4 source filename (it should end with <c>.c4</c> or
	/// <c>.likec4</c>).
	/// </remarks>
	/// <param name="sourcePath">
	/// The path to the source file. Relative paths are resolved from the current working
	/// directory at the time writing executes. If the file does not
	/// exist at startup the entry is silently skipped.
	/// </param>
	/// <returns>The same <see cref="IAspireC4Builder"/> for further configuration.</returns>
	IAspireC4Builder WithAdditionalDSLFile(string sourcePath);

	/// <summary>
	/// Registers an additional folder whose <c>.c4</c> files will be included in the LikeC4
	/// project via the <c>include.paths</c> field of the generated <c>likec4.config.json</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Unlike <see cref="WithAdditionalDSLFile"/>, this method does <b>not</b> copy files into
	/// the output directory. Instead it registers a directory whose contents are discovered by
	/// LikeC4 at runtime through the config file's <c>include.paths</c> mechanism
	/// (see <see href="https://likec4.dev/dsl/config/#include-additional-directories"/>).
	/// </para>
	/// <para>
	/// In Docker container mode, the folder is bind-mounted read-only into the container at a
	/// deterministic path under <c>/data/ext/</c> and the container-side config file is generated
	/// to reference that path.
	/// </para>
	/// </remarks>
	/// <param name="folderPath">
	/// The absolute path to a directory containing <c>.c4</c> source files. Relative paths are
	/// resolved against the current working directory at call time. The directory must exist when
	/// this method is called; a <see cref="DirectoryNotFoundException"/> is thrown otherwise.
	/// </param>
	/// <returns>The same <see cref="IAspireC4Builder"/> for further configuration.</returns>
	/// <exception cref="DirectoryNotFoundException">
	/// Thrown immediately if <paramref name="folderPath"/> does not refer to an existing directory.
	/// </exception>
	IAspireC4Builder WithAdditionalDSLFolder(string folderPath);

	/// <summary>
	/// Registers an image alias that maps a shorthand key (e.g. <c>"@icons"</c>) to a directory
	/// of image files.  The alias is written to the <c>imageAliases</c> section of the generated
	/// <c>likec4.config.json</c> (see <see href="https://likec4.dev/dsl/config/#image-aliases"/>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Once registered, the alias can be used inside <c>.c4</c> files as an icon prefix, e.g.
	/// <c>icon "@icons/service.svg"</c>.
	/// </para>
	/// <para>
	/// In Docker container mode, the folder is bind-mounted read-only into the container at a
	/// deterministic path under <c>/data/img/</c> and the container-side config file references
	/// that mount point.
	/// </para>
	/// </remarks>
	/// <param name="aliasKey">
	/// The alias identifier, which must start with <c>@</c> (e.g. <c>"@icons"</c>).
	/// An <see cref="ArgumentException"/> is thrown if the key does not start with <c>@</c>.
	/// </param>
	/// <param name="folderPath">
	/// The absolute path to the image directory. Relative paths are resolved against the current
	/// working directory at call time. The directory must exist; a
	/// <see cref="DirectoryNotFoundException"/> is thrown otherwise.
	/// </param>
	/// <returns>The same <see cref="IAspireC4Builder"/> for further configuration.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="aliasKey"/> does not start with <c>@</c>.
	/// </exception>
	/// <exception cref="DirectoryNotFoundException">
	/// Thrown immediately if <paramref name="folderPath"/> does not refer to an existing directory.
	/// </exception>
	IAspireC4Builder WithImageAliasFolder(string aliasKey, string folderPath);

	/// <summary>
	/// Disables the automatic generation of <c>likec4.config.json</c> in the output directory.
	/// </summary>
	/// <remarks>
	/// By default, AspireC4 writes a <c>likec4.config.json</c> that sets the project title,
	/// any registered include paths, and image aliases.  Call this method when you want full
	/// control over the config file — for example, because the output directory is already part
	/// of a hand-curated LikeC4 project with its own config.
	/// </remarks>
	/// <returns>The same <see cref="IAspireC4Builder"/> for further configuration.</returns>
	IAspireC4Builder WithoutConfigFileGeneration();
}
