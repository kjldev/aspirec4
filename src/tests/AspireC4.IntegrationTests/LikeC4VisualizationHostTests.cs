using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Integration tests that verify the full LikeC4 plugin lifecycle using a test Aspire application.
/// Tests that require a running LikeC4 server are skipped when Node.js is not available.
/// </summary>
public sealed class LikeC4VisualizationHostTests : IAsyncDisposable
{
	DistributedApplication? _app;
	string? _outputDir;

	static async Task<bool> IsDockerAvailableAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			linkedCts.CancelAfter(5_000);

			using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = "docker",
				Arguments = "info",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			});
			if (p is null)
				return false;

			await p.WaitForExitAsync(linkedCts.Token);
			return p.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	[Before(Test)]
	public async Task SetUpAsync(CancellationToken cancellationToken)
	{
		_outputDir = Path.Combine(Path.GetTempPath(), "likec4-tests-" + Guid.NewGuid().ToString("N")[..8]);

		var appBuilder = await DistributedApplicationTestingBuilder.CreateAsync<TestAppHostProgram>(cancellationToken);

		appBuilder.AddLikeC4Visualization(opts =>
		{
			opts.Title = "Integration Test Architecture";
			opts.OutputDirectory = _outputDir;
			opts.FileName = "test-model";
		});

		// Add a simple executable resource for diagram content verification.
		appBuilder.AddExecutable("fake-service", "dotnet", ".", "--version")
			.WithLikeC4Details(label: "Fake Service", technology: ".NET", description: "A fake service for testing");

		_app = await appBuilder.BuildAsync(cancellationToken);
		await _app.StartAsync(cancellationToken);
	}

	[After(Test)]
	public async Task TearDownAsync(CancellationToken cancellationToken)
	{
		if (_app is not null)
		{
			await _app.StopAsync(cancellationToken);
		}

		if (_outputDir is not null && Directory.Exists(_outputDir))
		{
			Directory.Delete(_outputDir, recursive: true);
		}
	}

	[Test]
	public async Task C4FileIsCreatedOnDisk()
	{
		var path = Path.Combine(_outputDir!, "test-model.c4");

		await Assert.That(File.Exists(path)).IsTrue();
	}

	[Test]
	public async Task C4FileContainsValidDslStructure(CancellationToken cancellationToken)
	{
		var path = Path.Combine(_outputDir!, "test-model.c4");
		var content = await File.ReadAllTextAsync(path, cancellationToken);

		await Assert.That(content).Contains("specification {");
		await Assert.That(content).Contains("model {");
		await Assert.That(content).Contains("views {");
		await Assert.That(content).Contains("title 'Integration Test Architecture'");
	}

	[Test]
	public async Task C4FileContainsAddedResource(CancellationToken cancellationToken)
	{
		var path = Path.Combine(_outputDir!, "test-model.c4");
		var content = await File.ReadAllTextAsync(path, cancellationToken);

		// Resource names are sanitized (hyphens become underscores) in LikeC4 identifiers.
		await Assert.That(content).Contains("fake_service");
		await Assert.That(content).Contains("Fake Service");
	}

	[Test]
	public async Task LikeC4ServerResource_ReachesRunningState(CancellationToken cancellationToken)
	{
		await Assert.That(await IsDockerAvailableAsync(cancellationToken)).IsTrue();

		await _app!.ResourceNotifications.WaitForResourceAsync("likec4-server", KnownResourceStates.Running, cancellationToken)
			.WaitAsync(TimeSpan.FromSeconds(120), cancellationToken);
	}

	[Test]
	public async Task LikeC4ServerEndpoint_ReturnsSuccess(CancellationToken cancellationToken)
	{
		await Assert.That(await IsDockerAvailableAsync(cancellationToken)).IsTrue();

		await _app!.ResourceNotifications.WaitForResourceAsync("likec4-server", KnownResourceStates.Running, cancellationToken)
			.WaitAsync(TimeSpan.FromSeconds(120), cancellationToken);

		using var client = _app!.CreateHttpClient("likec4-server", LikeC4ServerResource.HttpEndpointName);

		// The LikeC4 Vite dev server may take a few seconds to fully initialize after the
		// container reaches Running state; retry with back-off.
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

		await Assert.That(response).IsNotNull();
		await Assert.That((int)response!.StatusCode).IsLessThan(500);
	}

	public async ValueTask DisposeAsync()
	{
		if (_app is not null)
		{
			await _app.DisposeAsync();
		}
	}
}
