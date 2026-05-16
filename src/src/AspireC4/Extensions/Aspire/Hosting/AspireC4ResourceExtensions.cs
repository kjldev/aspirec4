using System.ComponentModel;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for <see cref="IResourceBuilder{T}"/> of <see cref="AspireC4Resource"/>
/// that configure the LikeC4 visualization sidecar.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4ResourceExtensions
{
	/// <summary>
	/// Switches the LikeC4 server from the default Docker container to a local JavaScript
	/// package manager CLI (<c>npx</c>, <c>pnpm exec</c>, <c>yarn dlx</c>, or <c>bunx</c>).
	/// </summary>
	/// <remarks>
	/// Use this when Docker is not available or you prefer a local Node.js-based workflow.
	/// The selected runtime must be installed and accessible on the system PATH.
	/// </remarks>
	/// <param name="builder">The <see cref="AspireC4Resource"/> builder.</param>
	/// <param name="runtime">
	/// The CLI runtime to use. Defaults to <see cref="LocalCLIRuntime.Auto"/>,
	/// which detects the first available runtime in the order: npx → pnpm → yarn → bun.
	/// </param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	[AspireExport(
		MethodName = "withLocalCLI",
		Description = "Switches the LikeC4 server to a local JavaScript package manager CLI."
	)]
	public static IResourceBuilder<AspireC4Resource> WithLocalCLI(
		this IResourceBuilder<AspireC4Resource> builder,
		LocalCLIRuntime runtime = LocalCLIRuntime.Auto
	)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var aspirec4 = builder.Resource;

		// Remove the existing server resource (container by default) from the app model.
		if (aspirec4.InnerResource is not null)
			builder.ApplicationBuilder.Resources.Remove(aspirec4.InnerResource);

		var resolvedRuntime = runtime == LocalCLIRuntime.Auto ? AspireC4Builder.DetectRuntime() : runtime;

		var (command, args) = AspireC4Builder.BuildLocalCLICommand(
			resolvedRuntime,
			aspirec4.OutputDirectory,
			LikeC4LocalServerResource.DefaultPort
		);

		LikeC4LocalServerResource localResource = new(
			aspirec4.Name + AspireC4DistributedApplicationBuilderExtensions.AspireC4ServerResourceSuffix,
			command,
			aspirec4.OutputDirectory
		);

		var localBuilder = builder
			.ApplicationBuilder.AddResource(localResource)
			.WithArgs(args)
			.WithHttpEndpoint(
				name: LikeC4LocalServerResource.HttpEndpointName,
				targetPort: LikeC4LocalServerResource.DefaultPort
			)
			.WithExternalHttpEndpoints()
			.WithAnnotation(new ExcludeFromLikeC4Annotation(), ResourceAnnotationMutationBehavior.Replace);

		aspirec4.InnerResource = localResource;

		builder.ApplicationBuilder.Services.Configure<ContainerWorkspaceOptions>(wsOpts =>
			wsOpts.LocalCLIRuntime = resolvedRuntime
		);

		return builder;
	}

	/// <summary>
	/// Hides the LikeC4 server resource from the Aspire dashboard and instead surfaces
	/// the diagram as a URL link and command button on every project resource row.
	/// </summary>
	/// <param name="builder">The <see cref="AspireC4Resource"/> builder.</param>
	/// <param name="displayName">
	/// The text shown for the link and command button. Defaults to <c>"Architecture Diagram"</c>.
	/// </param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	[AspireExport(
		MethodName = "withHideFromDashboard",
		Description = "Hides the LikeC4 server resource from the Aspire dashboard."
	)]
	public static IResourceBuilder<AspireC4Resource> WithHideFromDashboard(
		this IResourceBuilder<AspireC4Resource> builder,
		string displayName = "Architecture Diagram"
	)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

		builder.ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts =>
		{
			opts.HideFromDashboard = true;
			opts.DashboardLinkDisplayName = displayName;
		});

		return builder;
	}

	/// <summary>
	/// Registers an additional <c>.c4</c> source file that will be copied to the LikeC4
	/// output directory alongside the auto-generated model file.
	/// </summary>
	/// <param name="builder">The <see cref="AspireC4Resource"/> builder.</param>
	/// <param name="sourcePath">
	/// The path to the source file. Relative paths are resolved from the current working directory.
	/// </param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	[AspireExport(MethodName = "withAdditionalDSLFile", Description = "Registers an additional .c4 source file.")]
	public static IResourceBuilder<AspireC4Resource> WithAdditionalDSLFile(
		this IResourceBuilder<AspireC4Resource> builder,
		string sourcePath
	)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

		var absoluteSource = Path.GetFullPath(sourcePath);
		builder.ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts =>
			opts.AdditionalDSLFiles.Add(absoluteSource)
		);

		return builder;
	}

	/// <summary>
	/// Registers an additional folder whose <c>.c4</c> files will be included in the LikeC4
	/// project via the <c>include.paths</c> field of the generated <c>likec4.config.json</c>.
	/// </summary>
	/// <param name="builder">The <see cref="AspireC4Resource"/> builder.</param>
	/// <param name="folderPath">The absolute path to a directory containing <c>.c4</c> source files.</param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	/// <exception cref="DirectoryNotFoundException">
	/// Thrown if <paramref name="folderPath"/> does not refer to an existing directory.
	/// </exception>
	[AspireExport(
		MethodName = "withAdditionalDSLFolder",
		Description = "Registers an additional folder of .c4 source files."
	)]
	public static IResourceBuilder<AspireC4Resource> WithAdditionalDSLFolder(
		this IResourceBuilder<AspireC4Resource> builder,
		string folderPath
	)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

		var absoluteFolder = Path.GetFullPath(folderPath);
		if (!Directory.Exists(absoluteFolder))
			throw new DirectoryNotFoundException($"The additional DSL folder does not exist: '{absoluteFolder}'");

		builder.ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts =>
			opts.AdditionalDSLFolders.Add(absoluteFolder)
		);

		return builder;
	}

	/// <summary>
	/// Registers an image alias that maps a shorthand key (e.g. <c>"@icons"</c>) to a directory
	/// of image files, written to the <c>imageAliases</c> section of the generated
	/// <c>likec4.config.json</c>.
	/// </summary>
	/// <param name="builder">The <see cref="AspireC4Resource"/> builder.</param>
	/// <param name="aliasKey">The alias identifier, which must start with <c>@</c>.</param>
	/// <param name="folderPath">The absolute path to the image directory.</param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="aliasKey"/> does not start with <c>@</c>.</exception>
	/// <exception cref="DirectoryNotFoundException">Thrown if <paramref name="folderPath"/> does not exist.</exception>
	[AspireExport(MethodName = "withImageAliasFolder", Description = "Registers an image alias folder for LikeC4.")]
	public static IResourceBuilder<AspireC4Resource> WithImageAliasFolder(
		this IResourceBuilder<AspireC4Resource> builder,
		string aliasKey,
		string folderPath
	)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(aliasKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

		if (!aliasKey.StartsWith('@'))
			throw new ArgumentException("Image alias keys must start with '@'.", nameof(aliasKey));

		var absoluteFolder = Path.GetFullPath(folderPath);
		if (!Directory.Exists(absoluteFolder))
			throw new DirectoryNotFoundException($"The image alias folder does not exist: '{absoluteFolder}'");

		builder.ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts =>
			opts.ImageAliases[aliasKey] = absoluteFolder
		);

		return builder;
	}

	/// <summary>
	/// Disables the automatic generation of <c>likec4.config.json</c> in the output directory.
	/// </summary>
	/// <param name="builder">The <see cref="AspireC4Resource"/> builder.</param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	[AspireExport(
		MethodName = "withoutConfigFileGeneration",
		Description = "Disables automatic generation of likec4.config.json."
	)]
	public static IResourceBuilder<AspireC4Resource> WithoutConfigFileGeneration(
		this IResourceBuilder<AspireC4Resource> builder
	)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts => opts.GenerateConfigFile = false);

		return builder;
	}

	/// <summary>
	/// Provides access to the underlying LikeC4 server resource builder for advanced configuration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use this to apply annotations or other configuration directly to the inner server resource
	/// (e.g. <c>WithLikeC4Details</c>, custom annotations). The server resource is the Docker
	/// container (<see cref="LikeC4ServerResource"/>) unless <c>.WithLocalCLI()</c> has been called,
	/// in which case it is the CLI executable (<see cref="LikeC4LocalServerResource"/>).
	/// </para>
	/// <para>
	/// This must be called <b>after</b> any <c>.WithLocalCLI()</c> call if you want to configure the
	/// CLI resource; calling <c>ConfigureServer</c> first and then <c>WithLocalCLI</c> will configure
	/// the Docker container that is subsequently replaced.
	/// </para>
	/// </remarks>
	/// <param name="builder">The <see cref="AspireC4Resource"/> builder.</param>
	/// <param name="configure">A callback that receives the inner server resource builder.</param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	public static IResourceBuilder<AspireC4Resource> ConfigureServer(
		this IResourceBuilder<AspireC4Resource> builder,
		Action<IResourceBuilder<IResource>> configure
	)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var innerResource =
			builder.Resource.InnerResource
			?? throw new InvalidOperationException(
				"The inner server resource has not been initialised yet. Ensure AddAspireC4() has completed before calling ConfigureServer()."
			);

		var innerBuilder = builder.ApplicationBuilder.CreateResourceBuilder(innerResource);
		configure(innerBuilder);

		return builder;
	}
}
