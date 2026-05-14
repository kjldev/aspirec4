namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4NodeDetailsAnnotationTests
{
	[Test]
	public async Task Constructor_WithLabel_SetsLabel()
	{
		// Arrange

		// Act
		var annotation = CreateAnnotation("My Service");

		// Assert
		await Assert.That(annotation.Label).IsEqualTo("My Service");
		await Assert.That(annotation.Technology).IsNull();
		await Assert.That(annotation.Description).IsNull();
		await Assert.That(annotation.AutoIconEnabled).IsNull();
	}

	[Test]
	public async Task WithTechnology_WhenCalled_SetsTechnology()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act
		annotation.WithTechnology("ASP.NET Core");

		// Assert
		await Assert.That(annotation.Technology).IsEqualTo("ASP.NET Core");
	}

	[Test]
	public async Task WithDescription_WhenCalled_SetsDescription()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act
		annotation.WithDescription("A web service");

		// Assert
		await Assert.That(annotation.Description).IsEqualTo("A web service");
	}

	[Test]
	public async Task WithSummary_WhenCalled_SetsSummary()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act
		annotation.WithSummary("A summary");

		// Assert
		await Assert.That(annotation.Summary).IsEqualTo("A summary");
	}

	[Test]
	public async Task WithIcon_WhenCalled_SetsIcon()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act
		annotation.WithIcon("tech:dotnet");

		// Assert
		await Assert.That(annotation.Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task WithAutoIcon_WhenCalledWithFalse_SetsAutoIconEnabledFalse()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act
		annotation.WithAutoIcon(false);

		// Assert
		await Assert.That(annotation.AutoIconEnabled).IsFalse();
	}

	[Test]
	public async Task WithKind_WhenCalled_SetsKind()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act
		annotation.WithKind("service");

		// Assert
		await Assert.That(annotation.Kind).IsEqualTo("service");
	}

	[Test]
	public async Task WithLabel_UpdatesLabel()
	{
		// Arrange
		var annotation = CreateAnnotation("original");

		// Act
		annotation.WithLabel("updated");

		// Assert
		await Assert.That(annotation.Label).IsEqualTo("updated");
	}

	[Test]
	public async Task Constructor_WithEmptyLabel_Throws()
	{
		// Arrange

		// Act / Assert
		await Assert.That(() => new LikeC4NodeDetailsAnnotation("")).Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_WithWhiteSpaceLabel_Throws()
	{
		// Arrange

		// Act / Assert
		await Assert.That(() => new LikeC4NodeDetailsAnnotation("   ")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithIcon_WithWhiteSpace_Throws()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act / Assert
		await Assert.That(() => annotation.WithIcon("   ")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithTag_WithHashPrefix_NormalizesTag()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act
		annotation.WithTag("#external");

		// Assert
		await Assert.That(annotation.Tags).Contains("external");
		await Assert.That(annotation.Tags).DoesNotContain("#external");
	}

	[Test]
	public async Task WithTag_WithHashAndNonHashVersionsOfSameTag_BothNormalized()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act
		annotation.WithTag("#external").WithTag("external");

		// Assert
		await Assert.That(annotation.Tags.All(t => !t.StartsWith('#'))).IsTrue();
	}

	[Test]
	public async Task WithTag_WithHashOnly_Throws()
	{
		// Arrange
		var annotation = CreateAnnotation();

		// Act / Assert
		await Assert.That(() => annotation.WithTag("#")).Throws<ArgumentException>();
	}

	static LikeC4NodeDetailsAnnotation CreateAnnotation(string label = "My Service") => new(label);
}
