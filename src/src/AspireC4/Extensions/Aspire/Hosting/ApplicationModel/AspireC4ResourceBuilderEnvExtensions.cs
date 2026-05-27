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
	/// <param name="builder">The resource builder for the source resource.</param>
	/// <param name="source">The target resource builder with a connection string.</param>
	/// <param name="label">Short label shown on the relationship arrow.</param>
	/// <param name="technology">Technology or protocol used by the relationship (e.g., "HTTP/2", "gRPC").</param>
	/// <param name="description">Longer description of the relationship.</param>
	/// <param name="kind">Optional LikeC4 relationship kind identifier (e.g. "async", "sync").</param>
	/// <param name="navigateTo">Optional ID of a LikeC4 view to navigate to when the relationship is clicked.</param>
	/// <param name="connectionName">The connection name passed to Aspire's <c>WithReference</c>.</param>
	/// <param name="optional">Whether the reference is optional.</param>
	/// <param name="skipAspireReference">When <see langword="true"/>, skips calling Aspire's <c>WithReference</c>.</param>
	[AspireExport(
		"withLikeC4ReferenceWithEnvironment",
		MethodName = "withLikeC4ReferenceWithEnvironment",
		Description = "Create a new reference, while also allowing customization of how a resource appears in the generated LikeC4 diagram."
	)]
	public static IResourceBuilder<T> WithLikeC4Reference<T>(
		[NotNull] this IResourceBuilder<T> builder,
		IResourceBuilder<IResourceWithConnectionString> source,
		string? label = null,
		string? technology = null,
		string? description = null,
		string? kind = null,
		string? navigateTo = null,
		string? connectionName = null,
		bool optional = false,
		bool skipAspireReference = false
	)
		where T : IResourceWithEnvironment
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(source);

		if (!skipAspireReference)
			builder.WithReference(source, connectionName, optional);

		return builder.WithLikeC4Reference(source, label, technology, description, kind, navigateTo);
	}

	/// <summary>
	/// Adds a reference to another resource with a connection string, and configures it to be a LikeC4 relationship,
	/// using a fluent callback to customise the relationship.
	/// </summary>
	/// <param name="builder">The resource builder for the source resource.</param>
	/// <param name="source">The target resource builder with a connection string.</param>
	/// <param name="configure">An action that configures the relationship appearance.</param>
	/// <param name="connectionName">The connection name passed to Aspire's <c>WithReference</c>.</param>
	/// <param name="optional">Whether the reference is optional.</param>
	/// <param name="skipAspireReference">When <see langword="true"/>, skips calling Aspire's <c>WithReference</c>.</param>
	[AspireExportIgnore(
		Reason = "Action callback overload — use the parameter-based 'withLikeC4ReferenceWithEnvironment' overload for ATS compatibility."
	)]
	public static IResourceBuilder<T> WithLikeC4Reference<T>(
		[NotNull] this IResourceBuilder<T> builder,
		IResourceBuilder<IResourceWithConnectionString> source,
		Action<LikeC4RelationshipDetailsAnnotation>? configure = null,
		string? connectionName = null,
		bool optional = false,
		bool skipAspireReference = false
	)
		where T : IResourceWithEnvironment
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(source);

		if (!skipAspireReference)
			builder.WithReference(source, connectionName, optional);

		var annotation = new LikeC4RelationshipDetailsAnnotation(source.Resource.Name);
		configure?.Invoke(annotation);
		builder.Resource.Annotations.Add(annotation);

		return builder;
	}
}
