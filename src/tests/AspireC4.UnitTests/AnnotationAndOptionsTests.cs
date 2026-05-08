namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4NodeDetailsAnnotationTests
{
	[Test]
	public async Task Constructor_WithLabel_SetsLabel()
	{
		var annotation = new LikeC4NodeDetailsAnnotation("My Service");

		await Assert.That(annotation.Label).IsEqualTo("My Service");
		await Assert.That(annotation.Technology).IsNull();
		await Assert.That(annotation.Description).IsNull();
	}

	[Test]
	public async Task Constructor_WithAllParameters_SetsAllProperties()
	{
		var annotation = new LikeC4NodeDetailsAnnotation("My Service", "ASP.NET Core", "A web service");

		await Assert.That(annotation.Label).IsEqualTo("My Service");
		await Assert.That(annotation.Technology).IsEqualTo("ASP.NET Core");
		await Assert.That(annotation.Description).IsEqualTo("A web service");
	}

	[Test]
	public async Task Constructor_WithEmptyLabel_Throws()
	{
		await Assert.That(() => new LikeC4NodeDetailsAnnotation(""))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_WithWhiteSpaceLabel_Throws()
	{
		await Assert.That(() => new LikeC4NodeDetailsAnnotation("   "))
			.Throws<ArgumentException>();
	}
}

