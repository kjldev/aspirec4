using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Aspire eventing subscriber that generates the LikeC4 <c>.c4</c> model file before the
/// application starts.
/// </summary>
sealed class LikeC4VisualizationLifecycleHook(
	IOptions<LikeC4DiagramOptions> options,
	ILikeC4VisualizationLifecycleHookTelemetry telemetry) : IDistributedApplicationEventingSubscriber
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

	async Task GenerateC4FileAsync(
		DistributedApplicationModel appModel,
		DistributedApplicationExecutionContext executionContext,
		CancellationToken cancellationToken)
	{
		var opts = options.Value;

		telemetry.GeneratingLikeC4Model(appModel.Resources.Count);

		var model = LikeC4ModelBuilder.Build([.. appModel.Resources]);
		var dsl = LikeC4DslGenerator.Generate(model, opts);

		var outputDir = Path.GetFullPath(opts.OutputDirectory);
		Directory.CreateDirectory(outputDir);

		var outputPath = Path.Combine(outputDir, opts.FileName + ".c4");
		await File.WriteAllTextAsync(outputPath, dsl, cancellationToken);

		telemetry.LikeC4ModelWritten(outputPath);

		if (executionContext.IsPublishMode)
		{
			telemetry.PublishMode();
		}
	}
}
