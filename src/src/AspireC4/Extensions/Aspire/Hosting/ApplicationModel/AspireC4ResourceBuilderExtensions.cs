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
		) => WithLikeC4DetailsCore(builder, label, technology, description, summary, icon, autoIconEnabled: null);

		/// <summary>
		/// Customises how a resource appears in the generated LikeC4 diagram using fluent options.
		/// </summary>
		public IResourceBuilder<T> WithLikeC4Details(Action<LikeC4DetailsOptions> configure)
		{
			ArgumentNullException.ThrowIfNull(builder);
			ArgumentNullException.ThrowIfNull(configure);

			LikeC4DetailsOptions options = new();
			configure(options);

			return WithLikeC4DetailsCore(
				builder,
				options.Label,
				options.Technology,
				options.Description,
				options.Summary,
				options.Icon,
				options.AutoIconEnabled
			);
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
			Action<LikeC4RelationshipOptions>? configure = null
		)
			where TRef : IResource
		{
			ArgumentNullException.ThrowIfNull(builder);
			ArgumentNullException.ThrowIfNull(target);

			LikeC4RelationshipOptions options = new();
			if (configure is not null)
				configure(options);

			builder.Resource.Annotations.Add(
				new LikeC4RelationshipDetailsAnnotation(
					target.Resource.Name,
					options.Label,
					options.Technology,
					options.Description,
					options.Kind
				)
			);

			return builder;
		}

		/// <summary>
		/// Excludes a resource from the generated LikeC4 diagram.
		/// </summary>
		public IResourceBuilder<T> ExcludeFromLikeC4()
		{
			ArgumentNullException.ThrowIfNull(builder);

			return builder.WithAnnotation(
				new ExcludeFromLikeC4Annotation(),
				ResourceAnnotationMutationBehavior.Replace
			);
		}
	}

	static IResourceBuilder<T> WithLikeC4DetailsCore<T>(
		IResourceBuilder<T> builder,
		string? label,
		string? technology,
		string? description,
		string? summary,
		string? icon,
		bool? autoIconEnabled
	)
		where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);

		var effectiveLabel = label ?? builder.Resource.Name;
		return builder.WithAnnotation(
			new LikeC4NodeDetailsAnnotation(effectiveLabel, technology, description, summary, icon, autoIconEnabled),
			ResourceAnnotationMutationBehavior.Replace
		);
	}
}
