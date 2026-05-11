using System.Diagnostics;
using System.Globalization;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Integration tests that verify the LikeC4 visualization starts successfully in a real Aspire app host.
/// </summary>
[NotInParallel]
public sealed partial class AspireC4HostTests : IAsyncDisposable
{
	const string AspireC4ResourceName = AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName;
	static readonly TimeSpan LikeC4StartupTimeout = TimeSpan.FromSeconds(120);

	DistributedApplication? _app;
	EnvironmentVariableScope? _outputDirectoryScope;
	EnvironmentVariableScope? _fileNameScope;
	EnvironmentVariableScope? _titleScope;
	string? _outputDir;
	string? _modelPath;

	[Before(Test)]
	public async Task SetUpAsync(CancellationToken cancellationToken)
	{
		_outputDir = Path.Combine(Path.GetTempPath(), "likec4-integration-" + Guid.NewGuid().ToString("N")[..8]);
		_modelPath = Path.Combine(_outputDir, "model.gen.c4");
		_outputDirectoryScope = new EnvironmentVariableScope("AspireC4__OutputDirectory", _outputDir);
		_fileNameScope = new EnvironmentVariableScope("AspireC4__FileName", "model.gen");
		_titleScope = new EnvironmentVariableScope("AspireC4__Title", "Integration Test Architecture");

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

		using var client = _app!.CreateHttpClient(AspireC4ResourceName, LikeC4ServerResource.HttpEndpointName);

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

		using var client = _app!.CreateHttpClient(AspireC4ResourceName, LikeC4ServerResource.HttpEndpointName);
		var viteClient = await client.GetStringAsync("/@vite/client", cancellationToken);
		var tokenMatch = WSTokenRegex().Match(viteClient);
		var portMatch = HMRPortRegex().Match(viteClient);

		await Assert.That(tokenMatch.Success).IsTrue();
		await Assert.That(portMatch.Success).IsTrue();
		await Assert
			.That(int.Parse(portMatch.Groups[1].Value, CultureInfo.InvariantCulture))
			.IsEqualTo(LikeC4ServerResource.DefaultContainerUpdatePort);

		using var socket = new ClientWebSocket();
		socket.Options.AddSubProtocol("vite-hmr");

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		linkedCts.CancelAfter(TimeSpan.FromSeconds(10));

		await socket.ConnectAsync(
			new Uri(
				$"ws://127.0.0.1:{LikeC4ServerResource.DefaultContainerUpdatePort}/?token={tokenMatch.Groups[1].Value}"
			),
			linkedCts.Token
		);

		await Assert.That(socket.State).IsEqualTo(WebSocketState.Open);
	}

	async Task WaitForLikeC4ServerRunningAsync(CancellationToken cancellationToken)
	{
		await _app!
			.ResourceNotifications.WaitForResourceAsync(
				AspireC4ResourceName,
				KnownResourceStates.Running,
				cancellationToken
			)
			.WaitAsync(LikeC4StartupTimeout, cancellationToken);
	}

	[Test]
	public async Task ConfigFile_IsGeneratedInOutputDirectory(CancellationToken cancellationToken)
	{
		var configPath = Path.Combine(_outputDir!, "likec4.config.json");
		await Assert.That(File.Exists(configPath)).IsTrue();

		var json = await File.ReadAllTextAsync(configPath, cancellationToken);
		await Assert.That(json).Contains("aspirec4");
		await Assert.That(json).Contains("Integration Test Architecture");
		// The extensions folder is an include path; the config must reference it.
		await Assert.That(json).Contains("likec4-extensions");
	}

	[Test]
	public async Task GeneratedAndAdditionalDSLFiles_PassLikeC4Validate(CancellationToken cancellationToken)
	{
		var (totalErrors, rawOutput) = await RunLikeC4ValidateDirectoryAsync(_outputDir!, cancellationToken);

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
		var useDotFlag = IsDotAvailable() ? " --use-dot" : "";

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

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "dot availability check is best-effort; any failure means dot is unavailable"
	)]
	static bool IsDotAvailable()
	{
		try
		{
			using var proc = Process.Start(
				new ProcessStartInfo
				{
					FileName = "dot",
					Arguments = "-V",
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			);
			proc?.WaitForExit();
			return proc?.ExitCode == 0;
		}
		catch
		{
			return false;
		}
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

	[GeneratedRegex("hmrPort = (\\d+)")]
	private static partial Regex HMRPortRegex();

	[GeneratedRegex("wsToken = \\\"([^\\\"]+)\\\"")]
	private static partial Regex WSTokenRegex();
}
