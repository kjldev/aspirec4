namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Tests the publish-mode code path: the lifecycle hook should generate the .c4 file
/// but not start the live server.
/// </summary>
[NotInParallel]
public sealed class LikeC4PublishModeTests
{
	[Test]
	public async Task PublishMode_GeneratesC4FileWithoutStartingServer(CancellationToken cancellationToken)
	{
		var outputDir = Path.Combine(Path.GetTempPath(), "likec4-publish-" + Guid.NewGuid().ToString("N")[..8]);
		var modelOutputDir = Path.Combine(outputDir, "likec4");
		var appHostProject = GetTestAppHostProjectPath();
		var modelPath = Path.Combine(modelOutputDir, "publish-model.c4");

		try
		{
			Directory.CreateDirectory(outputDir);

			var startInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments =
					$"run --project \"{appHostProject}\" -- publish --publisher manifest --output-path \"{outputDir}\"",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			startInfo.Environment["LikeC4__OutputDirectory"] = modelOutputDir;
			startInfo.Environment["LikeC4__FileName"] = "publish-model";
			startInfo.Environment["LikeC4__Title"] = "Publish Mode Test";

			using var process = new System.Diagnostics.Process { StartInfo = startInfo };
			process.Start();

			var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
			var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

			await process.WaitForExitAsync(cancellationToken);

			var standardOutput = await standardOutputTask;
			var standardError = await standardErrorTask;
			var combinedOutput = standardOutput + Environment.NewLine + standardError;

			await Assert.That(process.ExitCode).IsEqualTo(0);
			await Assert.That(File.Exists(modelPath)).IsTrue();
			await Assert.That(File.Exists(Path.Combine(outputDir, "aspire-manifest.json"))).IsTrue();
			await Assert.That(combinedOutput).Contains("PublishMode");
			await Assert.That(combinedOutput).Contains("Published manifest to:");
			await Assert.That(combinedOutput).DoesNotContain("Starting DCP with arguments:");
			await Assert.That(combinedOutput).DoesNotContain("Distributed application started.");
		}
		finally
		{
			if (Directory.Exists(outputDir))
			{
				Directory.Delete(outputDir, recursive: true);
			}
		}
	}

	static string GetTestAppHostProjectPath() =>
		Path.GetFullPath(
			Path.Combine(
				AppContext.BaseDirectory,
				"..",
				"..",
				"..",
				"..",
				"AspireC4.TestAppHost",
				"AspireC4.TestAppHost.csproj"
			)
		);
}
