using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LikeC4;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.LikeC4;

/// <summary>
/// Integration tests that verify the full LikeC4 plugin lifecycle using a test Aspire application.
/// Tests that require a running LikeC4 server are skipped when Node.js is not available.
/// </summary>
public sealed class LikeC4VisualizationHostTests : IAsyncDisposable
{
    private DistributedApplication? _app;
    private string? _outputDir;

    private static bool IsNodeAvailable()
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "node",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(3_000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [Before(Test)]
    public async Task SetUpAsync()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "likec4-tests-" + Guid.NewGuid().ToString("N")[..8]);

        var appBuilder = await DistributedApplicationTestingBuilder.CreateAsync<TestAppHostProgram>();

        appBuilder.AddLikeC4Visualization(opts =>
        {
            opts.Title = "Integration Test Architecture";
            opts.OutputDirectory = _outputDir;
            opts.FileName = "test-model";
        });

        // Add a simple executable resource for diagram content verification.
        appBuilder.AddExecutable("fake-service", "dotnet", ".", "--version")
            .WithLikeC4Details(label: "Fake Service", technology: ".NET", description: "A fake service for testing");

        _app = await appBuilder.BuildAsync();
        await _app.StartAsync();
    }

    [After(Test)]
    public async Task TearDownAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
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
    public async Task C4FileContainsValidDslStructure()
    {
        var path = Path.Combine(_outputDir!, "test-model.c4");
        var content = await File.ReadAllTextAsync(path);

        await Assert.That(content).Contains("specification {");
        await Assert.That(content).Contains("model {");
        await Assert.That(content).Contains("views {");
        await Assert.That(content).Contains("title 'Integration Test Architecture'");
    }

    [Test]
    public async Task C4FileContainsAddedResource()
    {
        var path = Path.Combine(_outputDir!, "test-model.c4");
        var content = await File.ReadAllTextAsync(path);

        // Resource names are sanitized (hyphens become underscores) in LikeC4 identifiers.
        await Assert.That(content).Contains("fake_service");
        await Assert.That(content).Contains("Fake Service");
    }

    [Test]
    [Skip("Requires Node.js and npx likec4 to be installed; run manually.")]
    public async Task LikeC4ServerResource_ReachesRunningState()
    {
        await _app!.ResourceNotifications.WaitForResourceAsync("likec4-server", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(60));
    }

    [Test]
    [Skip("Requires Node.js and npx likec4 to be installed; run manually.")]
    public async Task LikeC4ServerEndpoint_ReturnsSuccess()
    {
        await _app!.ResourceNotifications.WaitForResourceAsync("likec4-server", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(60));

        using var client = _app!.CreateHttpClient("likec4-server", LikeC4ServerResource.HttpEndpointName);
        var response = await client.GetAsync("/");

        await Assert.That((int)response.StatusCode).IsLessThan(500);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
