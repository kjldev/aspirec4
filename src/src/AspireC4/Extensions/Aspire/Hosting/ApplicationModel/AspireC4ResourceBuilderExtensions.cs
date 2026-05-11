using System.ComponentModel;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class AspireC4ResourceBuilderExtensions
{
	extension<T>(IResourceBuilder<T> builder)
		where T : IResource
	{
		/// <summary>
		/// Customises how a resource appears in the generated LikeC4 diagram.
		/// </summary>
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
		/// <param name="builder">The source resource builder.</param>
		/// <param name="target">The target resource builder that the relationship points to.</param>
		/// <param name="configure">Optional action that configures the relationship appearance.</param>
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
		public IResourceBuilder<T> ExcludeFromLikeC4(bool exclude = true)
		{
			ArgumentNullException.ThrowIfNull(builder);

			return builder.WithAnnotation(
				new ExcludeFromLikeC4Annotation(exclude),
				ResourceAnnotationMutationBehavior.Replace
			);
		}
	}
}
