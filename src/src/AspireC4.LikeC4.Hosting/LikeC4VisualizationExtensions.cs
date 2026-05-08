using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LikeC4;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for integrating LikeC4 live architecture diagrams into an Aspire AppHost.
/// </summary>
public static class LikeC4VisualizationExtensions
{
    private const string ServerResourceName = "likec4-server";

    /// <summary>
    /// Adds a LikeC4 live architecture diagram to the Aspire application.
    /// </summary>
    /// <remarks>
    /// This registers a lifecycle hook that generates a <c>.c4</c> model file from the Aspire
    /// resource graph, and starts a <c>npx likec4 serve</c> sidecar that renders an interactive,
    /// hot-reloading diagram in the browser.
    /// <para>
    /// <b>Prerequisite:</b> Node.js must be installed and <c>node</c> must be on the PATH.
    /// </para>
    /// </remarks>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="configure">Optional callback to configure <see cref="LikeC4DiagramOptions"/>.</param>
    /// <returns>An <see cref="ILikeC4VisualizationBuilder"/> for further configuration.</returns>
    public static ILikeC4VisualizationBuilder AddLikeC4Visualization(
        this IDistributedApplicationBuilder builder,
        Action<LikeC4DiagramOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<LikeC4DiagramOptions>()
            .Configure(opts => configure?.Invoke(opts));

        builder.Services.AddEventingSubscriber<LikeC4VisualizationLifecycleHook>();

        // Resolve the output directory from options at build time so the server resource
        // working directory matches where the lifecycle hook writes the .c4 file.
        var opts = new LikeC4DiagramOptions();
        configure?.Invoke(opts);

        var outputDir = Path.GetFullPath(opts.OutputDirectory);
        var serverResource = new LikeC4ServerResource(ServerResourceName, outputDir);

        var serverBuilder = builder.AddResource(serverResource)
            .WithArgs("--", "npx", "likec4", "serve", ".", "--port", LikeC4ServerResource.DefaultPort.ToString())
            .WithHttpEndpoint(name: LikeC4ServerResource.HttpEndpointName, targetPort: LikeC4ServerResource.DefaultPort)
            .WithExternalHttpEndpoints()
            // Exclude the sidecar from the architecture diagram — it is tooling, not a system element.
            .WithAnnotation(new ExcludeFromLikeC4Annotation(), ResourceAnnotationMutationBehavior.Replace);

        return new LikeC4VisualizationBuilder(builder, serverBuilder);
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
