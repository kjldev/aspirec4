using Aspire.Hosting.AspireC4;

namespace Aspire.Hosting;

static class AspireC4Extensions
{
	public static IAspireC4Builder ConfigureTestHost(this IAspireC4Builder builder)
	{
		// Register hand-authored extension files (custom styles, views, model extensions).
		// The files sit next to the TestAppHost assembly so they are available in both the normal
		// run context (when the TestAppHost is launched directly) and the integration-test context
		// (where the TestAppHost assembly is copied to the test output directory).
		var extensionsDir = Path.Combine(
			Path.GetDirectoryName(typeof(TestAppHostProgram).Assembly.Location)!,
			"likec4-extensions"
		);
		if (Directory.Exists(extensionsDir))
		{
			builder.WithAdditionalDSLFolder(extensionsDir);
		}

		builder.WithImageAliasFolder("@", Path.Combine(AppContext.BaseDirectory, "../../../../../../assets/images/"));
		var imagesDir = Path.Combine(
			Path.GetDirectoryName(typeof(TestAppHostProgram).Assembly.Location)!,
			"likec4-images"
		);
		if (Directory.Exists(imagesDir))
		{
			builder.WithImageAliasFolder("@test-icons", imagesDir);
		}

		builder.LikeC4ResourceBuilder.WithLikeC4Details(opts =>
			opts.WithLabel("LikeC4")
				.WithSummary(
					"Describe your system architecture with code. Visualize, validate and share — all from a single source of truth."
				)
				.WithIcon("@/likec4/likec4-logo.svg")
				.WithLink("https://likec4.dev/", "Learn more about LikeC4")
				.WithLink("https://github.com/likec4/likec4/", "LikeC4 on GitHub")
				.WithLink("https://github.com/sponsors/likec4", "Sponsor LikeC4 🩷")
				.WithLink("https://github.com/davydkov", "Connect with the author on GitHub")
		);

		var excludeAnnotation = builder
			.LikeC4ResourceBuilder.Resource.Annotations.OfType<ExcludeFromLikeC4Annotation>()
			.FirstOrDefault();
		if (excludeAnnotation is not null)
		{
			// We're going to keep this for the sake of the app demo.
			builder.LikeC4ResourceBuilder.Resource.Annotations.Remove(excludeAnnotation);
		}

		return builder;
	}
}
