using Purview.Telemetry;

namespace Aspire.Hosting.AspireC4;

[Logger]
interface ILikeC4VisualizationLifecycleHookTelemetry
{
	[Info]
	void GeneratingLikeC4Model(int resourceCount);

	[Info]
	void LikeC4ModelWritten(string outputPath);

	[Info]
	void PublishMode();
}
