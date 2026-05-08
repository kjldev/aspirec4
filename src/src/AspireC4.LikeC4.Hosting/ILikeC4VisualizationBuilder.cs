using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LikeC4;

namespace Aspire.Hosting;

/// <summary>
/// Provides a fluent interface for configuring the LikeC4 visualization after calling
/// <see cref="LikeC4VisualizationExtensions.AddLikeC4Visualization"/>.
/// </summary>
public interface ILikeC4VisualizationBuilder
{
    /// <summary>The underlying distributed application builder.</summary>
    IDistributedApplicationBuilder ApplicationBuilder { get; }

    /// <summary>
    /// The resource builder for the LikeC4 server resource.
    /// <para>
    /// In the default (Docker) mode this is an <see cref="IResourceBuilder{T}"/> of
    /// <see cref="LikeC4ServerResource"/>. After calling <see cref="WithLocalCli"/> it becomes
    /// an <see cref="IResourceBuilder{T}"/> of <see cref="LikeC4LocalServerResource"/>.
    /// Both are assignable here because <c>IResourceBuilder&lt;out T&gt;</c> is covariant.
    /// </para>
    /// </summary>
    IResourceBuilder<IResource> ServerResourceBuilder { get; }

    /// <summary>
    /// Switches the LikeC4 server from the default Docker container to a local JavaScript
    /// package manager CLI (<c>npx</c>, <c>pnpm exec</c>, <c>yarn dlx</c>, or <c>bunx</c>).
    /// </summary>
    /// <remarks>
    /// Use this when Docker is not available or you prefer a local Node.js-based workflow.
    /// The selected runtime must be installed and accessible on the system PATH.
    /// </remarks>
    /// <param name="runtime">
    /// The CLI runtime to use. Defaults to <see cref="LikeC4LocalCliRuntime.Auto"/>,
    /// which detects the first available runtime in the order: npx → pnpm → yarn → bun.
    /// </param>
    /// <returns>An updated <see cref="ILikeC4VisualizationBuilder"/> with the local server resource.</returns>
    ILikeC4VisualizationBuilder WithLocalCli(LikeC4LocalCliRuntime runtime = LikeC4LocalCliRuntime.Auto);
}
