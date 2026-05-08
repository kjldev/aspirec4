using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LikeC4;

namespace Aspire.Hosting.LikeC4;

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

public sealed class LikeC4DiagramOptionsTests
{
    [Test]
    public async Task DefaultValues_AreCorrect()
    {
        var opts = new LikeC4DiagramOptions();

        await Assert.That(opts.Title).IsEqualTo("Architecture");
        await Assert.That(opts.OutputDirectory).IsEqualTo("./likec4");
        await Assert.That(opts.FileName).IsEqualTo("model");
        await Assert.That(opts.ContainerImageTag).IsNull();
    }
}
