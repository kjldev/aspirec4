namespace Aspire.Hosting.AspireC4.LikeC4.Models;

/// <summary>Defines the element kinds used in the LikeC4 DSL specification block.</summary>
[KnownLikeC4ElementKindRegistry]
public static class LikeC4ElementKind
{
	/// <summary>A fine-grained component element, typically contained within a <see cref="Container"/>.</summary>
	public const string Component = "component";

	/// <summary>A deployable unit or application (e.g. a service, app, or microservice).</summary>
	public const string Container = "container";

	/// <summary>A data-storage element (e.g. a relational or document database).</summary>
	public const string Database = "database";

	/// <summary>An executable process or standalone program.</summary>
	public const string Executable = "executable";

	/// <summary>A high-level system boundary grouping related containers.</summary>
	public const string System = "system";
}
