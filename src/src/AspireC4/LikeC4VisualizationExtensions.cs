using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Aspire.Hosting;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for integrating LikeC4 live architecture diagrams into an Aspire AppHost.
/// </summary>
public static class LikeC4VisualizationExtensions
{
	internal const string ServerResourceName = "likec4-visualization";

	/// <summary>
	/// Adds a LikeC4 live architecture diagram to the Aspire application.
	/// </summary>
	/// <remarks>
	/// This registers a lifecycle hook that generates a <c>.c4</c> model file from the Aspire
	/// resource graph, and starts the official <c>ghcr.io/likec4/likec4</c> container as a
	/// sidecar that renders an interactive, hot-reloading diagram in the browser.
	/// <para>
	/// <b>Prerequisite:</b> Docker must be available (standard Aspire requirement). To use a
	/// local Node.js CLI instead, call <c>.WithLocalCli()</c> on the returned builder.
	/// </para>
	/// </remarks>
	/// <param name="builder">The distributed application builder.</param>
	/// <param name="name">The name of the LikeC4 visualization resource (used for the server container and diagram file).</param>
	/// <param name="configure">Optional callback to configure <see cref="LikeC4DiagramOptions"/>.</param>
	/// <returns>An <see cref="ILikeC4VisualizationBuilder"/> for further configuration.</returns>
	public static ILikeC4VisualizationBuilder AddLikeC4Visualization(
		this IDistributedApplicationBuilder builder,
		[ResourceName]
		string name = ServerResourceName,
		Action<LikeC4DiagramOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(name);

		builder.Services.AddOptions<LikeC4DiagramOptions>()
			.Configure(opts => configure?.Invoke(opts));

		builder.Services.AddEventingSubscriber<LikeC4VisualizationLifecycleHook>();
		builder.Services.AddLikeC4VisualizationLifecycleHookTelemetry();

		// Resolve options at build time so the bind mount and lifecycle hook use the same path.
		var opts = new LikeC4DiagramOptions();
		configure?.Invoke(opts);

		var outputDir = Path.GetFullPath(opts.OutputDirectory);
		var imageTag = opts.ContainerImageTag ?? LikeC4ServerResource.DefaultTag;

		var serverResource = new LikeC4ServerResource(ServerResourceName);

		var serverBuilder = builder.AddResource(serverResource)
			.WithImage(LikeC4ServerResource.DefaultImage, imageTag)
			.WithImageRegistry(LikeC4ServerResource.DefaultRegistry)
			.WithArgs("serve", ".", "--port", LikeC4ServerResource.DefaultContainerPort.ToString())
			.WithBindMount(outputDir, LikeC4ServerResource.WorkspacePath)
			// Required on Windows/Docker Desktop: inotify events do not propagate from the host
			// filesystem into the container, so chokidar must fall back to polling to detect
			// changes to the generated .c4 file.
			.WithEnvironment("CHOKIDAR_USEPOLLING", "1")
			.WithEnvironment("CHOKIDAR_INTERVAL", "200")
			.WithHttpEndpoint(
				name: LikeC4ServerResource.HttpEndpointName,
				targetPort: LikeC4ServerResource.DefaultContainerPort)
			.WithExternalHttpEndpoints()
			// Exclude the sidecar from the architecture diagram — it is tooling, not a system element.
			.WithAnnotation(new ExcludeFromLikeC4Annotation(), ResourceAnnotationMutationBehavior.Replace);

		return new LikeC4VisualizationBuilder(builder, serverBuilder, outputDir);
	}

	/// <summary>
	/// Customises how a resource appears in the generated LikeC4 diagram.
	/// </summary>
	public static IResourceBuilder<T> WithLikeC4Details<T>(
		this IResourceBuilder<T> builder,
		string? label = null,
		string? technology = null,
		string? description = null) where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);

		var effectiveLabel = label ?? builder.Resource.Name;
		return builder.WithAnnotation(
			new LikeC4NodeDetailsAnnotation(effectiveLabel, technology, description),
			ResourceAnnotationMutationBehavior.Replace);
	}

	/// <summary>
	/// Excludes a resource from the generated LikeC4 diagram.
	/// </summary>
	public static IResourceBuilder<T> ExcludeFromLikeC4<T>(
		this IResourceBuilder<T> builder) where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithAnnotation(
			new ExcludeFromLikeC4Annotation(),
			ResourceAnnotationMutationBehavior.Replace);
	}
}
