using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
	[Test]
	public async Task Build_StateTagMap_RunningOverride_PrependsStateTagToElementTags()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.Running } };
		var stateTagMap = new Dictionary<string, string?> { [KnownResourceStates.Running] = "custom-running-tag" };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Tags).Contains("custom-running-tag");
	}

	[Test]
	public async Task Build_StateTagMap_NullOverride_DoesNotPrependTag()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.Running } };
		var stateTagMap = new Dictionary<string, string?> { [KnownResourceStates.Running] = null };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Tags).IsEmpty();
	}

	[Test]
	public async Task Build_StateTagMap_NullMap_AutoDerivesStateTagOnElement()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.Running } };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: null);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Tags).Contains("aspire-run-state-running");
	}

	[Test]
	public async Task Build_StateTagMap_StateTagPrependsBeforeUserTags()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithTag("backend").WithTag("v2"));

		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.FailedToStart } };
		var stateTagMap = new Dictionary<string, string?> { [KnownResourceStates.FailedToStart] = "custom-error-tag" };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		var tags = model.Elements[0].Tags;
		// Assert
		await Assert.That(tags[0]).IsEqualTo("custom-error-tag");
		await Assert.That(tags).Contains("backend");
		await Assert.That(tags).Contains("v2");
	}

	[Test]
	public async Task Build_StateTagMap_CustomTagName_UsedAsStateTag()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.RuntimeUnhealthy } };
		var stateTagMap = new Dictionary<string, string?>
		{
			[KnownResourceStates.RuntimeUnhealthy] = "my-custom-failed-tag",
		};

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		// Assert
		await Assert.That(model.Elements[0].Tags).Contains("my-custom-failed-tag");
	}
}
