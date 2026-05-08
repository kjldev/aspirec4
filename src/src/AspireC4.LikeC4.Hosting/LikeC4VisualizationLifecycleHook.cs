using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.LikeC4;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting;

/// <summary>
/// Aspire eventing subscriber that generates the LikeC4 <c>.c4</c> model file before the
/// application starts. In publish mode it skips the Node.js validation.
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

        if (!executionContext.IsPublishMode)
        {
            ValidateNodeJs();
        }

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

    private void ValidateNodeJs()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            process?.WaitForExit(3000);

            if (process?.ExitCode != 0)
            {
                logger.LogWarning(
                    "Node.js check returned a non-zero exit code. " +
                    "The LikeC4 live server may not start correctly.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Node.js was not found on PATH. The LikeC4 live server will not be available. " +
                "Install Node.js from https://nodejs.org/ and ensure 'node' is accessible.");
        }
    }
}
