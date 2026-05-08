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

    /// <summary>The resource builder for the LikeC4 server resource.</summary>
    IResourceBuilder<LikeC4ServerResource> ServerResourceBuilder { get; }
}
