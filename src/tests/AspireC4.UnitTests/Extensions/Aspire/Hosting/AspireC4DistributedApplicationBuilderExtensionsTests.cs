using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.DependencyInjection;
using static Aspire.Hosting.TestHelpers;

namespace Aspire.Hosting;

public sealed class AspireC4DistributedApplicationBuilderExtensionsTests
{
	[Test]
	public async Task AddAspireC4_ExposesHttpAndHmrEndpoints()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4();
		var endpoints = visualization
			.LikeC4ResourceBuilder.Resource.Annotations.OfType<EndpointAnnotation>()
			.OrderBy(endpoint => endpoint.Name, StringComparer.Ordinal)
			.ToArray();

		// Assert
		await Assert.That(endpoints).Count().IsEqualTo(2);
		await Assert.That(endpoints[0].Name).IsEqualTo(LikeC4ServerResource.HttpEndpointName);
		await Assert.That(endpoints[0].TargetPort).IsEqualTo(LikeC4ServerResource.DefaultContainerServePort);
		await Assert.That(endpoints[1].Name).IsEqualTo(LikeC4ServerResource.HMREndpointName);
		await Assert.That(endpoints[1].TargetPort).IsEqualTo(LikeC4ServerResource.DefaultContainerUpdatePort);
		await Assert.That(endpoints[1].Port).IsNull();
	}

	[Test]
	public async Task AddAspireC4_UsesHmrRelayForFixedPortMode()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.55.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContainerWorkspaceOptions>>();

		// Assert
		await Assert.That(workspaceOptions.Value.UseHMRRelay).IsTrue();
	}

	[Test]
	public async Task AddAspireC4_UsesHmrRelayOnWindows()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var expectedUseRelay = OperatingSystem.IsWindows();

		// Act
		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.56.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContainerWorkspaceOptions>>();

		// Assert
		await Assert.That(workspaceOptions.Value.UseHMRRelay).IsEqualTo(expectedUseRelay);
	}

	[Test]
	public async Task AddAspireC4_StoresLegacyHmrCompatibilityMode()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.55.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContainerWorkspaceOptions>>();

		// Assert
		await Assert.That(workspaceOptions.Value.HMRPortMode).IsEqualTo(HMRPortMode.FixedPort);
	}

	[Test]
	public async Task AddAspireC4_StoresConfigurableHmrCompatibilityModeForCurrentMinimumVersion()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "100.57.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContainerWorkspaceOptions>>();

		// Assert
		await Assert.That(workspaceOptions.Value.HMRPortMode).IsEqualTo(HMRPortMode.Configurable);
	}

	[Test]
	public async Task AddAspireC4_HasNoContainerMountAnnotationsAtConfigureTime()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4();
		var mounts = visualization
			.LikeC4ResourceBuilder.Resource.Annotations.OfType<ContainerMountAnnotation>()
			.ToArray();

		// Assert
		await Assert.That(mounts).IsEmpty();
	}

	[Test]
	public async Task AddAspireC4_CreatesConfiguredOutputDirectory()
	{
		// Arrange
		var outputDir = Path.Combine(Path.GetTempPath(), "likec4-unit-" + Guid.NewGuid().ToString("N")[..8]);

		try
		{
			var appBuilder = CreateAppBuilder();

			// Act
			appBuilder.AddAspireC4(configure: opts => opts.OutputDirectory = outputDir);

			// Assert
			await Assert.That(Directory.Exists(outputDir)).IsTrue();
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
