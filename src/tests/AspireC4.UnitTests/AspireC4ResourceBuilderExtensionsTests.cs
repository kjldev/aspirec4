namespace Aspire.Hosting.AspireC4;

public sealed class AspireC4ResourceBuilderExtensionsTests
{
	[Test]
	public async Task WithLikeC4Details_ActionOverload_ConfiguresAnnotation()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var resourceBuilder = appBuilder.AddExecutable("worker", "dotnet", ".");

		// Act
		resourceBuilder.WithLikeC4Details(a =>
			a.WithTechnology(".NET").WithDescription("Background job").WithAutoIcon(false)
		);

		// Assert
		var annotation = resourceBuilder.Resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().Last();
		await Assert.That(annotation.Label).IsEqualTo("worker");
		await Assert.That(annotation.Technology).IsEqualTo(".NET");
		await Assert.That(annotation.Description).IsEqualTo("Background job");
		await Assert.That(annotation.AutoIconEnabled).IsFalse();
	}

	private static IDistributedApplicationBuilder CreateAppBuilder() => DistributedApplication.CreateBuilder([]);
}
