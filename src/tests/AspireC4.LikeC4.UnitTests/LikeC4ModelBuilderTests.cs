using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LikeC4;
using NSubstitute;

namespace Aspire.Hosting.LikeC4;

public sealed class LikeC4ModelBuilderTests
{
    [Test]
    public async Task Build_WithNoResources_ReturnsEmptyModel()
    {
        var model = LikeC4ModelBuilder.Build([]);

        await Assert.That(model.Elements).IsEmpty();
        await Assert.That(model.Relationships).IsEmpty();
    }

    [Test]
    public async Task Build_ProjectResource_MapsToComponentKind()
    {
        var resource = CreateProjectResource("api");

        var model = LikeC4ModelBuilder.Build([resource]);

        await Assert.That(model.Elements).Count().IsEqualTo(1);
        await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Component);
        await Assert.That(model.Elements[0].Name).IsEqualTo("api");
    }

    [Test]
    public async Task Build_ContainerResource_MapsToContainerKind()
    {
        var resource = CreateContainerResource("redis");

        var model = LikeC4ModelBuilder.Build([resource]);

        await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Container);
    }

    [Test]
    public async Task Build_ExecutableResource_MapsToExecutableKind()
    {
        var resource = new ExecutableResource("worker", "dotnet", ".");
        resource.Annotations.Add(new ResourceSnapshotAnnotation(new CustomResourceSnapshot
        {
            ResourceType = "Executable",
            Properties = [],
        }));

        var model = LikeC4ModelBuilder.Build([resource]);

        await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Executable);
    }

    [Test]
    public async Task Build_ResourceWithConnectionString_MapsToDatabaseKind()
    {
        var resource = new TestDatabaseResource("db");

        var model = LikeC4ModelBuilder.Build([resource]);

        await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Database);
    }

    [Test]
    public async Task Build_UnknownResource_MapsToSystemKind()
    {
        var resource = new TestSystemResource("ext");

        var model = LikeC4ModelBuilder.Build([resource]);

        await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.System);
    }

    [Test]
    public async Task Build_ResourceWithExcludeAnnotation_IsSkipped()
    {
        var resource = CreateProjectResource("api");
        resource.Annotations.Add(new ExcludeFromLikeC4Annotation());

        var model = LikeC4ModelBuilder.Build([resource]);

        await Assert.That(model.Elements).IsEmpty();
    }

    [Test]
    public async Task Build_HiddenResource_IsSkipped()
    {
        var resource = new TestSystemResource("hidden");
        resource.Annotations.Add(new ResourceSnapshotAnnotation(new CustomResourceSnapshot
        {
            ResourceType = "System",
            Properties = [],
            IsHidden = true,
        }));

        var model = LikeC4ModelBuilder.Build([resource]);

        await Assert.That(model.Elements).IsEmpty();
    }

    [Test]
    public async Task Build_ResourceWithNodeDetailsAnnotation_UsesAnnotationValues()
    {
        var resource = CreateProjectResource("api");
        resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API Service", "ASP.NET Core", "Handles HTTP requests"));

        var model = LikeC4ModelBuilder.Build([resource]);

        var element = model.Elements[0];
        await Assert.That(element.Label).IsEqualTo("API Service");
        await Assert.That(element.Technology).IsEqualTo("ASP.NET Core");
        await Assert.That(element.Description).IsEqualTo("Handles HTTP requests");
    }

    [Test]
    public async Task Build_ResourceWithNoNodeDetailsAnnotation_UsesResourceNameAsLabel()
    {
        var resource = CreateProjectResource("my-api");

        var model = LikeC4ModelBuilder.Build([resource]);

        await Assert.That(model.Elements[0].Label).IsEqualTo("my-api");
    }

    [Test]
    public async Task Build_ResourceRelationshipAnnotation_CreatesRelationship()
    {
        var api = CreateProjectResource("api");
        var db = new TestDatabaseResource("db");
        api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

        var model = LikeC4ModelBuilder.Build([api, db]);

        await Assert.That(model.Relationships).Count().IsEqualTo(1);
        var rel = model.Relationships[0];
        await Assert.That(rel.SourceName).IsEqualTo("api");
        await Assert.That(rel.TargetName).IsEqualTo("db");
    }

    [Test]
    public async Task Build_WaitForRelationship_IsSkipped()
    {
        var api = CreateProjectResource("api");
        var db = new TestDatabaseResource("db");
        api.Annotations.Add(new ResourceRelationshipAnnotation(db, "WaitFor"));

        var model = LikeC4ModelBuilder.Build([api, db]);

        await Assert.That(model.Relationships).IsEmpty();
    }

    [Test]
    public async Task Build_DuplicateRelationship_IsDeduped()
    {
        var api = CreateProjectResource("api");
        var db = new TestDatabaseResource("db");
        api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
        api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

        var model = LikeC4ModelBuilder.Build([api, db]);

        await Assert.That(model.Relationships).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Build_RelationshipToExcludedTarget_IsSkipped()
    {
        var api = CreateProjectResource("api");
        var hidden = new TestSystemResource("infra");
        hidden.Annotations.Add(new ExcludeFromLikeC4Annotation());
        api.Annotations.Add(new ResourceRelationshipAnnotation(hidden, "Reference"));

        var model = LikeC4ModelBuilder.Build([api, hidden]);

        await Assert.That(model.Relationships).IsEmpty();
    }

    [Test]
    public async Task Build_ResourceWithParent_SetsParentName()
    {
        var parent = CreateContainerResource("postgres");
        var child = new TestChildResource("postgres-db", parent);

        var model = LikeC4ModelBuilder.Build([parent, child]);

        var childElement = model.Elements.Single(e => e.Name == "postgres-db");
        await Assert.That(childElement.ParentName).IsEqualTo("postgres");
    }

    [Test]
    public async Task Build_NonReferenceRelationshipType_SetsLabel()
    {
        var api = CreateProjectResource("api");
        var queue = new TestSystemResource("queue");
        api.Annotations.Add(new ResourceRelationshipAnnotation(queue, "Publishes"));

        var model = LikeC4ModelBuilder.Build([api, queue]);

        await Assert.That(model.Relationships[0].Label).IsEqualTo("Publishes");
    }

    // --- Helpers ---

    private static ProjectResource CreateProjectResource(string name)
    {
        var resource = new ProjectResource(name);
        return resource;
    }

    private static ContainerResource CreateContainerResource(string name)
    {
        var resource = new ContainerResource(name);
        return resource;
    }

    private sealed class TestDatabaseResource(string name)
        : Resource(name), IResourceWithConnectionString
    {
        public ReferenceExpression ConnectionStringExpression =>
            ReferenceExpression.Create($"host=localhost");
    }

    private sealed class TestSystemResource(string name) : Resource(name);

    private sealed class TestChildResource(string name, IResource parent)
        : Resource(name), IResourceWithParent
    {
        public IResource Parent { get; } = parent;
    }
}
