using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

static partial class AspireC4ResourceBuilderEnvExtensions
{
	extension<T>(IResourceBuilder<T> builder)
		where T : IResourceWithEnvironment
	{
		public IResourceBuilder<T> WithLikeC4Reference(
			IResourceBuilder<IResourceWithConnectionString> source,
			Action<LikeC4RelationshipOptions>? configure,
			string? connectionName = null,
			bool optional = false
		)
		{
			builder.WithReference(source, connectionName, optional).WithLikeC4Reference(source, configure);

			return builder;
		}
	}
}
