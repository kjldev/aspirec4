using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

static partial class AspireC4ResourceBuilderEnvExtensions
{
	extension<T>(IResourceBuilder<T> builder)
		where T : IResourceWithEnvironment
	{
		/// <summary>
		/// Adds a reference (<see cref="ResourceBuilderExtensions.WithReference{T}"/>) to another resource with a connection string, and configures it to be a LikeC4 relationship.
		/// </summary>
		public IResourceBuilder<T> WithLikeC4Reference(
			IResourceBuilder<IResourceWithConnectionString> source,
			Action<LikeC4RelationshipDetailsAnnotation>? configure,
			string? connectionName = null,
			bool optional = false
		)
		{
			builder.WithReference(source, connectionName, optional).WithLikeC4Reference(source, configure);

			return builder;
		}
	}
}
