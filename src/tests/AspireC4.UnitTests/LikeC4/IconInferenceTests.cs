namespace Aspire.Hosting.AspireC4.LikeC4;

/// <summary>
/// Data-driven icon inference tests. Each <see cref="IconTestScenario"/> declares the resource
/// configuration (control) and the expected LikeC4 icon string (desired state). To add coverage
/// for a new resource, add a single entry to <see cref="Scenarios"/>.
/// </summary>
public sealed partial class IconInferenceTests
{
	[Test]
	[MethodDataSource(nameof(Scenarios))]
	[DisplayName("Inferring LikeC4 icon from Aspire resource: ${scenario.Name}")]
	public async Task Build_WithConfiguredResource_InfersExpectedIcon(IconTestScenario scenario)
	{
		// Arrange
		var (visible, hidden) = scenario.CreateResources();
		IReadOnlyList<IResource> resources = hidden is null ? [visible] : [visible, hidden];

		// Act
		var model = ModelBuilder.Build(resources);

		// Assert
		var element = model.Elements.Single(e => e.Name == visible.Name);
		await Assert.That(element.Icon).IsEqualTo(scenario.ExpectedIcon);
	}
}
