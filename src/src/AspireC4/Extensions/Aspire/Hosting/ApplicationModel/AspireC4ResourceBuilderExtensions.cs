using System.ComponentModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for <see cref="IResourceBuilder{T}"/> to add annotations that customize how resources and their relationships
/// are represented in the generated LikeC4 diagrams. These methods allow you to specify details such as labels, technologies, descriptions,
/// summaries, and icons for resources, as well as details for relationships between resources.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4ResourceBuilderExtensions
{
	extension<T>(IResourceBuilder<T> builder)
		where T : IResource
	{
		/// <summary>
		/// Customises how a resource appears in the generated LikeC4 diagram.
		/// </summary>
		[AspireExport(
			"withLikeC4DetailsParameters",
			MethodName = "withLikeC4Details",
			Description = "Customises how a resource appears in the generated LikeC4 diagram."
		)]
		public IResourceBuilder<T> WithLikeC4Details(
			string? label = null,
			string? technology = null,
			string? description = null,
			string? summary = null,
			string? icon = null
		)
		{
			ArgumentNullException.ThrowIfNull(builder);

			var annotation = new LikeC4NodeDetailsAnnotation(label ?? builder.Resource.Name)
				.WithTechnology(technology)
				.WithDescription(description)
				.WithSummary(summary)
				.WithIcon(icon);

			return builder.WithAnnotation(annotation, ResourceAnnotationMutationBehavior.Replace);
		}

		/// <summary>
		/// Customises how a resource appears in the generated LikeC4 diagram using fluent options.
		/// </summary>
		[AspireExport(
			"withLikeC4DetailsFluent",
			MethodName = "withLikeC4Details",
			Description = "Customises how a resource appears in the generated LikeC4 diagram using fluent options."
		)]
		public IResourceBuilder<T> WithLikeC4Details(Action<LikeC4NodeDetailsAnnotation> configure)
		{
			ArgumentNullException.ThrowIfNull(builder);
			ArgumentNullException.ThrowIfNull(configure);

			var annotation = new LikeC4NodeDetailsAnnotation(builder.Resource.Name);
			configure(annotation);

			return builder.WithAnnotation(annotation, ResourceAnnotationMutationBehavior.Replace);
		}

		/// <summary>
		/// Customises how the relationship from this resource to <paramref name="target"/> appears in the
		/// generated LikeC4 diagram.
		/// </summary>
		/// <remarks>
		/// This method only adds the LikeC4 diagram annotation — it does <em>not</em> call
		/// <c>WithReference</c>. Continue to use Aspire's <c>WithReference</c> to establish the actual
		/// runtime dependency, or use the overload that accepts <c>withAspireReference: true</c>.
		/// </remarks>
		/// <param name="target">The target resource builder that the relationship points to.</param>
		/// <param name="configure">Optional action that configures the relationship appearance.</param>
		[AspireExport(
			"withLikeC4Reference",
			MethodName = "withLikeC4Reference",
			Description = "Customises how the relationship from this resource to the target appears in the generated LikeC4 diagram."
		)]
		public IResourceBuilder<T> WithLikeC4Reference<TRef>(
			IResourceBuilder<TRef> target,
			Action<LikeC4RelationshipDetailsAnnotation>? configure = null
		)
			where TRef : IResource
		{
			ArgumentNullException.ThrowIfNull(builder);
			ArgumentNullException.ThrowIfNull(target);

			var annotation = new LikeC4RelationshipDetailsAnnotation(target.Resource.Name);
			configure?.Invoke(annotation);
			builder.Resource.Annotations.Add(annotation);

			return builder;
		}

		/// <summary>
		/// Assigns this resource to a named group in the generated LikeC4 diagram.
		/// Resources sharing the same <paramref name="groupName"/> are emitted inside a
		/// <c>group 'label' { include ... }</c> block in the generated view.
		/// </summary>
		[AspireExport(
			"withLikeC4Group",
			MethodName = "withLikeC4Group",
			Description = "Assigns this resource to a named group in the generated LikeC4 diagram."
		)]
		public IResourceBuilder<T> WithLikeC4Group(string groupName)
		{
			ArgumentNullException.ThrowIfNull(builder);
			ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

			return builder.WithAnnotation(
				new LikeC4GroupAnnotation(groupName),
				ResourceAnnotationMutationBehavior.Replace
			);
		}

		/// <summary>
		/// Excludes a resource from the generated LikeC4 diagram.
		/// </summary>
		[AspireExport(
			"excludeFromLikeC4",
			MethodName = "excludeFromLikeC4",
			Description = "Excludes a resource from the generated LikeC4 diagram."
		)]
		public IResourceBuilder<T> ExcludeFromLikeC4()
		{
			ArgumentNullException.ThrowIfNull(builder);

			return builder.WithAnnotation(
				new ExcludeFromLikeC4Annotation(),
				ResourceAnnotationMutationBehavior.Replace
			);
		}
	}
}
