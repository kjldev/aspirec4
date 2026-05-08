using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.LikeC4;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting;

/// <summary>
/// Aspire eventing subscriber that generates the LikeC4 <c>.c4</c> model file before the
/// application starts.
/// </summary>
internal sealed class LikeC4VisualizationLifecycleHook(
    IOptions<LikeC4DiagramOptions> options,
    ILogger<LikeC4VisualizationLifecycleHook> logger) : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
            GenerateC4FileAsync(evt.Model, executionContext, ct));

        return Task.CompletedTask;
    }

    private Task GenerateC4FileAsync(
        DistributedApplicationModel appModel,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;

        logger.LogInformation("Generating LikeC4 model for {ResourceCount} resources", appModel.Resources.Count);

        var model = LikeC4ModelBuilder.Build([.. appModel.Resources]);
        var dsl = LikeC4DslGenerator.Generate(model, opts);

        var outputDir = Path.GetFullPath(opts.OutputDirectory);
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, opts.FileName + ".c4");
        File.WriteAllText(outputPath, dsl);

        logger.LogInformation("LikeC4 model written to {Path}", outputPath);

        if (executionContext.IsPublishMode)
        {
            logger.LogInformation("Publish mode: LikeC4 live server will not be started");
        }

        return Task.CompletedTask;
    }
}
