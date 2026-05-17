namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Tests the publish-mode code path: the lifecycle hook should generate the .c4 file
/// but not start the live server.
/// </summary>
public sealed class LikeC4PublishModeTests
{
	[Test]
	public async Task PublishAsync_InPublishMode_GeneratesC4FileWithoutStartingServer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
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
			startInfo.Environment["AspireC4__OutputDirectory"] = modelOutputDir;
			startInfo.Environment["AspireC4__FileName"] = "publish-model";
			startInfo.Environment["AspireC4__Title"] = "Publish Mode Test";
			startInfo.Environment["Logging__LogLevel__Default"] = "Debug";
			// Disable strict mode: the TestAppHost has intentionally undeclared values used to
			// trigger source generator warnings. The PostConfigure in AddAspireC4 re-reads this
			// from config after the user's configure callback, so the env var takes effect.
			startInfo.Environment["AspireC4__Strict__Mode"] = "None";

			// Act
			using var process = new System.Diagnostics.Process { StartInfo = startInfo };
			process.Start();

			var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
			var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

			await process.WaitForExitAsync(cancellationToken);

			var standardOutput = await standardOutputTask;
			var standardError = await standardErrorTask;
			var combinedOutput = standardOutput + Environment.NewLine + standardError;

			// Assert
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
				"..",
				"src",
				"AspireC4.TestAppHost",
				"AspireC4.TestAppHost.csproj"
			)
		);
}
