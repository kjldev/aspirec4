using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Integration tests that verify the LikeC4 visualization starts successfully in a real Aspire app host.
/// A single app instance is shared across all tests in this class (started in <see cref="ClassSetUpAsync"/>
/// and torn down in <see cref="ClassTearDownAsync"/>) to avoid the resource contention that occurs when
/// 10+ Aspire apps (each with postgres/redis/docker containers) start in parallel.
/// HMR is disabled so no relay port is bound during testing.
/// </summary>
public sealed partial class AspireC4HostTests
{
	const string AspireC4ResourceName = AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName;
	const string AspireC4ServerResourceName =
		AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName
		+ AspireC4DistributedApplicationBuilderExtensions.AspireC4ServerResourceSuffix;
	static readonly TimeSpan LikeC4StartupTimeout = TimeSpan.FromSeconds(120);

	// Shared across all tests in this class — set once in ClassSetUpAsync.
	static DistributedApplication? s_app;
	static string? s_outputDir;
	static string? s_modelPath;

	[Before(Class)]
	public static async Task ClassSetUpAsync(CancellationToken cancellationToken)
	{
		// Use a directory alongside the TestAppHost output so all relevant paths
		// (extensions, image aliases, output) share the same drive — required by the
		// single-bind-mount architecture that computes a common ancestor.
		var testHostDir = Path.GetDirectoryName(typeof(TestAppHostProgram).Assembly.Location)!;
		s_outputDir = Path.Combine(testHostDir, "test-output-" + Guid.NewGuid().ToString("N")[..8]);
		s_modelPath = Path.Combine(s_outputDir, "model.gen.c4");

		var appBuilder = await DistributedApplicationTestingBuilder.CreateAsync<TestAppHostProgram>(cancellationToken);

		// Inject test-specific configuration directly into the builder's configuration system.
		appBuilder.Configuration.AddInMemoryCollection(
			new Dictionary<string, string?>
			{
				["AspireC4:OutputDirectory"] = s_outputDir,
				["AspireC4:FileName"] = "model.gen",
				["AspireC4:Title"] = "Integration Test Architecture",
				// Disable HMR so no relay port is bound during testing.
				["AspireC4:DisableHMR"] = "true",
			}
		);

		// PostConfigure wins over all Configure callbacks, including the TestAppHost's
		// configure callback that sets ValidateBeforeStart=true and the default FormatGeneratedFile=true.
		// Both invoke `npx likec4 …` which traverses up the directory tree and scans the entire
		// repository workspace when run from within the repo — hanging the BeforeStartEvent handler.
		appBuilder.Services.PostConfigure<AspireC4DiagramOptions>(static opts =>
		{
			opts.ValidateBeforeStart = false;
			opts.FormatGeneratedFile = false;
		});

		s_app = await appBuilder.BuildAsync(cancellationToken);
		await s_app.StartAsync(cancellationToken);
	}

	[After(Class)]
	public static async Task ClassTearDownAsync(CancellationToken cancellationToken)
	{
		if (s_app is not null)
		{
			await s_app.StopAsync(cancellationToken);
		}

		if (s_outputDir is not null && Directory.Exists(s_outputDir))
		{
			Directory.Delete(s_outputDir, recursive: true);
		}
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_GeneratesC4File()
	{
		// Arrange
		// (shared app started in ClassSetUpAsync)

		// Act
		// (side effect occurred during startup)

		// Assert
		await Assert.That(s_modelPath).IsNotNull();
		await Assert.That(File.Exists(s_modelPath!)).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_C4FileContainsDslStructure(CancellationToken cancellationToken)
	{
		// Arrange
		// (shared app started in ClassSetUpAsync)

		// Act
		var content = await File.ReadAllTextAsync(s_modelPath!, cancellationToken);

		// Assert
		await Assert.That(content).Contains("specification {");
		await Assert.That(content).Contains("model {");
		await Assert.That(content).Contains("views {");
		await Assert.That(content).Contains("node_app");
		await Assert.That(content).Contains("AspireC4 Architecture");
	}

	[Test]
	[Timeout(120_000)]
	public async Task StartAsync_WithLikeC4Container_ReachesRunningState(CancellationToken cancellationToken)
	{
		// Arrange
		// (shared app started in ClassSetUpAsync)

		// Act
		await WaitForLikeC4ServerRunningAsync(cancellationToken);

		// Assert
		await Assert.That(s_app).IsNotNull();
	}

	[Test]
	[Timeout(120_000)]
	public async Task StartAsync_WithLikeC4Container_HttpEndpointReturnsSuccess(CancellationToken cancellationToken)
	{
		// Arrange
		// (shared app started in ClassSetUpAsync)
		await WaitForLikeC4ServerRunningAsync(cancellationToken);
		using var client = s_app!.CreateHttpClient(AspireC4ServerResourceName, LikeC4ServerResource.HttpEndpointName);

		// Act
		HttpResponseMessage? response = null;
		for (var attempt = 0; attempt < 10; attempt++)
		{
			try
			{
				response = await client.GetAsync("/", cancellationToken);
				break;
			}
			catch (HttpRequestException) when (attempt < 9)
			{
				await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
			}
		}

		// Assert
		await Assert.That(response).IsNotNull();
		await Assert.That((int)response!.StatusCode).IsLessThan(500);
	}

	async Task WaitForLikeC4ServerRunningAsync(CancellationToken cancellationToken)
	{
		// Watch all resource events, filtering to the aspirec4 resource.
		// Report terminal states immediately rather than timing out silently.
		string? lastObservedState = null;
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(LikeC4StartupTimeout);

		try
		{
			await foreach (var evt in s_app!.ResourceNotifications.WatchAsync(cts.Token))
			{
				if (!evt.Resource.Name.Equals(AspireC4ResourceName, StringComparison.OrdinalIgnoreCase))
					continue;

				lastObservedState = evt.Snapshot.State?.Text;

				if (lastObservedState == KnownResourceStates.Running)
					return;

				// Terminal failure states — no point waiting further.
				if (
					lastObservedState == KnownResourceStates.FailedToStart
					|| lastObservedState == KnownResourceStates.RuntimeUnhealthy
					|| lastObservedState == KnownResourceStates.Exited
				)
				{
					throw new InvalidOperationException(
						$"LikeC4 container reached terminal state '{lastObservedState}' instead of Running."
					);
				}
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new TimeoutException(
				$"LikeC4 container did not reach Running within {LikeC4StartupTimeout.TotalSeconds}s. "
					+ $"Last observed state: '{lastObservedState ?? "(none)"}'"
			);
		}
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_GeneratesLikeC4ConfigFile(CancellationToken cancellationToken)
	{
		// Arrange
		// (shared app started in ClassSetUpAsync)
		var configPath = Path.Combine(s_outputDir!, "likec4.config.json");

		// Act
		var json = await File.ReadAllTextAsync(configPath, cancellationToken);

		// Assert
		await Assert.That(File.Exists(configPath)).IsTrue();
		await Assert.That(json).Contains("aspirec4");
		await Assert.That(json).Contains("AspireC4 Test App");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ConfigFileContainsExtensionsPath(CancellationToken cancellationToken)
	{
		// Arrange
		// (shared app started in ClassSetUpAsync)
		var configPath = Path.Combine(s_outputDir!, "likec4.config.json");

		// Act
		var json = await File.ReadAllTextAsync(configPath, cancellationToken);
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var includeFound = root.TryGetProperty("include", out var include);
		JsonElement paths = default;
		var pathsFound = includeFound && include.TryGetProperty("paths", out paths);
		var hasExtensionsPath =
			pathsFound
			&& paths
				.EnumerateArray()
				.Any(p => p.GetString()?.Contains("likec4-extensions", StringComparison.OrdinalIgnoreCase) == true);

		// Assert
		await Assert.That(includeFound).IsTrue();
		await Assert.That(pathsFound).IsTrue();
		await Assert.That(hasExtensionsPath).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ConfigFileContainsImageAlias(CancellationToken cancellationToken)
	{
		// Arrange
		// (shared app started in ClassSetUpAsync)
		var configPath = Path.Combine(s_outputDir!, "likec4.config.json");

		// Act
		var json = await File.ReadAllTextAsync(configPath, cancellationToken);
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var aliasesFound = root.TryGetProperty("imageAliases", out var aliases);
		var imageAliasFound = aliasesFound && aliases.TryGetProperty("@test-icons", out _);

		// Assert
		await Assert.That(aliasesFound).IsTrue();
		await Assert.That(imageAliasFound).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ImageAliasFolderContainsFiles()
	{
		// Arrange
		// (shared app started in ClassSetUpAsync)
		var imagesDir = Path.Combine(
			Path.GetDirectoryName(typeof(TestAppHostProgram).Assembly.Location)!,
			"likec4-images"
		);

		// Act
		var files = Directory.GetFiles(imagesDir);
		var svgCount = files.Count(f => f.EndsWith(".svg", StringComparison.OrdinalIgnoreCase));
		var pngCount = files.Count(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

		// Assert
		await Assert.That(Directory.Exists(imagesDir)).IsTrue();
		await Assert.That(svgCount).IsGreaterThan(0);
		await Assert.That(pngCount).IsGreaterThan(0);
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_GeneratedDslPassesValidation(CancellationToken cancellationToken)
	{
		// Arrange
		// (shared app started in ClassSetUpAsync)

		// Act
		var (totalErrors, rawOutput) = await RunLikeC4ValidateDirectoryAsync(s_outputDir!, cancellationToken);

		// Assert
		if (totalErrors < 0)
		{
			throw new InvalidOperationException($"LikeC4 validation did not produce JSON output.\n\n{rawOutput}");
		}

		if (totalErrors != 0)
		{
			throw new InvalidOperationException(
				$"Expected 0 LikeC4 validation errors but got {totalErrors}.\n\nValidator output:\n{rawOutput}"
			);
		}

		await Assert.That(totalErrors).IsEqualTo(0);
	}

	static async Task<(int TotalErrors, string RawOutput)> RunLikeC4ValidateDirectoryAsync(
		string directory,
		CancellationToken cancellationToken
	)
	{
		var useDotFlag = await Helpers.IsDotAvailableAsync(cancellationToken) ? " --use-dot" : "";

		string shellFile,
			shellArgs;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			shellFile = "cmd.exe";
			shellArgs = $"/c npx --yes likec4 validate --json --no-layout{useDotFlag} \"{directory}\"";
		}
		else
		{
			shellFile = "/bin/sh";
			shellArgs = $"-c \"npx --yes likec4 validate --json --no-layout{useDotFlag} '{directory}'\"";
		}

		using var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = shellFile,
				Arguments = shellArgs,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = directory,
				CreateNoWindow = true,
			},
		};

		process.Start();
		var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		var rawOutput = $"stdout:\n{stdout}\nstderr:\n{stderr}";

		var jsonStart = stdout.IndexOf('{', StringComparison.Ordinal);
		var jsonEnd = stdout.LastIndexOf('}');
		if (jsonStart < 0 || jsonEnd < 0)
		{
			return (-1, rawOutput);
		}

		var json = stdout[jsonStart..(jsonEnd + 1)];
		using var doc = JsonDocument.Parse(json);
		var stats = doc.RootElement.GetProperty("stats");
		var totalErrors = stats.GetProperty("totalErrors").GetInt32();
		return (totalErrors, rawOutput);
	}
}
