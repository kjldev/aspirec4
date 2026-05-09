using TUnit.Core;

namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4DslGeneratorTests
{
	static readonly LikeC4DiagramOptions DefaultOptions = new()
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
	public async Task Generate_ElementWithIcon_IncludesIconLine()
	{
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "db",
					Label = "DB",
					Kind = LikeC4ElementKind.Database,
					Icon = "tech:postgresql",
				},
			],
			Relationships = [],
		};

		var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

		await Assert.That(dsl).Contains("icon tech:postgresql");
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
		// label-only: the relationship line should NOT open a block
		await Assert.That(dsl).DoesNotContain("api -> queue 'Publishes' {");
	}

	[Test]
	public async Task Generate_RelationshipWithTechnology_EmitsBlock()
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
				new LikeC4Relationship { SourceName = "api", TargetName = "db", Technology = "PostgreSQL" },
			],
		};

		var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

		await Assert.That(dsl).Contains("api -> db {");
		await Assert.That(dsl).Contains("technology 'PostgreSQL'");
	}

	[Test]
	public async Task Generate_RelationshipWithDescription_EmitsBlock()
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
				new LikeC4Relationship { SourceName = "api", TargetName = "db", Description = "Stores user data" },
			],
		};

		var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

		await Assert.That(dsl).Contains("api -> db {");
		await Assert.That(dsl).Contains("description 'Stores user data'");
	}

	[Test]
	public async Task Generate_RelationshipWithLabelAndTechnology_EmitsBoth()
	{
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element { Name = "api", Label = "API", Kind = LikeC4ElementKind.Component },
				new LikeC4Element { Name = "cache", Label = "Cache", Kind = LikeC4ElementKind.Container },
			],
			Relationships =
			[
				new LikeC4Relationship { SourceName = "api", TargetName = "cache", Label = "Caches data", Technology = "Redis Protocol" },
			],
		};

		var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

		await Assert.That(dsl).Contains("api -> cache 'Caches data' {");
		await Assert.That(dsl).Contains("technology 'Redis Protocol'");
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

	static int CountOccurrences(string source, string substring)
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

	[Test]
	[MethodDataSource(nameof(StateStyleMappings))]
	public async Task Generate_ElementWithState_RendersExpectedStyle(
		LikeC4ResourceState state, string? expectedColor, int? expectedOpacity)
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
					State = state,
				},
			],
			Relationships = [],
		};

		var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

		if (expectedColor is null)
		{
			await Assert.That(dsl).DoesNotContain("color ");
			await Assert.That(dsl).DoesNotContain("opacity ");
		}
		else
		{
			// Color overrides must appear inside a style rule in the views section.
			await Assert.That(dsl).Contains($"style api {{");
			await Assert.That(dsl).Contains($"color {expectedColor}");

			var viewsIdx = dsl.IndexOf("views {", StringComparison.Ordinal);
			var colorIdx = dsl.IndexOf($"color {expectedColor}", StringComparison.Ordinal);
			await Assert.That(colorIdx).IsGreaterThan(viewsIdx);

			// Opacity should be present for states that visually distinguish transitional
			// from terminal: Stopping (60%) is more visible than Exited (30%).
			if (expectedOpacity is not null)
			{
				await Assert.That(dsl).Contains($"opacity {expectedOpacity}%");
				var opacityIdx = dsl.IndexOf($"opacity {expectedOpacity}%", StringComparison.Ordinal);
				await Assert.That(opacityIdx).IsGreaterThan(viewsIdx);
			}
			else
			{
				await Assert.That(dsl).DoesNotContain("opacity ");
			}
		}
	}

	/// <summary>
	/// State → (color, opacity) style mappings.
	/// <para>
	/// Opacity differentiates transitional from terminal states:
	/// <list type="bullet">
	///   <item><description>Stopping: 60 % — transitional, winding down but still visible.</description></item>
	///   <item><description>Exited: 30 % — terminal, clearly faded/inactive.</description></item>
	/// </list>
	/// </para>
	/// </summary>
	public static IEnumerable<(LikeC4ResourceState State, string? Color, int? Opacity)> StateStyleMappings()
	{
		yield return (LikeC4ResourceState.Unknown, null, null);
		yield return (LikeC4ResourceState.Starting, "sky", null);
		yield return (LikeC4ResourceState.Running, "green", null);
		yield return (LikeC4ResourceState.Stopping, "slate", 60);
		yield return (LikeC4ResourceState.Exited, "muted", 30);
		yield return (LikeC4ResourceState.Failed, "amber", null);
		yield return (LikeC4ResourceState.Error, "red", null);
	}

	[Test]
	public async Task Generate_ElementWithStateAndTechnology_RendersColorInViewsNotModel()
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
					Technology = ".NET",
					State = LikeC4ResourceState.Error,
				},
			],
			Relationships = [],
		};

		var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

		// Technology stays in model block.
		await Assert.That(dsl).Contains("technology '.NET'");

		// Color must be in views section only.
		var viewsIdx = dsl.IndexOf("views {", StringComparison.Ordinal);
		var colorIdx = dsl.IndexOf("color red", StringComparison.Ordinal);
		await Assert.That(colorIdx).IsGreaterThan(viewsIdx);

		// model block must NOT contain color.
		var modelSection = dsl[..viewsIdx];
		await Assert.That(modelSection).DoesNotContain("color");
	}

	[Test]
	public async Task Generate_ElementWithIconAndState_RendersIconInModelAndColorInViews()
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
					Icon = "tech:dotnet",
					State = LikeC4ResourceState.Running,
				},
			],
			Relationships = [],
		};

		var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

		var viewsIdx = dsl.IndexOf("views {", StringComparison.Ordinal);
		var iconIdx = dsl.IndexOf("icon tech:dotnet", StringComparison.Ordinal);
		var colorIdx = dsl.IndexOf("color green", StringComparison.Ordinal);

		await Assert.That(iconIdx).IsLessThan(viewsIdx);
		await Assert.That(colorIdx).IsGreaterThan(viewsIdx);
	}

	[Test]
	public async Task Generate_ElementWithUnknownState_NoStyleRuleRendered()
	{
		// An element with Unknown state should produce no style block in views.
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
					State = LikeC4ResourceState.Unknown,
				},
			],
			Relationships = [],
		};

		var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

		await Assert.That(dsl).DoesNotContain("style api");
		await Assert.That(dsl).DoesNotContain("color");
	}

	[Test]
	public async Task Generate_MultipleElementsWithDifferentStates_StylesGroupedByColor()
	{
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element { Name = "api", Label = "API", Kind = LikeC4ElementKind.Component, State = LikeC4ResourceState.Running },
				new LikeC4Element { Name = "db", Label = "DB", Kind = LikeC4ElementKind.Database, State = LikeC4ResourceState.Running },
				new LikeC4Element { Name = "cache", Label = "Cache", Kind = LikeC4ElementKind.Container, State = LikeC4ResourceState.Error },
			],
			Relationships = [],
		};

		var dsl = LikeC4DslGenerator.Generate(model, DefaultOptions);

		// api and db both Running→green should be grouped.
		await Assert.That(dsl).Contains("style api, db {");
		await Assert.That(dsl).Contains("color green");
		// cache Error→red should be separate.
		await Assert.That(dsl).Contains("style cache {");
		await Assert.That(dsl).Contains("color red");
	}
}
