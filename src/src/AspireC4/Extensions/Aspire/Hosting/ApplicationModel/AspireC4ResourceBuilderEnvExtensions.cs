using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for <see cref="IResourceBuilder{T}"/> (where T : <see cref="IResourceWithEnvironment"/>) to
/// add annotations that customize how resources and their relationships
/// are represented in the generated LikeC4 diagrams. These methods allow you to specify details such as labels, technologies, descriptions,
/// summaries, and icons for resources, as well as details for relationships between resources.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4ResourceBuilderEnvExtensions
{
	/// <summary>
	/// Adds a reference to another resource with a connection string, and configures it to be a LikeC4 relationship.
	/// </summary>
	[AspireExport(
		"withLikeC4ReferenceWithEnvironment",
		MethodName = "withLikeC4Reference",
		Description = "Create a new reference, while also allowing customization of how a resource appears in the generated LikeC4 diagram."
	)]
	public static IResourceBuilder<T> WithLikeC4Reference<T>(
		[NotNull] this IResourceBuilder<T> builder,
		IResourceBuilder<IResourceWithConnectionString> source,
		Action<LikeC4RelationshipDetailsAnnotation>? configure,
		string? connectionName = null,
		bool optional = false,
		bool skipAspireReference = false
	)
		where T : IResourceWithEnvironment
	{
		if (!skipAspireReference)
			builder.WithReference(source, connectionName, optional);

		return builder.WithLikeC4Reference(source, configure);
	}
}
