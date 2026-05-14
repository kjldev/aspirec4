using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
	[Test]
	public async Task Build_StrictGroups_AllowedGroup_DoesNotThrow()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4GroupAnnotation("Frontend"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Groups, groups: ["Frontend"]);

		// Act / Assert
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictGroups_DisallowedGroup_ThrowsInvalidOperationException()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4GroupAnnotation("UnknownGroup"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Groups, groups: ["Frontend", "Backend"]);

		// Act / Assert
		await Assert
			.That(() => ModelBuilder.Build([resource], strict: strict))
			.ThrowsException()
			.WithMessageContaining("UnknownGroup", StringComparison.Ordinal);
	}

	[Test]
	public async Task Build_StrictGroups_NoGroup_DoesNotThrow()
	{
		// Arrange — resource without a group should not be validated
		var resource = CreateProjectResource("api");
		var strict = CreateStrictOptions(AspireC4StrictMode.Groups, groups: []);

		// Act / Assert
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictGroups_CaseInsensitiveGroupName_DoesNotThrow()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4GroupAnnotation("frontend"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Groups, groups: ["Frontend"]);

		// Act / Assert — comparison should be case-insensitive
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}
}
