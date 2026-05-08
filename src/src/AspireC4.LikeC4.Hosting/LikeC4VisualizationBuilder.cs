using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LikeC4;

namespace Aspire.Hosting;

internal sealed class LikeC4VisualizationBuilder(
    IDistributedApplicationBuilder applicationBuilder,
    IResourceBuilder<LikeC4ServerResource> serverResourceBuilder) : ILikeC4VisualizationBuilder
{
    public IDistributedApplicationBuilder ApplicationBuilder { get; } = applicationBuilder;
    public IResourceBuilder<LikeC4ServerResource> ServerResourceBuilder { get; } = serverResourceBuilder;
}
