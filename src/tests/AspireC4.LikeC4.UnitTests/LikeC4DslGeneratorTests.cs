using Aspire.Hosting.LikeC4;

namespace Aspire.Hosting.LikeC4;

public sealed class LikeC4DslGeneratorTests
{
    private static readonly LikeC4DiagramOptions DefaultOptions = new()
    {
        Title = "Test Architecture",
        OutputDirectory = "./likec4",
        FileName = "model",
    };

    [Test]
    public async Task Generate_EmptyModel_ProducesMinimalValidDsl()
    {
        var dsl = LikeC4DslGenerator.Generate(LikeC4Model.Empty, DefaultOptions);

        // Should produce valid structure even with no elements.
        await Assert.That(dsl).Contains("specification {");
        await Assert.That(dsl).Contains("model {");
        await Assert.That(dsl).Contains("views {");
        await Assert.That(dsl).Contains("view index {");
    }

    [Test]
    public async Task Generate_SingleElement_IncludesKindInSpecification()
    {
        var model = new LikeC4Model
        {
            Elements = [new LikeC4Element { Name = "api", Label = "API", Kind = LikeC4ElementKind.Component }],
            Relationships = [],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        await Assert.That(dsl).Contains("element component");
    }

    [Test]
    public async Task Generate_MultipleKinds_AllKindsInSpecification()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element { Name = "api", Label = "API", Kind = LikeC4ElementKind.Component },
                new LikeC4Element { Name = "db", Label = "DB", Kind = LikeC4ElementKind.Database },
                new LikeC4Element { Name = "cache", Label = "Cache", Kind = LikeC4ElementKind.Container },
            ],
            Relationships = [],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        await Assert.That(dsl).Contains("element component");
        await Assert.That(dsl).Contains("element database");
        await Assert.That(dsl).Contains("element container");
    }

    [Test]
    public async Task Generate_DuplicateKinds_KindAppearsOnceInSpecification()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element { Name = "api1", Label = "API 1", Kind = LikeC4ElementKind.Component },
                new LikeC4Element { Name = "api2", Label = "API 2", Kind = LikeC4ElementKind.Component },
            ],
            Relationships = [],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        var count = CountOccurrences(dsl, "element component");
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Generate_ElementWithTechnology_IncludesTechnologyLine()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element
                {
                    Name = "api",
                    Label = "API",
                    Kind = LikeC4ElementKind.Component,
                    Technology = "ASP.NET Core",
                },
            ],
            Relationships = [],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        await Assert.That(dsl).Contains("technology 'ASP.NET Core'");
    }

    [Test]
    public async Task Generate_ElementWithDescription_IncludesDescriptionLine()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element
                {
                    Name = "api",
                    Label = "API",
                    Kind = LikeC4ElementKind.Component,
                    Description = "Handles HTTP requests",
                },
            ],
            Relationships = [],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        await Assert.That(dsl).Contains("description 'Handles HTTP requests'");
    }

    [Test]
    public async Task Generate_LabelWithSingleQuote_IsEscaped()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element { Name = "api", Label = "O'Brien's API", Kind = LikeC4ElementKind.Component },
            ],
            Relationships = [],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        await Assert.That(dsl).Contains(@"'O\'Brien\'s API'");
    }

    [Test]
    public async Task Generate_NameWithSpecialChars_IsSanitized()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element { Name = "my-api.service", Label = "API", Kind = LikeC4ElementKind.Component },
            ],
            Relationships = [],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        await Assert.That(dsl).Contains("my_api_service = component");
    }

    [Test]
    public async Task Generate_Relationship_RenderedAsArrow()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element { Name = "api", Label = "API", Kind = LikeC4ElementKind.Component },
                new LikeC4Element { Name = "db", Label = "DB", Kind = LikeC4ElementKind.Database },
            ],
            Relationships =
            [
                new LikeC4Relationship { SourceName = "api", TargetName = "db" },
            ],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        await Assert.That(dsl).Contains("api -> db");
    }

    [Test]
    public async Task Generate_RelationshipWithLabel_IncludesLabel()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element { Name = "api", Label = "API", Kind = LikeC4ElementKind.Component },
                new LikeC4Element { Name = "queue", Label = "Queue", Kind = LikeC4ElementKind.System },
            ],
            Relationships =
            [
                new LikeC4Relationship { SourceName = "api", TargetName = "queue", Label = "Publishes" },
            ],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        await Assert.That(dsl).Contains("api -> queue 'Publishes'");
    }

    [Test]
    public async Task Generate_ViewContainsTitle()
    {
        var dsl = LikeC4DslGenerator.Generate(LikeC4Model.Empty, DefaultOptions);

        await Assert.That(dsl).Contains("title 'Test Architecture'");
    }

    [Test]
    public async Task Generate_TitleWithSingleQuote_IsEscaped()
    {
        var opts = new LikeC4DiagramOptions { Title = "O'Reilly's App", OutputDirectory = "." };

        var dsl = LikeC4DslGenerator.Generate(LikeC4Model.Empty, opts);

        await Assert.That(dsl).Contains(@"title 'O\'Reilly\'s App'");
    }

    [Test]
    public async Task Generate_NonEmptyModel_ViewContainsIncludeStar()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element { Name = "api", Label = "API", Kind = LikeC4ElementKind.Component },
            ],
            Relationships = [],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        await Assert.That(dsl).Contains("include *");
    }

    [Test]
    public async Task Generate_NestedElement_RenderedInsideParent()
    {
        var model = new LikeC4Model
        {
            Elements =
            [
                new LikeC4Element { Name = "postgres", Label = "Postgres", Kind = LikeC4ElementKind.Container },
                new LikeC4Element { Name = "appdb", Label = "App DB", Kind = LikeC4ElementKind.Database, ParentName = "postgres" },
            ],
            Relationships = [],
        };

        var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

        // The child should appear inside the parent's block
        var postgresIdx = dsl.IndexOf("postgres = container", StringComparison.Ordinal);
        var appdbIdx = dsl.IndexOf("appdb = database", StringComparison.Ordinal);
        await Assert.That(postgresIdx).IsLessThan(appdbIdx);
        // Parent block should be opened before child
        var braceIdx = dsl.IndexOf('{', postgresIdx);
        await Assert.That(braceIdx).IsLessThan(appdbIdx);
    }

    private static int CountOccurrences(string source, string substring)
    {
        var count = 0;
        var idx = 0;
        while ((idx = source.IndexOf(substring, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += substring.Length;
        }

        return count;
    }
}
