using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
	[Test]
	public async Task Build_DashboardLinks_WithBaseUrl_InjectsConsoleAndStructuredLogLinks()
	{
		// Arrange
		var resource = CreateProjectResource("my-api");

		// Act
		var model = ModelBuilder.Build([resource], dashboardBaseUrl: "https://localhost:15086");

		var links = model.Elements[0].Links;
		var consoleLink = links.FirstOrDefault(l => l.Title == "Dashboard: Console Logs");
		var structuredLink = links.FirstOrDefault(l => l.Title == "Dashboard: Structured Logs");

		// Assert
		await Assert.That(consoleLink).IsNotNull();
		await Assert.That(structuredLink).IsNotNull();
		await Assert.That(consoleLink!.Uri).IsEqualTo("https://localhost:15086/consolelogs/resource/my-api");
		await Assert.That(structuredLink!.Uri).IsEqualTo("https://localhost:15086/structuredlogs/resource/my-api");
	}

	[Test]
	public async Task Build_DashboardLinks_WithTokenAndBaseUrl_GeneratesLoginRedirectUrls()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build(
			[resource],
			dashboardBaseUrl: "https://localhost:15086",
			dashboardBrowserToken: "secret-token"
		);

		var links = model.Elements[0].Links;
		var consoleLink = links.First(l => l.Title == "Dashboard: Console Logs");
		var structuredLink = links.First(l => l.Title == "Dashboard: Structured Logs");

		var consolePath = Uri.EscapeDataString("/consolelogs/resource/api");
		var structuredPath = Uri.EscapeDataString("/structuredlogs/resource/api");
		var encodedToken = Uri.EscapeDataString("secret-token");

		// Assert
		await Assert
			.That(consoleLink.Uri)
			.IsEqualTo($"https://localhost:15086/login?t={encodedToken}&returnUrl={consolePath}");
		await Assert
			.That(structuredLink.Uri)
			.IsEqualTo($"https://localhost:15086/login?t={encodedToken}&returnUrl={structuredPath}");
	}

	[Test]
	public async Task Build_DashboardLinks_WithSpecialCharsInResourceName_EncodesNameInUrl()
	{
		// Arrange
		var resource = CreateProjectResource("my api+service");

		// Act
		var model = ModelBuilder.Build([resource], dashboardBaseUrl: "https://localhost:15086");

		var consoleLink = model.Elements[0].Links.FirstOrDefault(l => l.Title == "Dashboard: Console Logs");
		// Assert
		await Assert.That(consoleLink).IsNotNull();
		await Assert
			.That(consoleLink!.Uri)
			.IsEqualTo($"https://localhost:15086/consolelogs/resource/{Uri.EscapeDataString("my api+service")}");
	}

	[Test]
	public async Task Build_DashboardLinks_WithNoDashboardUrl_NoLinksInjected()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource], dashboardBaseUrl: null);

		var dashboardLinks = model
			.Elements[0]
			.Links.Where(l => l.Title?.StartsWith("Dashboard:", StringComparison.Ordinal) == true);
		// Assert
		await Assert.That(dashboardLinks).IsEmpty();
	}

	[Test]
	public async Task Build_DashboardLinks_Disabled_NoLinksInjectedEvenWithBaseUrl()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build(
			[resource],
			includeDashboardLinks: false,
			dashboardBaseUrl: "https://localhost:15086"
		);

		var dashboardLinks = model
			.Elements[0]
			.Links.Where(l => l.Title?.StartsWith("Dashboard:", StringComparison.Ordinal) == true);
		// Assert
		await Assert.That(dashboardLinks).IsEmpty();
	}

	[Test]
	public async Task Build_DashboardLinks_LinksInclusionDisabled_NoLinksInjected()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build(
			[resource],
			aspireMetadataInclusion: AspireMetadataInclusion.Metadata,
			dashboardBaseUrl: "https://localhost:15086"
		);

		var dashboardLinks = model
			.Elements[0]
			.Links.Where(l => l.Title?.StartsWith("Dashboard:", StringComparison.Ordinal) == true);
		// Assert
		await Assert.That(dashboardLinks).IsEmpty();
	}

	[Test]
	public async Task Build_DashboardLinks_UserLinkWithSameUriNotDuplicated()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		var expectedUrl = "https://localhost:15086/consolelogs/resource/api";
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithLink(expectedUrl, "My custom link"));

		// Act
		var model = ModelBuilder.Build([resource], dashboardBaseUrl: "https://localhost:15086");

		var matchingLinks = model.Elements[0].Links.Where(l => l.Uri == expectedUrl).ToList();
		// Assert
		await Assert.That(matchingLinks).Count().IsEqualTo(1);
		await Assert.That(matchingLinks[0].Title).IsEqualTo("My custom link");
	}
}
