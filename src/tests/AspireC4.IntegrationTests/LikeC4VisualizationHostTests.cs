using System.Net.WebSockets;
using System.Globalization;
using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Integration tests that verify the LikeC4 visualization starts successfully in a real Aspire app host.
/// </summary>
[NotInParallel]
public sealed class LikeC4VisualizationHostTests : IAsyncDisposable
{
	const string LikeC4ResourceName = "likec4-visualization";
	static readonly TimeSpan LikeC4StartupTimeout = TimeSpan.FromSeconds(120);

	DistributedApplication? _app;
	EnvironmentVariableScope? _containerRuntimeScope;
	EnvironmentVariableScope? _outputDirectoryScope;
	EnvironmentVariableScope? _fileNameScope;
	EnvironmentVariableScope? _titleScope;
	string? _outputDir;
	string? _modelPath;

	static async Task<bool> IsDockerAvailableAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			linkedCts.CancelAfter(5_000);

			using var process = System.Diagnostics.Process.Start(
				new System.Diagnostics.ProcessStartInfo
				{
					FileName = "docker",
					Arguments = "info",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			);

			if (process is null)
			{
				return false;
			}

			await process.WaitForExitAsync(linkedCts.Token);
			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	[Before(Test)]
	public async Task SetUpAsync(CancellationToken cancellationToken)
	{
		await Assert.That(await IsDockerAvailableAsync(cancellationToken)).IsTrue();

		_outputDir = Path.Combine(Path.GetTempPath(), "likec4-integration-" + Guid.NewGuid().ToString("N")[..8]);
		_modelPath = Path.Combine(_outputDir, "model.c4");
		_containerRuntimeScope = new EnvironmentVariableScope("ASPIRE_CONTAINER_RUNTIME", "docker");
		_outputDirectoryScope = new EnvironmentVariableScope("LikeC4__OutputDirectory", _outputDir);
		_fileNameScope = new EnvironmentVariableScope("LikeC4__FileName", "model");
		_titleScope = new EnvironmentVariableScope("LikeC4__Title", "Integration Test Architecture");

		var appBuilder = await DistributedApplicationTestingBuilder.CreateAsync<TestAppHostProgram>(cancellationToken);
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

		_titleScope?.Dispose();
		_titleScope = null;
		_fileNameScope?.Dispose();
		_fileNameScope = null;
		_outputDirectoryScope?.Dispose();
		_outputDirectoryScope = null;
		_containerRuntimeScope?.Dispose();
		_containerRuntimeScope = null;
	}

	[Test]
	public async Task C4FileIsGeneratedDuringStartup()
	{
		await Assert.That(_modelPath).IsNotNull();
		await Assert.That(File.Exists(_modelPath!)).IsTrue();
	}

	[Test]
	public async Task C4FileContainsExpectedDslStructure(CancellationToken cancellationToken)
	{
		var content = await File.ReadAllTextAsync(_modelPath!, cancellationToken);

		await Assert.That(content).Contains("specification {");
		await Assert.That(content).Contains("model {");
		await Assert.That(content).Contains("views {");
		await Assert.That(content).Contains("node_app");
		await Assert.That(content).Contains("Integration Test Architecture");
	}

	[Test]
	public async Task LikeC4Visualization_ReachesRunningState(CancellationToken cancellationToken)
	{
		await WaitForLikeC4ServerRunningAsync(cancellationToken);
	}

	[Test]
	public async Task LikeC4Visualization_EndpointReturnsSuccess(CancellationToken cancellationToken)
	{
		await WaitForLikeC4ServerRunningAsync(cancellationToken);

		using var client = _app!.CreateHttpClient(LikeC4ResourceName, LikeC4ServerResource.HttpEndpointName);

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

	[Test]
	public async Task LikeC4Visualization_HmrEndpointAcceptsWebSocketConnections(CancellationToken cancellationToken)
	{
		await WaitForLikeC4ServerRunningAsync(cancellationToken);

		using var client = _app!.CreateHttpClient(LikeC4ResourceName, LikeC4ServerResource.HttpEndpointName);
		var viteClient = await client.GetStringAsync("/@vite/client", cancellationToken);
		var tokenMatch = Regex.Match(viteClient, "wsToken = \\\"([^\\\"]+)\\\"");
		var portMatch = Regex.Match(viteClient, "hmrPort = (\\d+)");

		await Assert.That(tokenMatch.Success).IsTrue();
		await Assert.That(portMatch.Success).IsTrue();
		await Assert.That(int.Parse(portMatch.Groups[1].Value, CultureInfo.InvariantCulture))
			.IsEqualTo(LikeC4ServerResource.DefaultContainerUpdatePort);

		using var socket = new ClientWebSocket();
		socket.Options.AddSubProtocol("vite-hmr");

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		linkedCts.CancelAfter(TimeSpan.FromSeconds(10));

		await socket.ConnectAsync(
			new Uri($"ws://127.0.0.1:{LikeC4ServerResource.DefaultContainerUpdatePort}/?token={tokenMatch.Groups[1].Value}"),
			linkedCts.Token
		);

		await Assert.That(socket.State).IsEqualTo(WebSocketState.Open);
	}

	async Task WaitForLikeC4ServerRunningAsync(CancellationToken cancellationToken)
	{
		await _app!.ResourceNotifications.WaitForResourceAsync(
				LikeC4ResourceName,
				KnownResourceStates.Running,
				cancellationToken
			)
			.WaitAsync(LikeC4StartupTimeout, cancellationToken);
	}
	public async ValueTask DisposeAsync()
	{
		if (_app is not null)
		{
			await _app.DisposeAsync();
		}

		if (_outputDir is not null && Directory.Exists(_outputDir))
		{
			Directory.Delete(_outputDir, recursive: true);
		}

		_titleScope?.Dispose();
		_fileNameScope?.Dispose();
		_outputDirectoryScope?.Dispose();
		_containerRuntimeScope?.Dispose();
	}

	sealed class EnvironmentVariableScope : IDisposable
	{
		readonly string _name;
		readonly string? _previousValue;

		public EnvironmentVariableScope(string name, string value)
		{
			_name = name;
			_previousValue = Environment.GetEnvironmentVariable(name);
			Environment.SetEnvironmentVariable(name, value);
		}

		public void Dispose()
		{
			Environment.SetEnvironmentVariable(_name, _previousValue);
		}
	}
}
