using Aspire.Hosting.Testing;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Tests the publish-mode code path: the lifecycle hook should generate the .c4 file
/// but not start the live server.
/// </summary>
public sealed class LikeC4PublishModeTests
{
	[Test]
	public async Task PublishMode_GeneratesC4FileWithoutStartingServer(CancellationToken cancellationToken)
	{
		var outputDir = Path.Combine(Path.GetTempPath(), "likec4-publish-" + Guid.NewGuid().ToString("N")[..8]);

		try
		{
			var appBuilder = await DistributedApplicationTestingBuilder.CreateAsync<TestAppHostProgram>(cancellationToken);

			appBuilder.AddLikeC4Visualization(configure: opts =>
			{
				opts.Title = "Publish Mode Test";
				opts.OutputDirectory = outputDir;
				opts.FileName = "publish-model";
			});

			await using var app = await appBuilder.BuildAsync(cancellationToken);
			await app.StartAsync(cancellationToken);

			var path = Path.Combine(outputDir, "publish-model.c4");
			await Assert.That(File.Exists(path)).IsTrue();

			var content = await File.ReadAllTextAsync(path, cancellationToken);
			await Assert.That(content).Contains("title 'Publish Mode Test'");

			await app.StopAsync(cancellationToken);
		}
		finally
		{
			if (Directory.Exists(outputDir))
			{
				Directory.Delete(outputDir, recursive: true);
			}
		}
	}
}
